// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Extensions.Logging;

namespace NewRelic.Agent.Core.ContinuousProfiling;

public class ManagedThreadIdRegistry : IManagedThreadIdRegistry
{
    private readonly ICurrentOsThreadIdProvider _osThreadIdProvider;

    // Latch: the OS TID provider can throw on some platforms (PlatformNotSupportedException on an
    // unrecognized architecture, DllNotFoundException/EntryPointNotFoundException for the libc P/Invoke on
    // musl or a stripped libc). This runs on the transaction-creation hot path and the Scheduler's
    // agent-work path, so one bad P/Invoke must not be retried on every call -- trip once and never attempt
    // the provider again. Instance-scoped rather than a static: production only ever uses the single
    // process-wide ManagedThreadIdRegistry.Instance, so this is process-wide in practice without also
    // latching every test's own throwaway registry/mock combination together.
    private int _providerFailed;

    // Stores a WeakReference to the registering thread alongside its managed id so a later thread that
    // reuses the same (recycled) OS TID, but has not yet called EnsureRegistered itself, is detected as a
    // miss instead of resolving to the dead thread's stale managed id.
    private readonly ConcurrentDictionary<long, (WeakReference<Thread> ThreadRef, int ManagedThreadId)> _osTidToManagedId = new ConcurrentDictionary<long, (WeakReference<Thread> ThreadRef, int ManagedThreadId)>();

    // Per-thread gate: track which registry instances the CURRENT MANAGED THREAD has already registered
    // with. [ThreadStatic] storage belongs to the managed thread, so a new thread that reuses a recycled
    // ManagedThreadId gets fresh (empty) storage and always re-registers -- a ManagedThreadId-keyed
    // dictionary instead would find the dead thread's gate entry already set and wrongly skip
    // registration, permanently losing that OS TID's mapping for the life of the process.
    [ThreadStatic]
    private static HashSet<ManagedThreadIdRegistry> _registeredInstances;

    // Process-wide seam so TransactionService, ContinuousProfilingContext, and OtlpProfileBuilder can
    // all reach the same registry without DI plumbing through every layer between them -- mirrors
    // ContinuousProfilingContext.Instance.
    public static IManagedThreadIdRegistry Instance { get; set; } = new ManagedThreadIdRegistry(new CurrentOsThreadIdProvider());

    public ManagedThreadIdRegistry(ICurrentOsThreadIdProvider osThreadIdProvider)
    {
        _osThreadIdProvider = osThreadIdProvider;
    }

    public void EnsureRegistered()
    {
        if (Volatile.Read(ref _providerFailed) != 0)
        {
            return;
        }

        var registeredInstances = _registeredInstances ??= new HashSet<ManagedThreadIdRegistry>();
        if (!registeredInstances.Add(this))
        {
            return;
        }

        try
        {
            var osThreadId = _osThreadIdProvider.GetCurrentOsThreadId();
            _osTidToManagedId[osThreadId] = (new WeakReference<Thread>(Thread.CurrentThread), Thread.CurrentThread.ManagedThreadId);
        }
        catch (Exception ex)
        {
            // Trip the latch first so a burst of concurrent callers all lose the CompareExchange race
            // and skip straight to the early-return above instead of each retrying the same broken
            // P/Invoke before this thread's Log.Warn below has a chance to run.
            if (Interlocked.CompareExchange(ref _providerFailed, 1, 0) == 0)
            {
                Log.Warn(ex, "Continuous Profiling: unable to determine the current OS thread id; managed/OS thread id mapping will be unavailable for the life of the process.");
            }
        }
    }

    public bool TryGetManagedThreadId(long osThreadId, out int managedThreadId)
    {
        // A thread that has merely finished running (but is still reachable, e.g. via a caller's local
        // variable) must still resolve -- only a GARBAGE-COLLECTED registering thread counts as stale,
        // since that is the only state in which its OS TID is safe to have been reassigned to someone
        // else. Checking Thread.IsAlive here instead would wrongly miss on every thread that simply ran
        // to completion before this lookup, which is the common case for background/worker threads.
        if (_osTidToManagedId.TryGetValue(osThreadId, out var entry))
        {
            if (entry.ThreadRef.TryGetTarget(out _))
            {
                managedThreadId = entry.ManagedThreadId;
                return true;
            }

            // Remove only the exact dead entry just observed -- a plain TryRemove(osThreadId) would
            // delete whatever is under that key NOW, which can be a different thread's fresh
            // registration written between the TryGetValue above and this call.
            ((ICollection<KeyValuePair<long, (WeakReference<Thread> ThreadRef, int ManagedThreadId)>>)_osTidToManagedId)
                .Remove(new KeyValuePair<long, (WeakReference<Thread> ThreadRef, int ManagedThreadId)>(osThreadId, entry));
        }

        managedThreadId = 0;
        return false;
    }
}

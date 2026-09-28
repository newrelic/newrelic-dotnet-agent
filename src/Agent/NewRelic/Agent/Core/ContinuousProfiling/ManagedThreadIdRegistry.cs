// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using NewRelic.Agent.Core.Utilities;

namespace NewRelic.Agent.Core.ContinuousProfiling;

public class ManagedThreadIdRegistry : IManagedThreadIdRegistry
{
    private readonly ICurrentOsThreadIdProvider _osThreadIdProvider;

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
        var registeredInstances = _registeredInstances ??= new HashSet<ManagedThreadIdRegistry>();
        if (!registeredInstances.Add(this))
        {
            return;
        }

        var osThreadId = _osThreadIdProvider.GetCurrentOsThreadId();
        _osTidToManagedId[osThreadId] = (new WeakReference<Thread>(Thread.CurrentThread), Thread.CurrentThread.ManagedThreadId);
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

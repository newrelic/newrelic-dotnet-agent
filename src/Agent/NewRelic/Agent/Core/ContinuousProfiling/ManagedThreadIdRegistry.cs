// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Concurrent;
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

    // Per-thread gate: track which threads have already registered on THIS registry instance.
    // Uses ManagedThreadId as key; note the CLR can reuse a ManagedThreadId after a thread terminates, so
    // a new thread reusing an id can find the gate already set. That does not go stale the way the OS-TID
    // mapping above does -- see TryGetManagedThreadId's doc comment for the staleness mode that key choice
    // introduces there.
    private readonly ConcurrentDictionary<int, bool> _threadsRegistered = new ConcurrentDictionary<int, bool>();

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
        var threadId = Thread.CurrentThread.ManagedThreadId;
        if (_threadsRegistered.ContainsKey(threadId))
        {
            return;
        }

        var osThreadId = _osThreadIdProvider.GetCurrentOsThreadId();
        _osTidToManagedId[osThreadId] = (new WeakReference<Thread>(Thread.CurrentThread), threadId);
        _threadsRegistered.TryAdd(threadId, true);
    }

    public bool TryGetManagedThreadId(long osThreadId, out int managedThreadId)
    {
        // A thread that has merely finished running (but is still reachable, e.g. via a caller's local
        // variable) must still resolve -- only a GARBAGE-COLLECTED registering thread counts as stale,
        // since that is the only state in which its OS TID is safe to have been reassigned to someone
        // else. Checking Thread.IsAlive here instead would wrongly miss on every thread that simply ran
        // to completion before this lookup, which is the common case for background/worker threads.
        if (_osTidToManagedId.TryGetValue(osThreadId, out var entry) && entry.ThreadRef.TryGetTarget(out _))
        {
            managedThreadId = entry.ManagedThreadId;
            return true;
        }

        _osTidToManagedId.TryRemove(osThreadId, out _);
        managedThreadId = 0;
        return false;
    }
}

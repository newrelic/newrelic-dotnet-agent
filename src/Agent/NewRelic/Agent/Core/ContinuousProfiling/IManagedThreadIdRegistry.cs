// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.Core.ContinuousProfiling;

/// <summary>
/// Process-wide map from OS thread id to .NET Thread.ManagedThreadId, populated by threads that
/// opportunistically report themselves via EnsureRegistered. Exists because the CP native capture
/// path can only resolve a thread's OS TID (see ContinuousProfiler.h) -- no ICorProfilerInfo version
/// (through the vendored ICorProfilerInfo11) or CLR ETW event maps an OS TID or the profiling API's
/// opaque ThreadID handle to Thread.ManagedThreadId, so the mapping must be built from the managed
/// side, one thread's own self-report at a time.
/// </summary>
public interface IManagedThreadIdRegistry
{
    /// <summary>
    /// Records the calling thread's (OS TID, Thread.ManagedThreadId) pair, if not already recorded
    /// for this thread. Cheap to call from any code path that runs once per thread lifetime worth of
    /// work (a new transaction, an agent background-dispatch tick) -- NOT from a per-call hot path.
    /// </summary>
    void EnsureRegistered();

    /// <summary>
    /// Looks up the managed thread id previously registered for the given OS TID.
    ///
    /// OS TIDs are recycled by the OS after a thread exits, so a registered entry can outlive the
    /// thread that created it. To narrow (not eliminate) the window where a dead thread's stale
    /// managed id could be returned for its OS TID's new owner, each entry also holds a
    /// <see cref="System.WeakReference{T}"/> to the registering thread; once that <see cref="System.Threading.Thread"/>
    /// object itself has been garbage-collected, the entry is treated as a miss -- not a hit -- and is
    /// evicted. This only catches collection, not mere thread exit: a thread that has already finished
    /// running but whose <see cref="System.Threading.Thread"/> object is still reachable (not yet
    /// collected) still resolves as a hit, on the assumption its OS TID has not yet been reused --
    /// deliberately, since gating on <see cref="System.Threading.Thread.IsAlive"/> as well would also
    /// treat that common, still-correct case as a miss. A stale-but-uncollected entry self-heals as
    /// soon as any thread next calls <see cref="EnsureRegistered"/> for that OS TID. Threads that never
    /// run agent-visible managed code (most idle ThreadPool/runtime-infra threads CP samples) never
    /// appear here at all -- a miss for those is the common case, not a bug. Callers must treat a miss
    /// as "unknown," never guess.
    /// </summary>
    bool TryGetManagedThreadId(long osThreadId, out int managedThreadId);
}

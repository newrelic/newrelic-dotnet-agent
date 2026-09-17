// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Threading;

namespace NewRelic.Agent.Core.Utilities;

// Process-wide lock serializing the ThreadProfilingService/ContinuousProfilingService start-guard
// check-and-arm sequences, so at most one profiler can decide "the other isn't active" and arm
// itself at a time. Mirrors the native SuspendMutex (Profiler/ContinuousProfiler/SuspendMutex.h),
// which is the backstop against concurrent suspend/walk; this lock is what makes the two profilers'
// *liveness* mutually exclusive, not just their suspend calls.
public static class ProfilingMutualExclusionGate
{
    private static readonly object _lock = new object();

    // Returned IDisposable releases the lock -- callers take it via `using (ProfilingMutualExclusionGate.Acquire())`
    // rather than reaching into a raw lock object, so the gate can't be acquired outside this handshake.
    //
    // Invariant: once the lock is taken, there is always exactly one live path to release it.
    //   1. Releaser is allocated BEFORE the lock is taken -- an allocation failure (e.g. OOM) can't land
    //      between acquiring the lock and having the object that releases it, orphaning the gate forever.
    //   2. Monitor.Enter(ref lockTaken) reports acquisition atomically even under an async exception
    //      mid-Enter; if the lock was taken but we can't hand back the Releaser, the catch releases it.
    public static IDisposable Acquire()
    {
        // Allocate before entering: nothing that can throw now sits between Enter and returning the Releaser.
        var releaser = new Releaser();

        var lockTaken = false;
        try
        {
            Monitor.Enter(_lock, ref lockTaken);
            return releaser;
        }
        catch
        {
            if (lockTaken)
                Monitor.Exit(_lock);
            throw;
        }
    }

    private sealed class Releaser : IDisposable
    {
        private bool _released;

        public void Dispose()
        {
            if (_released)
                return;

            // Release first, mark released only on success. Monitor is thread-affine: a Dispose from a
            // thread that never Acquired throws SynchronizationLockException here. We deliberately let that
            // propagate as the signal of misuse -- every real call site is a same-thread `using`. Because
            // _released is set only AFTER a successful Exit, a stray cross-thread Dispose cannot flip the
            // flag and cause the owning thread's own Dispose to no-op; the owner can still release, so the
            // gate is never orphaned by a wrong-thread call.
            Monitor.Exit(_lock);
            _released = true;
        }
    }
}

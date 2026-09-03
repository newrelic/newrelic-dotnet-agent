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
    public static IDisposable Acquire()
    {
        Monitor.Enter(_lock);
        return new Releaser();
    }

    private sealed class Releaser : IDisposable
    {
        private bool _released;

        public void Dispose()
        {
            if (_released)
                return;

            _released = true;
            Monitor.Exit(_lock);
        }
    }
}

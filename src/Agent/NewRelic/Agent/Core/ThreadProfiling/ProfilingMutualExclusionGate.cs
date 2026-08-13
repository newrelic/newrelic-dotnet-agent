// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.Core.ThreadProfiling;

// Process-wide lock serializing the ThreadProfilingService/ContinuousProfilingService start-guard
// check-and-arm sequences, so at most one profiler can decide "the other isn't active" and arm
// itself at a time. Mirrors the native SuspendMutex (Profiler/ContinuousProfiler/SuspendMutex.h),
// which is the backstop against concurrent suspend/walk; this lock is what makes the two profilers'
// *liveness* mutually exclusive, not just their suspend calls.
public static class ProfilingMutualExclusionGate
{
    public static readonly object Lock = new object();
}

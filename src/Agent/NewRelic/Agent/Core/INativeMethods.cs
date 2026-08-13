// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Runtime.InteropServices;

namespace NewRelic.Agent.Core;

public interface INativeMethods
{
    void ReleaseProfile();
    int RequestFunctionNames(UIntPtr[] functionIds, int length, [Out] out IntPtr functionInfo);
    int RequestProfile([Out] out IntPtr snapshots, [Out] out int length);
    void ShutdownNativeThreadProfiler();

    int InstrumentationRefresh();
    int ReloadConfiguration();
    int AddCustomInstrumentation(string fileName, string xml);
    int ApplyCustomInstrumentation();

    void ContinuousProfilerStart(int intervalMs);
    void ContinuousProfilerStop();
    int ContinuousProfilerReadThreadSamples(int len, byte[] buffer);
    void ContinuousProfilerSetTraceContext(long traceIdHigh, long traceIdLow, long spanId);
    void ContinuousProfilerResetTraceContext();
    void ContinuousProfilerSetAgentWork();
    void ContinuousProfilerResetAgentWork();
    void ContinuousProfilerShutdown();

    /// <summary>
    /// Starts (or resumes) allocation sampling, capped at <paramref name="maxSamplesPerMinute"/> samples
    /// per minute. Safe to call repeatedly; pairs with <see cref="AllocationSamplerStop"/> as the
    /// enable/disable half of the lifecycle. Has no effect after <see cref="AllocationSamplerShutdown"/>.
    /// </summary>
    void AllocationSamplerStart(int maxSamplesPerMinute);

    /// <summary>
    /// Pauses allocation sampling, leaving the native EventPipe session open so a later
    /// <see cref="AllocationSamplerStart"/> can resume it. This -- never
    /// <see cref="AllocationSamplerShutdown"/> -- is what a config/command-driven disable must call.
    /// </summary>
    void AllocationSamplerStop();

    /// <summary>
    /// Tears down allocation sampling permanently: closes the native EventPipe session and drains any
    /// in-flight callback. TERMINAL -- the native sampler refuses every subsequent
    /// <see cref="AllocationSamplerStart"/>, so this must be called exactly once, at agent shutdown.
    /// </summary>
    void AllocationSamplerShutdown();

    /// <summary>
    /// Drains one filled allocation-sample buffer into <paramref name="buffer"/>, returning the number of
    /// bytes written (0 when nothing is ready). Same wire format as
    /// <see cref="ContinuousProfilerReadThreadSamples"/>, carrying opcode 0x08 allocation samples.
    /// </summary>
    int ContinuousProfilerReadAllocationSamples(int len, byte[] buffer);
}

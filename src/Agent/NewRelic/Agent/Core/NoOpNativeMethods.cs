// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;

namespace NewRelic.Agent.Core;

/// <summary>
/// <see cref="INativeMethods"/> for hosts with no New Relic profiler loaded (distro mode). Every member does
/// nothing and returns the default for its type (0, with out parameters set to their defaults).
/// </summary>
public sealed class NoOpNativeMethods : INativeMethods
{
    public void ReleaseProfile()
    {
    }

    public int RequestFunctionNames(UIntPtr[] functionIds, int length, out IntPtr functionInfo)
    {
        functionInfo = IntPtr.Zero;
        return 0;
    }

    public int RequestProfile(out IntPtr snapshots, out int length)
    {
        snapshots = IntPtr.Zero;
        length = 0;
        return 0;
    }

    public void ShutdownNativeThreadProfiler()
    {
    }

    public int InstrumentationRefresh() => 0;

    public int ReloadConfiguration() => 0;

    public int AddCustomInstrumentation(string fileName, string xml) => 0;

    public int ApplyCustomInstrumentation() => 0;

    public int ContinuousProfilerStart(int intervalMs) => 0;

    public int ContinuousProfilerStop() => 0;

    public int ContinuousProfilerReadThreadSamples(int len, byte[] buffer) => 0;

    public int ContinuousProfilerSetTraceContext(long traceIdHigh, long traceIdLow, long spanId) => 0;

    public int ContinuousProfilerResetTraceContext() => 0;

    public int ContinuousProfilerRetireSpans(long[] spanIds, int count) => 0;

    public int ContinuousProfilerGetPendingPushCell(out IntPtr cell)
    {
        cell = IntPtr.Zero;
        return 0;
    }

    public int ContinuousProfilerSetAgentWork() => 0;

    public int ContinuousProfilerResetAgentWork() => 0;

    public int ContinuousProfilerShutdown() => 0;
}

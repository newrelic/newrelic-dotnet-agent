// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;

namespace NewRelic.Agent.Core.ContinuousProfiling;

/// <summary>
/// Thin adapter over <see cref="INativeMethods"/>: every member delegates straight to the matching
/// <c>ContinuousProfiler*</c> P/Invoke, with no state of its own.
/// </summary>
public class NativeContinuousProfilerSampleSource : ISampleSource, INativeContinuousProfiler
{
    private readonly INativeMethods _nativeMethods;

    public NativeContinuousProfilerSampleSource(INativeMethods nativeMethods)
    {
        _nativeMethods = nativeMethods;
    }

    public int Start(int intervalMs) => _nativeMethods.ContinuousProfilerStart(intervalMs);

    public int Stop() => _nativeMethods.ContinuousProfilerStop();

    public int Shutdown() => _nativeMethods.ContinuousProfilerShutdown();

    public int ReadBatch(byte[] destination) => _nativeMethods.ContinuousProfilerReadThreadSamples(destination.Length, destination);

    public int SetTraceContext(long traceIdHigh, long traceIdLow, long spanId) => _nativeMethods.ContinuousProfilerSetTraceContext(traceIdHigh, traceIdLow, spanId);

    public int ResetTraceContext() => _nativeMethods.ContinuousProfilerResetTraceContext();

    public int RetireSpans(long[] spanIds, int count) => _nativeMethods.ContinuousProfilerRetireSpans(spanIds, count);

    // The one member that does not forward its status: a failure carries no address worth returning, and
    // every failure cause means the same thing to the caller ("no cell"), so it collapses to IntPtr.Zero.
    public IntPtr GetPendingPushCell()
    {
        return _nativeMethods.ContinuousProfilerGetPendingPushCell(out var cell) == 0 ? cell : IntPtr.Zero;
    }

    public int SetAgentWork() => _nativeMethods.ContinuousProfilerSetAgentWork();

    public int ResetAgentWork() => _nativeMethods.ContinuousProfilerResetAgentWork();
}

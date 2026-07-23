// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.Core.ContinuousProfiling;

/// <summary>
/// Plan-B sample source: a thin adapter over <see cref="INativeMethods"/> that both drives the native
/// continuous-profiler lifecycle (<see cref="INativeContinuousProfiler"/>) and drains its filled sample
/// buffers (<see cref="ISampleSource"/>). Every member delegates straight to the matching
/// <c>ContinuousProfiler*</c> P/Invoke; there is no state of its own. Replaces <see cref="NoOpSampleSource"/>
/// at runtime while that placeholder stays for tests/fallback.
/// </summary>
public class NativeContinuousProfilerSampleSource : ISampleSource, INativeContinuousProfiler
{
    private readonly INativeMethods _nativeMethods;

    public NativeContinuousProfilerSampleSource(INativeMethods nativeMethods)
    {
        _nativeMethods = nativeMethods;
    }

    public void Start(int intervalMs) => _nativeMethods.ContinuousProfilerStart(intervalMs);

    public void Stop() => _nativeMethods.ContinuousProfilerStop();

    public void Shutdown() => _nativeMethods.ContinuousProfilerShutdown();

    public int ReadBatch(byte[] destination) => _nativeMethods.ContinuousProfilerReadThreadSamples(destination.Length, destination);

    public void SetTraceContext(long traceIdHigh, long traceIdLow, long spanId) => _nativeMethods.ContinuousProfilerSetTraceContext(traceIdHigh, traceIdLow, spanId);

    public void ResetTraceContext() => _nativeMethods.ContinuousProfilerResetTraceContext();
}

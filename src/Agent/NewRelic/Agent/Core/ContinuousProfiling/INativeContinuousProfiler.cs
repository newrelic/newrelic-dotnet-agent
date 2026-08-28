// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.Core.ContinuousProfiling;

/// <summary>
/// Lifecycle + trace-context seam over the native continuous profiler. Kept separate from
/// <see cref="ISampleSource"/> (which owns the drain read) so the session service can depend on the
/// start/stop surface without also owning the buffer read, and so both surfaces mock independently.
/// A single object may implement both interfaces (see <see cref="NativeContinuousProfilerSampleSource"/>).
/// </summary>
public interface INativeContinuousProfiler
{
    void Start(int intervalMs);

    void Stop();

    /// <summary>Idempotent; call once during normal teardown so the thread is joined deterministically
    /// rather than relying solely on the native destructor's safety-net join.</summary>
    void Shutdown();

    void SetTraceContext(long traceIdHigh, long traceIdLow, long spanId);

    void ResetTraceContext();

    void SetAgentWork();

    void ResetAgentWork();
}

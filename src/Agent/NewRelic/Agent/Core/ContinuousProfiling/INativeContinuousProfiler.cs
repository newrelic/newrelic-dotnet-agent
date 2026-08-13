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
    /// <summary>Starts (or resumes) native sampling at the given interval.</summary>
    void Start(int intervalMs);

    /// <summary>Stops native sampling; the native worker stays alive but idle.</summary>
    void Stop();

    /// <summary>Signals the native worker thread to terminate and joins it. Idempotent; call once
    /// during normal teardown so the thread is joined deterministically rather than relying solely
    /// on the native destructor's safety-net join.</summary>
    void Shutdown();

    /// <summary>Records the calling thread's active distributed-tracing context in the native profiler.</summary>
    void SetTraceContext(long traceIdHigh, long traceIdLow, long spanId);

    /// <summary>Clears the calling thread's active distributed-tracing context in the native profiler.</summary>
    void ResetTraceContext();
}

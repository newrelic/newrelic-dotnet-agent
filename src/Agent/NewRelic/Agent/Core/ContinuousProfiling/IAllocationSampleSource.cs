// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.Core.ContinuousProfiling;

/// <summary>
/// The allocation sampler's lifecycle plus drain surface. Deliberately ONE interface rather than the
/// <see cref="ISampleSource"/> / <see cref="INativeContinuousProfiler"/> split the thread-sampling path uses:
/// that split exists so the session service can depend on start/stop without also owning the trace-context
/// push, and the allocation sampler has no trace-context surface of its own (it reads the thread sampler's
/// shared native map). Start/Stop/ReadBatch here are three views of a single small native object, so keeping
/// them together costs the service one constructor dependency instead of two.
/// </summary>
public interface IAllocationSampleSource : ISampleSource
{
    /// <summary>
    /// Starts (or re-arms) allocation sampling with the given per-minute sample budget. Idempotent, and
    /// calling it again while already started simply re-paces the sub-sampler at the new budget without
    /// opening a second native session -- which is how a live budget change is applied.
    /// </summary>
    void Start(int maxSamplesPerMinute);

    /// <summary>
    /// Stops producing samples while leaving the underlying native session open, so a later
    /// <see cref="Start"/> can resume. This -- never <see cref="Shutdown"/> -- is what a disable must call.
    /// </summary>
    void Stop();

    /// <summary>
    /// TERMINAL teardown: closes the native session and drains in-flight sampling. The native sampler latches
    /// on this call and REFUSES every subsequent <see cref="Start"/> for the life of the process, so this must
    /// be called exactly once, only from real agent teardown. Any path that can run more than once (a config
    /// toggle, an agent command, a retune) must use <see cref="Stop"/>/<see cref="Start"/> instead.
    /// </summary>
    void Shutdown();
}

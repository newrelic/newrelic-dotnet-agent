// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;

namespace NewRelic.Agent.Core.ContinuousProfiling;

/// <summary>
/// Lifecycle + trace-context seam over the native continuous profiler. Kept separate from
/// <see cref="ISampleSource"/> (which owns the drain read) so the session service can depend on the
/// start/stop surface without also owning the buffer read, and so both surfaces mock independently.
/// A single object may implement both interfaces (see <see cref="NativeContinuousProfilerSampleSource"/>).
/// </summary>
public interface INativeContinuousProfiler
{
    // Every member returns an int status (0 == success, non-zero == an HRESULT-style failure) so a
    // non-throwing native failure can reach managed code, carrying the status the underlying
    // INativeMethods ABI now provides. ContinuousProfilingService acts on Start's result (see StartLocked /
    // TryResumeSamplingLocked); the remaining calls treat theirs as best-effort but the channel still exists.
    int Start(int intervalMs);

    int Stop();

    /// <summary>Idempotent; call once during normal teardown so the thread is joined deterministically
    /// rather than relying solely on the native destructor's safety-net join.</summary>
    int Shutdown();

    int SetTraceContext(long traceIdHigh, long traceIdLow, long spanId);

    int ResetTraceContext();

    /// <summary>
    /// Marks the first <paramref name="count"/> span ids in <paramref name="spanIds"/> as ended. Called
    /// once per terminating transaction, possibly on the GC finalizer thread.
    /// </summary>
    int RetireSpans(long[] spanIds, int count);

    /// <summary>
    /// Returns the CALLING thread's native pending-push cell, or <see cref="IntPtr.Zero"/> when the native
    /// side could not supply one. Unlike the other members this projects the status into the return value:
    /// there is nothing a caller can do with a failure except push without publishing its intent first,
    /// which is the pre-cell behaviour. Valid only for the calling thread and only until that thread exits,
    /// so it must never be cached anywhere with a longer lifetime than the thread.
    /// </summary>
    IntPtr GetPendingPushCell();

    int SetAgentWork();

    int ResetAgentWork();
}

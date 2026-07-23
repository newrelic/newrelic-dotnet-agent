// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.Core.ContinuousProfiling;

/// <summary>
/// The managed-to-native trace-context push seam. Called on the executing application thread so the
/// native profiler can key the pushed context by that thread's OS thread id (the Task-4 TLS contract).
/// Kept behind an interface so the hot-path caller (the wrapper pipeline) can be handed an inert
/// default while continuous profiling is disabled, and a real instance only while it is enabled.
/// </summary>
public interface IContinuousProfilingContext
{
    /// <summary>
    /// Cheap gate for the hot path: a single field read that is <c>false</c> while continuous profiling is
    /// off, so the wrapper pipeline pays essentially nothing (no decompose, no native call) when disabled.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Records the calling thread's current distributed-tracing context in the native profiler by decomposing
    /// the W3C-style hex ids into the (high, low, span) longs the native side and <see cref="OtlpProfileBuilder"/>
    /// expect. A no-op while disabled. Never throws into the application.
    /// </summary>
    /// <param name="traceId">32-char (16-byte) lowercase-or-uppercase hex trace id, or null when there is no trace.</param>
    /// <param name="spanId">16-char (8-byte) hex span id, or null when there is no span.</param>
    void PushTraceContext(string traceId, string spanId);

    /// <summary>
    /// Clears the calling thread's distributed-tracing context in the native profiler. A no-op while disabled.
    /// Never throws into the application.
    /// </summary>
    void ResetTraceContext();
}

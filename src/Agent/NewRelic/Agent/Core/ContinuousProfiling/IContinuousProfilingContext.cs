// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;

namespace NewRelic.Agent.Core.ContinuousProfiling;

/// <summary>
/// The managed-to-native trace-context push seam. Called on the executing application thread so the
/// native profiler can key the pushed context by that thread's OS thread id (the Task-4 TLS contract).
/// Kept behind an interface so the hot-path caller (the wrapper pipeline) can be handed an inert
/// default while continuous profiling is disabled, and a real instance only while it is enabled.
/// </summary>
public interface IContinuousProfilingContext
{
    /// <summary>Cheap gate for the hot path: <c>false</c> while continuous profiling is off.</summary>
    bool IsEnabled { get; }

    /// <param name="traceId">32-char (16-byte) lowercase-or-uppercase hex trace id, or null when there is no trace.</param>
    /// <param name="spanId">16-char (8-byte) hex span id, or null when there is no span.</param>
    void PushTraceContext(string traceId, string spanId);

    void ResetTraceContext();

    /// <summary>
    /// Marks every span id in <paramref name="spanIdHex"/> as ended, so no later CPU sample is linked to
    /// a span whose transaction has finished. Ids are the raw 16-char hex form <c>Segment</c> holds;
    /// malformed or null entries are skipped. Called once per terminating transaction. Never throws.
    /// </summary>
    void RetireSpans(IReadOnlyList<string> spanIdHex);

    /// <summary>Nesting-safe; must be paired 1:1 with <see cref="ResetAgentWork"/>.</summary>
    void SetAgentWork();

    void ResetAgentWork();
}

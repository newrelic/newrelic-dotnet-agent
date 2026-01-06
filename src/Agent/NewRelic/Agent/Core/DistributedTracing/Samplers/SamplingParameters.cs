// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.Core.DistributedTracing.Samplers;

public class SamplingParameters(string traceId, float priority, W3CTraceContext traceContext = null, bool newRelicTraceContextWasAccepted = false, DistributedTracePayload newRelicPayload = null, bool newRelicPayloadWasAccepted = false) : ISamplingParameters
{
    public string TraceId { get; } = traceId;
    public float Priority { get; } = priority;
    public W3CTraceContext TraceContext { get; } = traceContext;
    public bool NewRelicTraceContextWasAccepted { get; } = newRelicTraceContextWasAccepted;
    public DistributedTracePayload NewRelicPayload { get; } = newRelicPayload;
    public bool NewRelicPayloadWasAccepted { get; } = newRelicPayloadWasAccepted;
}

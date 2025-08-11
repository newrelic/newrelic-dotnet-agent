// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.Core.DistributedTracing.Samplers;

public class SamplingParameters(string traceId, float priority) : ISamplingParameters
{
    public string TraceId { get; } = traceId;
    public float Priority { get; } = priority;
}

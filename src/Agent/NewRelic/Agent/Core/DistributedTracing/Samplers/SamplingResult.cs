// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.Core.DistributedTracing.Samplers;

public class SamplingResult(bool sampled, float priority) : ISamplingResult
{
    public bool Sampled { get; } = sampled;
    public float Priority { get; } = priority;
}

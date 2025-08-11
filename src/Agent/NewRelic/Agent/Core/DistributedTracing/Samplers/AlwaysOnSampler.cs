// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.Core.DistributedTracing.Samplers;

public class AlwaysOnSampler : ISampler
{
    public static AlwaysOnSampler Instance { get; } = new AlwaysOnSampler();
    private AlwaysOnSampler()
    {
    }

    private static readonly SamplingResult _samplingResult = new SamplingResult(true, 2.0f);
    public ISamplingResult ShouldSample(ISamplingParameters samplingParameters) => _samplingResult;

    public void StartTransaction()
    {
    }
}

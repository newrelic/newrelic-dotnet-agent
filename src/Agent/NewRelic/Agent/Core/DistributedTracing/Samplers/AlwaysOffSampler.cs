// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.Core.DistributedTracing.Samplers;

public class AlwaysOffSampler: ISampler
{
    public static AlwaysOffSampler Instance { get; } = new AlwaysOffSampler();
    private AlwaysOffSampler()
    {
    }

    private static readonly SamplingResult _samplingResult = new SamplingResult(false, 0.0f);
    public ISamplingResult ShouldSample(ISamplingParameters samplingParameters) => _samplingResult;
    public void StartTransaction()
    {
    }
}

// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.Core.DistributedTracing.Samplers;

public class AlwaysOnSampler : ISampler
{
    public ISamplingResult ShouldSample(ISamplingParameters samplingParameters) => new SamplingResult(true, 2.0f);

    public void StartTransaction()
    {
    }
}

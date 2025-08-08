// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.Core.DistributedTracing.Samplers;

public class AlwaysOffSampler: ISampler
{
    public ISamplingResult ShouldSample(ISamplingParameters samplingParameters) => new SamplingResult(false, 0.0f);
    public void StartTransaction()
    {
    }
}

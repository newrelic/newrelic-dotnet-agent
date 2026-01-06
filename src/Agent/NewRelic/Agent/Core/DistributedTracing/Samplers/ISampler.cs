// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.Core.DistributedTracing.Samplers;

public interface ISampler
{
    /// <summary>
    /// Decides whether to sample a trace based on the provided sampling parameters.
    /// </summary>
    /// <param name="samplingParameters"></param>
    /// <returns>The sampling result</returns>
    ISamplingResult ShouldSample(ISamplingParameters samplingParameters);

    void StartTransaction();
}

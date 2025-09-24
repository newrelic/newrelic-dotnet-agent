// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Configuration;

namespace NewRelic.Agent.Core.DistributedTracing.Samplers
{
    public interface ISamplerService
    {
        /// <summary>
        /// Returns the appropriately configured sampler based on the SamplerType
        /// </summary>
        /// <param name="samplerLevel"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        ISampler GetSampler(SamplerLevel samplerLevel);

        /// <summary>
        /// Used only for testing to replace the adaptive sampler with a mock or stub.
        /// </summary>
        /// <param name="sampler">The mock or stub sampler to use for testing.</param>
        void ReplaceAdaptiveSamplerForTesting(ISampler sampler);
    }
}

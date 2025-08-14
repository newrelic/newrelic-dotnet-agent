// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;

namespace NewRelic.Agent.Core.DistributedTracing.Samplers
{
    public interface ISamplerService
    {
        /// <summary>
        /// Returns the appropriately configured sampler based on the SamplerType
        /// </summary>
        /// <param name="samplerType"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        ISampler GetSampler(SamplerType samplerType);
    }
}

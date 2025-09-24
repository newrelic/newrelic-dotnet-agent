// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Extensions.Logging;

namespace NewRelic.Agent.Core.DistributedTracing.Samplers
{
    public class SamplerFactory : ConfigurationBasedService, ISamplerFactory
    {
        public SamplerFactory()
        {
        }

        public ISampler CreateSampler(SamplerLevel samplerLevel, SamplerType samplerType, float? traceIdRatioSamplerRatio)
        {
            switch (samplerType)
            {
                // only the root sampler can use the adaptive sampler
                case SamplerType.Default:
                case SamplerType.Adaptive:
                    return samplerLevel == SamplerLevel.Root ? GetAdaptiveSampler() : null;
                case SamplerType.AlwaysOn:
                    return AlwaysOnSampler.Instance;
                case SamplerType.AlwaysOff:
                    return AlwaysOffSampler.Instance;

                // if the ratio is not set, log a warning and use the default sampler
                case SamplerType.TraceIdRatioBased when !traceIdRatioSamplerRatio.HasValue:
                    Log.Warn($"The configured TraceIdRatioBased sampler for {samplerLevel} is missing a ratio value. Using default sampler.");
                    return samplerLevel == SamplerLevel.Root ? GetAdaptiveSampler() : null;
                case SamplerType.TraceIdRatioBased:
                    return new TraceIdRatioSampler(traceIdRatioSamplerRatio.Value);
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private ISampler GetAdaptiveSampler() =>
            new AdaptiveSampler(
                _configuration.SamplingTarget ?? AdaptiveSampler.DefaultTargetSamplesPerInterval,
                _configuration.SamplingTargetPeriodInSeconds ?? AdaptiveSampler.DefaultTargetSamplingIntervalInSeconds, null,
                _configuration.ServerlessModeEnabled);

        protected override void OnConfigurationUpdated(ConfigurationUpdateSource configurationUpdateSource)
        {
        }
    }
}

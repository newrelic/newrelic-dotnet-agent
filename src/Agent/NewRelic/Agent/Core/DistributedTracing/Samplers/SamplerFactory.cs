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
        // we use a single instance of the adaptive sampler because it manages its own state internally
        private ISampler _adaptiveSampler;

        public SamplerFactory()
        {
            _adaptiveSampler = GetAdaptiveSampler();
        }

        public ISampler CreateSampler(SamplerType samplerType, float? traceIdRatioSamplerRatio)
        {
            switch (samplerType)
            {
                case SamplerType.Default:
                case SamplerType.Adaptive:
                    return _adaptiveSampler;
                case SamplerType.AlwaysOn:
                    return AlwaysOnSampler.Instance;
                case SamplerType.AlwaysOff:
                    return AlwaysOffSampler.Instance;

                // if the ratio is not set, log a warning and use the default sampler
                case SamplerType.TraceIdRatioBased when !traceIdRatioSamplerRatio.HasValue:
                    Log.Warn($"The configured TraceIdRatioBased sampler is missing a ratio value. Using default sampler.");
                    return _adaptiveSampler;
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
            _adaptiveSampler = GetAdaptiveSampler(); // create a new adaptive sampler with the updated configuration
        }

    }
}

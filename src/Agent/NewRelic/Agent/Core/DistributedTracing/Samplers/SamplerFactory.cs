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
        private AdaptiveSampler _adaptiveSampler;

        public SamplerFactory()
        {
            _adaptiveSampler = GetAdaptiveSampler();
        }

        // virtual to allow for mocking in tests
        public virtual ISampler GetSampler(SamplerType samplerType, float? traceIdRatioSamplerRatio)
        {
           switch (samplerType)
            {
                case SamplerType.Adaptive:
                    return _adaptiveSampler;
                case SamplerType.AlwaysOn:
                    return AlwaysOnSampler.Instance;
                case SamplerType.AlwaysOff:
                    return AlwaysOffSampler.Instance;
                case SamplerType.TraceIdRatioBased:
                    // if the ratio is not set, log a warning and use the default sampler
                    if (traceIdRatioSamplerRatio.HasValue)
                        return new TraceIdRatioSampler(traceIdRatioSamplerRatio.Value); // always return a new instance since it is stateless

                    Log.Warn($"The configured TraceIdRatioBased sampler is missing a ratio value. Using default sampler.");
                    return _adaptiveSampler;
                default:
                    throw new ArgumentOutOfRangeException(nameof(samplerType), samplerType, null);
            }
        }

        private AdaptiveSampler GetAdaptiveSampler() =>
            new(
                _configuration.SamplingTarget ?? AdaptiveSampler.DefaultTargetSamplesPerInterval,
                _configuration.SamplingTargetPeriodInSeconds ?? AdaptiveSampler.DefaultTargetSamplingIntervalInSeconds, null,
                _configuration.ServerlessModeEnabled);

        protected override void OnConfigurationUpdated(ConfigurationUpdateSource configurationUpdateSource)
        {
            // update the AdaptiveSampler's sampling target and period, which will start a new sampling interval.
            _adaptiveSampler.UpdateSamplingTarget(_configuration.SamplingTarget ?? AdaptiveSampler.DefaultTargetSamplesPerInterval, _configuration.SamplingTargetPeriodInSeconds ?? AdaptiveSampler.DefaultTargetSamplingIntervalInSeconds);
        }

    }
}

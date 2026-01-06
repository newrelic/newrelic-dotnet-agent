// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Threading;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Extensions.Logging;

namespace NewRelic.Agent.Core.DistributedTracing.Samplers
{
    public class SamplerFactory : ConfigurationBasedService, ISamplerFactory
    {
        // we use a single instance of the adaptive sampler because it manages its own state internally
        // Now lazily initialized so we only create it if/when needed and after the most recent configuration is available.
        private readonly Lazy<AdaptiveSampler> _adaptiveSampler;

        public SamplerFactory()
        {
            _adaptiveSampler = new Lazy<AdaptiveSampler>(CreateAdaptiveSampler, LazyThreadSafetyMode.ExecutionAndPublication);
        }

        // virtual to allow for mocking in tests
        public virtual ISampler GetSampler(SamplerType samplerType, float? traceIdRatioSamplerRatio)
        {
            switch (samplerType)
            {
                case SamplerType.Adaptive:
                    return _adaptiveSampler.Value;
                case SamplerType.AlwaysOn:
                    return AlwaysOnSampler.Instance;
                case SamplerType.AlwaysOff:
                    return AlwaysOffSampler.Instance;
                case SamplerType.TraceIdRatioBased:
                    // if the ratio is not set, log a warning and use the default sampler
                    if (traceIdRatioSamplerRatio.HasValue)
                        return new TraceIdRatioBasedSampler(traceIdRatioSamplerRatio.Value); // always return a new instance since it is stateless

                    Log.Warn($"The configured TraceIdRatioBased sampler is missing a ratio value. Using default sampler.");
                    return _adaptiveSampler.Value;
                default:
                    throw new ArgumentOutOfRangeException(nameof(samplerType), samplerType, null);
            }
        }

        // Factory method used by Lazy<T> so the latest configuration values are applied at first use.
        private AdaptiveSampler CreateAdaptiveSampler() =>
            new(
                _configuration.SamplingTarget ?? AdaptiveSampler.DefaultTargetSamplesPerInterval,
                _configuration.SamplingTargetPeriodInSeconds ?? AdaptiveSampler.DefaultTargetSamplingIntervalInSeconds,
                null,
                _configuration.ServerlessModeEnabled);

        protected override void OnConfigurationUpdated(ConfigurationUpdateSource configurationUpdateSource)
        {
            // Ensure an adaptive sampler exists after a configuration update so subsequent requests
            // immediately use a sampler reflecting the newest configuration.
            if (!_adaptiveSampler.IsValueCreated)
            {
                _ = _adaptiveSampler.Value; // forces creation with current configuration
                return;
            }

            // If already created, update its sampling target and period (starts a new interval).
            _adaptiveSampler.Value.UpdateSamplingTarget(
                _configuration.SamplingTarget ?? AdaptiveSampler.DefaultTargetSamplesPerInterval,
                _configuration.SamplingTargetPeriodInSeconds ?? AdaptiveSampler.DefaultTargetSamplingIntervalInSeconds);
        }

    }
}

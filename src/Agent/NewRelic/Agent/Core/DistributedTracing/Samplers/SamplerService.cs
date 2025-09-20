// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Extensions.Logging;

namespace NewRelic.Agent.Core.DistributedTracing.Samplers;

public class SamplerService : ConfigurationBasedService, ISamplerService
{
    private readonly Dictionary<SamplerLevel, ISampler> _samplers = new();

    public SamplerService()
    {
        InitializeSamplers();
    }

    /// <summary>
    /// Returns the appropriate sampler based on the SamplerLevel.
    /// Will be <c>null</c> for RemoteParentSampled or RemoteParentNotSampled if the behavior is set to Default.
    /// </summary>
    /// <param name="SamplerLevel"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public ISampler GetSampler(SamplerLevel SamplerLevel) => _samplers.TryGetValue(SamplerLevel, out var sampler) ? sampler : throw new ArgumentOutOfRangeException();

    protected override void OnConfigurationUpdated(ConfigurationUpdateSource configurationUpdateSource)
    {
        InitializeSamplers();

        // if the root sampler is the adaptive sampler,
        // update its sampling target and period, which will start a new sampling interval.
        if (_samplers.TryGetValue(SamplerLevel.Root, out var rootSampler) && rootSampler is AdaptiveSampler adaptiveSampler)
        {
            adaptiveSampler.UpdateSamplingTarget(_configuration.SamplingTarget ?? AdaptiveSampler.DefaultTargetSamplesPerInterval, _configuration.SamplingTargetPeriodInSeconds ?? AdaptiveSampler.DefaultTargetSamplingIntervalInSeconds);
        }
    }

    private void InitializeSamplers()
    {
        _samplers[SamplerLevel.Root] = GetConfiguredSampler(SamplerLevel.Root, _configuration.RootSamplerType, _configuration.RootTraceIdRatioSamplerRatio);
        _samplers[SamplerLevel.RemoteParentSampled] = GetConfiguredSampler(SamplerLevel.RemoteParentSampled, _configuration.RemoteParentSampledSamplerType, _configuration.RemoteParentSampledTraceIdRatioSamplerRatio);
        _samplers[SamplerLevel.RemoteParentNotSampled] = GetConfiguredSampler(SamplerLevel.RemoteParentNotSampled, _configuration.RemoteParentNotSampledSamplerType, _configuration.RemoteParentNotSampledTraceIdRatioSamplerRatio);
    }

    private ISampler GetConfiguredSampler(SamplerLevel samplerLevel, SamplerType samplerType, float? traceIdRatioSamplerRatio)
    {
        switch (samplerType)
        {
            // only the root sampler can use the adaptive sampler
            case SamplerType.Default:
                return samplerLevel == SamplerLevel.Root
                    ? new AdaptiveSampler(
                        _configuration.SamplingTarget ?? AdaptiveSampler.DefaultTargetSamplesPerInterval,
                        _configuration.SamplingTargetPeriodInSeconds ??
                        AdaptiveSampler.DefaultTargetSamplingIntervalInSeconds, null,
                        _configuration.ServerlessModeEnabled)
                    : null;
            case SamplerType.AlwaysOn:
                return AlwaysOnSampler.Instance;
            case SamplerType.AlwaysOff:
                return AlwaysOffSampler.Instance;

            // if the ratio is not set, log a warning and use the default sampler
            case SamplerType.TraceIdRatioBased when !traceIdRatioSamplerRatio.HasValue:
                Log.Warn($"The configured TraceIdRatioBased sampler for {samplerLevel} is missing a ratio value. Using default sampler.");

                return samplerLevel == SamplerLevel.Root
                    ? new AdaptiveSampler(
                        _configuration.SamplingTarget ?? AdaptiveSampler.DefaultTargetSamplesPerInterval,
                        _configuration.SamplingTargetPeriodInSeconds ??
                        AdaptiveSampler.DefaultTargetSamplingIntervalInSeconds, null,
                        _configuration.ServerlessModeEnabled)
                    : null;
            case SamplerType.TraceIdRatioBased:
                return new TraceIdRatioSampler(traceIdRatioSamplerRatio.Value);
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    /// <summary>
    /// Used only for testing to replace the adaptive sampler with a mock or stub.
    /// </summary>
    /// <param name="sampler">The mock or stub sampler to use for testing.</param>
    public void ReplaceAdaptiveSamplerForTesting(ISampler sampler)
    {
        foreach (var key in _samplers.Keys.ToList())
        {
            if (_samplers[key] is AdaptiveSampler)
            {
                _samplers[key] = sampler;
            }
        }
    }
}

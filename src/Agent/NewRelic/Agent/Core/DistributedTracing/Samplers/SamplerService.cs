// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Utilities;

namespace NewRelic.Agent.Core.DistributedTracing.Samplers;

public class SamplerService : ConfigurationBasedService, ISamplerService
{
    private readonly ISamplerFactory _samplerFactory;
    private readonly Dictionary<SamplerLevel, ISampler> _samplers = new();

    public SamplerService(ISamplerFactory samplerFactory)
    {
        _samplerFactory = samplerFactory;
        InitializeSamplers();
    }

    /// <summary>
    /// Returns the appropriate sampler based on the SamplerLevel.
    /// Will be <c>null</c> for RemoteParentSampled or RemoteParentNotSampled if the behavior is set to Default.
    /// </summary>
    /// <param name="samplerLevel"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public ISampler GetSampler(SamplerLevel samplerLevel) => _samplers.TryGetValue(samplerLevel, out var sampler) ? sampler : throw new ArgumentOutOfRangeException();

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
        _samplers[SamplerLevel.Root] = _samplerFactory.CreateSampler(_configuration.RootSamplerType, _configuration.RootTraceIdRatioSamplerRatio);
        _samplers[SamplerLevel.RemoteParentSampled] = _samplerFactory.CreateSampler(_configuration.RemoteParentSampledSamplerType, _configuration.RemoteParentSampledTraceIdRatioSamplerRatio);
        _samplers[SamplerLevel.RemoteParentNotSampled] = _samplerFactory.CreateSampler(_configuration.RemoteParentNotSampledSamplerType, _configuration.RemoteParentNotSampledTraceIdRatioSamplerRatio);
    }
}

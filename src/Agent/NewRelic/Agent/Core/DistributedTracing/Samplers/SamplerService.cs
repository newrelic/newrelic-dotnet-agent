// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Extensions.Logging;

namespace NewRelic.Agent.Core.DistributedTracing.Samplers;

public class SamplerService : ConfigurationBasedService, ISamplerService
{
    private readonly Dictionary<SamplerType, ISampler> _samplers = new();

    public SamplerService()
    {
        InitializeSamplers();
    }

    /// <summary>
    /// Returns the appropriate sampler based on the SamplerType.
    /// Will be <c>null</c> for RemoteParentSampled or RemoteParentNotSampled if the behavior is set to Default.
    /// </summary>
    /// <param name="samplerType"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public ISampler GetSampler(SamplerType samplerType) => _samplers.TryGetValue(samplerType, out var sampler) ? sampler : throw new ArgumentOutOfRangeException();

    protected override void OnConfigurationUpdated(ConfigurationUpdateSource configurationUpdateSource)
    {
        InitializeSamplers();

        // if the root sampler is the adaptive sampler,
        // update its sampling target and period, which will start a new sampling interval.
        if (_samplers.TryGetValue(SamplerType.Root, out var rootSampler) &&
            rootSampler is AdaptiveSampler adaptiveSampler &&
            !_configuration.TraceIdRatioBasedSamplingEnabled) // TODO: This needs to use the root-level sampler configuration setting when implemented
        {
            adaptiveSampler.UpdateSamplingTarget(_configuration.SamplingTarget ?? AdaptiveSampler.DefaultTargetSamplesPerInterval, _configuration.SamplingTargetPeriodInSeconds ?? AdaptiveSampler.DefaultTargetSamplingIntervalInSeconds);
        }
    }

    private void InitializeSamplers()
    {
        if (_configuration.TraceIdRatioBasedSamplingEnabled) // TODO: This needs to use the root-level sampler configuration setting when implemented
        {
            Log.Finest("Trace ID ratio based sampling is enabled. Using TracedIdRatioSampler for root sampling.");
            _samplers[SamplerType.Root] = new TraceIdRatioSampler(_configuration.TraceIdRatioBasedSamplingRatio.Value);
        }
        else
        {
            Log.Finest("Trace ID ratio based sampling is not enabled. Using the default AdaptiveSampler for root sampling.");
            _samplers[SamplerType.Root] = new AdaptiveSampler(_configuration.SamplingTarget ?? AdaptiveSampler.DefaultTargetSamplesPerInterval, _configuration.SamplingTargetPeriodInSeconds ?? AdaptiveSampler.DefaultTargetSamplingIntervalInSeconds, null, _configuration.ServerlessModeEnabled);
        }
        _samplers[SamplerType.RemoteParentSampled] = GetRemoteParentSampler(_configuration.RemoteParentSampledBehavior);
        _samplers[SamplerType.RemoteParentNotSampled] = GetRemoteParentSampler(_configuration.RemoteParentNotSampledBehavior);
    }

    private ISampler GetRemoteParentSampler(RemoteParentSampledBehavior behavior)
    {
        return behavior switch
        {
            RemoteParentSampledBehavior.Default => null,
            RemoteParentSampledBehavior.AlwaysOn => AlwaysOnSampler.Instance,
            RemoteParentSampledBehavior.AlwaysOff => AlwaysOffSampler.Instance,
            RemoteParentSampledBehavior.TraceIdRatioBased => new TraceIdRatioSampler(_configuration.TraceIdRatioBasedSamplingRatio.Value),
            _ => throw new ArgumentOutOfRangeException()
        };
    }
}

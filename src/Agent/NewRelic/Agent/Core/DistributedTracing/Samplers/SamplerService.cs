// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Extensions.Logging;

namespace NewRelic.Agent.Core.DistributedTracing.Samplers;

public enum SamplerType
{
    Root,
    RemoteParentSampled,
    RemoteParentNotSampled,
}

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

public class SamplerService : ConfigurationBasedService, ISamplerService
{
    private ISampler _rootSampler;
    private ISampler _remoteParentSampledSampler;
    private ISampler _remoteParentNotSampledSampler;

    protected override void OnConfigurationUpdated(ConfigurationUpdateSource configurationUpdateSource)
    {
        // if the root sampler is the adaptive sampler, update its sampling target and period if necessary
        if (_rootSampler is AdaptiveSampler adaptiveSampler &&
            !_configuration.TraceIdRatioBasedSamplingEnabled)
        {
            Log.Finest("Configuration updated. Updating AdaptiveSampler with new sampling target and period.");
            adaptiveSampler.UpdateSamplingTarget(_configuration.SamplingTarget ?? AdaptiveSampler.DefaultTargetSamplesPerInterval, _configuration.SamplingTargetPeriodInSeconds ?? AdaptiveSampler.DefaultTargetSamplingIntervalInSeconds);
        }
        else
        {
            Log.Finest("Configuration updated. Reinitializing samplers.");
            InitializeSamplers();
        }
    }

    private void InitializeSamplers()
    {
        if (_configuration.TraceIdRatioBasedSamplingEnabled) // TODO: This needs to use the root-level sampler configuration setting when implemented
        {
            Log.Finest("Trace ID ratio based sampling is enabled. Using TracedIdRatioSampler for root sampling.");
            _rootSampler = new TraceIdRatioSampler(_configuration.TraceIdRatioBasedSamplingRatio.Value);
        }
        else
        {
            Log.Finest("Trace ID ratio based sampling is not enabled. Using the default AdaptiveSampler for root sampling.");
            _rootSampler = new AdaptiveSampler(_configuration.SamplingTarget ?? AdaptiveSampler.DefaultTargetSamplesPerInterval, _configuration.SamplingTargetPeriodInSeconds ?? AdaptiveSampler.DefaultTargetSamplingIntervalInSeconds, null, _configuration.ServerlessModeEnabled);
        }
        _remoteParentSampledSampler = GetRemoteParentSampler(_configuration.RemoteParentSampledBehavior);
        _remoteParentNotSampledSampler = GetRemoteParentSampler(_configuration.RemoteParentNotSampledBehavior);
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

    /// <summary>
    /// Returns the appropriate sampler based on the SamplerType. Will be <c>null</c> for RemoteParentSampled or RemoteParentNotSampled if the behavior is set to Default.
    /// </summary>
    /// <param name="samplerType"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public ISampler GetSampler(SamplerType samplerType)
    {
        return samplerType switch
        {
            SamplerType.Root => _rootSampler,
            SamplerType.RemoteParentSampled => _remoteParentSampledSampler,
            SamplerType.RemoteParentNotSampled => _remoteParentNotSampledSampler,
            _ => throw new ArgumentOutOfRangeException(nameof(samplerType), samplerType, null)
        };
    }
}

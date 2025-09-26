// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Utilities;

namespace NewRelic.Agent.Core.DistributedTracing.Samplers;

public class SamplerService : ConfigurationBasedService, ISamplerService
{
    private readonly ISamplerFactory _samplerFactory;
    private readonly Dictionary<SamplerLevel, ISampler> _samplers = new();
    private readonly object _samplerInitLock = new();

    public SamplerService(ISamplerFactory samplerFactory)
    {
        _samplerFactory = samplerFactory;
    }

    /// <summary>
    /// Returns the appropriate sampler based on the SamplerLevel.
    /// Will be <c>null</c> for RemoteParentSampled or RemoteParentNotSampled if the behavior is set to Default.
    /// </summary>
    /// <param name="samplerLevel"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public ISampler GetSampler(SamplerLevel samplerLevel)
    {
        EnsureSamplersInitialized();

        return _samplers.TryGetValue(samplerLevel, out var sampler)
            ? sampler
            : throw new ArgumentOutOfRangeException(nameof(samplerLevel), samplerLevel, null);
    }

    protected override void OnConfigurationUpdated(ConfigurationUpdateSource configurationUpdateSource)
    {
        // Invalidate so we lazily rebuild with the latest configuration on next access.
        lock (_samplerInitLock)
        {
            _samplers.Clear();
        }
    }

    private void EnsureSamplersInitialized()
    {
        if (_samplers.Any())
            return;

        lock (_samplerInitLock)
        {
            if (!_samplers.Any())
            {
                InitializeSamplers();
            }
        }
    }

    private void InitializeSamplers()
    {
        // Always create all sampler entries based on current configuration.
        _samplers[SamplerLevel.Root] = _samplerFactory.GetSampler(_configuration.RootSamplerType, _configuration.RootTraceIdRatioSamplerRatio);
        _samplers[SamplerLevel.RemoteParentSampled] = _samplerFactory.GetSampler(_configuration.RemoteParentSampledSamplerType, _configuration.RemoteParentSampledTraceIdRatioSamplerRatio);
        _samplers[SamplerLevel.RemoteParentNotSampled] = _samplerFactory.GetSampler(_configuration.RemoteParentNotSampledSamplerType, _configuration.RemoteParentNotSampledTraceIdRatioSamplerRatio);
    }
}

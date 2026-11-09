// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Core.Config;

namespace NewRelic.Agent.Core.OtelDistro;

/// <summary>
/// Wraps the <see cref="IBootstrapConfiguration"/> agent-mode-shared startup loads, overriding only
/// <see cref="DistroModeEnabled"/> to true. <see cref="DistroAgentManager"/>'s <c>AdaptBootstrapConfig</c>
/// applies this wrapper in distro mode (agent mode never overrides that seam and so never constructs it), so
/// agent mode's bootstrap configuration instance -- and every value read from it -- is unchanged.
/// </summary>
public sealed class DistroBootstrapConfiguration : IBootstrapConfiguration
{
    private readonly IBootstrapConfiguration _inner;

    public DistroBootstrapConfiguration(IBootstrapConfiguration inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    public bool ServerlessModeEnabled => _inner.ServerlessModeEnabled;
    public int DebugStartupDelaySeconds => _inner.DebugStartupDelaySeconds;
    public string ConfigurationFileName => _inner.ConfigurationFileName;
    public bool AgentEnabled => _inner.AgentEnabled;
    public string AgentEnabledAt => _inner.AgentEnabledAt;
    public ILogConfig LogConfig => _inner.LogConfig;
    public string ServerlessFunctionName => _inner.ServerlessFunctionName;
    public string ServerlessFunctionVersion => _inner.ServerlessFunctionVersion;
    public bool AzureFunctionModeDetected => _inner.AzureFunctionModeDetected;
    public bool GCSamplerV2Enabled => _inner.GCSamplerV2Enabled;
    public bool AgentControlEnabled => _inner.AgentControlEnabled;
    public string HealthDeliveryLocation => _inner.HealthDeliveryLocation;
    public int HealthFrequency => _inner.HealthFrequency;

    public bool DistroModeEnabled => true;
}

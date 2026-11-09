// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Api;
using NewRelic.Agent.Core.Config;
using NewRelic.Agent.Core.DependencyInjection;
using NewRelic.Agent.Core.Events;

namespace NewRelic.Agent.Core;

/// <summary>
/// The process-wide statics the <see cref="AgentManagerBase"/> start-up and shutdown sequence touches, one
/// member per static, so the sequence itself can be unit tested. <see cref="AgentManagerEnvironment"/> is the
/// production pass-through.
/// </summary>
public interface IAgentManagerEnvironment
{
    string ExtensionsDirectory { get; }

    (configuration LocalConfig, IBootstrapConfiguration BootstrapConfig) LoadConfiguration();

    void ConfigureDefaultLogger();

    void ConfigureLogger(ILogConfig logConfig);

    void InitializeBootstrapLogger();

    IContainer CreateContainer();

    void RegisterServices(IContainer container, bool serverlessModeEnabled, bool gcSamplerV2Enabled, bool distroModeEnabled);

    void StartServices(IContainer container, bool serverlessModeEnabled, bool gcSamplerV2Enabled, bool distroModeEnabled);

    void PublishDeserializedConfiguration(configuration localConfig);

    void SubscribeToKillAgent(Action<KillAgentEvent> handler);

    void UnsubscribeFromKillAgent(Action<KillAgentEvent> handler);

    void InitializeExtensions(string extensionsDirectory);

    void SetAgentApiImplementation(IAgentApi agentApi);

    void SubscribeToExit(EventHandler handler);

    void UnsubscribeFromExit(EventHandler handler);

    void MarkAgentShuttingDown();

    void PublishCleanShutdownEvents();

    void CloseLogging();
}

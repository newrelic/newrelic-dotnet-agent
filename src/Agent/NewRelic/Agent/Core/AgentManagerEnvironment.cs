// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Api;
using NewRelic.Agent.Core.Config;
using NewRelic.Agent.Core.DependencyInjection;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Logging;
using NewRelic.Agent.Core.Utilities;

namespace NewRelic.Agent.Core;

/// <summary>
/// Production <see cref="IAgentManagerEnvironment"/>: each member passes straight through to the static
/// agent mode has always used.
/// </summary>
public class AgentManagerEnvironment : IAgentManagerEnvironment
{
    public string ExtensionsDirectory => AgentInstallConfiguration.InstallPathExtensionsDirectory;

    public (configuration LocalConfig, IBootstrapConfiguration BootstrapConfig) LoadConfiguration()
    {
        var localConfig = ConfigurationLoader.Initialize(false);
        return (localConfig, ConfigurationLoader.BootstrapConfig);
    }

    public void ConfigureDefaultLogger()
    {
        LoggerBootstrapper.ConfigureLogger(BootstrapConfiguration.GetDefault().LogConfig);
    }

    public void ConfigureLogger(ILogConfig logConfig)
    {
        LoggerBootstrapper.ConfigureLogger(logConfig);
    }

    public void InitializeBootstrapLogger()
    {
        LoggerBootstrapper.Initialize();
    }

    public IContainer CreateContainer()
    {
        return AgentServices.GetContainer();
    }

    public void RegisterServices(IContainer container, bool serverlessModeEnabled, bool gcSamplerV2Enabled, bool distroModeEnabled)
    {
        AgentServices.RegisterServices(container, serverlessModeEnabled, gcSamplerV2Enabled, distroModeEnabled);
    }

    public void StartServices(IContainer container, bool serverlessModeEnabled, bool gcSamplerV2Enabled, bool distroModeEnabled)
    {
        AgentServices.StartServices(container, serverlessModeEnabled, gcSamplerV2Enabled, distroModeEnabled);
    }

    public void PublishDeserializedConfiguration(configuration localConfig)
    {
        ConfigurationLoader.PublishDeserializedEvent(localConfig);
    }

    public void SubscribeToKillAgent(Action<KillAgentEvent> handler)
    {
        EventBus<KillAgentEvent>.Subscribe(handler);
    }

    public void UnsubscribeFromKillAgent(Action<KillAgentEvent> handler)
    {
        EventBus<KillAgentEvent>.Unsubscribe(handler);
    }

    public void InitializeExtensions(string extensionsDirectory)
    {
        ExtensionsLoader.Initialize(extensionsDirectory);
    }

    public void SetAgentApiImplementation(IAgentApi agentApi)
    {
        InternalApi.SetAgentApiImplementation(agentApi);
    }

    // Agent mode's exit hook: AgentInitializer.OnExit is raised from both ProcessExit and DomainUnload.
    public virtual void SubscribeToExit(EventHandler handler)
    {
        AgentInitializer.OnExit += handler;
    }

    public virtual void UnsubscribeFromExit(EventHandler handler)
    {
        AgentInitializer.OnExit -= handler;
    }

    public void MarkAgentShuttingDown()
    {
        Agent.IsAgentShuttingDown = true;
    }

    public void PublishCleanShutdownEvents()
    {
        EventBus<PreCleanShutdownEvent>.Publish(new PreCleanShutdownEvent());
        EventBus<CleanShutdownEvent>.Publish(new CleanShutdownEvent());
    }

    public void CloseLogging()
    {
        Serilog.Log.CloseAndFlush();
    }
}

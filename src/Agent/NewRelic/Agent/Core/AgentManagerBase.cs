// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.Api;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.Config;
using NewRelic.Agent.Core.Configuration;
using NewRelic.Agent.Core.DataTransport;
using NewRelic.Agent.Core.DependencyInjection;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Core.Wrapper;
using NewRelic.Agent.Extensions.Logging;
#if NETSTANDARD
using System.Runtime.InteropServices;
#endif

namespace NewRelic.Agent.Core;

/// <summary>
/// The start-up and shutdown sequence shared by agent mode (<see cref="AgentManager"/>) and OTel distro mode.
/// <see cref="Start"/> and <see cref="Shutdown"/> are the sequence, step by step; a mode adds its own work in
/// the empty virtual mode members, each called at one fixed point in the sequence.
/// </summary>
public abstract class AgentManagerBase : IDisposable
{
    private readonly ConfigurationSubscriber _configurationSubscription;

    private volatile bool _shutdownEventReceived;
    private volatile bool _isInitialized;

    protected IAgentManagerEnvironment Environment { get; }
    protected IConfiguration Configuration => _configurationSubscription.Configuration;
    protected IContainer Container { get; private set; }
    protected IAgentHealthReporter HealthReporter { get; private set; }
    protected IAgentApi AgentApi { get; private set; }
    protected IBootstrapConfiguration BootstrapConfig { get; private set; }
    protected bool ServerlessModeEnabled { get; private set; }
    protected string ExtensionsDirectory { get; private set; }

    public bool IsInitialized => _isInitialized;
    public bool ShutdownEventReceived => _shutdownEventReceived;

    protected abstract bool DistroModeEnabled { get; }

    /// <summary>
    /// Start-up step 1b: lets a mode adjust the just-loaded <see cref="IBootstrapConfiguration"/> before it is
    /// published. The base no-op keeps agent mode's instance exactly what <c>LoadConfiguration()</c> produced.
    /// <see cref="OtelDistro.DistroAgentManager"/> overrides this to wrap it so
    /// <see cref="IBootstrapConfiguration.DistroModeEnabled"/> reads true -- kept out of this shared base so no
    /// distro type is referenced here.
    /// </summary>
    protected virtual IBootstrapConfiguration AdaptBootstrapConfig(IBootstrapConfiguration bootstrapConfig) => bootstrapConfig;

    protected AgentManagerBase(IAgentManagerEnvironment environment)
    {
        Environment = environment ?? throw new ArgumentNullException(nameof(environment));
        _configurationSubscription = new ConfigurationSubscriber();
    }

    /// <summary>
    /// Runs the start-up sequence. The derived ctor calls this as its last line.
    /// </summary>
    protected void Start()
    {
        // 1. Load the configuration. If that fails, try to at least default configure the logger to record the exception before we bail.
        configuration localConfig;
        try
        {
            (localConfig, BootstrapConfig) = Environment.LoadConfiguration();
            BootstrapConfig = AdaptBootstrapConfig(BootstrapConfig);
        }
        catch
        {
            try
            {
                Environment.ConfigureDefaultLogger();
            }
            catch
            {
                // ignored
            }

            throw;
        }

        // 2. Create the container.
        Container = Environment.CreateContainer();

        try
        {
            // 3. Register services.
            ServerlessModeEnabled = ResolveServerlessMode(BootstrapConfig);
            Environment.RegisterServices(Container, ServerlessModeEnabled, BootstrapConfig.GCSamplerV2Enabled, DistroModeEnabled);

            // 4. Resolve IConfigurationService (so that it starts listening to config change events) and then publish the serialized event.
            Container.Resolve<IConfigurationService>();
            Environment.PublishDeserializedConfiguration(localConfig);

            // 5.
            AfterConfigurationPublished();

            // 6. Configure the logger. From here on all configuration checks should use Configuration instead of the local and bootstrap configs.
            Environment.ConfigureLogger(BootstrapConfig.LogConfig);

            // 7. Start the AgentHealthReporter early so that we can potentially report health issues during startup,
            //    then validate the configuration to ensure that the agent can start, and shut down if not.
            HealthReporter = Container.Resolve<IAgentHealthReporter>();
            if (!HealthReporter.ValidateAgentConfiguration())
            {
                Shutdown(false);

                // this exception won't get logged, but it will get caught by the AgentSingleton and prevent the agent from starting
                throw new Exception("There was a fatal configuration problem preventing the agent from starting. Please see previous log messages for details.");
            }

            // 8.
            Environment.SubscribeToKillAgent(OnShutdownAgent);

            // 9. Initialize the extensions loader with the extensions folder based on the install path.
            ExtensionsDirectory = Environment.ExtensionsDirectory;
            Environment.InitializeExtensions(ExtensionsDirectory);

            // 10. The AgentApiImplementation needs to be resolved before the WrapperService, because resolving the
            //     WrapperService triggers an agent connect but it doesn't instantiate the CustomEventAggregator, so we
            //     need to resolve the AgentApiImplementation to get the CustomEventAggregator instantiated before the
            //     connect process is triggered. If that doesn't happen the CustomEventAggregator will not start its
            //     harvest timer when the agent connect response comes back.
            AgentApi = Container.Resolve<IAgentApi>();
            var wrapperService = Container.Resolve<IWrapperService>();
            AfterWrapperServiceResolved(wrapperService);

            // 11. Attempt to auto start the agent once all services have resolved, except in serverless mode.
            if (!ServerlessModeEnabled)
            {
                Container.Resolve<IConnectionManager>().AttemptAutoStart();
            }
            else
            {
                Log.Info("The New Relic agent is operating in serverless mode.");
            }

            // 12.
            Environment.StartServices(Container, ServerlessModeEnabled, BootstrapConfig.GCSamplerV2Enabled, DistroModeEnabled);

            // 13. Set up the internal API first so that AgentApi can use it.
            Environment.SetAgentApiImplementation(AgentApi);
            AfterAgentApiSet();

            // 14.
            Environment.SubscribeToExit(OnExit);

            // 15.
            StartModeServices();

            // 16.
            LogInitialized();

            // 17.
            _isInitialized = true;
        }
        catch
        {
            OnStartFailed();
            throw;
        }
    }

    /// <summary>Decides serverless mode for steps 3, 11 and 12. Agent mode reads it from the bootstrap configuration.</summary>
    protected virtual bool ResolveServerlessMode(IBootstrapConfiguration c) => c.ServerlessModeEnabled;

    /// <summary>Start-up step 5: after the deserialized configuration is published, before the logger is configured.</summary>
    protected virtual void AfterConfigurationPublished() { }

    /// <summary>Start-up step 10: right after the agent API and the wrapper service are resolved, before connect.</summary>
    protected virtual void AfterWrapperServiceResolved(IWrapperService wrapperService) { }

    /// <summary>Start-up step 13: right after the internal API implementation is set.</summary>
    protected virtual void AfterAgentApiSet() { }

    /// <summary>Start-up step 15: after the exit hook is subscribed, before the "started" log line.</summary>
    protected virtual void StartModeServices() { }

    /// <summary>Called when any start-up step from 3 onward throws, before the exception is rethrown.</summary>
    protected virtual void OnStartFailed() { }

    /// <summary>The first thing <see cref="Shutdown"/> does after marking the agent as shutting down.</summary>
    protected virtual void BeforeShutdown() { }

    /// <summary>Shutdown: stops the mode's public services. A throw is reported as an unhealthy shutdown.</summary>
    protected virtual void StopModeServices() { }

    private void LogInitialized()
    {
        Log.Info("The New Relic .NET Agent v{0} started (pid {1}) on app domain '{2}'", AgentInstallConfiguration.AgentVersion, AgentInstallConfiguration.ProcessId, AgentInstallConfiguration.AppDomainAppVirtualPath ?? AgentInstallConfiguration.AppDomainName);
        //Log here for debugging configuration issues
        if (Log.IsDebugEnabled)
        {
            // Non-NEW_RELIC environment variables we want to log
            List<string> unprefixedEnvVars = new List<string> {
                "APP_POOL_ID",
                "COR_ENABLE_PROFILING",
                "COR_PROFILER",
                "COR_PROFILER_PATH",
                "COR_PROFILER_PATH_32",
                "COR_PROFILER_PATH_64",
                "CORECLR_ENABLE_PROFILING",
                "CORECLR_PROFILER",
                "CORECLR_NEW_RELIC_HOME",
                "CORECLR_PROFILER_PATH",
                "CORECLR_PROFILER_PATH_32",
                "CORECLR_PROFILER_PATH_64",
                "RoleName",
                "IISEXPRESS_SITENAME",
                "MAX_TRANSACTION_SAMPLES_STORED",
                "MAX_EVENT_SAMPLES_STORED"
            };

            // Sensitive environment variables that should not have their values logged
            HashSet<string> envVarsSensitive = new HashSet<string> {
                "NEW_RELIC_LICENSE_KEY",
                "NEWRELIC_LICENSEKEY",
                "NEW_RELIC_SECURITY_POLICIES_TOKEN",
                "NEW_RELIC_PROXY_USER",
                "NEW_RELIC_PROXY_PASS",
                "NEW_RELIC_CONFIG_OBSCURING_KEY",
                "NEW_RELIC_PROXY_PASS_OBFUSCATED",
                "NEW_RELIC_CLOUD_AWS_ACCOUNT_ID"
            };

            // This list purposefully does not include NEWRELIC_LICENSEKEY
            List<(string,string)> envVarsDeprecated = new List<(string, string)>
            {
                ("CORECLR_NEWRELIC_HOME","CORECLR_NEW_RELIC_HOME"),
                ("NEWRELIC_HOME", "NEW_RELIC_HOME"),
                ("NEWRELIC_INSTALL_PATH", "NEW_RELIC_INSTALL_PATH"),
                ("NEWRELIC_LOG_DIRECTORY", "NEW_RELIC_LOG_DIRECTORY"),
                ("NEWRELIC_LOG_LEVEL", "NEW_RELIC_LOG_LEVEL"),
                ("NEWRELIC_PROFILER_LOG_DIRECTORY", "NEW_RELIC_PROFILER_LOG_DIRECTORY"),
                ("NEWRELIC_FORCE_PROFILING", "NEW_RELIC_FORCE_PROFILING"),
                ("NEWRELIC_AGENT_VERSION_OVERRIDE", "NEW_RELIC_AGENT_VERSION_OVERRIDE")
            };

            var envVarsToLog = new List<string>(unprefixedEnvVars);

            // Add deprecated environment variable names to the list to log values
            envVarsToLog.AddRange(envVarsDeprecated.Select(tuple => tuple.Item1));

            var environment = new SharedInterfaces.Environment(); // ensures we use the agent's cached env var values

            // Get all environment variables beginning with NEW_RELIC_ from the system for logging
            envVarsToLog.AddRange(environment.GetEnvironmentVariablesWithPrefix("NEW_RELIC_").Select(ev => ev.Key));
            envVarsToLog.Sort();

            // Log environment variables, respecting sensitive variables
            foreach (var ev in envVarsToLog)
            {
                if (!string.IsNullOrEmpty(environment.GetEnvironmentVariable(ev)))
                {
                    if (envVarsSensitive.Contains(ev))
                    {
                        Log.Debug("Environment Variable {0} is configured with a value. Not logging potentially sensitive value", ev);
                    }
                    else
                    {
                        Log.Debug("Environment Variable {0} value: {1}", ev, environment.GetEnvironmentVariable(ev));
                    }
                }
            }

            // Add NEWRELIC_LICENSEKEY to the deprecated list for warning purposes
            envVarsDeprecated.Add(("NEWRELIC_LICENSEKEY", "NEW_RELIC_LICENSE_KEY"));
            foreach (var ev in envVarsDeprecated)
            {
                if (!string.IsNullOrEmpty(environment.GetEnvironmentVariable(ev.Item1)))
                {
                    Log.Warn("Environment Variable {OldName} is deprecated and may be removed in a future major version. Please use {NewName} instead.", ev.Item1, ev.Item2);
                }
            }

#if NETSTANDARD
            Log.Debug($".NET Runtime Version: {RuntimeInformation.FrameworkDescription}");
#else
                Log.Debug($".NET Runtime Version: {AppDomain.CurrentDomain.SetupInformation.TargetFrameworkName}");
#endif
        }

#if NETFRAMEWORK
            if (DotnetVersion.IsUnsupportedDotnetFrameworkVersion(AgentInstallConfiguration.DotnetFrameworkVersion))
            {
                Log.Warn("Unsupported installed .NET Framework version {0} detected. Please use a version of .NET Framework >= 4.6.2.", AgentInstallConfiguration.DotnetFrameworkVersion);
            }
#else
        if (DotnetVersion.IsUnsupportedDotnetCoreVersion(AgentInstallConfiguration.DotnetCoreVersion))
        {
            Log.Warn(".NET version {0} has reached EOL, and support will be removed in the next major release of the .NET Agent. Please use net8 or newer.", AgentInstallConfiguration.DotnetCoreVersion);
        }
#endif
    }

    protected virtual void Shutdown(bool cleanShutdown, bool closeLogging = true)
    {
        Environment.MarkAgentShuttingDown();

        BeforeShutdown();

        try
        {
            Log.Info("Starting the shutdown process for the .NET Agent.");

            Environment.UnsubscribeFromExit(OnExit);

            if (cleanShutdown)
            {
                Log.Debug("Agent is connected, executing a clean shutdown.");
                Environment.PublishCleanShutdownEvents();
            }

            Log.Debug("Shutting down public agent services...");
            StopModeServices();
            HealthReporter?.SetAgentControlStatus(HealthCodes.AgentShutdownHealthy);
        }
        catch (Exception e)
        {
            HealthReporter?.SetAgentControlStatus(HealthCodes.AgentShutdownError, e.Message);
            Log.Info(e, "Unexpected exception during agent shutdown");
        }
        finally
        {
            HealthReporter?.PublishAgentControlHealthCheck();
            Log.Debug("Shutting down internal agent services...");
            Dispose();

            Log.Info("The New Relic .NET Agent v{Version} has shutdown (pid {pid}) on app domain '{appDomain}'", AgentInstallConfiguration.AgentVersion, AgentInstallConfiguration.ProcessId, AgentInstallConfiguration.AppDomainAppVirtualPath ?? AgentInstallConfiguration.AppDomainName);

            if (closeLogging)
                Environment.CloseLogging();
        }
    }

    public virtual void Dispose()
    {
        _configurationSubscription.Dispose();
        Container?.Dispose();
    }

    /// <summary>
    /// Unsubscribes the kill-agent and exit handlers that <see cref="Start"/> subscribed. Agent mode never calls
    /// this (it has never unsubscribed its kill-agent handler).
    /// </summary>
    protected void UnsubscribeFromAgentEvents()
    {
        Environment.UnsubscribeFromKillAgent(OnShutdownAgent);
        Environment.UnsubscribeFromExit(OnExit);
    }

    private void OnExit(object sender, EventArgs e)
    {
        Log.Debug("Received a ProcessExit CLR event for the application domain. About to shut down the .NET Agent...");

        Shutdown(true);
    }

    private void OnShutdownAgent(KillAgentEvent eventData)
    {
        _shutdownEventReceived = true;

        //If the agent manager is not initialized yet, we do not want to execute the shutdown process,
        //because it is still in the middle of initializing itself. In this scenario, the caller that
        //constructed it checks ShutdownEventReceived after construction and calls Shutdown appropriately.
        if (_isInitialized) Shutdown(false);
    }
}

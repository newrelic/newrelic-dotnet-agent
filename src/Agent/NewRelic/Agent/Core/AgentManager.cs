// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Api;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Commands;
using NewRelic.Agent.Core.Config;
using NewRelic.Agent.Core.Configuration;
using NewRelic.Agent.Core.DataTransport;
using NewRelic.Agent.Core.DependencyInjection;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Instrumentation;
using NewRelic.Agent.Core.Metrics;
using NewRelic.Agent.Core.ThreadProfiling;
using NewRelic.Agent.Core.Tracer;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Core.Wrapper;
using NewRelic.Core.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace NewRelic.Agent.Core
{
    public sealed class AgentManager : IAgentManager, IDisposable
    {
        private readonly IContainer _container;
        private readonly ConfigurationSubscriber _configurationSubscription = new ConfigurationSubscriber();
        private static readonly IAgentManager DisabledAgentManager = new DisabledAgentManager();
        private static readonly AgentSingleton Singleton = new AgentSingleton();

        private sealed class AgentSingleton : Singleton<IAgentManager>
        {
            public AgentSingleton() : base(DisabledAgentManager) { }

            // Called by Singleton::Singleton
            protected override IAgentManager CreateInstance()
            {
                try
                {
                    var agentManager = new AgentManager();

                    //If the AgentManager received a shutdown event by the time the .ctor completes it means that the agent
                    //was unable to start correctly. The shutdown process cannot switch the Singleton to use
                    //the DisabledAgentManager because the Singleton is still being created so we need to do that here.
                    if (agentManager._shutdownEventReceived)
                    {
                        agentManager.Shutdown(false);
                        return DisabledAgentManager;
                    }

                    return agentManager;
                }
                catch (Exception exception)
                {
                    try
                    {
                        Log.Error(exception, "There was an error initializing the agent");
                        return DisabledAgentManager;
                    }
                    catch
                    {
                        return DisabledAgentManager;
                    }
                }
            }
        }

        public static IAgentManager Instance
        {
            get
            {
                // The singleton pointer may be null if we instrument a method that's invoked in the agent constructor.
                return Singleton?.ExistingInstance ?? DisabledAgentManager;
            }
        }

        private IConfiguration Configuration { get { return _configurationSubscription.Configuration; } }
        private ThreadProfilingService _threadProfilingService;
        private readonly IWrapperService _wrapperService;

        private volatile bool _shutdownEventReceived;
        private volatile bool _isInitialized;

        /// <summary> 
        /// Creates an instance of the <see cref="AgentManager"/> class./>
        /// </summary>
        /// <remarks>
        /// The agent should be constructed as early as possible in order to perform
        /// initialization of the logging system.
        /// </remarks>
        private AgentManager()
        {
            // load the configuration
            configuration localConfig = null;
            IBootstrapConfiguration bootstrapConfig = null;
            try
            {
                localConfig = ConfigurationLoader.Initialize(false);
                bootstrapConfig = ConfigurationLoader.BootstrapConfig;
            }
            catch
            {
                // If the ConfigurationLoader fails, try to at least default configure the Logger to record the exception before we bail...
                try
                {
                    LoggerBootstrapper.ConfigureLogger(BootstrapConfiguration.GetDefault().LogConfig);
                }
                catch { }

                throw;
            }

            _container = AgentServices.GetContainer();
            AgentServices.RegisterServices(_container, bootstrapConfig.ServerlessModeEnabled);

            // Resolve IConfigurationService (so that it starts listening to config change events) and then publish the serialized event
            _container.Resolve<IConfigurationService>();
            ConfigurationLoader.PublishDeserializedEvent(localConfig);


            // delay agent startup to allow a debugger to be attached. Used primarily for local debugging of AWS Lambda functions
            if (bootstrapConfig.DebugStartupDelaySeconds > 0)
            {
                // writing directly to console, as Log output doesn't get flushed immediately. And, for some processes, even this doesn't write to the console. 
                Console.WriteLine($"Delaying {bootstrapConfig.DebugStartupDelaySeconds} seconds. Attach debugger to {Process.GetCurrentProcess().MainModule?.FileName} now...");

                Thread.Sleep(bootstrapConfig.DebugStartupDelaySeconds * 1000);
                Debugger.Break(); // break the debugger, if one is attached
            }

            LoggerBootstrapper.ConfigureLogger(bootstrapConfig.LogConfig);

            // At this point all configuration checks should use Configuration instead of the local and bootstrap configs.

            AssertAgentEnabled();

            EventBus<KillAgentEvent>.Subscribe(OnShutdownAgent);

            //Initialize the extensions loader with extensions folder based on the the install path
            ExtensionsLoader.Initialize(AgentInstallConfiguration.InstallPathExtensionsDirectory);

            // Resolve all services once we've ensured that the agent is enabled
            // The AgentApiImplementation needs to be resolved before the WrapperService, because
            // resolving the WrapperService triggers an agent connect but it doesn't instantiate
            // the CustomEventAggregator, so we need to resolve the AgentApiImplementation to
            // get the CustomEventAggregator instantiated before the connect process is triggered.
            // If that doesn't happen the CustomEventAggregator will not start its harvest timer
            // when the agent connect response comes back. The agent DI, startup, and connect
            // process really needs to be refactored so that it's more explicit in its behavior.
            var agentApi = _container.Resolve<IAgentApi>();
            _wrapperService = _container.Resolve<IWrapperService>();

            // Attempt to auto start the agent once all services have resolved, except in serverless mode
            if (!bootstrapConfig.ServerlessModeEnabled)
                _container.Resolve<IConnectionManager>().AttemptAutoStart();
            else
            {
                Log.Info("The New Relic agent is operating in serverless mode.");
            }

            AgentServices.StartServices(_container, bootstrapConfig.ServerlessModeEnabled);

            // Setup the internal API first so that AgentApi can use it.
            InternalApi.SetAgentApiImplementation(agentApi);
            AgentApi.SetSupportabilityMetricCounters(_container.Resolve<IApiSupportabilityMetricCounters>());

            Initialize(bootstrapConfig.ServerlessModeEnabled);
            _isInitialized = true;
        }

        private void AssertAgentEnabled()
        {
            if (!Configuration.AgentEnabled)
                throw new Exception(string.Format("The New Relic agent is disabled.  Update {0}  to re-enable it.", Configuration.AgentEnabledAt));

            if (!Configuration.ServerlessModeEnabled) // license key is not required to be set in serverless mode
            {
                if ("REPLACE_WITH_LICENSE_KEY".Equals(Configuration.AgentLicenseKey))
                    throw new Exception("Please set your license key.");
            }
        }

        private void Initialize(bool serverlessModeEnabled)
        {
            AgentInitializer.OnExit += ProcessExit;

            var nativeMethods = _container.Resolve<INativeMethods>();
            var instrumentationService = _container.Resolve<IInstrumentationService>();

            _threadProfilingService = new ThreadProfilingService(_container.Resolve<IDataTransportService>(), nativeMethods);

            if (!serverlessModeEnabled)
            {
                var commandService = _container.Resolve<CommandService>();
                commandService.AddCommands(
                    new RestartCommand(),
                    new ShutdownCommand(),
                    new StartThreadProfilerCommand(_threadProfilingService),
                    new StopThreadProfilerCommand(_threadProfilingService),
                    new InstrumentationUpdateCommand(instrumentationService)
                );
            }

            StartServices();
            LogInitialized();
        }

        private void LogInitialized()
        {
            Log.Info("The New Relic .NET Agent v{0} started (pid {1}) on app domain '{2}'", AgentInstallConfiguration.AgentVersion, AgentInstallConfiguration.ProcessId, AgentInstallConfiguration.AppDomainAppVirtualPath ?? AgentInstallConfiguration.AppDomainName);
            //Log here for debugging configuration issues
            if (Log.IsDebugEnabled)
            {
                List<string> environmentVariables = new List<string> {
                    "CORECLR_ENABLE_PROFILING",
                    "CORECLR_PROFILER",
                    "CORECLR_NEWRELIC_HOME",
                    "CORECLR_PROFILER_PATH",
                    "CORECLR_PROFILER_PATH_32",
                    "CORECLR_PROFILER_PATH_64",
                    "COR_ENABLE_PROFILING",
                    "COR_PROFILER",
                    "COR_PROFILER_PATH",
                    "COR_PROFILER_PATH_32",
                    "COR_PROFILER_PATH_64",
                    "NEWRELIC_HOME",
                    "NEWRELIC_INSTALL_PATH",
                    "NEW_RELIC_APP_NAME",
                    "RoleName",
                    "IISEXPRESS_SITENAME",
                    "APP_POOL_ID",
                    "NEW_RELIC_APPLICATION_LOGGING_ENABLED",
                    "NEW_RELIC_APPLICATION_LOGGING_METRICS_ENABLED",
                    "NEW_RELIC_APPLICATION_LOGGING_FORWARDING_ENABLED",
                    "NEW_RELIC_APPLICATION_LOGGING_FORWARDING_MAX_SAMPLES_STORED",
                    "NEW_RELIC_APPLICATION_LOGGING_LOCAL_DECORATING_ENABLED",
                    "NEW_RELIC_DISTRIBUTED_TRACING_ENABLED",
                    "NEW_RELIC_SPAN_EVENTS_ENABLED",
                    "NEW_RELIC_SPAN_EVENTS_MAX_SAMPLES_STORED",
                    "MAX_TRANSACTION_SAMPLES_STORED",
                    "MAX_EVENT_SAMPLES_STORED",
                    "NEW_RELIC_DISABLE_SAMPLERS",
                    "NEW_RELIC_PROCESS_HOST_DISPLAY_NAME",
                    "NEW_RELIC_IGNORE_SERVER_SIDE_CONFIG",
                    "NEW_RELIC_LOG",
                    "NEWRELIC_PROFILER_LOG_DIRECTORY",
                    "NEWRELIC_LOG_LEVEL",
                    "NEW_RELIC_LOG_ENABLED",
                    "NEW_RELIC_LOG_CONSOLE",
                    "NEW_RELIC_LABELS",
                    "NEW_RELIC_PROXY_HOST",
                    "NEW_RELIC_PROXY_URI_PATH",
                    "NEW_RELIC_PROXY_PORT",
                    "NEW_RELIC_PROXY_DOMAIN",
                    "NEW_RELIC_ALLOW_ALL_HEADERS",
                    "NEW_RELIC_ATTRIBUTES_ENABLED",
                    "NEW_RELIC_ATTRIBUTES_INCLUDE",
                    "NEW_RELIC_ATTRIBUTES_EXCLUDE",
                    "NEW_RELIC_HIGH_SECURITY",
                    "NEW_RELIC_INFINITE_TRACING_TIMEOUT_CONNECT",
                    "NEW_RELIC_INFINITE_TRACING_TIMEOUT_SEND",
                    "NEW_RELIC_INFINITE_TRACING_EXIT_TIMEOUT",
                    "NEW_RELIC_INFINITE_TRACING_SPAN_EVENTS_STREAMS_COUNT",
                    "NEW_RELIC_INFINITE_TRACING_TRACE_OBSERVER_HOST",
                    "NEW_RELIC_INFINITE_TRACING_TRACE_OBSERVER_PORT",
                    "NEW_RELIC_INFINITE_TRACING_TRACE_OBSERVER_SSL",
                    "NEW_RELIC_INFINITE_TRACING_SPAN_EVENTS_QUEUE_SIZE",
                    "NEW_RELIC_INFINITE_TRACING_SPAN_EVENTS_PARTITION_COUNT",
                    "NEW_RELIC_INFINITE_TRACING_SPAN_EVENTS_BATCH_SIZE",
                    "NEW_RELIC_INFINITE_TRACING_COMPRESSION",
                    "NEW_RELIC_UTILIZATION_DETECT_AWS",
                    "NEW_RELIC_UTILIZATION_DETECT_AZURE",
                    "NEW_RELIC_UTILIZATION_DETECT_GCP",
                    "NEW_RELIC_UTILIZATION_DETECT_PCF",
                    "NEW_RELIC_UTILIZATION_DETECT_DOCKER",
                    "NEW_RELIC_UTILIZATION_DETECT_KUBERNETES",
                    "NEW_RELIC_UTILIZATION_LOGICAL_PROCESSORS",
                    "NEW_RELIC_UTILIZATION_TOTAL_RAM_MIB",
                    "NEW_RELIC_UTILIZATION_BILLING_HOSTNAME",
                    "NEW_RELIC_DISABLE_APPDOMAIN_CACHING",
                    "NEW_RELIC_FORCE_NEW_TRANSACTION_ON_NEW_THREAD",
                    "NEW_RELIC_CODE_LEVEL_METRICS_ENABLED",
                    "NEW_RELIC_SEND_DATA_ON_EXIT",
                    "NEW_RELIC_SEND_DATA_ON_EXIT_THRESHOLD_MS"
                };

                List<string> environmentVariablesSensitive = new List<string> {
                    "NEW_RELIC_LICENSE_KEY",
                    "NEWRELIC_LICENSEKEY",
                    "NEW_RELIC_SECURITY_POLICIES_TOKEN",
                    "NEW_RELIC_PROXY_USER",
                    "NEW_RELIC_PROXY_PASS",
                    "NEW_RELIC_CONFIG_OBSCURING_KEY",
                    "NEW_RELIC_PROXY_PASS_OBFUSCATED"
                };

                foreach (var ev in environmentVariables)
                {
                    if (!string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable(ev)))
                    {
                        Log.Debug("Environment Variable {0} value: {1}", ev, System.Environment.GetEnvironmentVariable(ev));
                    }
                }

                foreach (var evs in environmentVariablesSensitive)
                {
                    if (!string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable(evs)))
                    {
                        Log.Debug("Environment Variable {0} is configured with a value. Not logging potentially sensitive value", evs);
                    }
                }

                Log.Debug($".NET Runtime Version: {RuntimeInformation.FrameworkDescription}");
            }

#if NETFRAMEWORK
            if (NewRelic.Core.DotnetVersion.IsUnsupportedDotnetFrameworkVersion(AgentInstallConfiguration.DotnetFrameworkVersion))
            {
                Log.Warn("Unsupported installed .NET Framework version {0} detected. Please use a version of .NET Framework >= 4.6.2.", AgentInstallConfiguration.DotnetFrameworkVersion);
            }
#else
            if (NewRelic.Core.DotnetVersion.IsUnsupportedDotnetCoreVersion(AgentInstallConfiguration.DotnetCoreVersion))
            {
                Log.Warn("Unsupported .NET version {0} detected. Please use net6 or net8 or newer.", AgentInstallConfiguration.DotnetCoreVersion);
            }
#endif
        }

        private void StartServices()
        {
            _container.Resolve<InstrumentationWatcher>().Start();
            _threadProfilingService.Start();
        }

        private void StopServices()
        {
            _threadProfilingService.Stop();
        }

        /// <summary>
        /// Returns a tracer.
        /// 
        /// Implements the interface needed for IAgent.
        /// Called from AgentShim::GetTracer, which, in turn, are calls injected into profiled code.
        /// Consequently, this is called every time a profiled function gets called.
        /// </summary>
        /// <param name="tracerFactoryName">The fully qualified name of the TracerFactory,
        /// from the mapping held in the CoreInstrumentation.xml file,
        /// as set up by unmanaged the C++ profiler code.</param>
        /// <param name="tracerArguments">A packed value with items from the instrumentation .xml files</param>
        /// <param name="metricName"></param>
        /// <param name="assemblyName"></param>
        /// <param name="type"></param>
        /// <param name="typeName"></param>
        /// <param name="methodName"></param>
        /// <param name="argumentSignature"></param>
        /// <param name="invocationTarget"></param>
        /// <param name="arguments"></param>
        /// <returns>Returns an ITracer, although it is given as the much simpler Object;
        /// an Object is the preferred type because it has a trival type signature.</returns>
        public ITracer GetTracerImpl(string tracerFactoryName, uint tracerArguments, string metricName, string assemblyName, Type type, string typeName, string methodName, string argumentSignature, object invocationTarget, object[] arguments, ulong functionId)
        {
            try
            {
                // First try to get a wrapper from the newer WrapperService
                var afterWrappedMethodDelegate = _wrapperService.BeforeWrappedMethod(type, methodName, argumentSignature, invocationTarget, arguments, tracerFactoryName, metricName, tracerArguments, functionId);
                return (afterWrappedMethodDelegate != null) ? new WrapperTracer(afterWrappedMethodDelegate) : null;
            }
            catch (Exception e)
            {
                Log.Error(e, "Tracer invocation error");
                return null;
            }
        }

        private void ProcessExit(object sender, EventArgs e)
        {
            Log.Debug("Received a ProcessExit CLR event for the application domain. About to shut down the .NET Agent...");

            Shutdown(true);
        }

        private void Shutdown(bool cleanShutdown)
        {
            Agent.IsAgentShuttingDown = true;

            //Not every call to Shutdown will have access to the AgentSingleton, because some of the calls to Shutdown
            //will occur while the Singleton is being created. In those scenarios, the AgentSingleton will handle
            //Swapping out the AgentManager for the DisabledAgentManager.
            Singleton?.SetInstance(DisabledAgentManager);

            try
            {
                Log.Debug("Starting the shutdown process for the .NET Agent.");

                AgentInitializer.OnExit -= ProcessExit;

                if (cleanShutdown)
                {
                    Log.Debug("Agent is connected, executing a clean shutdown.");
                    EventBus<PreCleanShutdownEvent>.Publish(new PreCleanShutdownEvent());
                    EventBus<CleanShutdownEvent>.Publish(new CleanShutdownEvent());
                }

                Log.Debug("Shutting down public agent services...");
                StopServices();
                Log.Info("The New Relic .NET Agent v{0} has shutdown (pid {1}) on app domain '{2}'", AgentInstallConfiguration.AgentVersion, AgentInstallConfiguration.ProcessId, AgentInstallConfiguration.AppDomainAppVirtualPath ?? AgentInstallConfiguration.AppDomainName);
            }
            catch (Exception e)
            {
                Log.Debug(e, "Shutdown error");
            }
            finally
            {
                Dispose();
                Serilog.Log.CloseAndFlush();
            }
        }

        public void Dispose()
        {
            _configurationSubscription.Dispose();
            _container.Dispose();
        }

        #region Event handlers

        private void OnShutdownAgent(KillAgentEvent eventData)
        {
            _shutdownEventReceived = true;

            //If the AgentManager is not initialized yet, we do not want to execute the shutdown process,
            //because the AgentManager is still in the middle of initializing itself. In this scenario,
            //the AgentSingleton will check to see if a shutdownEvent was received, and call Shutdown
            //appropriately.
            if (_isInitialized) Shutdown(false);
        }

        #endregion Event handlers
    }
}

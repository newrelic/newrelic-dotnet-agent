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
using NewRelic.Agent.Core.Metric;
using NewRelic.Agent.Core.ThreadProfiling;
using NewRelic.Agent.Core.Tracer;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Core.Wrapper;
using NewRelic.Core.Logging;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace NewRelic.Agent.Core
{
    sealed public class AgentManager : IAgentManager, IDisposable
    {
        private readonly IContainer _container;
        private readonly ConfigurationSubscriber _configurationSubscription = new ConfigurationSubscriber();
        private readonly static IAgentManager DisabledAgentManager = new DisabledAgentManager();
        private readonly static AgentSingleton Singleton = new AgentSingleton();

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
                        Log.Error($"There was an error initializing the agent: {exception}");
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
            _container = AgentServices.GetContainer();
            AgentServices.RegisterServices(_container);

            // Resolve IConfigurationService (so that it starts listening to config changes) before loading newrelic.config
            _container.Resolve<IConfigurationService>();

            configuration config = null;

            try
            {
                config = ConfigurationLoader.Initialize();
            }
            catch
            {
                // If the ConfigurationLoader fails, try to at least default configure the Logger to record the exception before we bail...

                try
                {
                    LoggerBootstrapper.ConfigureLogger(new configurationLog());
                }
                catch { }

                throw;
            }

            LoggerBootstrapper.ConfigureLogger(config.LogConfig);

            AssertAgentEnabled(config);

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

            //We need to attempt to auto start the agent once all services have resolved

            // MT Innovation Days: The connect needs to be synchronous, but any attempt at waiting
            // causes the Agent to die when it invokes httpClient.SendAsync() for the first time.
            // No errors if we don't try to wait, but that causes other problems. Maybe we need some kind of
            // global flag to tell us when the agent is connected and all other processing waits on that? 
            var autoStartTask  = Task.Run(async () =>
            {
                Log.Debug("Attempting AutoStartAsync");
                var connectionManager = _container.Resolve<IConnectionManager>();
                await connectionManager.AttemptAutoStartAsync();
                Log.Debug("AutoStartAsync complete");
            });//.GetAwaiter().GetResult();

            AgentServices.StartServices(_container);

            // Setup the internal API first so that AgentApi can use it.
            InternalApi.SetAgentApiImplementation(agentApi);
            AgentApi.SetSupportabilityMetricCounters(_container.Resolve<IApiSupportabilityMetricCounters>());

            Initialize();
            _isInitialized = true;
        }

        private void AssertAgentEnabled(configuration config)
        {
            if (!Configuration.AgentEnabled)
                throw new Exception(string.Format("The New Relic agent is disabled.  Update {0}  to re-enable it.", config.AgentEnabledAt));

            if ("REPLACE_WITH_LICENSE_KEY".Equals(Configuration.AgentLicenseKey))
                throw new Exception("Please set your license key.");
        }

        private void Initialize()
        {
            AgentInitializer.OnExit += ProcessExit;

            var nativeMethods = _container.Resolve<INativeMethods>();
            var instrumentationService = _container.Resolve<IInstrumentationService>();

            _threadProfilingService = new ThreadProfilingService(_container.Resolve<IDataTransportService>(), nativeMethods);

            var commandService = _container.Resolve<CommandService>();
            commandService.AddCommands(
                new RestartCommand(),
                new ShutdownCommand(),
                new StartThreadProfilerCommand(_threadProfilingService),
                new StopThreadProfilerCommand(_threadProfilingService),
                new InstrumentationUpdateCommand(instrumentationService)
            );

            StartServices();
            LogInitialized();
        }

        private void LogInitialized()
        {
            Log.InfoFormat("The New Relic .NET Agent v{0} started (pid {1}) on app domain '{2}'", AgentInstallConfiguration.AgentVersion, AgentInstallConfiguration.ProcessId, AgentInstallConfiguration.AppDomainAppVirtualPath ?? AgentInstallConfiguration.AppDomainName);
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
                    "NEW_RELIC_LABELS",
                    "NEW_RELIC_PROXY_HOST",
                    "NEW_RELIC_PROXY_URI_PATH",
                    "NEW_RELIC_PROXY_PORT",
                    "NEW_RELIC_PROXY_DOMAIN",
                    "NEW_RELIC_ALLOW_ALL_HEADERS",
                    "NEW_RELIC_ATTRIBUTES_ENABLED",
                    "NEW_RELIC_ATTRIBUTES_INCLUDE",
                    "NEW_RELIC_ATTRIBUTES_EXCLUDE",
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
                    if (!String.IsNullOrEmpty(System.Environment.GetEnvironmentVariable(ev)))
                    {
                        Log.DebugFormat("Environment Variable {0} value: {1}", ev, System.Environment.GetEnvironmentVariable(ev));
                    }
                }

                foreach (var evs in environmentVariablesSensitive)
                {
                    if (!String.IsNullOrEmpty(System.Environment.GetEnvironmentVariable(evs)))
                    {
                        Log.DebugFormat("Environment Variable {0} is configured with a value. Not logging potentially sensitive value", evs);
                    }
                }

                Log.Debug($".NET Runtime Version: {RuntimeInformation.FrameworkDescription}");
            }

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
                Log.Error($"Tracer invocation error: {e}");
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
                    EventBus<PreCleanShutdownEvent>.Publish(new PreCleanShutdownEvent());
                    EventBus<CleanShutdownEvent>.Publish(new CleanShutdownEvent());
                }

                Log.Debug("Shutting down public agent services...");
                StopServices();
                Log.InfoFormat("The New Relic .NET Agent v{0} has shutdown (pid {1}) on app domain '{2}'", AgentInstallConfiguration.AgentVersion, AgentInstallConfiguration.ProcessId, AgentInstallConfiguration.AppDomainAppVirtualPath ?? AgentInstallConfiguration.AppDomainName);
            }
            catch (Exception e)
            {
                Log.Debug($"Shutdown error: {e}");
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

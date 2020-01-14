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

namespace NewRelic.Agent.Core
{
	public static class AgentVersion
	{
		// put in a separate class to avoid the static initializer on Agent being hit in testing
		public static readonly string Version = typeof(AgentVersion).Assembly.GetName().Version.ToString();
	}

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
				try
				{
					return Singleton.ExistingInstance;
				}
				catch (NullReferenceException)
				{
					// The singleton pointer may be null if we instrument a method that's invoked in the agent constructor.
					return DisabledAgentManager;
				}
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
			var config = ConfigurationLoader.Initialize();

			// TODO: DI the logging system, right now this needs to happen before any services start
			LoggerBootstrapper.ConfigureLogger(config.LogConfig);

			AssertAgentEnabled(config);

			EventBus<KillAgentEvent>.Subscribe(OnShutdownAgent);

			//Initialize the extensions loader with extensions folder based on the the install path
			ExtensionsLoader.Initialize(AgentInstallConfiguration.InstallPathExtensionsDirectory);

			// Resolve all services once we've ensured that the agent is enabled
			_wrapperService = _container.Resolve<IWrapperService>();

			//We need to attempt to auto start the agent once all services have resolved
			_container.Resolve<IConnectionManager>().AttemptAutoStart();

			AgentServices.StartServices(_container);

			// Setup the internal API first so that AgentApi can use it.
			InternalApi.SetAgentApiImplementation(_container.Resolve<IAgentApi>());
			AgentApi.SetSupportabilityMetricCounters(_container.Resolve<IApiSupportabilityMetricCounters>());

			Initialize();
			_isInitialized = true;
		}

		private void AssertAgentEnabled(configuration config)
		{
			// TODO: migrate all of these settings to the new new config system so that we can just use IConfiguration
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
			instrumentationService.LoadRuntimeInstrumentation();
			instrumentationService.ApplyInstrumentation();

			// TODO: remove IAgent dependency from these services so they can be DI'd
			_threadProfilingService = new ThreadProfilingService(_container.Resolve<IDataTransportService>(), nativeMethods);

			// TODO: DI these commands (need to make them all DI'able first)
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
			Log.InfoFormat("The New Relic .NET Agent v{0} started (pid {1}) on app domain '{2}'", AgentVersion.Version, AgentInstallConfiguration.ProcessId, AgentInstallConfiguration.AppDomainAppVirtualPath ?? AgentInstallConfiguration.AppDomainName);
			//Log here for debugging configuration issues
			if (Log.IsDebugEnabled)
			{
				Log.DebugFormat("Environment Variable NEWRELIC_HOME value: {0}", System.Environment.GetEnvironmentVariable("NEWRELIC_HOME"));
				Log.DebugFormat("Environment Variable NEWRELIC_INSTALL_PATH value: {0}", System.Environment.GetEnvironmentVariable("NEWRELIC_INSTALL_PATH"));
				Log.DebugFormat("Environment Variable CORECLR_NEWRELIC_HOME value: {0}", System.Environment.GetEnvironmentVariable("CORECLR_NEWRELIC_HOME"));
				Log.DebugFormat("Environment Variable COR_PROFILER_PATH value: {0}", System.Environment.GetEnvironmentVariable("COR_PROFILER_PATH"));
				Log.DebugFormat("Environment Variable CORECLR_PROFILER_PATH value: {0}", System.Environment.GetEnvironmentVariable("CORECLR_PROFILER_PATH"));
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
				Log.InfoFormat("The New Relic .NET Agent v{0} has shutdown (pid {1}) on app domain '{2}'", AgentVersion.Version, AgentInstallConfiguration.ProcessId, AgentInstallConfiguration.AppDomainAppVirtualPath ?? AgentInstallConfiguration.AppDomainName);
			}
			catch (Exception e)
			{
				Log.Debug($"Shutdown error: {e}");
			}
			finally
			{
				Dispose();
				log4net.LogManager.Shutdown();
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

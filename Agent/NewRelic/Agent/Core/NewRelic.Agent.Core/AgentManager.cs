using JetBrains.Annotations;
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
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Core.Logging;
using System;
using System.Diagnostics;
using System.Web;

namespace NewRelic.Agent.Core
{
	public static class AgentVersion
	{
		// put in a separate class to avoid the static initializer on Agent being hit in testing
		[NotNull] public static readonly String Version = typeof(AgentVersion).Assembly.GetName().Version.ToString();
	}

	sealed public class AgentManager : IAgentManager, IDisposable
	{
		[NotNull]
		private readonly IContainer _container;

		[NotNull]
		private readonly ConfigurationSubscriber _configurationSubscription = new ConfigurationSubscriber();

		[NotNull]
		private readonly static IAgentManager DisabledAgentManager = new DisabledAgentManager();

		[NotNull]
		private readonly static AgentSingleton Singleton = new AgentSingleton();

		private sealed class AgentSingleton : Singleton<IAgentManager>
		{
			public AgentSingleton() : base(DisabledAgentManager) { }

			// Called by Singleton::Singleton
			[NotNull]
			protected override IAgentManager CreateInstance()
			{
				try
				{
					return new AgentManager();
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

		[NotNull]
		private IConfiguration Configuration { get { return _configurationSubscription.Configuration; } }

		private ThreadProfilingService _threadProfilingService;

		[NotNull]
		private readonly IWrapperService _wrapperService;

		private volatile AgentState _agentState = AgentState.Uninitialized;
		public AgentState State { get { return _agentState; } }

		/// <summary> 
		/// Creates an instance of the <see cref="AgentManager"/> class./>
		/// </summary>
		/// <remarks>
		/// The agent should be constructed as early as possible in order to perform
		/// initialization of the logging system.
		/// </remarks>
		private AgentManager()
		{
			_agentState = AgentStateHelper.Transition(_agentState, AgentState.Starting);

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

			AgentServices.StartServices(_container);

			// Setup the internal API first so that AgentApi can use it.
			InternalApi.SetAgentApiImplementation(_container.Resolve<IAgentApi>());
			AgentApi.SetSupportabilityMetricCounters(_container.Resolve<IApiSupportabilityMetricCounters>());

			Initialize();
		}

		private void AssertAgentEnabled([NotNull] configuration config)
		{
			// TODO: migrate all of these settings to the new new config system so that we can just use IConfiguration
			if (!Configuration.AgentEnabled)
				throw new Exception(String.Format("The New Relic agent is disabled.  Update {0}  to re-enable it.", config.AgentEnabledAt));

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
				new StartThreadProfilerCommand(_threadProfilingService, AgentInstallConfiguration.IsWindows),
				new StopThreadProfilerCommand(_threadProfilingService),
				new InstrumentationUpdateCommand(instrumentationService)
			);

			StartServices();
			LogInitialized();

			_agentState = AgentStateHelper.Transition(_agentState, AgentState.Started);
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
		public ITracer GetTracerImpl(String tracerFactoryName, UInt32 tracerArguments, String metricName, String assemblyName, Type type, String typeName, String methodName, String argumentSignature, Object invocationTarget, Object[] arguments, UInt64 functionId)
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

		private void Shutdown(Boolean cleanShutdown)
		{
			Singleton.SetInstance(DisabledAgentManager);

			try
			{
				_agentState = AgentStateHelper.Transition(_agentState, AgentState.Stopping);
				Log.Debug("Starting the shutdown process for the .NET Agent.");

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
				_agentState = AgentState.Stopped; // don't use the state helper - we don't want this transition to throw
				log4net.LogManager.Shutdown();
			}
		}

		public void Dispose()
		{
			_configurationSubscription.Dispose();
			_container.Dispose();
		}

#region Event handlers

		private void OnShutdownAgent([NotNull] KillAgentEvent eventData)
		{
			Shutdown(false);
		}

#endregion Event handlers
	}
}

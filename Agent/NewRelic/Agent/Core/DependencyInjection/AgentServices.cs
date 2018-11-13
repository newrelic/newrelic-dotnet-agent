using System;
using System.Collections.Generic;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.Aggregators;
using NewRelic.Agent.Core.Api;
using NewRelic.Agent.Core.AssemblyLoading;
using NewRelic.Agent.Core.BrowserMonitoring;
using NewRelic.Agent.Core.CallStack;
using NewRelic.Agent.Core.Commands;
using NewRelic.Agent.Core.Config;
using NewRelic.Agent.Core.Configuration;
using NewRelic.Agent.Core.Database;
using NewRelic.Agent.Core.DataTransport;
using NewRelic.Agent.Core.DistributedTracing;
using NewRelic.Agent.Core.Instrumentation;
using NewRelic.Agent.Core.Metric;
using NewRelic.Agent.Core.Metrics;
using NewRelic.Agent.Core.NewRelic.Agent.Core.Timing;
using NewRelic.Agent.Core.Samplers;
using NewRelic.Agent.Core.SharedInterfaces;
using NewRelic.Agent.Core.ThreadProfiling;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.TransactionTraces;
using NewRelic.Agent.Core.Transformers;
using NewRelic.Agent.Core.Transformers.TransactionTransformer;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Core.WireModels;
using NewRelic.Agent.Core.Wrapper;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.CrossApplicationTracing;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Synthetics;
using NewRelic.Agent.Extensions.Providers;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.SystemInterfaces;
using NewRelic.SystemInterfaces.Web;

// ReSharper disable RedundantTypeArgumentsOfMethod
namespace NewRelic.Agent.Core.DependencyInjection
{
	public static class AgentServices
	{
		public static IContainer GetContainer()
		{
#if NET45
			return new WindsorContainer();
#else
			return new CoreContainer();
#endif
		}

		/// <summary>
		/// Registers all of the services needed for the agent to run.
		/// </summary>
		/// <param name="container"></param>
		public static void RegisterServices(IContainer container)
		{
			// we register this factory instead of just loading the storage contexts here because deferring the logic gives us a logger
			container.RegisterFactory<IEnumerable<IContextStorageFactory>>(ExtensionsLoader.LoadContextStorageFactories);
			container.Register<ICallStackManagerFactory, ResolvedCallStackManagerFactory>();

			// IWrapper map
			container.RegisterFactory<IEnumerable<IWrapper>>(() => ExtensionsLoader.LoadWrappers());
			container.Register<IWrapperMap, WrapperMap>();

			// Other
			container.Register<ICpuSampleTransformer, CpuSampleTransformer>();
			container.Register<IMemorySampleTransformer, MemorySampleTransformer>();
			container.Register<IEnvironment, SystemInterfaces.Environment>();
			container.Register<IAgentWrapperApi, AgentWrapperApi>();
			container.Register<CpuSampler, CpuSampler>();
			container.Register<MemorySampler, MemorySampler>();
			container.Register<IBrowserMonitoringPrereqChecker, BrowserMonitoringPrereqChecker>();
			container.Register<IProcessStatic, ProcessStatic>();
			container.Register<IDnsStatic, DnsStatic>();
			container.Register<IHttpRuntimeStatic, HttpRuntimeStatic>();
			container.Register<IConfigurationManagerStatic, ConfigurationManagerStatic>();
			container.Register<ISerializer, JsonSerializer>();
			container.Register<ICollectorWireFactory, HttpCollectorWireFactory>();
			container.Register<Environment, Environment>();
			container.Register<IConnectionHandler, ConnectionHandler>();
			container.Register<IConnectionManager, ConnectionManager>();
			container.Register<IDataTransportService, DataTransportService>();
			container.Register<IScheduler, Scheduler>();
			container.Register<ISystemInfo, SystemInfo>();
			container.Register<ITimerFactory, TimerFactory>();
			container.Register<IDateTimeStatic, DateTimeStatic>();
			container.Register<IMetricAggregator, MetricAggregator>();
			container.Register<IAllMetricStatsCollection, MetricWireModel > ();
			container.Register<IAllMetricStatsCollection, TransactionMetricStatsCollection > ();
			container.Register<Func<MetricWireModel, MetricWireModel, MetricWireModel>>(MetricWireModel.Merge);
			container.Register<ITransactionTraceAggregator, TransactionTraceAggregator>();
			container.Register<ITransactionEventAggregator, TransactionEventAggregator>();
			container.Register<ISqlTraceAggregator, SqlTraceAggregator>();
			container.Register<IErrorTraceAggregator, ErrorTraceAggregator>();
			container.Register<IErrorEventAggregator, ErrorEventAggregator>();
			container.Register<ICustomEventAggregator, CustomEventAggregator>();
			container.Register<ISpanEventAggregator, SpanEventAggregator>();
			container.Register<ISpanEventMaker, SpanEventMaker>();
			container.Register<IMetricBuilder, MetricWireModel.MetricBuilder>();
			container.Register<IAgentHealthReporter, IOutOfBandMetricSource, AgentHealthReporter>();
			container.Register<IApiSupportabilityMetricCounters, IOutOfBandMetricSource, ApiSupportabilityMetricCounters>();
			container.Register<IAgentTimerService, AgentTimerService>();
#if NET45
			container.RegisterFactory<IEnumerable<IOutOfBandMetricSource>>(container.ResolveAll<IOutOfBandMetricSource>);
#endif
			container.Register<IThreadPoolStatic, ThreadPoolStatic>();
			container.Register<ITransactionTransformer, TransactionTransformer>();
			container.Register<ICustomEventTransformer, CustomEventTransformer>();
			container.Register<ICustomErrorDataTransformer, CustomErrorDataTransformer>();
			container.Register<ISegmentTreeMaker, SegmentTreeMaker>();
			container.Register<ITransactionMetricNameMaker, TransactionMetricNameMaker>();
			container.Register<ITransactionTraceMaker, TransactionTraceMaker>();
			container.Register<ITransactionEventMaker, TransactionEventMaker>();
			container.Register<ICallStackManager, CallStackManager>();
			container.Register<IAdaptiveSampler, AdaptiveSampler>();

			var transactionCollectors = new List<ITransactionCollector> {
				new SlowestTransactionCollector(),
				new SyntheticsTransactionCollector(),
				new KeyTransactionCollector() };
			container.Register<ITransactionCollector, SlowestTransactionCollector>();
			container.Register<ITransactionCollector, SyntheticsTransactionCollector>();
			container.Register<ITransactionCollector, KeyTransactionCollector>();
			container.Register<IEnumerable<ITransactionCollector>>(transactionCollectors);

			container.Register<ITransactionAttributeMaker, TransactionAttributeMaker>();
			container.Register<IErrorTraceMaker, ErrorTraceMaker>();
			container.Register<IErrorEventMaker, ErrorEventMaker>();
			container.Register<ICatHeaderHandler, CatHeaderHandler>();
			container.Register<IDistributedTracePayloadHandler, DistributedTracePayloadHandler>();
			container.Register<ISyntheticsHeaderHandler, SyntheticsHeaderHandler>();
			container.Register<IPathHashMaker, PathHashMaker>();
			container.Register<ITransactionFinalizer, TransactionFinalizer>();
			container.Register<IBrowserMonitoringScriptMaker, BrowserMonitoringScriptMaker>();
			container.Register<ISqlTraceMaker, SqlTraceMaker>();
			container.Register<IAgentApi, AgentApiImplementation>();
			container.Register<IDefaultWrapper, DefaultWrapper>();
			container.Register<INoOpWrapper, NoOpWrapper>();

			container.Register<AssemblyResolutionService, AssemblyResolutionService>();
			container.Register<IConfigurationService, ConfigurationService>();
			container.Register<IMetricNameService, MetricNameService>();
			container.Register<IWrapperService, WrapperService>();
			container.Register<ILabelsService, LabelsService>();
			
			container.Register<ITransactionService, TransactionService>();
			container.Register<IAttributeService, AttributeService>();
			container.Register<DatabaseService, DatabaseService>();
			container.Register<CommandService, CommandService>();
			container.Register<ConfigurationTracker, ConfigurationTracker>();
			container.Register<IDatabaseService, DatabaseService>();

			container.RegisterFactory<IEnumerable<IRuntimeInstrumentationGenerator>>(ExtensionsLoader.LoadRuntimeInstrumentationGenerators);
			container.Register<IInstrumentationService, InstrumentationService>();
			container.Register<InstrumentationWatcher, InstrumentationWatcher>();
			container.Register<LiveInstrumentationServerConfigurationListener, LiveInstrumentationServerConfigurationListener>();

			if (AgentInstallConfiguration.IsWindows)
			{
				container.Register<INativeMethods, WindowsNativeMethods>();
			}
			else
			{
				container.Register<INativeMethods, LinuxNativeMethods>();
			}
			container.Register<ITracePriorityManager, TracePriorityManager>();

			container.Build();
		}

		/// <summary>
		/// Starts all of the services needed by resolving them.
		/// </summary>
		public static void StartServices(IContainer container)
		{
			container.Resolve<AssemblyResolutionService>();
			container.Resolve<ITransactionFinalizer>();
			container.Resolve<IAgentHealthReporter>();
			container.Resolve<CpuSampler>();
			container.Resolve<MemorySampler>();
			container.Resolve<ConfigurationTracker>();
			container.Resolve<LiveInstrumentationServerConfigurationListener>();
		}
	}
}

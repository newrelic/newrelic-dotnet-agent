// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Threading;
using NewRelic.Agent.Api;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.Aggregators;
using NewRelic.Agent.Core.Api;
using NewRelic.Agent.Core.AssemblyLoading;
using NewRelic.Agent.Core.Attributes;
using NewRelic.Agent.Core.BrowserMonitoring;
using NewRelic.Agent.Core.CallStack;
using NewRelic.Agent.Core.Commands;
using NewRelic.Agent.Core.Config;
using NewRelic.Agent.Core.Configuration;
using NewRelic.Agent.Core.Database;
using NewRelic.Agent.Core.DataTransport;
using NewRelic.Agent.Core.DataTransport.Client;
using NewRelic.Agent.Core.DataTransport.Client.Interfaces;
using NewRelic.Agent.Core.DistributedTracing;
using NewRelic.Agent.Core.Errors;
using NewRelic.Agent.Core.Instrumentation;
using NewRelic.Agent.Core.Metrics;
using NewRelic.Agent.Core.Samplers;
using NewRelic.Agent.Core.SharedInterfaces;
using NewRelic.Agent.Core.Spans;
using NewRelic.Agent.Core.Segments;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.TransactionTraces;
using NewRelic.Agent.Core.Transformers;
using NewRelic.Agent.Core.Transformers.TransactionTransformer;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Core.WireModels;
using NewRelic.Agent.Core.Wrapper;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.CrossApplicationTracing;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Synthetics;
using NewRelic.Agent.Extensions.Providers;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.SystemInterfaces;
using NewRelic.SystemInterfaces.Web;
using NewRelicCore = NewRelic.Core;
using NewRelic.Agent.Core.Labels;

namespace NewRelic.Agent.Core.DependencyInjection
{
    public static class AgentServices
    {
        public static IContainer GetContainer()
        {
            return new AgentContainer();
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
#if NETFRAMEWORK
            container.Register<IHttpClientFactory, WebRequestHttpClientFactory>();
#else
            container.Register<IHttpClientFactory, NRHttpClientFactory>();
#endif

            // Other
            container.Register<ICpuSampleTransformer, CpuSampleTransformer>();
            container.RegisterInstance<AgentInstallConfiguration.IsWindowsDelegate>(AgentInstallConfiguration.GetIsWindows);
            container.Register<IMemorySampleTransformer, MemorySampleTransformer>();
            container.Register<IThreadStatsSampleTransformer, ThreadStatsSampleTransformer>();
            container.Register<IEnvironment, SystemInterfaces.Environment>();
            container.Register<IAgent, Agent>();
            container.Register<CpuSampler, CpuSampler>();
            container.Register<MemorySampler, MemorySampler>();
            container.RegisterInstance<Func<ISampledEventListener<ThreadpoolThroughputEventsSample>>>(() => new ThreadEventsListener());
            container.Register<ThreadStatsSampler, ThreadStatsSampler>();
            container.Register<IGcSampleTransformer, GcSampleTransformer>();
#if NETFRAMEWORK
			container.RegisterInstance<Func<string, IPerformanceCounterCategoryProxy>>(PerformanceCounterProxyFactory.DefaultCreatePerformanceCounterCategoryProxy);
			container.RegisterInstance<Func<string, string, string, IPerformanceCounterProxy>>(PerformanceCounterProxyFactory.DefaultCreatePerformanceCounterProxy);
			container.Register<IPerformanceCounterProxyFactory, PerformanceCounterProxyFactory>();
			container.Register<GcSampler, GcSampler>();
#else
            container.RegisterInstance<Func<ISampledEventListener<Dictionary<GCSampleType, float>>>>(() => new GCEventsListener());
            container.RegisterInstance<Func<GCSamplerNetCore.SamplerIsApplicableToFrameworkResult>>(GCSamplerNetCore.FXsamplerIsApplicableToFrameworkDefault);
            container.Register<GCSamplerNetCore, GCSamplerNetCore>();
#endif

            container.Register<IBrowserMonitoringPrereqChecker, BrowserMonitoringPrereqChecker>();
            container.Register<IProcessStatic, ProcessStatic>();
            container.Register<INetworkData, NetworkData>();
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
            container.Register<ISimpleTimerFactory, SimpleTimerFactory>();
            container.Register<IDateTimeStatic, DateTimeStatic>();
            container.Register<IMetricAggregator, MetricAggregator>();
            container.Register<IAllMetricStatsCollection, MetricWireModel>();
            container.Register<IAllMetricStatsCollection, TransactionMetricStatsCollection>();
            container.RegisterInstance<Func<MetricWireModel, MetricWireModel, MetricWireModel>>(MetricWireModel.Merge);
            container.Register<ITransactionTraceAggregator, TransactionTraceAggregator>();
            container.Register<ITransactionEventAggregator, TransactionEventAggregator>();
            container.Register<ISqlTraceAggregator, SqlTraceAggregator>();
            container.Register<IErrorTraceAggregator, ErrorTraceAggregator>();
            container.Register<IErrorEventAggregator, ErrorEventAggregator>();
            container.Register<ICustomEventAggregator, CustomEventAggregator>();
            container.Register<ISpanEventAggregator, SpanEventAggregator>();
            container.Register<ISpanEventAggregatorInfiniteTracing, SpanEventAggregatorInfiniteTracing>();
            container.Register<ILogEventAggregator, LogEventAggregator>();
            container.Register<ILogContextDataFilter, LogContextDataFilter>();
            container.Register<IGrpcWrapper<SpanBatch, RecordStatus>, SpanBatchGrpcWrapper>();
            container.Register<IDelayer, Delayer>();
            container.Register<IDataStreamingService<Span, SpanBatch, RecordStatus>, SpanStreamingService>();
            container.Register<ISpanEventMaker, SpanEventMaker>();
            container.Register<IMetricBuilder, MetricWireModel.MetricBuilder>();
            container.Register<IAgentHealthReporter, IOutOfBandMetricSource, AgentHealthReporter>();
            container.Register<IApiSupportabilityMetricCounters, IOutOfBandMetricSource, ApiSupportabilityMetricCounters>();
            container.Register<ICATSupportabilityMetricCounters, IOutOfBandMetricSource, CATSupportabilityMetricCounters>();
            container.Register<IAgentTimerService, AgentTimerService>();
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
            container.RegisterInstance<IEnumerable<ITransactionCollector>>(transactionCollectors);

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
            container.RegisterInstance<Func<IAttributeFilter, IAttributeDefinitions>>((filter) => new AttributeDefinitions(filter));
            container.Register<IAttributeDefinitionService, AttributeDefinitionService>();
            container.Register<CommandService, CommandService>();
            container.Register<ConfigurationTracker, ConfigurationTracker>();
            container.Register<IDatabaseService, DatabaseService>();
            container.Register<IErrorService, ErrorService>();

            container.Register<IInstrumentationService, InstrumentationService>();
            container.Register<InstrumentationWatcher, InstrumentationWatcher>();
            container.Register<LiveInstrumentationServerConfigurationListener, LiveInstrumentationServerConfigurationListener>();

            container.Register<IDatabaseStatementParser, DatabaseStatementParser>();
            container.Register<ITraceMetadataFactory, TraceMetadataFactory>();

            if (AgentInstallConfiguration.IsWindows)
            {
                container.Register<INativeMethods, WindowsNativeMethods>();
            }
            else
            {
                container.Register<INativeMethods, LinuxNativeMethods>();
            }
            container.Register<NewRelicCore.DistributedTracing.ITracePriorityManager, NewRelicCore.DistributedTracing.TracePriorityManager>();
            container.Register<NewRelic.Agent.Api.Experimental.ISimpleSchedulingService, SimpleSchedulingService>();

            container.Register<UpdatedLoadedModulesService, UpdatedLoadedModulesService>();

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
#if NETFRAMEWORK
			// Start GCSampler on separate thread due to delay in collecting Instance Names,
			// which can stall application startup and cause the app start to timeout
			// (e.g. Windows Services have a default startup timeout of 30 seconds)
			var gcSampler = container.Resolve<GcSampler>();
			var samplerStartThread = new Thread(() => gcSampler.Start());
			samplerStartThread.IsBackground = true;
			samplerStartThread.Start();
#else
            container.Resolve<GCSamplerNetCore>().Start();
#endif
            container.Resolve<CpuSampler>().Start();
            container.Resolve<MemorySampler>().Start();
            container.Resolve<ThreadStatsSampler>().Start();
            container.Resolve<ConfigurationTracker>();
            container.Resolve<LiveInstrumentationServerConfigurationListener>();
            container.Resolve<UpdatedLoadedModulesService>();
        }
    }
}

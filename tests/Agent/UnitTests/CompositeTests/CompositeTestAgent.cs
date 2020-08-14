// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using MoreLinq;
using NewRelic.Agent.Api;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core;
using NewRelic.Agent.Core.Aggregators;
using NewRelic.Agent.Core.Attributes;
using NewRelic.Agent.Core.CallStack;
using NewRelic.Agent.Core.Config;
using NewRelic.Agent.Core.Configuration;
using NewRelic.Agent.Core.DataTransport;
using NewRelic.Agent.Core.DependencyInjection;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Instrumentation;
using NewRelic.Agent.Core.Logging;
using NewRelic.Agent.Core.Metric;
using NewRelic.Agent.Core.Requests;
using NewRelic.Agent.Core.Segments;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Core.WireModels;
using NewRelic.Agent.Core.Wrapper;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders;
using NewRelic.Agent.Extensions.Providers;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Core.Logging;
using NewRelic.Providers.Storage.AsyncLocal;
using NewRelic.SystemInterfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using Telerik.JustMock;

namespace CompositeTests
{
    /// <summary>
    /// An agent used for composite tests that spins up almost all of the stack that the normal Agent spins up.
    /// Only a few outlying services are mocked (most notably, DataTransportService.
    /// Using this test agent in combination with the Agent allows us to write tests that cover a very
    /// broad stroke of the code base with very good performance (e.g. no/minimal disk activity).
    /// </summary>
    public class CompositeTestAgent
    {
        private readonly object _harvestActionsLockObject = new object();
        private readonly object _queuedCallbacksLockObject = new object();

        private readonly IContainer _container;

        private readonly ICollection<Action> _harvestActions;

        private readonly ICollection<WaitCallback> _queuedCallbacks;

        private IContextStorage<IInternalTransaction> _primaryTransactionContextStorage = new TestTransactionContext<IInternalTransaction>();

        public List<MetricWireModel> Metrics { get; } = new List<MetricWireModel>();

        public List<CustomEventWireModel> CustomEvents { get; } = new List<CustomEventWireModel>();

        public List<TransactionTraceWireModel> TransactionTraces { get; } = new List<TransactionTraceWireModel>();

        public List<TransactionEventWireModel> TransactionEvents { get; } = new List<TransactionEventWireModel>();

        public List<ErrorTraceWireModel> ErrorTraces { get; } = new List<ErrorTraceWireModel>();

        public EventHarvestData AdditionalHarvestData { get; } = new EventHarvestData();

        public List<ErrorEventWireModel> ErrorEvents { get; } = new List<ErrorEventWireModel>();

        public List<ISpanEventWireModel> SpanEvents { get; } = new List<ISpanEventWireModel>();

        public configuration LocalConfiguration { get; }

        public ServerConfiguration ServerConfiguration { get; }

        public IConfiguration CurrentConfiguration { get; private set; }

        public SecurityPoliciesConfiguration SecurityConfiguration { get; }

        public INativeMethods NativeMethods { get; }

        public IInstrumentationService InstrumentationService { get; }

        public InstrumentationWatcher InstrumentationWatcher { get; }

        private IAttributeDefinitionService _attribDefSvc;
        public IAttributeDefinitions AttributeDefinitions => _attribDefSvc?.AttributeDefs;

        private readonly bool _shouldAllowThreads;

        public IContainer Container => _container;

        public void ResetHarvestData()
        {
            Metrics.Clear();
            CustomEvents.Clear();
            TransactionTraces.Clear();
            TransactionEvents.Clear();
            ErrorTraces.Clear();
            ErrorEvents.Clear();
            SpanEvents.Clear();
        }

        public List<SqlTraceWireModel> SqlTraces { get; } = new List<SqlTraceWireModel>();

        public CompositeTestAgent() : this(shouldAllowThreads: false, includeAsyncLocalStorage: false)
        {
        }

        public CompositeTestAgent(bool shouldAllowThreads, bool includeAsyncLocalStorage)
        {
            Log.Initialize(new Logger());

            _shouldAllowThreads = shouldAllowThreads;

            // Create the fake classes necessary to construct services

            var mockFactory = Mock.Create<IContextStorageFactory>();
            Mock.Arrange(() => mockFactory.CreateContext<IInternalTransaction>(Arg.AnyString)).Returns(_primaryTransactionContextStorage);
            var transactionContextFactories = new List<IContextStorageFactory> { mockFactory };
            if (includeAsyncLocalStorage)
            {
                transactionContextFactories.Add(new AsyncLocalStorageFactory());
            }

            var wrappers = Enumerable.Empty<IWrapper>();
            var mockEnvironment = Mock.Create<IEnvironment>();
            var dataTransportService = Mock.Create<IDataTransportService>();
            var scheduler = Mock.Create<IScheduler>();
            NativeMethods = Mock.Create<INativeMethods>();
            _harvestActions = new List<Action>();
            Mock.Arrange(() => scheduler.ExecuteEvery(Arg.IsAny<Action>(), Arg.IsAny<TimeSpan>(), Arg.IsAny<TimeSpan?>()))
                .DoInstead<Action, TimeSpan, TimeSpan?>((action, _, __) => { lock (_harvestActionsLockObject) { _harvestActions.Add(action); } });
            var threadPoolStatic = Mock.Create<IThreadPoolStatic>();
            _queuedCallbacks = new List<WaitCallback>();
            Mock.Arrange(() => threadPoolStatic.QueueUserWorkItem(Arg.IsAny<WaitCallback>()))
                .DoInstead<WaitCallback>(callback => { lock (_queuedCallbacksLockObject) { _queuedCallbacks.Add(callback); } });

            var configurationManagerStatic = Mock.Create<IConfigurationManagerStatic>();
            Mock.Arrange(() => configurationManagerStatic.GetAppSetting("NewRelic.LicenseKey"))
                .Returns("Composite test license key");

            // Construct services
            _container = AgentServices.GetContainer();
            AgentServices.RegisterServices(_container);

            // Replace existing registrations with mocks before resolving any services
            _container.ReplaceRegistration(mockEnvironment);
            _container.ReplaceRegistration<IEnumerable<IContextStorageFactory>>(transactionContextFactories);
            _container.ReplaceRegistration<ICallStackManagerFactory>(
                new TestCallStackManagerFactory());
            _container.ReplaceRegistration(wrappers);
            _container.ReplaceRegistration(dataTransportService);
            _container.ReplaceRegistration(scheduler);
            _container.ReplaceRegistration(NativeMethods);

            _container.ReplaceRegistration(Mock.Create<ICATSupportabilityMetricCounters>());

            if (!_shouldAllowThreads)
            {
                _container.ReplaceRegistration(threadPoolStatic);
            }

            _container.ReplaceRegistration(configurationManagerStatic);

            InstrumentationService = _container.Resolve<IInstrumentationService>();
            InstrumentationWatcher = _container.Resolve<InstrumentationWatcher>();
            AgentServices.StartServices(_container);

            DisableAgentInitializer();
            InternalApi.SetAgentApiImplementation(_container.Resolve<IAgentApi>());
            AgentApi.SetSupportabilityMetricCounters(_container.Resolve<IApiSupportabilityMetricCounters>());

            // Update configuration (will also start services)
            LocalConfiguration = GetDefaultTestLocalConfiguration();
            ServerConfiguration = GetDefaultTestServerConfiguration();
            SecurityConfiguration = GetDefaultSecurityPoliciesConfiguration();
            InstrumentationWatcher.Start();
            PushConfiguration();

            _attribDefSvc = _container.Resolve<IAttributeDefinitionService>();

            // Redirect the mock DataTransportService to capture harvested wire models
            Mock.Arrange(() => dataTransportService.Send(Arg.IsAny<IEnumerable<MetricWireModel>>()))
                .Returns(SaveDataAndReturnSuccess(Metrics));
            Mock.Arrange(() => dataTransportService.Send(Arg.IsAny<IEnumerable<CustomEventWireModel>>()))
                .Returns(SaveDataAndReturnSuccess(CustomEvents));
            Mock.Arrange(() => dataTransportService.Send(Arg.IsAny<IEnumerable<TransactionTraceWireModel>>()))
                .Returns(SaveDataAndReturnSuccess(TransactionTraces));
            Mock.Arrange(() => dataTransportService.Send(Arg.IsAny<EventHarvestData>(), Arg.IsAny<IEnumerable<TransactionEventWireModel>>()))
                .Returns(SaveDataAndReturnSuccess(AdditionalHarvestData, TransactionEvents));
            Mock.Arrange(() => dataTransportService.Send(Arg.IsAny<IEnumerable<ErrorTraceWireModel>>()))
                .Returns(SaveDataAndReturnSuccess(ErrorTraces));
            Mock.Arrange(() => dataTransportService.Send(Arg.IsAny<IEnumerable<SqlTraceWireModel>>()))
                .Returns(SaveDataAndReturnSuccess(SqlTraces));
            Mock.Arrange(() => dataTransportService.Send(Arg.IsAny<EventHarvestData>(), Arg.IsAny<IEnumerable<ErrorEventWireModel>>()))
                .Returns(SaveDataAndReturnSuccess(AdditionalHarvestData, ErrorEvents));
            Mock.Arrange(() => dataTransportService.Send(Arg.IsAny<EventHarvestData>(), Arg.IsAny<IEnumerable<ISpanEventWireModel>>()))
                .Returns(SaveDataAndReturnSuccess(AdditionalHarvestData, SpanEvents));

            EnableAggregators();
        }

        /// <summary>
        /// Disables the static initializer that fires the first time the AgentApi type is referenced. This is necessary to call during tests to reference AgentApi to prevent an agent from spinning up.
        /// </summary>
        private static void DisableAgentInitializer()
        {
            var propInfo = typeof(AgentInitializer)
                .GetProperty("InitializeAgent", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            propInfo.SetValue(null, new Action(() => { }));
        }

        private static Func<IEnumerable<T>, DataTransportResponseStatus> SaveDataAndReturnSuccess<T>(List<T> dataBucket)
        {
            return datas =>
            {
                if (datas != null)
                {
                    dataBucket.AddRange(datas);
                }

                return DataTransportResponseStatus.RequestSuccessful;
            };
        }

        private static Func<EventHarvestData, IEnumerable<T>, DataTransportResponseStatus> SaveDataAndReturnSuccess<T>(EventHarvestData additions, List<T> dataBucket)
        {
            return (_, datas) =>
            {
                if (datas != null)
                    dataBucket.AddRange(datas);

                return DataTransportResponseStatus.RequestSuccessful;
            };
        }

        public void Dispose()
        {
            //Force the created transaction to finish if necessary so that it won't be garbage collected and harvested
            //by another test.
            var transaction = _primaryTransactionContextStorage.GetData();
            transaction?.Finish();

            _container.Dispose();
        }

        public IAgent GetAgent()
        {
            return _container.Resolve<IAgent>();
        }

        public IDatabaseStatementParser GetDatabaseStatementParser()
        {
            return _container.Resolve<DatabaseStatementParser>();
        }

        public IWrapperService GetWrapperService()
        {
            return _container.Resolve<IWrapperService>();
        }

        /// <summary>
        /// This method can be used for arranging tests to force a transaction to exist in the composite test agent's primary transaction context storage.
        /// </summary>
        /// <param name="transaction"></param>
        public void SetTransactionOnPrimaryContextStorage(ITransaction transaction)
        {
            _primaryTransactionContextStorage.SetData(transaction as IInternalTransaction);
        }

        public IAgentApi GetAgentApiImplementation()
        {
            return _container.Resolve<IAgentApi>();
        }

        /// <summary>
        /// Simulates a harvest, collecting any transaction samples and events generated since the last harvest. 
        /// Will update <see cref="TransactionTraces"/>, <see cref="TransactionEvents"/> and <see cref="ErrorEvents" />.
        /// </summary>
        public void Harvest()
        {
            if (!_shouldAllowThreads)
            {
                ExecuteThreadPoolQueuedCallbacks();
            }

            lock (_harvestActionsLockObject)
            {
                _harvestActions.ForEach(action => action?.Invoke());
            }
        }

        public void ExecuteThreadPoolQueuedCallbacks()
        {
            if (_shouldAllowThreads)
            {
                throw new InvalidOperationException("When shouldAllowThreads is true, the thread pool will not be mocked or stubbed.");
            }

            lock (_queuedCallbacksLockObject)
            {
                _queuedCallbacks.ForEach(callback => callback?.Invoke(null));
            }
        }

        /// <summary>
        /// Pushes out the configuration options stored in <see cref="LocalConfiguration"/> and <see cref="ServerConfiguration"/>. Will update <see cref="CurrentConfiguration"/> with the result from ConfigurationService.
        /// </summary>
        public void PushConfiguration()
        {
            // Push LocalConfigurationUpdates
            EventBus<ConfigurationDeserializedEvent>.Publish(new ConfigurationDeserializedEvent(LocalConfiguration));

            // Push ServerConfigurationUpdates
            EventBus<ServerConfigurationUpdatedEvent>.Publish(new ServerConfigurationUpdatedEvent(ServerConfiguration));

            EventBus<SecurityPoliciesConfigurationUpdatedEvent>.Publish(new SecurityPoliciesConfigurationUpdatedEvent(SecurityConfiguration));

            // Update CurrentConfiguration
            IConfiguration newConfig = null;
            RequestBus<GetCurrentConfigurationRequest, IConfiguration>.Post(new GetCurrentConfigurationRequest(), config => newConfig = config);
            if (newConfig == null)
                throw new NullReferenceException("newConfig");
            CurrentConfiguration = newConfig;
        }

        public void SetEventListenerSamplersEnabled(bool enable)
        {
            CurrentConfiguration.EventListenerSamplersEnabled = enable;
        }

        private void EnableAggregators()
        {
            EventBus<AgentConnectedEvent>.Publish(new AgentConnectedEvent());
        }

        private static configuration GetDefaultTestLocalConfiguration()
        {
            var configuration = new configuration();

            // Distributed tracing is disabled by default. However, we have fewer tests that need it disabled than we do that need it enabled.
            configuration.distributedTracing.enabled = true;
            return configuration;
        }

        private static ServerConfiguration GetDefaultTestServerConfiguration()
        {
            return new ServerConfiguration
            {
                AgentRunId = "NotSet",
                CatId = "123#456",
                RpmConfig = new ServerConfiguration.AgentConfig
                {
                    // Set an incredibly low trace threshold to make sure that traces are always generated by default
                    TransactionTracerThreshold = TimeSpan.FromTicks(1).TotalSeconds,
                }
            };
        }

        private static SecurityPoliciesConfiguration GetDefaultSecurityPoliciesConfiguration()
        {
            return new SecurityPoliciesConfiguration();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using JetBrains.Annotations;
using MoreLinq;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core;
using NewRelic.Agent.Core.Aggregators;
using NewRelic.Agent.Core.Api;
using NewRelic.Agent.Core.CallStack;
using NewRelic.Agent.Core.Config;
using NewRelic.Agent.Core.Configuration;
using NewRelic.Agent.Core.DataTransport;
using NewRelic.Agent.Core.DependencyInjection;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Requests;
using NewRelic.Agent.Core.SharedInterfaces;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Core.WireModels;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders;
using NewRelic.Agent.Extensions.Providers;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Agent.TestUtilities;
using NewRelic.SystemInterfaces;
using Telerik.JustMock;
using ITransaction = NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders.ITransaction;

namespace CompositeTests
{
    /// <summary>
    /// An agent used for composite tests that spins up almost all of the stack that the normal Agent spins up. Only a few outlying services are mocked (most notably, <see cref="DataTransportService"/>. Using this test agent in combination with the <see cref="AgentWrapperApi"/> allows us to write tests that cover a very broad stroke of the code base with very good performance (e.g. no/minimal disk activity).
    /// </summary>
    public class CompositeTestAgent
    {
        [NotNull]
        private readonly IContainer _container;

        [NotNull]
        private readonly ICollection<Action> _harvestActions;

        [NotNull]
        private readonly ICollection<WaitCallback> _queuedCallbacks;

        [NotNull]
        public List<MetricWireModel> Metrics { get; } = new List<MetricWireModel>();

        [NotNull]
        public List<CustomEventWireModel> CustomEvents { get; } = new List<CustomEventWireModel>();

        [NotNull]
        public List<TransactionTraceWireModel> TransactionTraces { get; } = new List<TransactionTraceWireModel>();

        [NotNull]
        public List<TransactionEventWireModel> TransactionEvents { get; } = new List<TransactionEventWireModel>();

        [NotNull]
        public List<ErrorTraceWireModel> ErrorTraces { get; } = new List<ErrorTraceWireModel>();

        public ErrorEventAdditions ErrorEventAdditionalInfo { get; } = new ErrorEventAdditions();

        [NotNull]
        public List<ErrorEventWireModel> ErrorEvents { get; } = new List<ErrorEventWireModel>();

        [NotNull]
        public configuration LocalConfiguration { get; }

        [NotNull]
        public ServerConfiguration ServerConfiguration { get; }

        public IConfiguration CurrentConfiguration { get; private set; }

        private readonly bool _shouldAllowThreads;


        [NotNull]
        public List<SqlTraceWireModel> SqlTraces { get; private set; } = new List<SqlTraceWireModel>();

        public CompositeTestAgent() : this(shouldAllowThreads: false)
        {

        }

        public CompositeTestAgent(bool shouldAllowThreads)
        {
            _shouldAllowThreads = shouldAllowThreads;

            // Create the fake classes necessary to construct services
            var mockFactory = Mock.Create<IContextStorageFactory>();
            Mock.Arrange(() => mockFactory.CreateContext<ITransaction>(Arg.AnyString)).Returns(new TestTransactionContext<ITransaction>());
            var transactionContextFactories = new[] { mockFactory };
            var wrappers = Enumerable.Empty<IWrapper>();
            var mockEnvironment = Mock.Create<IEnvironment>();
            var dataTransportService = Mock.Create<IDataTransportService>();
            var scheduler = Mock.Create<IScheduler>();
            _harvestActions = new List<Action>();
            Mock.Arrange(() => scheduler.ExecuteEvery(Arg.IsAny<Action>(), Arg.IsAny<TimeSpan>(), Arg.IsAny<TimeSpan?>()))
                .DoInstead<Action, TimeSpan, TimeSpan?>((action, _, __) => _harvestActions.Add(action));
            var threadPoolStatic = Mock.Create<IThreadPoolStatic>();
            _queuedCallbacks = new List<WaitCallback>();
            Mock.Arrange(() => threadPoolStatic.QueueUserWorkItem(Arg.IsAny<WaitCallback>()))
                .DoInstead<WaitCallback>(callback => _queuedCallbacks.Add(callback));

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

            if (!_shouldAllowThreads)
            {
                _container.ReplaceRegistration(threadPoolStatic);
            }

            _container.ReplaceRegistration(configurationManagerStatic);
            AgentServices.StartServices(_container);

            DisableAgentInitializer();
            AgentApi.SetAgentApiImplementation(_container.Resolve<IAgentApi>());

            // Update configuration (will also start services)
            LocalConfiguration = GetDefaultTestLocalConfiguration();
            ServerConfiguration = GetDefaultTestServerConfiguration();
            PushConfiguration();

            // Redirect the mock DataTransportService to capture harvested wire models
            Mock.Arrange(() => dataTransportService.Send(Arg.IsAny<IEnumerable<MetricWireModel>>()))
                .Returns(SaveDataAndReturnSuccess(Metrics));
            Mock.Arrange(() => dataTransportService.Send(Arg.IsAny<IEnumerable<CustomEventWireModel>>()))
                .Returns(SaveDataAndReturnSuccess(CustomEvents));
            Mock.Arrange(() => dataTransportService.Send(Arg.IsAny<IEnumerable<TransactionTraceWireModel>>()))
                .Returns(SaveDataAndReturnSuccess(TransactionTraces));
            Mock.Arrange(() => dataTransportService.Send(Arg.IsAny<IEnumerable<TransactionEventWireModel>>()))
                .Returns(SaveDataAndReturnSuccess(TransactionEvents));
            Mock.Arrange(() => dataTransportService.Send(Arg.IsAny<IEnumerable<ErrorTraceWireModel>>()))
                .Returns(SaveDataAndReturnSuccess(ErrorTraces));
            Mock.Arrange(() => dataTransportService.Send(Arg.IsAny<IEnumerable<SqlTraceWireModel>>()))
                .Returns(SaveDataAndReturnSuccess(SqlTraces));
            Mock.Arrange(() => dataTransportService.Send(Arg.IsAny<ErrorEventAdditions>(), Arg.IsAny<IEnumerable<ErrorEventWireModel>>()))
                .Returns(SaveDataAndReturnSuccess(ErrorEventAdditionalInfo, ErrorEvents));
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

        private static Func<IEnumerable<T>, DataTransportResponseStatus> SaveDataAndReturnSuccess<T>([NotNull] List<T> dataBucket)
        {
            return datas =>
            {
                if (datas != null)
                    dataBucket.AddRange(datas);

                return DataTransportResponseStatus.RequestSuccessful;
            };
        }

        private static Func<ErrorEventAdditions, IEnumerable<T>, DataTransportResponseStatus> SaveDataAndReturnSuccess<T>(ErrorEventAdditions additions, [NotNull] List<T> dataBucket)
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
            _container.Dispose();
        }

        [NotNull]
        public IAgentWrapperApi GetAgentWrapperApi()
        {
            return _container.Resolve<IAgentWrapperApi>();
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

            _harvestActions.ForEach(action => action?.Invoke());
        }

        public void ExecuteThreadPoolQueuedCallbacks()
        {
            if (_shouldAllowThreads)
            {
                throw new InvalidOperationException("When shouldAllowThreads is true, the thread pool will not be mocked or stubbed.");
            }

            _queuedCallbacks.ForEach(callback => callback?.Invoke(null));
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

            // Update CurrentConfiguration
            IConfiguration newConfig = null;
            RequestBus<GetCurrentConfigurationRequest, IConfiguration>.Post(new GetCurrentConfigurationRequest(), config => newConfig = config);
            if (newConfig == null)
                throw new NullReferenceException("newConfig");
            CurrentConfiguration = newConfig;
        }

        [NotNull]
        private static configuration GetDefaultTestLocalConfiguration()
        {
            return new configuration();
        }

        [NotNull]
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
    }
}

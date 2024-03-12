// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Aggregators;
using NewRelic.Agent.Core.Attributes;
using NewRelic.Agent.Core.Config;
using NewRelic.Agent.Core.Configuration;
using NewRelic.Agent.Core.DistributedTracing;
using NewRelic.Agent.Core.Errors;
using NewRelic.Agent.Core.Metrics;
using NewRelic.Agent.Core.Spans;
using NewRelic.Agent.Core.Segments;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.Transformers;
using NewRelic.Agent.Core.Transformers.TransactionTransformer;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Core.WireModels;
using NewRelic.SystemInterfaces;
using NewRelic.SystemInterfaces.Web;
using Newtonsoft.Json;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NewRelic.Agent.TestUtilities;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.CrossAgentTests
{
    internal class TestableDefaultConfiguration : DefaultConfiguration
    {
        public TestableDefaultConfiguration(IEnvironment environment, configuration localConfig, ServerConfiguration serverConfig, RunTimeConfiguration runTimeConfiguration, SecurityPoliciesConfiguration securityPoliciesConfiguration, IBootstrapConfiguration bootstrapConfiguration, IProcessStatic processStatic, IHttpRuntimeStatic httpRuntimeStatic, IConfigurationManagerStatic configurationManagerStatic, IDnsStatic dnsStatic) : base(environment, localConfig, serverConfig, runTimeConfiguration, securityPoliciesConfiguration, bootstrapConfiguration, processStatic, httpRuntimeStatic, configurationManagerStatic, dnsStatic) { }
    }

    [TestFixture]
    public class ServerSentEventTests
    {
        private configuration _localConfig;
        private ServerConfiguration _serverConfig;
        private RunTimeConfiguration _runTimeConfig;
        private IBootstrapConfiguration _bootstrapConfig;
        private DefaultConfiguration _defaultConfig;

        private ISpanEventAggregator _spanEventAggregator;
        private ISpanEventAggregatorInfiniteTracing _spanEventAggregatorInfiniteTracing;
        private ISpanEventMaker _spanEventMaker;
        private ICustomEventAggregator _customEventAggregator;
        private IAgentTimerService _agentTimerService;
        private TransactionTransformer _transactionTransformer;
        private CustomEventTransformer _customEventTransformer;
        private IErrorEventAggregator _errorEventAggregator;
        private IErrorEventMaker _errorEventMaker;
        private ISqlTraceAggregator _sqlTraceAggregator;
        private ISqlTraceMaker _sqlTraceMaker;
        private ITransactionMetricNameMaker _transactionMetricNameMaker;
        private ITransactionSegmentState _transactionSegmentState;
        private ISegmentTreeMaker _segmentTreeMaker;
        private IMetricBuilder _metricBuilder;
        private IMetricAggregator _metricAggregator;
        private IConfigurationService _configurationService;
        private ITransactionTraceAggregator _transactionTraceAggregator;
        private ITransactionTraceMaker _transactionTraceMaker;
        private ITransactionEventAggregator _transactionEventAggregator;
        private ITransactionEventMaker _transactionEventMaker;
        private ITransactionAttributeMaker _transactionAttributeMaker;
        private IErrorTraceAggregator _errorTraceAggregator;
        private IErrorTraceMaker _errorTraceMaker;
        private IMetricNameService _metricNameService;
        private IErrorService _errorService;
        private IAttributeDefinitionService _attribDefSvc;

        [SetUp]
        public void SetUp()
        {
            _localConfig = new configuration();
            _localConfig.distributedTracing.enabled = true;
            _serverConfig = new ServerConfiguration();
            _runTimeConfig = new RunTimeConfiguration();
            _bootstrapConfig = Mock.Create<IBootstrapConfiguration>();
            _defaultConfig = new TestableDefaultConfiguration(Mock.Create<IEnvironment>(), _localConfig, _serverConfig, _runTimeConfig,
                new SecurityPoliciesConfiguration(), _bootstrapConfig, Mock.Create<IProcessStatic>(), Mock.Create<IHttpRuntimeStatic>(), Mock.Create<IConfigurationManagerStatic>(),
                Mock.Create<IDnsStatic>());

            _transactionMetricNameMaker = Mock.Create<ITransactionMetricNameMaker>();
            Mock.Arrange(() => _transactionMetricNameMaker.GetTransactionMetricName(Arg.Matches<ITransactionName>(txName => txName.IsWeb)))
                .Returns(new TransactionMetricName("WebTransaction", "TransactionName"));
            Mock.Arrange(() => _transactionMetricNameMaker.GetTransactionMetricName(Arg.Matches<ITransactionName>(txName => !txName.IsWeb))).Returns(new TransactionMetricName("OtherTransaction", "TransactionName"));

            _transactionSegmentState = Mock.Create<ITransactionSegmentState>();
            Mock.Arrange(() => _transactionSegmentState.GetRelativeTime()).Returns(() => TimeSpan.Zero);
            Mock.Arrange(() => _transactionSegmentState.AttribDefs).Returns(() => new AttributeDefinitions(new AttributeFilter(new AttributeFilter.Settings())));

            _segmentTreeMaker = Mock.Create<ISegmentTreeMaker>();

            _metricNameService = Mock.Create<IMetricNameService>();

            _metricBuilder = Mock.Create<IMetricBuilder>();
            _metricAggregator = Mock.Create<IMetricAggregator>();

            _configurationService = Mock.Create<IConfigurationService>();
            Mock.Arrange(() => _configurationService.Configuration).Returns(_defaultConfig);

            _transactionTraceAggregator = Mock.Create<ITransactionTraceAggregator>();
            _transactionTraceMaker = Mock.Create<ITransactionTraceMaker>();
            _transactionEventAggregator = Mock.Create<ITransactionEventAggregator>();
            _transactionEventMaker = Mock.Create<ITransactionEventMaker>();
            _transactionAttributeMaker = Mock.Create<ITransactionAttributeMaker>();
            _errorTraceAggregator = Mock.Create<IErrorTraceAggregator>();
            _errorTraceMaker = Mock.Create<IErrorTraceMaker>();
            _errorEventAggregator = Mock.Create<IErrorEventAggregator>();
            _errorEventMaker = Mock.Create<IErrorEventMaker>();
            _sqlTraceAggregator = Mock.Create<ISqlTraceAggregator>();
            _sqlTraceMaker = Mock.Create<ISqlTraceMaker>();

            _spanEventAggregator = Mock.Create<ISpanEventAggregator>();
            Mock.Arrange(() => _spanEventAggregator.IsServiceEnabled).Returns(() => _defaultConfig != null && _defaultConfig.SpanEventsEnabled && _defaultConfig.SpanEventsMaxSamplesStored > 0 && _defaultConfig.DistributedTracingEnabled);
            Mock.Arrange(() => _spanEventAggregator.IsServiceAvailable).Returns(() => _defaultConfig != null && _defaultConfig.SpanEventsEnabled && _defaultConfig.SpanEventsMaxSamplesStored > 0 && _defaultConfig.DistributedTracingEnabled);

            _spanEventAggregatorInfiniteTracing = Mock.Create<ISpanEventAggregatorInfiniteTracing>();
            Mock.Arrange(() => _spanEventAggregatorInfiniteTracing.IsServiceEnabled).Returns(() => !string.IsNullOrWhiteSpace(_defaultConfig?.InfiniteTracingTraceObserverHost));
            Mock.Arrange(() => _spanEventAggregatorInfiniteTracing.IsServiceAvailable).Returns(() => !string.IsNullOrWhiteSpace(_defaultConfig?.InfiniteTracingTraceObserverHost));

            _spanEventMaker = Mock.Create<ISpanEventMaker>();
            _customEventAggregator = Mock.Create<ICustomEventAggregator>();
            _errorService = Mock.Create<IErrorService>();

            _attribDefSvc = new AttributeDefinitionService((f) => new AttributeDefinitions(f));

            _agentTimerService = Mock.Create<IAgentTimerService>();
            var logEventAggregator = Mock.Create<ILogEventAggregator>();
            _transactionTransformer = new TransactionTransformer(_transactionMetricNameMaker, _segmentTreeMaker, _metricNameService, _metricAggregator, _configurationService, _transactionTraceAggregator, _transactionTraceMaker, _transactionEventAggregator, _transactionEventMaker, _transactionAttributeMaker, _errorTraceAggregator, _errorTraceMaker, _errorEventAggregator, _errorEventMaker, _sqlTraceAggregator, _sqlTraceMaker, _spanEventAggregator, _spanEventMaker, _agentTimerService, Mock.Create<IAdaptiveSampler>(), _errorService, _spanEventAggregatorInfiniteTracing, logEventAggregator);
            _customEventTransformer = new CustomEventTransformer(_configurationService, _customEventAggregator, _attribDefSvc);
        }

        [TearDown]
        public void TearDown()
        {
            _attribDefSvc.Dispose();
            _metricNameService.Dispose();
            _sqlTraceAggregator.Dispose();
        }

        [Test]
        [TestCaseSource(typeof(ServerSentEventTests), nameof(TestCases))]
        public void Test(TestCase testCase)
        {
            // ARRANGE
            var errorEvent = Mock.Create<ErrorEventWireModel>();
            var errorTrace = Mock.Create<ErrorTraceWireModel>();
            var transactionEvent = Mock.Create<TransactionEventWireModel>();
            var transactionTrace = Mock.Create<TransactionTraceWireModel>();
            var spanEvents = new List<ISpanEventWireModel>() { new SpanAttributeValueCollection() };
            var customEvent = Mock.Create<CustomEventWireModel>();
            var transaction = TestTransactions.CreateDefaultTransaction(uri: "http://www.newrelic.com/test?param=value", statusCode: 404);

            Mock.Arrange(() => _errorEventMaker.GetErrorEvent(Arg.IsAny<ImmutableTransaction>(), Arg.IsAny<IAttributeValueCollection>())).Returns(errorEvent);
            Mock.Arrange(() => _errorTraceMaker.GetErrorTrace(Arg.IsAny<ImmutableTransaction>(), Arg.IsAny<IAttributeValueCollection>(), Arg.IsAny<TransactionMetricName>())).Returns(errorTrace);
            Mock.Arrange(() => _transactionEventMaker.GetTransactionEvent(Arg.IsAny<ImmutableTransaction>(), Arg.IsAny<IAttributeValueCollection>())).Returns(transactionEvent);
            Mock.Arrange(() => _transactionTraceMaker.GetTransactionTrace(Arg.IsAny<ImmutableTransaction>(), Arg.IsAny<IEnumerable<ImmutableSegmentTreeNode>>(), Arg.IsAny<TransactionMetricName>(), Arg.IsAny<IAttributeValueCollection>())).Returns(transactionTrace);
            Mock.Arrange(() => _spanEventMaker.GetSpanEvents(Arg.IsAny<ImmutableTransaction>(), Arg.IsAny<string>(), Arg.IsAny<IAttributeValueCollection>())).Returns(spanEvents);

            Action assertAction = null;

            var num = int.Parse(testCase.ExpectedDataSeen.FirstOrDefault()["count"].ToString());

            if (testCase.TestName.Contains("collect_error_events_"))
            {
                _serverConfig.ErrorEventCollectionEnabled = testCase.ConnectResponse["collect_error_events"];

                assertAction = new Action(() => Mock.Assert(() => _errorEventAggregator.Collect(errorEvent), Occurs.Exactly(num)));
            }
            else if (testCase.TestName.Contains("collect_analytics_events_"))
            {
                _serverConfig.AnalyticsEventCollectionEnabled = testCase.ConnectResponse["collect_analytics_events"];
                assertAction = new Action(() => Mock.Assert(() => _transactionEventAggregator.Collect(transactionEvent), Occurs.Exactly(num)));
            }
            else if (testCase.TestName.Contains("collect_errors_"))
            {
                _serverConfig.ErrorCollectionEnabled = testCase.ConnectResponse["collect_errors"];
                assertAction = new Action(() => Mock.Assert(() => _errorTraceAggregator.Collect(errorTrace), Occurs.Exactly(num)));
            }
            else if (testCase.TestName.Contains("collect_traces_"))
            {
                _serverConfig.TraceCollectionEnabled = testCase.ConnectResponse["collect_traces"];
                assertAction = new Action(() => Mock.Assert(() => _transactionTraceAggregator.Collect(Arg.IsAny<TransactionTraceWireModelComponents>()), Occurs.Exactly(num)));
            }
            else if (testCase.TestName.Contains("collect_custom_events_"))
            {
                _serverConfig.CustomEventCollectionEnabled = testCase.ConnectResponse["collect_custom_events"];
                assertAction = new Action(() => Mock.Assert(() => _customEventAggregator.Collect(Arg.IsAny<CustomEventWireModel>()), Occurs.Exactly(num)));
            }
            else if (testCase.TestName.Contains("collect_span_events_"))
            {
                _serverConfig.SpanEventCollectionEnabled = testCase.ConnectResponse["collect_span_events"];

                //if transaction.Sampled is null or false, span events aren't generated.
                transaction = TestTransactions.CreateDefaultTransaction(uri: "http://www.newrelic.com/test?param=value", statusCode: 404, sampled: true);

                assertAction = new Action(() => Mock.Assert(() => _spanEventAggregator.Collect(spanEvents), Occurs.Exactly(num)));
            }

            // ACTION
            _transactionTransformer.Transform(transaction);
            _customEventTransformer.Transform("myEvent", new Dictionary<string, object>(), 1);

            // ASSERT
            assertAction();
        }

        public static IEnumerable<TestCase[]> TestCases
        {
            get
            {
                string location = Assembly.GetExecutingAssembly().GetLocation();
                var dllPath = Path.GetDirectoryName(new Uri(location).LocalPath);
                var jsonPath = Path.Combine(dllPath, "CrossAgentTests", "ServerSentEvent", "data_collection_server_configuration.json");
                var jsonString = File.ReadAllText(jsonPath);

                var testCases = JsonConvert.DeserializeObject<IEnumerable<TestCase>>(jsonString);
                Assert.That(testCases, Is.Not.Null);
                return testCases
                    .Where(testCase => testCase != null)
                    .Select(testCase => new[] { testCase });
            }
        }

        public class TestCase
        {
            [JsonProperty(PropertyName = "test_name")]
            public readonly string TestName;

            [JsonProperty(PropertyName = "connect_response")]
            public readonly IDictionary<string, bool> ConnectResponse;

            [JsonProperty(PropertyName = "expected_data_seen")]
            public readonly IList<IDictionary<string, object>> ExpectedDataSeen;

            [JsonProperty(PropertyName = "expected_endpoint_calls")]
            public readonly IList<IDictionary<string, object>> ExpectedEndpointCalls;

            public override string ToString()
            {
                return TestName;
            }
        }

    }
}

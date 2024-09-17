// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Aggregators;
using NewRelic.Agent.Core.Attributes;
using NewRelic.Agent.Core.Config;
using NewRelic.Agent.Core.Configuration;
using NewRelic.Agent.Core.Configuration.UnitTest;
using NewRelic.Agent.Core.Database;
using NewRelic.Agent.Core.Errors;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Fixtures;
using NewRelic.Agent.Core.Metrics;
using NewRelic.Agent.Core.Segments;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.Transformers.TransactionTransformer;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Data;
using NewRelic.Agent.Extensions.Parsing;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Agent.TestUtilities;
using NewRelic.Agent.Core.SharedInterfaces;
using NewRelic.Agent.Core.SharedInterfaces.Web;
using NewRelic.Testing.Assertions;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.Spans.UnitTest
{
    [TestFixture]
    public class SpanEventMakerTests
    {
        private const float Priority = 0.5f;
        private const string MethodCallType = "type";
        private const string MethodCallMethod = "method";
        private const string SegmentName = "test";
        private const string DistributedTraceTraceId = "distributedTraceTraceId";
        private const string DistributedTraceGuid = "distributedTraceGuid";
        private const string W3cParentId = "w3cParentId";
        private const string Vendor1 = "dd";
        private const string Vendor2 = "earl";
        private readonly List<string> VendorStateEntries = new List<string>() { $"{Vendor1}=YzRiMTIxODk1NmVmZTE4ZQ", $"{Vendor2}=aaaaaaaaaaaaaa" };

        private const string GenericCategory = "generic";
        private const string DatastoreCategory = "datastore";
        private const string HttpCategory = "http";
        private const string ShortQuery = "Select * from users where ssn = 433871122";

        private const string HttpUri = "http://localhost:80/api/test";
        private const string HttpMethod = "GET";

        private const string TransactionName = "WebTransaction/foo/bar";

        private const string MessageBrokerVendor = "RabbitMQ";
        private const string MessageBrokerQueue = "MyQueue";
        private const string ServerAddress = "localhost";
        private const int ServerPort = 5672;
        private const string MessageBrokerCloudAccountId = "1234";
        private const string MessageBrokerCloudRegion = "us-west-2";
        private const string MessageBrokerRoutingKey = "myroutingkey";


        private SpanEventMaker _spanEventMaker;
        private IDatabaseService _databaseService;
        private ITransactionEventMaker _transactionEventMaker;
        private string _transactionGuid;
        private DateTime _startTime;
        private Segment _baseGenericSegment;
        private Segment _baseGenericAsyncSegment;
        private Segment _childGenericSegment;
        private Segment _baseDatastoreSegment;
        private Segment _baseHttpSegment;
        private Segment _baseMessageBrokerConsumeSegment;
        private Segment _baseMessageBrokerProduceSegment;

        private string _obfuscatedSql;
        private ParsedSqlStatement _parsedSqlStatement;
        private ConnectionInfo _connectionInfo;

        private ConfigurationAutoResponder _configAutoResponder;
        private IAttributeDefinitionService _attribDefSvc;
        private IAttributeDefinitions _attribDefs => _attribDefSvc?.AttributeDefs;
        private IMetricNameService _metricNameSvc;
        private ITransactionMetricNameMaker _transactionMetricNameMaker;
        private ITransactionAttributeMaker _transactionAttribMaker;

        private IConfiguration _config;
        private IConfigurationService _configurationService;
        private IEnvironment _environment;
        private IProcessStatic _processStatic;
        private IHttpRuntimeStatic _httpRuntimeStatic;
        private IConfigurationManagerStatic _configurationManagerStatic;
        private IDnsStatic _dnsStatic;
        private SecurityPoliciesConfiguration _securityPoliciesConfiguration;
        private RunTimeConfiguration _runTimeConfiguration;
        private ServerConfiguration _serverConfig;
        private IBootstrapConfiguration _bootstrapConfiguration;
        private configuration _localConfig;


        private void SetLocalConfigurationDefaults()
        {
            _localConfig = new configuration();

            _localConfig.attributes.enabled = true;

            _localConfig.errorCollector.enabled = true;

            _localConfig.distributedTracing.enabled = true;
            _localConfig.spanEvents.enabled = true;
            _localConfig.spanEvents.attributes.enabled = true;

            _localConfig.transactionTracer.enabled = true;
            _localConfig.transactionEvents.enabled = true;
            _localConfig.transactionTracer.attributes.enabled = true;
            _localConfig.transactionEvents.attributes.enabled = true;
        }

        private void PublishConfig()
        {
            var config = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfiguration, _securityPoliciesConfiguration, _bootstrapConfiguration, _processStatic, _httpRuntimeStatic, _configurationManagerStatic, _dnsStatic);
            _config = config;
            EventBus<ConfigurationUpdatedEvent>.Publish(new ConfigurationUpdatedEvent(_config, ConfigurationUpdateSource.Local));
        }

        [SetUp]
        public void SetUp()
        {
            _environment = Mock.Create<IEnvironment>();
            _processStatic = Mock.Create<IProcessStatic>();
            _httpRuntimeStatic = Mock.Create<IHttpRuntimeStatic>();
            _configurationManagerStatic = Mock.Create<IConfigurationManagerStatic>();
            _dnsStatic = Mock.Create<IDnsStatic>();
            _securityPoliciesConfiguration = new SecurityPoliciesConfiguration();
            _bootstrapConfiguration = Mock.Create<IBootstrapConfiguration>();
            
            _runTimeConfiguration = new RunTimeConfiguration();
            _serverConfig = new ServerConfiguration();

            SetLocalConfigurationDefaults();
            PublishConfig();

            _configAutoResponder = new ConfigurationAutoResponder(_config);

            _configurationService = Mock.Create<IConfigurationService>();
            Mock.Arrange(() => _configurationService.Configuration).Returns(() => _config);

            _attribDefSvc = new AttributeDefinitionService((f) => new AttributeDefinitions(f));

            _metricNameSvc = new MetricNameService();
            _transactionMetricNameMaker =  new TransactionMetricNameMaker(_metricNameSvc);

            _transactionAttribMaker = new TransactionAttributeMaker(_configurationService, _attribDefSvc);

            _spanEventMaker = new SpanEventMaker(_attribDefSvc, _configurationService);
            _databaseService = new DatabaseService();

            _transactionEventMaker =  new TransactionEventMaker(_attribDefSvc);


            _transactionGuid = GuidGenerator.GenerateNewRelicGuid();
            _startTime = new DateTime(2018, 7, 18, 7, 0, 0, DateTimeKind.Utc); // unixtime = 1531897200000

            // Generic Segments
            _baseGenericSegment = new Segment(CreateTransactionSegmentState(3, null, 777), new MethodCallData(MethodCallType, MethodCallMethod, 1));
            _baseGenericSegment.SetSegmentData(new SimpleSegmentData(SegmentName));

            _baseGenericAsyncSegment = new Segment(CreateTransactionSegmentState(5, null, 888), new MethodCallData(MethodCallType, MethodCallMethod, 1, true));
            _baseGenericAsyncSegment.SetSegmentData(new SimpleSegmentData(SegmentName));

            _childGenericSegment = new Segment(CreateTransactionSegmentState(4, 3, 777), new MethodCallData(MethodCallType, MethodCallMethod, 1));
            _childGenericSegment.SetSegmentData(new SimpleSegmentData(SegmentName));

            // Datastore Segments
            _connectionInfo = new ConnectionInfo(DatastoreVendor.MSSQL.ToKnownName(), "localhost", 1234, "default", "maininstance");
            _parsedSqlStatement = SqlParser.GetParsedDatabaseStatement(DatastoreVendor.MSSQL, System.Data.CommandType.Text, ShortQuery);

            _obfuscatedSql = _databaseService.GetObfuscatedSql(ShortQuery, DatastoreVendor.MSSQL);
            _baseDatastoreSegment = new Segment(CreateTransactionSegmentState(3, null, 777), new MethodCallData(MethodCallType, MethodCallMethod, 1));
            _baseDatastoreSegment.SetSegmentData(new DatastoreSegmentData(_databaseService, _parsedSqlStatement, ShortQuery, _connectionInfo));

            // Http Segments
            _baseHttpSegment = new Segment(CreateTransactionSegmentState(3, null, 777), new MethodCallData(MethodCallType, MethodCallMethod, 1));
            _baseHttpSegment.SetSegmentData(new ExternalSegmentData(new Uri(HttpUri), HttpMethod));

            // MessageBroker Segments
            _baseMessageBrokerConsumeSegment = new Segment(CreateTransactionSegmentState(3, null, 777), new MethodCallData(MethodCallType, MethodCallMethod, 1));
            _baseMessageBrokerConsumeSegment.SetSegmentData(
                new MessageBrokerSegmentData(
                    vendor: MessageBrokerVendor,
                    destination: MessageBrokerQueue,
                    destinationType: MetricNames.MessageBrokerDestinationType.Queue,
                    action: MetricNames.MessageBrokerAction.Consume,
                    messagingSystemName: MessageBrokerVendor,
                    cloudAccountId: MessageBrokerCloudAccountId,
                    cloudRegion: MessageBrokerCloudRegion,
                    serverAddress: ServerAddress,
                    serverPort: ServerPort,
                    routingKey: MessageBrokerRoutingKey));

            _baseMessageBrokerProduceSegment = new Segment(CreateTransactionSegmentState(3, null, 777), new MethodCallData(MethodCallType, MethodCallMethod, 1));
            _baseMessageBrokerProduceSegment.SetSegmentData(
                new MessageBrokerSegmentData(
                    vendor: MessageBrokerVendor,
                    destination: MessageBrokerQueue,
                    destinationType: MetricNames.MessageBrokerDestinationType.Queue,
                    action: MetricNames.MessageBrokerAction.Produce,
                    messagingSystemName: MessageBrokerVendor,
                    cloudAccountId: MessageBrokerCloudAccountId,
                    cloudRegion: MessageBrokerCloudRegion,
                    serverAddress: ServerAddress,
                    serverPort: ServerPort,
                    routingKey: MessageBrokerRoutingKey));
        }

        [TearDown]
        public void TearDown()
        {
            _attribDefSvc.Dispose();
            _configAutoResponder?.Dispose();
            _databaseService.Dispose();
            _metricNameSvc.Dispose();
        }

        #region Generic and  General Tests

        [Test]
        public void GetSpanEvent_ReturnsSpanEventPerSegment_ValidateCount()
        {
            // ARRANGE
            var segments = new List<Segment>()
            {
                _baseGenericSegment.CreateSimilar(TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(5), new List<KeyValuePair<string, object>>()),
                _childGenericSegment.CreateSimilar(TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(5), new List<KeyValuePair<string, object>>())
            };
            var immutableTransaction = BuildTestTransaction(segments, sampled: true, hasIncomingPayload: false);

            var transactionMetricName = _transactionMetricNameMaker.GetTransactionMetricName(immutableTransaction.TransactionName);
            var metricStatsCollection = new TransactionMetricStatsCollection(transactionMetricName);
            var transactionAttribs = _transactionAttribMaker.GetAttributes(immutableTransaction, transactionMetricName, TimeSpan.FromSeconds(1), immutableTransaction.Duration, metricStatsCollection);

            // ACT
            var spanEvents = _spanEventMaker.GetSpanEvents(immutableTransaction, TransactionName, transactionAttribs);

            // ASSERT
            // +1 is for the faux root segment.
            Assert.That(spanEvents.Count(), Is.EqualTo(segments.Count + 1));
        }

        [Test]
        public void GetSpanEvent_ReturnsSpanEventPerSegment_ValidateChildValues()
        {
            // ARRANGE
            var segments = new List<Segment>()
            {
                _baseGenericSegment.CreateSimilar(TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(5), new List<KeyValuePair<string, object>>()),
                _childGenericSegment.CreateSimilar(TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(5), new List<KeyValuePair<string, object>>())
            };

            var immutableTransaction = BuildTestTransaction(segments, true, false);
            var transactionMetricName = _transactionMetricNameMaker.GetTransactionMetricName(immutableTransaction.TransactionName);
            var metricStatsCollection = new TransactionMetricStatsCollection(transactionMetricName);
            var transactionAttribs = _transactionAttribMaker.GetAttributes(immutableTransaction, transactionMetricName, TimeSpan.FromSeconds(1), immutableTransaction.Duration, metricStatsCollection);

            // ACT
            var spanEvents = _spanEventMaker.GetSpanEvents(immutableTransaction, TransactionName, transactionAttribs);
            var spanEvent = spanEvents.ToList()[2]; // look at child span only since it has all the values
            var spanEventIntrinsicAttributes = spanEvent.IntrinsicAttributes();

            Assert.Multiple(() =>
            {
                // ASSERT
                Assert.That((string)spanEventIntrinsicAttributes["type"], Is.EqualTo("Span"));
                Assert.That((string)spanEventIntrinsicAttributes["traceId"], Is.EqualTo(DistributedTraceTraceId));
                Assert.That((string)spanEventIntrinsicAttributes["guid"], Is.EqualTo(_childGenericSegment.SpanId));
                Assert.That((string)spanEventIntrinsicAttributes["parentId"], Is.EqualTo(_baseGenericSegment.SpanId));
                Assert.That((string)spanEventIntrinsicAttributes["transactionId"], Is.EqualTo(_transactionGuid));
                Assert.That((bool)spanEventIntrinsicAttributes["sampled"], Is.EqualTo(true));
                Assert.That((double)spanEventIntrinsicAttributes["priority"], Is.EqualTo(Priority));
                Assert.That((long)spanEventIntrinsicAttributes["timestamp"], Is.EqualTo(1531897200001));
                Assert.That((double)spanEventIntrinsicAttributes["duration"], Is.EqualTo(0.005));
                Assert.That((string)spanEventIntrinsicAttributes["name"], Is.EqualTo(SegmentName));
                Assert.That((string)spanEventIntrinsicAttributes["category"], Is.EqualTo(GenericCategory));
            });
            Assert.That(spanEventIntrinsicAttributes.ContainsKey("nr.entryPoint"), Is.False);
        }

        [Test]
        public void GetSpanEvent_ReturnsSpanEventPerSegment_ParentIdIsDistributedTraceGuid_FirstSegmentWithPayload()
        {
            // ARRANGE
            var segments = new List<Segment>()
            {
                _baseGenericSegment.CreateSimilar(TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(5), new List<KeyValuePair<string, object>>())
            };
            var immutableTransaction = BuildTestTransaction(segments, true, true);
            var transactionMetricName = _transactionMetricNameMaker.GetTransactionMetricName(immutableTransaction.TransactionName);
            var metricStatsCollection = new TransactionMetricStatsCollection(transactionMetricName);
            var transactionAttribs = _transactionAttribMaker.GetAttributes(immutableTransaction, transactionMetricName, TimeSpan.FromSeconds(1), immutableTransaction.Duration, metricStatsCollection);

            // ACT
            var spanEvents = _spanEventMaker.GetSpanEvents(immutableTransaction, TransactionName, transactionAttribs).ToList();
            var spanEvent = spanEvents[1];
            var rootSpanEvent = spanEvents[0];

            // ASSERT
            Assert.That((string)spanEvent.IntrinsicAttributes()["parentId"], Is.EqualTo((string)rootSpanEvent.IntrinsicAttributes()["guid"]));
        }

        [Test]
        public void GetSpanEvent_ReturnsSpanEventPerSegment_W3CAttributes()
        {
            // ARRANGE
            var segments = new List<Segment>()
            {
                _baseGenericSegment.CreateSimilar(TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(5), new List<KeyValuePair<string, object>>())
            };

            var immutableTransaction = new ImmutableTransactionBuilder()
                .WithW3CTracing(DistributedTraceGuid, W3cParentId, VendorStateEntries)
                .Build();

            var transactionMetricName = _transactionMetricNameMaker.GetTransactionMetricName(immutableTransaction.TransactionName);
            var metricStatsCollection = new TransactionMetricStatsCollection(transactionMetricName);
            var transactionAttribs = _transactionAttribMaker.GetAttributes(immutableTransaction, transactionMetricName, TimeSpan.FromSeconds(1), immutableTransaction.Duration, metricStatsCollection);


            // ACT
            var spanEvents = _spanEventMaker.GetSpanEvents(immutableTransaction, TransactionName, transactionAttribs);
            var spanEvent = spanEvents.ToList()[1];
            var rootSpanEvent = spanEvents.ToList()[0];

            Assert.Multiple(() =>
            {
                // ASSERT
                Assert.That((string)rootSpanEvent.IntrinsicAttributes()["parentId"], Is.EqualTo(W3cParentId));
                Assert.That((string)rootSpanEvent.IntrinsicAttributes()["trustedParentId"], Is.EqualTo(DistributedTraceGuid));
                Assert.That((string)rootSpanEvent.IntrinsicAttributes()["tracingVendors"], Is.EqualTo($"{Vendor1},{Vendor2}"));
            });
        }

        [Test]
        public void GetSpanEvent_ReturnsSpanEventPerSegment_IsRootSegment()
        {
            // ARRANGE
            var segments = new List<Segment>()
            {
                _baseGenericSegment.CreateSimilar(TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(5), new List<KeyValuePair<string, object>>())
            };
            var immutableTransaction = BuildTestTransaction(segments, true, false);

            var transactionMetricName = _transactionMetricNameMaker.GetTransactionMetricName(immutableTransaction.TransactionName);
            var metricStatsCollection = new TransactionMetricStatsCollection(transactionMetricName);
            var transactionAttribs = _transactionAttribMaker.GetAttributes(immutableTransaction, transactionMetricName, TimeSpan.FromSeconds(1), immutableTransaction.Duration, metricStatsCollection);

            // ACT
            var spanEvents = _spanEventMaker.GetSpanEvents(immutableTransaction, TransactionName, transactionAttribs);
            var spanEvent = spanEvents.ToList()[0];

            Assert.Multiple(() =>
            {
                // ASSERT
                Assert.That((bool)spanEvent.IntrinsicAttributes()["nr.entryPoint"], Is.True);
                Assert.That((string)spanEvent.IntrinsicAttributes()["name"], Is.EqualTo(TransactionName));
            });
        }

        [Test]
        public void GetSpanEvent_IncludesErrorAttributes_WhenThereIsAnError()
        {
            // ARRANGE
            var testError = new ErrorData("error message", "ErrorType", "stack trace", DateTime.UtcNow, null, false, null);
            var segments = new List<Segment>()
            {
                _baseGenericSegment.CreateSimilar(TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(5), new List<KeyValuePair<string, object>>())
            };
            segments[0].ErrorData = testError;
            var immutableTransaction = BuildTestTransaction(segments, sampled: true, hasIncomingPayload: true);
            var transactionMetricName = _transactionMetricNameMaker.GetTransactionMetricName(immutableTransaction.TransactionName);
            var metricStatsCollection = new TransactionMetricStatsCollection(transactionMetricName);
            var transactionAttributes = _transactionAttribMaker.GetAttributes(immutableTransaction, transactionMetricName, TimeSpan.FromSeconds(1), immutableTransaction.Duration, metricStatsCollection);

            // ACT
            var spanEvents = _spanEventMaker.GetSpanEvents(immutableTransaction, TransactionName, transactionAttributes).ToList();
            var spanEvent = spanEvents[1];
            var rootSpanEvent = spanEvents[0];
            var errorEventAttributes = new AttributeValueCollection(transactionAttributes, AttributeDestinations.ErrorEvent);

            // ASSERT
            NrAssert.Multiple(
                () => Assert.That(errorEventAttributes.GetAttributeValuesDic(AttributeClassification.Intrinsics)["spanId"], Is.EqualTo(segments[0].SpanId)),
                () => Assert.That((string)spanEvent.AgentAttributes()["error.class"], Is.EqualTo(testError.ErrorTypeName)),
                () => Assert.That((string)rootSpanEvent.AgentAttributes()["error.class"], Is.EqualTo(testError.ErrorTypeName))
            );
        }

        [Test]
        public void GetSpanEvent_DoesNotIncludesErrorAttributes_WhenThereIsAnError_IfErrorCollectionIsDisabled()
        {
            // ARRANGE
            _localConfig.errorCollector.enabled = false;
            PublishConfig();
            var testError = new ErrorData("error message", "ErrorType", "stack trace", DateTime.UtcNow, null, false, null);
            var segments = new List<Segment>()
            {
                _baseGenericSegment.CreateSimilar(TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(5), new List<KeyValuePair<string, object>>())
            };
            segments[0].ErrorData = testError;
            var immutableTransaction = BuildTestTransaction(segments, sampled: true, hasIncomingPayload: true);
            var transactionMetricName = _transactionMetricNameMaker.GetTransactionMetricName(immutableTransaction.TransactionName);
            var metricStatsCollection = new TransactionMetricStatsCollection(transactionMetricName);
            var transactionAttributes = _transactionAttribMaker.GetAttributes(immutableTransaction, transactionMetricName, TimeSpan.FromSeconds(1), immutableTransaction.Duration, metricStatsCollection);

            // ACT
            var spanEvents = _spanEventMaker.GetSpanEvents(immutableTransaction, TransactionName, transactionAttributes).ToList();
            var spanEvent = spanEvents[1];
            var rootSpanEvent = spanEvents[0];
            var errorEventAttributes = new AttributeValueCollection(transactionAttributes, AttributeDestinations.ErrorEvent);

            // ASSERT
            NrAssert.Multiple(
                () => Assert.That(errorEventAttributes.GetAttributeValuesDic(AttributeClassification.Intrinsics).Keys, Has.No.Member("spanId")),
                () => Assert.That(spanEvent.AgentAttributes().Keys, Has.No.Member("error.class")),
                () => Assert.That(rootSpanEvent.AgentAttributes().Keys, Has.No.Member("error.class"))
            );
        }

        [TestCase(true)]
        [TestCase(false)]
        public void GetSpanEvent_Generates_ExpecedErrorAttribute(bool hasExpectedError)
        {
            // ARRANGE
            var testError = new ErrorData("error message", "ErrorType", "stack trace", DateTime.UtcNow, null, hasExpectedError, null);
            var segments = new List<Segment>()
            {
                _baseGenericSegment.CreateSimilar(TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(5), new List<KeyValuePair<string, object>>())
            };
            segments[0].ErrorData = testError;
            var immutableTransaction = BuildTestTransaction(segments, sampled: true, hasIncomingPayload: true);
            var transactionMetricName = _transactionMetricNameMaker.GetTransactionMetricName(immutableTransaction.TransactionName);
            var metricStatsCollection = new TransactionMetricStatsCollection(transactionMetricName);
            var transactionAttributes = _transactionAttribMaker.GetAttributes(immutableTransaction, transactionMetricName, TimeSpan.FromSeconds(1), immutableTransaction.Duration, metricStatsCollection);

            // ACT
            var spanEvents = _spanEventMaker.GetSpanEvents(immutableTransaction, TransactionName, transactionAttributes).ToList();
            var rootSpanEvent = spanEvents[0];
            var spanEvent = spanEvents[1];

            // ASSERT
            if (hasExpectedError)
            {
                Assert.Multiple(() =>
                {
                    Assert.That(rootSpanEvent.AgentAttributes().Keys, Has.Member("error.expected"));
                    Assert.That(rootSpanEvent.AgentAttributes()["error.expected"], Is.EqualTo(true));
                    Assert.That(spanEvent.AgentAttributes().Keys, Has.Member("error.expected"));
                    Assert.That(spanEvent.AgentAttributes()["error.expected"], Is.EqualTo(true));
                });
            }
            else
            {
                Assert.Multiple(() =>
                {
                    Assert.That(rootSpanEvent.AgentAttributes().Keys, Has.No.Member("error.expected"));
                    Assert.That(spanEvent.AgentAttributes().Keys, Has.No.Member("error.expected"));
                });
            }
        }

        #endregion

        #region MessageBroker

        [Test]
        public void GetSpanEvent_ReturnsSpanEventPerSegment_ValidateMessageBrokerValues()
        {
            // ARRANGE
            var segments = new List<Segment>()
            {
                _baseMessageBrokerConsumeSegment.CreateSimilar(TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(5), new List<KeyValuePair<string, object>>()),
                _baseMessageBrokerProduceSegment.CreateSimilar(TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(5), new List<KeyValuePair<string, object>>()),
            };

            var immutableTransaction = BuildTestTransaction(segments, true, false);

            var transactionMetricName = _transactionMetricNameMaker.GetTransactionMetricName(immutableTransaction.TransactionName);
            var metricStatsCollection = new TransactionMetricStatsCollection(transactionMetricName);
            var transactionAttribs = _transactionAttribMaker.GetAttributes(immutableTransaction, transactionMetricName, TimeSpan.FromSeconds(1), immutableTransaction.Duration, metricStatsCollection);

            // ACT
            var spanEvents = _spanEventMaker.GetSpanEvents(immutableTransaction, TransactionName, transactionAttribs);


            var consumeSpanEvent = spanEvents.ToList()[1];
            var consumeSpanEventIntrinsicAttributes = consumeSpanEvent.IntrinsicAttributes();
            var consumeSpanEventAgentAttributes = consumeSpanEvent.AgentAttributes();

            var produceSpanEvent = spanEvents.ToList()[2];
            var produceSpanEventIntrinsicAttributes = produceSpanEvent.IntrinsicAttributes();
            var produceSpanEventAgentAttributes = produceSpanEvent.AgentAttributes();

            // ASSERT
            NrAssert.Multiple
            (
                // Consume
                () => Assert.That((string)consumeSpanEventIntrinsicAttributes["span.kind"], Is.EqualTo("consumer")),
                () => Assert.That((string)consumeSpanEventAgentAttributes["server.address"], Is.EqualTo(ServerAddress)),
                () => Assert.That(consumeSpanEventAgentAttributes["server.port"], Is.EqualTo(ServerPort)),
                () => Assert.That((string)consumeSpanEventAgentAttributes["messaging.destination.name"], Is.EqualTo(MessageBrokerQueue)),
                () => Assert.That((string)consumeSpanEventAgentAttributes["message.queueName"], Is.EqualTo(MessageBrokerQueue)),
                () => Assert.That((string)consumeSpanEventAgentAttributes["messaging.destination_publish.name"], Is.EqualTo(MessageBrokerQueue)),

                // Produce
                () => Assert.That((string)produceSpanEventIntrinsicAttributes["span.kind"], Is.EqualTo("producer")),
                () => Assert.That((string)produceSpanEventAgentAttributes["server.address"], Is.EqualTo(ServerAddress)),
                () => Assert.That(produceSpanEventAgentAttributes["server.port"], Is.EqualTo(ServerPort)),
                () => Assert.That((string)produceSpanEventAgentAttributes["messaging.destination.name"], Is.EqualTo(MessageBrokerQueue)),
                () => Assert.That((string)produceSpanEventAgentAttributes["message.routingKey"], Is.EqualTo(MessageBrokerRoutingKey)),
                () => Assert.That((string)produceSpanEventAgentAttributes["messaging.rabbitmq.destination.routing_key"], Is.EqualTo(MessageBrokerRoutingKey))
            );
        }

        [Test]
        public void Do_Not_Generate_MessageBroker_Attributes_When_Data_IsNull()
        {
            var consumeSegment = new Segment(CreateTransactionSegmentState(3, null, 777), new MethodCallData(MethodCallType, MethodCallMethod, 1));
            consumeSegment.SetSegmentData(
                new MessageBrokerSegmentData(
                    vendor: MessageBrokerVendor,
                    destination: MessageBrokerQueue,
                    destinationType: MetricNames.MessageBrokerDestinationType.Queue,
                    action: MetricNames.MessageBrokerAction.Consume,
                    messagingSystemName: null,
                    cloudAccountId: null,
                    cloudRegion: null,
                    serverAddress: null,
                    serverPort: null,
                    routingKey: null));

            var produceSegment = new Segment(CreateTransactionSegmentState(3, null, 777), new MethodCallData(MethodCallType, MethodCallMethod, 1));
            produceSegment.SetSegmentData(
                new MessageBrokerSegmentData(
                    vendor: MessageBrokerVendor,
                    destination: MessageBrokerQueue,
                    destinationType: MetricNames.MessageBrokerDestinationType.Queue,
                    action: MetricNames.MessageBrokerAction.Produce,
                    messagingSystemName: null,
                    cloudAccountId: null,
                    cloudRegion: null,
                    serverAddress: null,
                    serverPort: null,
                    routingKey: null));

            // ARRANGE
            var segments = new List<Segment>()
            {
                consumeSegment,
                produceSegment
            };

            var immutableTransaction = BuildTestTransaction(segments, true, false);

            var transactionMetricName = _transactionMetricNameMaker.GetTransactionMetricName(immutableTransaction.TransactionName);
            var metricStatsCollection = new TransactionMetricStatsCollection(transactionMetricName);
            var transactionAttribs = _transactionAttribMaker.GetAttributes(immutableTransaction, transactionMetricName, TimeSpan.FromSeconds(1), immutableTransaction.Duration, metricStatsCollection);

            // ACT
            var spanEvents = _spanEventMaker.GetSpanEvents(immutableTransaction, TransactionName, transactionAttribs);
            var consumeSpanEvent = spanEvents.ToList()[1];
            var consumeSpanEventIntrinsicAttributes = consumeSpanEvent.IntrinsicAttributes();
            var consumeSpanEventAgentAttributes = consumeSpanEvent.AgentAttributes();

            var produceSpanEvent = spanEvents.ToList()[2];
            var produceSpanEventIntrinsicAttributes = produceSpanEvent.IntrinsicAttributes();
            var produceSpanEventAgentAttributes = produceSpanEvent.AgentAttributes();

            // ASSERT

            NrAssert.Multiple
            (
                // consume
                () => Assert.That((string)consumeSpanEventIntrinsicAttributes["span.kind"], Is.EqualTo("consumer")),
                () => Assert.That(!consumeSpanEventAgentAttributes.ContainsKey("server.address"), Is.True),
                () => Assert.That(!consumeSpanEventAgentAttributes.ContainsKey("server.port"), Is.True),

                // produce
                () => Assert.That((string)produceSpanEventIntrinsicAttributes["span.kind"], Is.EqualTo("producer")),
                () => Assert.That(!produceSpanEventAgentAttributes.ContainsKey("server.address"), Is.True),
                () => Assert.That(!produceSpanEventAgentAttributes.ContainsKey("server.port"), Is.True),
                () => Assert.That(!produceSpanEventAgentAttributes.ContainsKey("message.routingKey"), Is.True),
                () => Assert.That(!produceSpanEventAgentAttributes.ContainsKey("messaging.rabbitmq.destination.routing_key"), Is.True)
            );
        }

        #endregion

        #region Datastore

        [Test]
        public void GetSpanEvent_ReturnsSpanEventPerSegment_ValidateDatastoreValues()
        {
            // ARRANGE
            var segments = new List<Segment>()
            {
                _baseDatastoreSegment.CreateSimilar(TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(5), new List<KeyValuePair<string, object>>()),
            };

            var immutableTransaction = BuildTestTransaction(segments, true, false);

            var transactionMetricName = _transactionMetricNameMaker.GetTransactionMetricName(immutableTransaction.TransactionName);
            var metricStatsCollection = new TransactionMetricStatsCollection(transactionMetricName);
            var transactionAttribs = _transactionAttribMaker.GetAttributes(immutableTransaction, transactionMetricName, TimeSpan.FromSeconds(1), immutableTransaction.Duration, metricStatsCollection);

            // ACT
            var spanEvents = _spanEventMaker.GetSpanEvents(immutableTransaction, TransactionName, transactionAttribs);
            var spanEvent = spanEvents.ToList()[1];
            var spanEventIntrinsicAttributes = spanEvent.IntrinsicAttributes();
            var spanEventAgentAttributes = spanEvent.AgentAttributes();

            // ASSERT
            NrAssert.Multiple
            (
                () => Assert.That((string)spanEventIntrinsicAttributes["category"], Is.EqualTo(DatastoreCategory)),
                () => Assert.That((string)spanEventIntrinsicAttributes["component"], Is.EqualTo(DatastoreVendor.MSSQL.ToString())),
                () => Assert.That((string)spanEventAgentAttributes["db.system"], Is.EqualTo(DatastoreVendor.MSSQL.ToKnownName())),
                () => Assert.That((string)spanEventAgentAttributes["db.operation"], Is.EqualTo(_parsedSqlStatement.Operation)),
                () => Assert.That((string)spanEventAgentAttributes["server.address"], Is.EqualTo(_connectionInfo.Host)),
                () => Assert.That(spanEventAgentAttributes["server.port"], Is.EqualTo(_connectionInfo.Port.Value)),

                //This also tests the lazy instantiation on span event attrib values
                () => Assert.That((string)spanEventAgentAttributes["db.statement"], Is.EqualTo(_obfuscatedSql)),

                () => Assert.That((string)spanEventAgentAttributes["db.instance"], Is.EqualTo(_connectionInfo.DatabaseName)),
                () => Assert.That((string)spanEventAgentAttributes["peer.address"], Is.EqualTo($"{_connectionInfo.Host}:{_connectionInfo.PortPathOrId}")),
                () => Assert.That((string)spanEventAgentAttributes["peer.hostname"], Is.EqualTo(_connectionInfo.Host)),
                () => Assert.That((string)spanEventIntrinsicAttributes["span.kind"], Is.EqualTo("client"))
            );
        }

        [Test]
        public void Do_Not_Generate_DbCollection_Attribute_When_Model_IsNullOrEmpty()
        {
            var testSegment = new Segment(CreateTransactionSegmentState(3, null, 777), new MethodCallData(MethodCallType, MethodCallMethod, 1));
            testSegment.SetSegmentData(new DatastoreSegmentData(_databaseService,
                parsedSqlStatement: new ParsedSqlStatement(DatastoreVendor.CosmosDB, string.Empty, "ReadDatabase"),
                connectionInfo: new ConnectionInfo("none", "localhost", "1234", "default", "maininstance")));

            // ARRANGE
            var segments = new List<Segment>()
            {
                testSegment
            };
            

            var immutableTransaction = BuildTestTransaction(segments, true, false);

            var transactionMetricName = _transactionMetricNameMaker.GetTransactionMetricName(immutableTransaction.TransactionName);
            var metricStatsCollection = new TransactionMetricStatsCollection(transactionMetricName);
            var transactionAttribs = _transactionAttribMaker.GetAttributes(immutableTransaction, transactionMetricName, TimeSpan.FromSeconds(1), immutableTransaction.Duration, metricStatsCollection);

            // ACT
            var spanEvents = _spanEventMaker.GetSpanEvents(immutableTransaction, TransactionName, transactionAttribs);
            var spanEvent = spanEvents.ToList()[1];
            var spanEventIntrinsicAttributes = spanEvent.IntrinsicAttributes();
            var spanEventAgentAttributes = spanEvent.AgentAttributes();

            // ASSERT

            NrAssert.Multiple
            (
                () => Assert.That(!spanEventAgentAttributes.ContainsKey("db.collection"), Is.True),
                () => Assert.That((string)spanEventAgentAttributes["db.instance"], Is.EqualTo("default")),
                () => Assert.That((string)spanEventAgentAttributes["peer.address"], Is.EqualTo("localhost:1234")),
                () => Assert.That((string)spanEventAgentAttributes["peer.hostname"], Is.EqualTo("localhost"))
            );
        }

        private void GetSpanEvent_ReturnsSpanEventPerSegment_DatastoreTruncateLongStatement()
        {

            var customerStmt = new string[]
            {
                new string('U', 2015),				//1-byte per char
				new string('仮', 1015)				//3-bytes per char
			};

            var expectedStmtTrunc = new string[]
            {
                new string('U', 1996) + "...",		//1-byte per char
				new string('仮', 666) + "..."		//3-bytes per char
			};

            for (int i = 0; i < customerStmt.Length; i++)
            {
                // ARRANGE
                var longSqlStatement = new ParsedSqlStatement(DatastoreVendor.MSSQL, customerStmt[i], "select");
                var longDatastoreSegment = new Segment(CreateTransactionSegmentState(3, null, 777), new MethodCallData(MethodCallType, MethodCallMethod, 1));
                longDatastoreSegment.SetSegmentData(new DatastoreSegmentData(_databaseService, longSqlStatement, customerStmt[i], _connectionInfo));

                var segments = new List<Segment>()
                                {
                                    longDatastoreSegment.CreateSimilar(TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(5), new List<KeyValuePair<string, object>>()),
                                };
                var immutableTransaction = BuildTestTransaction(segments, true, false);
                var transactionMetricName = _transactionMetricNameMaker.GetTransactionMetricName(immutableTransaction.TransactionName);
                var metricStatsCollection = new TransactionMetricStatsCollection(transactionMetricName);
                var transactionAttribs = _transactionAttribMaker.GetAttributes(immutableTransaction, transactionMetricName, TimeSpan.FromSeconds(1), immutableTransaction.Duration, metricStatsCollection);

                // ACT
                var spanEvents = _spanEventMaker.GetSpanEvents(immutableTransaction, TransactionName, transactionAttribs);
                var spanEvent = spanEvents.ToList()[1];

                // ASSERT
                var attribStatement = (string)spanEvent.AgentAttributes()["db.statement"];
                var attribStmtLenBytes = Encoding.UTF8.GetByteCount(attribStatement);

                Assert.Multiple(() =>
                {
                    Assert.That(attribStatement, Is.EqualTo(expectedStmtTrunc[i]));
                    Assert.That(attribStmtLenBytes, Is.LessThanOrEqualTo(1999));
                    Assert.That(attribStmtLenBytes, Is.GreaterThanOrEqualTo(1996));
                });
            }
        }

        #endregion

        #region Root Span

        [Test]
        public void RootSpanAttribFiltering_SpanFiltersIndependentOfTransactionFilters()
        {
            _localConfig.transactionEvents.attributes.exclude = new List<string> { "filterOnTrx*" };
            _localConfig.spanEvents.attributes.exclude = new List<string> { "filterOnSpan*" };

            PublishConfig();

            var segments = new List<Segment>()
            {
                _baseDatastoreSegment.CreateSimilar(TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(5), new List<KeyValuePair<string, object>>()),
            };

            var immutableTransaction = BuildTestTransaction(segments, true, false);

            var transactionMetricName = _transactionMetricNameMaker.GetTransactionMetricName(immutableTransaction.TransactionName);
            var metricStatsCollection = new TransactionMetricStatsCollection(transactionMetricName);
            var transactionAttribs = _transactionAttribMaker.GetAttributes(immutableTransaction, transactionMetricName, TimeSpan.FromSeconds(1), immutableTransaction.Duration, metricStatsCollection);

            _attribDefs.GetCustomAttributeForTransaction("filterOnTrx").TrySetValue(transactionAttribs, "trxCustomAttribValue1");
            _attribDefs.GetCustomAttributeForTransaction("filterOnSpan").TrySetValue(transactionAttribs, "trxCustomAttribValue2");

            var trxEvent = _transactionEventMaker.GetTransactionEvent(immutableTransaction, transactionAttribs);
            var spanEvents = _spanEventMaker.GetSpanEvents(immutableTransaction, TransactionName, transactionAttribs);

            var rootSpan = spanEvents.ToList()[0];

            var rootSpanUserAttribDic = rootSpan.GetAttributeValuesDic(AttributeClassification.UserAttributes);
            var trxUserAttribDic = trxEvent.AttributeValues.GetAttributeValuesDic(AttributeClassification.UserAttributes);

            NrAssert.Multiple
            (
                () => Assert.That(trxUserAttribDic.ContainsKey("filterOnTrx"), Is.False),
                () => Assert.That(trxUserAttribDic.ContainsKey("filterOnSpan"), Is.True),

                () => Assert.That(rootSpanUserAttribDic.ContainsKey("filterOnTrx"), Is.True),
                () => Assert.That(rootSpanUserAttribDic.ContainsKey("filterOnSpan"), Is.False)
            );




        }

        [Test]
        public void RootSpanEventHasTransactionNameSpecialSpanAttrib()
        {
            var segments = new List<Segment>()
            {
                _baseDatastoreSegment.CreateSimilar(TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(5), new List<KeyValuePair<string, object>>()),
            };

            var immutableTransaction = BuildTestTransaction(segments, true, false);

            var transactionMetricName = _transactionMetricNameMaker.GetTransactionMetricName(immutableTransaction.TransactionName);
            var metricStatsCollection = new TransactionMetricStatsCollection(transactionMetricName);
            var transactionAttribs = _transactionAttribMaker.GetAttributes(immutableTransaction, transactionMetricName, TimeSpan.FromSeconds(1), immutableTransaction.Duration, metricStatsCollection);

            // ACT
            var spanEvents = _spanEventMaker.GetSpanEvents(immutableTransaction, TransactionName, transactionAttribs);
            var rootSpan = spanEvents.ToList()[0];

            var rootSpanIntrinsicsDic = rootSpan.GetAttributeValuesDic(AttributeClassification.Intrinsics);

            NrAssert.Multiple
            (
                ()=> Assert.That(rootSpanIntrinsicsDic.ContainsKey("transaction.name"), Is.True),
                ()=> Assert.That(rootSpanIntrinsicsDic["transaction.name"], Is.EqualTo(transactionMetricName.PrefixedName))
            );
        }
         
        [Test]
        public void RootSpanEventHasTransactionsUserAndAgentAttributes([Values(true,false)] bool transactionEventsEnabled)
        {
            _localConfig.transactionEvents.enabled = false;

            PublishConfig();


            var segments = new List<Segment>()
            {
                _baseDatastoreSegment.CreateSimilar(TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(5), new List<KeyValuePair<string, object>>()),
            };

            var immutableTransaction = BuildTestTransaction(segments, true, false);

            var transactionMetricName = _transactionMetricNameMaker.GetTransactionMetricName(immutableTransaction.TransactionName);
            var metricStatsCollection = new TransactionMetricStatsCollection(transactionMetricName);
            var allAttribValues = _transactionAttribMaker.GetAttributes(immutableTransaction, transactionMetricName, TimeSpan.FromSeconds(1), immutableTransaction.Duration, metricStatsCollection);

            _attribDefs.GetCustomAttributeForTransaction("trxCustomAttrib").TrySetValue(allAttribValues, "trxCustomAttribValue");
            _attribDefs.GetLambdaAttribute("lambdaAttributeKey").TrySetValue(allAttribValues, "lambdaAttributeValue");
            _attribDefs.GetFaasAttribute("faasAttributeKey").TrySetValue(allAttribValues, "faasAttributeValue");
            _attribDefs.OriginalUrl.TrySetValue(allAttribValues, "http://www.test.com");

            // ACT
            var spanEvents = _spanEventMaker.GetSpanEvents(immutableTransaction, TransactionName, allAttribValues);
            var rootSpan = spanEvents.ToList()[0];

            var assertions = new List<Action>();
            foreach (var classification in new[] { AttributeClassification.AgentAttributes, AttributeClassification.UserAttributes })
            {
                var classificationLocal = classification;
                var trxAttribs = allAttribValues.GetAttributeValues(classification);
                var rootSpanAttribsDic = rootSpan.GetAttributeValuesDic(classification);

                var hasExistCheck = false;

                foreach (var trxAttrib in trxAttribs)
                {
                    var attribName = trxAttrib.AttributeDefinition.Name;

                    if (trxAttrib.AttributeDefinition.IsAvailableForAny(AttributeDestinations.SpanEvent))
                    {
                        hasExistCheck = true;
                        assertions.Add(() => Assert.That(rootSpanAttribsDic.ContainsKey(attribName), Is.True, $"{classificationLocal} attributes should have attribute {attribName}"));
                        assertions.Add(() => Assert.That(rootSpanAttribsDic[attribName], Is.EqualTo(trxAttrib.Value), $"{classificationLocal} attribute '{attribName}'"));
                    }
                    else
                    {
                        assertions.Add(() => Assert.That(rootSpanAttribsDic.ContainsKey(attribName), Is.False, $"{classificationLocal} attributes should have attribute {attribName}"));
                    }
                }

                assertions.Add(() => Assert.That(hasExistCheck, Is.True, $"Didn't validate existence of any {classificationLocal} attrib on root span"));
            }

            NrAssert.Multiple(assertions.ToArray());
        }

        #endregion

        #region Http (Externals)

        [Test]
        public void GetSpanEvent_ReturnsSpanEventPerSegment_HttpCategory()
        {
            // ARRANGE
            var segments = new List<Segment>()
            {
                _baseHttpSegment.CreateSimilar(TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(5), new List<KeyValuePair<string, object>>())
            };
            var immutableTransaction = BuildTestTransaction(segments, true, false);
            var transactionMetricName = _transactionMetricNameMaker.GetTransactionMetricName(immutableTransaction.TransactionName);
            var metricStatsCollection = new TransactionMetricStatsCollection(transactionMetricName);
            var transactionAttribs = _transactionAttribMaker.GetAttributes(immutableTransaction, transactionMetricName, TimeSpan.FromSeconds(1), immutableTransaction.Duration, metricStatsCollection);

            // ACT
            var spanEvents = _spanEventMaker.GetSpanEvents(immutableTransaction, TransactionName, transactionAttribs);
            var spanEvent = spanEvents.ToList()[1];

            // ASSERT
            Assert.That((string)spanEvent.IntrinsicAttributes()["category"], Is.EqualTo(HttpCategory));
        }

        [Test]
        public void GetSpanEvent_ReturnsSpanEventPerSegment_ValidateHttpValues()
        {
            // ARRANGE
            var segments = new List<Segment>()
            {
                _baseHttpSegment.CreateSimilar(TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(5), new List<KeyValuePair<string, object>>()),
            };
            var immutableTransaction = BuildTestTransaction(segments, true, false);
            var transactionMetricName = _transactionMetricNameMaker.GetTransactionMetricName(immutableTransaction.TransactionName);
            var metricStatsCollection = new TransactionMetricStatsCollection(transactionMetricName);
            var transactionAttribs = _transactionAttribMaker.GetAttributes(immutableTransaction, transactionMetricName, TimeSpan.FromSeconds(1), immutableTransaction.Duration, metricStatsCollection);

            // ACT
            var spanEvents = _spanEventMaker.GetSpanEvents(immutableTransaction, TransactionName, transactionAttribs);
            var spanEvent = spanEvents.ToList()[1];
            var spanEventIntrinsicAttributes = spanEvent.IntrinsicAttributes();
            var spanEventAgentAttributes = spanEvent.AgentAttributes();

            Assert.Multiple(() =>
            {
                // ASSERT
                Assert.That((string)spanEventAgentAttributes["http.url"], Is.EqualTo(HttpUri));
                Assert.That((string)spanEventAgentAttributes["http.request.method"], Is.EqualTo(HttpMethod));
                Assert.That((string)spanEventIntrinsicAttributes["component"], Is.EqualTo("type"));
                Assert.That((string)spanEventIntrinsicAttributes["span.kind"], Is.EqualTo("client"));
            });
        }

        [Test]
        public void GetSpanEvent_ReturnsSpanEventPerSegment_NoHttpStatusCode()
        {
            // ARRANGE
            var segments = new List<Segment>()
            {
                _baseHttpSegment.CreateSimilar(TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(5), new List<KeyValuePair<string, object>>())
            };
            var immutableTransaction = BuildTestTransaction(segments, true, false);
            var transactionMetricName = _transactionMetricNameMaker.GetTransactionMetricName(immutableTransaction.TransactionName);
            var metricStatsCollection = new TransactionMetricStatsCollection(transactionMetricName);
            var transactionAttribs = _transactionAttribMaker.GetAttributes(immutableTransaction, transactionMetricName, TimeSpan.FromSeconds(1), immutableTransaction.Duration, metricStatsCollection);

            // ACT
            var spanEvents = _spanEventMaker.GetSpanEvents(immutableTransaction, TransactionName, transactionAttribs);
            var spanEvent = spanEvents.ToList()[1];

            // ASSERT
            Assert.That(spanEvent.AgentAttributes().Keys, Has.No.Member("http.statusCode"));
        }

        [Test]
        public void GetSpanEvent_ReturnsSpanEventPerSegment_HasHttpStatusCode()
        {
            // ARRANGE
            var segments = new List<Segment>()
            {
                _baseHttpSegment.CreateSimilar(TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(5), new List<KeyValuePair<string, object>>())
            };
            var externalSegmentData = segments[0].Data as ExternalSegmentData;
            externalSegmentData.SetHttpStatusCode(200);

            var immutableTransaction = BuildTestTransaction(segments, true, false);
            var transactionMetricName = _transactionMetricNameMaker.GetTransactionMetricName(immutableTransaction.TransactionName);
            var metricStatsCollection = new TransactionMetricStatsCollection(transactionMetricName);
            var transactionAttribs = _transactionAttribMaker.GetAttributes(immutableTransaction, transactionMetricName, TimeSpan.FromSeconds(1), immutableTransaction.Duration, metricStatsCollection);

            // ACT
            var spanEvents = _spanEventMaker.GetSpanEvents(immutableTransaction, TransactionName, transactionAttribs);
            var spanEvent = spanEvents.ToList()[1];

            // ASSERT
            Assert.That(spanEvent.AgentAttributes()["http.statusCode"], Is.EqualTo(200));
        }

        #endregion

        [Test]
        public void GetSpanEvent_CheckThreadIdAttribute()
        {
            var segments = new List<Segment>()
            {
                _baseGenericSegment.CreateSimilar(TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(5), new List<KeyValuePair<string, object>>())
            };
            var immutableTransaction = BuildTestTransaction(segments, true, false);
            var transactionMetricName = _transactionMetricNameMaker.GetTransactionMetricName(immutableTransaction.TransactionName);
            var metricStatsCollection = new TransactionMetricStatsCollection(transactionMetricName);
            var transactionAttribs = _transactionAttribMaker.GetAttributes(immutableTransaction, transactionMetricName, TimeSpan.FromSeconds(1), immutableTransaction.Duration, metricStatsCollection);

            var spanEvents = _spanEventMaker.GetSpanEvents(immutableTransaction, TransactionName, transactionAttribs);
            var spanEvent = spanEvents.ToList()[1];

            Assert.That(spanEvent.IntrinsicAttributes()["thread.id"], Is.EqualTo(777));
        }

        [Test]
        public void GetSpanEvent_CheckMissingThreadIdAttribute()
        {
            var segments = new List<Segment>()
            {
                _baseGenericAsyncSegment.CreateSimilar(TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(5), new List<KeyValuePair<string, object>>())
            };
            var immutableTransaction = BuildTestTransaction(segments, true, false);
            var transactionMetricName = _transactionMetricNameMaker.GetTransactionMetricName(immutableTransaction.TransactionName);
            var metricStatsCollection = new TransactionMetricStatsCollection(transactionMetricName);
            var transactionAttribs = _transactionAttribMaker.GetAttributes(immutableTransaction, transactionMetricName, TimeSpan.FromSeconds(1), immutableTransaction.Duration, metricStatsCollection);

            var spanEvents = _spanEventMaker.GetSpanEvents(immutableTransaction, TransactionName, transactionAttribs);
            var spanEvent = spanEvents.ToList()[1];

            Assert.That(spanEvent.IntrinsicAttributes().ContainsKey("thread.id"), Is.False);
        }

        private ImmutableTransaction BuildTestTransaction(List<Segment> segments, bool sampled, bool hasIncomingPayload)
        {
            var builder = new ImmutableTransactionBuilder()
                .IsWebTransaction("foo", "bar")
                .WithPriority(Priority)
                .WithDistributedTracing(DistributedTraceGuid, DistributedTraceTraceId, sampled, hasIncomingPayload)
                .WithSegments(segments)
                .WithStartTime(_startTime)
                .WithTransactionGuid(_transactionGuid);

            var segmentWithError = segments.FirstOrDefault(s => s.ErrorData != null);
            if (segmentWithError != null)
            {
                builder.WithExceptionFromSegment(segmentWithError);
            }

            return builder.Build();
        }

        private ITransactionSegmentState CreateTransactionSegmentState(int uniqueId, int? parentId, int managedThreadId = 1)
        {
            var segmentState = Mock.Create<ITransactionSegmentState>();
            Mock.Arrange(() => segmentState.AttribDefs).Returns(() => new AttributeDefinitions(new AttributeFilter(new AttributeFilter.Settings())));
            Mock.Arrange(() => segmentState.ParentSegmentId()).Returns(parentId);
            Mock.Arrange(() => segmentState.CallStackPush(Arg.IsAny<Segment>())).Returns(uniqueId);
            Mock.Arrange(() => segmentState.CurrentManagedThreadId).Returns(managedThreadId);
            Mock.Arrange(() => segmentState.ErrorService).Returns(new ErrorService(_configurationService));
            return segmentState;
        }
    }
}

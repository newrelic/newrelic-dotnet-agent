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
using NewRelic.Core;
using NewRelic.Parsing;
using NewRelic.SystemInterfaces;
using NewRelic.SystemInterfaces.Web;
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
            var config = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfiguration, _securityPoliciesConfiguration, _processStatic, _httpRuntimeStatic, _configurationManagerStatic, _dnsStatic);
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
        }

        [TearDown]
        public void TearDown()
        {
            _configAutoResponder?.Dispose();
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
            Assert.AreEqual(segments.Count + 1, spanEvents.Count());
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

            // ASSERT
            Assert.AreEqual("Span", (string)spanEventIntrinsicAttributes["type"]);
            Assert.AreEqual(DistributedTraceTraceId, (string)spanEventIntrinsicAttributes["traceId"]);
            Assert.AreEqual(_childGenericSegment.SpanId, (string)spanEventIntrinsicAttributes["guid"]);
            Assert.AreEqual(_baseGenericSegment.SpanId, (string)spanEventIntrinsicAttributes["parentId"]);
            Assert.AreEqual(_transactionGuid, (string)spanEventIntrinsicAttributes["transactionId"]);
            Assert.AreEqual(true, (bool)spanEventIntrinsicAttributes["sampled"]);
            Assert.AreEqual(Priority, (double)spanEventIntrinsicAttributes["priority"]);
            Assert.AreEqual(1531897200001, (long)spanEventIntrinsicAttributes["timestamp"]);
            Assert.AreEqual(0.005, (double)spanEventIntrinsicAttributes["duration"]);
            Assert.AreEqual(SegmentName, (string)spanEventIntrinsicAttributes["name"]);
            Assert.AreEqual(GenericCategory, (string)spanEventIntrinsicAttributes["category"]);
            Assert.False(spanEventIntrinsicAttributes.ContainsKey("nr.entryPoint"));
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
            Assert.AreEqual((string)rootSpanEvent.IntrinsicAttributes()["guid"], (string)spanEvent.IntrinsicAttributes()["parentId"]);
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

            // ASSERT
            Assert.AreEqual(W3cParentId, (string)rootSpanEvent.IntrinsicAttributes()["parentId"]);
            Assert.AreEqual(DistributedTraceGuid, (string)rootSpanEvent.IntrinsicAttributes()["trustedParentId"]);
            Assert.AreEqual($"{Vendor1},{Vendor2}", (string)rootSpanEvent.IntrinsicAttributes()["tracingVendors"]);
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

            // ASSERT
            Assert.True((bool)spanEvent.IntrinsicAttributes()["nr.entryPoint"]);
            Assert.AreEqual(TransactionName, (string)spanEvent.IntrinsicAttributes()["name"]);
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
                () => Assert.AreEqual(segments[0].SpanId, errorEventAttributes.GetAttributeValuesDic(AttributeClassification.Intrinsics)["spanId"]),
                () => Assert.AreEqual(testError.ErrorTypeName, (string)spanEvent.AgentAttributes()["error.class"]),
                () => Assert.AreEqual(testError.ErrorTypeName, (string)rootSpanEvent.AgentAttributes()["error.class"])
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
                () => CollectionAssert.DoesNotContain(errorEventAttributes.GetAttributeValuesDic(AttributeClassification.Intrinsics).Keys, "spanId"),
                () => CollectionAssert.DoesNotContain(spanEvent.AgentAttributes().Keys, "error.class"),
                () => CollectionAssert.DoesNotContain(rootSpanEvent.AgentAttributes().Keys, "error.class")
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
                CollectionAssert.Contains(rootSpanEvent.AgentAttributes().Keys, "error.expected");
                Assert.AreEqual(true, rootSpanEvent.AgentAttributes()["error.expected"]);
                CollectionAssert.Contains(spanEvent.AgentAttributes().Keys, "error.expected");
                Assert.AreEqual(true, spanEvent.AgentAttributes()["error.expected"]);
            }
            else
            {
                CollectionAssert.DoesNotContain(rootSpanEvent.AgentAttributes().Keys, "error.expected");
                CollectionAssert.DoesNotContain(spanEvent.AgentAttributes().Keys, "error.expected");
            }
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
                () => Assert.AreEqual(DatastoreCategory, (string)spanEventIntrinsicAttributes["category"]),
                () => Assert.AreEqual(DatastoreVendor.MSSQL.ToString(), (string)spanEventIntrinsicAttributes["component"]),
                () => Assert.AreEqual(DatastoreVendor.MSSQL.ToKnownName(), (string)spanEventAgentAttributes["db.system"]),
                () => Assert.AreEqual(_parsedSqlStatement.Operation, (string)spanEventAgentAttributes["db.operation"]),
                () => Assert.AreEqual(_connectionInfo.Host, (string)spanEventAgentAttributes["server.address"]),
                () => Assert.AreEqual(_connectionInfo.Port.Value, spanEventAgentAttributes["server.port"]),

                //This also tests the lazy instantiation on span event attrib values
                () => Assert.AreEqual(_obfuscatedSql, (string)spanEventAgentAttributes["db.statement"]),

                () => Assert.AreEqual(_connectionInfo.DatabaseName, (string)spanEventAgentAttributes["db.instance"]),
                () => Assert.AreEqual($"{_connectionInfo.Host}:{_connectionInfo.PortPathOrId}", (string)spanEventAgentAttributes["peer.address"]),
                () => Assert.AreEqual(_connectionInfo.Host, (string)spanEventAgentAttributes["peer.hostname"]),
                () => Assert.AreEqual("client", (string)spanEventIntrinsicAttributes["span.kind"])
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
                () => Assert.True(!spanEventAgentAttributes.ContainsKey("db.collection")),
                () => Assert.AreEqual("default", (string)spanEventAgentAttributes["db.instance"]),
                () => Assert.AreEqual("localhost:1234", (string)spanEventAgentAttributes["peer.address"]),
                () => Assert.AreEqual("localhost", (string)spanEventAgentAttributes["peer.hostname"])
            );
        }

        public void GetSpanEvent_ReturnsSpanEventPerSegment_DatastoreTruncateLongStatement()
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

                Assert.AreEqual(expectedStmtTrunc[i], attribStatement);
                Assert.True(attribStmtLenBytes <= 1999);
                Assert.True(attribStmtLenBytes >= 1996);
            }
        }

        #endregion

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
                () => Assert.IsFalse(trxUserAttribDic.ContainsKey("filterOnTrx")),
                () => Assert.IsTrue(trxUserAttribDic.ContainsKey("filterOnSpan")),

                () => Assert.IsTrue(rootSpanUserAttribDic.ContainsKey("filterOnTrx")),
                () => Assert.IsFalse(rootSpanUserAttribDic.ContainsKey("filterOnSpan"))
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
                ()=> Assert.IsTrue(rootSpanIntrinsicsDic.ContainsKey("transaction.name")),
                ()=> Assert.AreEqual(transactionMetricName.PrefixedName, rootSpanIntrinsicsDic["transaction.name"])
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
                        assertions.Add(() => Assert.IsTrue(rootSpanAttribsDic.ContainsKey(attribName), $"{classificationLocal} attributes should have attribute {attribName}"));
                        assertions.Add(() => Assert.AreEqual(trxAttrib.Value, rootSpanAttribsDic[attribName], $"{classificationLocal} attribute '{attribName}'"));
                    }
                    else
                    {
                        assertions.Add(() => Assert.IsFalse(rootSpanAttribsDic.ContainsKey(attribName), $"{classificationLocal} attributes should have attribute {attribName}"));
                    }
                }

                assertions.Add(() => Assert.IsTrue(hasExistCheck, $"Didn't validate existence of any {classificationLocal} attrib on root span"));
            }

            NrAssert.Multiple(assertions.ToArray());
        }




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
            Assert.AreEqual(HttpCategory, (string)spanEvent.IntrinsicAttributes()["category"]);
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

            // ASSERT
            Assert.AreEqual(HttpUri, (string)spanEventAgentAttributes["http.url"]);
            Assert.AreEqual(HttpMethod, (string)spanEventAgentAttributes["http.request.method"]);
            Assert.AreEqual("type", (string)spanEventIntrinsicAttributes["component"]);
            Assert.AreEqual("client", (string)spanEventIntrinsicAttributes["span.kind"]);
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
            CollectionAssert.DoesNotContain(spanEvent.AgentAttributes().Keys, "http.statusCode");
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
            Assert.AreEqual(200, spanEvent.AgentAttributes()["http.statusCode"]);
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

            Assert.AreEqual(777, spanEvent.IntrinsicAttributes()["thread.id"]);
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

            Assert.IsFalse(spanEvent.IntrinsicAttributes().ContainsKey("thread.id"));
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

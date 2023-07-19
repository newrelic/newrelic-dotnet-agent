// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Aggregators;
using NewRelic.Agent.Core.Attributes;
using NewRelic.Agent.Core.CallStack;
using NewRelic.Agent.Core.Config;
using NewRelic.Agent.Core.Configuration;
using NewRelic.Agent.Core.Configuration.UnitTest;
using NewRelic.Agent.Core.Database;
using NewRelic.Agent.Core.DistributedTracing;
using NewRelic.Agent.Core.Errors;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Fixtures;
using NewRelic.Agent.Core.Metric;
using NewRelic.Agent.Core.Segments;
using NewRelic.Agent.Core.Timing;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Core.WireModels;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Agent.TestUtilities;
using NewRelic.Core;
using NewRelic.Core.DistributedTracing;
using NewRelic.SystemInterfaces;
using NewRelic.SystemInterfaces.Web;
using NewRelic.Testing.Assertions;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.Transformers.TransactionTransformer.UnitTest
{
    [TestFixture]
    public class TransactionAttributeMakerTests
    {
        private IConfiguration _configuration;
        private IConfigurationService _configurationService;
        private ITransactionAttributeMaker _transactionAttributeMaker;
        private IDatabaseService _databaseService;
        private IErrorService _errorService;
        private IDistributedTracePayloadHandler _distributedTracePayloadHandler;
        private IAttributeDefinitionService _attribDefSvc;
        private IAttributeDefinitions _attribDefs => _attribDefSvc?.AttributeDefs;

        private configuration _localConfig;
        private ServerConfiguration _serverConfig;
        private RunTimeConfiguration _runTimeConfiguration;
        private SecurityPoliciesConfiguration _securityPoliciesConfiguration;

        private IEnvironment _environment;
        private IHttpRuntimeStatic _httpRuntimeStatic;
        private IProcessStatic _processStatic;
        private IConfigurationManagerStatic _configurationManagerStatic;
        private IDnsStatic _dnsStatic;

        private ConfigurationAutoResponder _configAutoResponder;

        private void UpdateConfiguration()
        {
            _configuration = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfiguration, _securityPoliciesConfiguration, _processStatic, _httpRuntimeStatic, _configurationManagerStatic, _dnsStatic);
            Mock.Arrange(() => _configurationService.Configuration).Returns(_configuration);
            EventBus<ConfigurationUpdatedEvent>.Publish(new ConfigurationUpdatedEvent(_configuration, ConfigurationUpdateSource.Local));
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
            _configurationService = Mock.Create<IConfigurationService>();

            _runTimeConfiguration = new RunTimeConfiguration();
            _serverConfig = new ServerConfiguration();
            _localConfig = new configuration();

            _localConfig.attributes.include = new List<string>() { "request.parameters.*", "request.headers.*" };

            _localConfig.allowAllHeaders.enabled = true;

            UpdateConfiguration();

            _configAutoResponder = new ConfigurationAutoResponder(_configuration);

            _databaseService = new DatabaseService();
            _attribDefSvc = new AttributeDefinitionService((f) => new AttributeDefinitions(f));
            _transactionAttributeMaker = new TransactionAttributeMaker(_configurationService, _attribDefSvc);
            _errorService = new ErrorService(_configurationService);
            _distributedTracePayloadHandler = Mock.Create<IDistributedTracePayloadHandler>();
        }

        [TearDown]
        public void TearDown()
        {
            _configAutoResponder?.Dispose();
        }

        #region GetAttributes

        [Test]
        public void GetAttributes_ReturnsAllAttributesCreatedByTransactionAttributeMaker()
        {
            // ARRANGE
            var timer = Mock.Create<ITimer>();
            var expectedStartTime = DateTime.Now;
            var expectedDuration = TimeSpan.FromMilliseconds(500);
            Mock.Arrange(() => timer.Duration).Returns(expectedDuration);

            var priority = 0.5f;
            var internalTransaction = new Transaction(_configuration, TransactionName.ForOtherTransaction("transactionCategory", "transactionName"), timer, expectedStartTime, Mock.Create<ICallStackManager>(), _databaseService, priority, Mock.Create<IDatabaseStatementParser>(), _distributedTracePayloadHandler, _errorService, _attribDefs);
            var immutableTransaction = internalTransaction.ConvertToImmutableTransaction();
            var transactionMetricName = new TransactionMetricName("WebTransaction", "TransactionName");
            var apdexT = null as TimeSpan?;
            var totalTime = TimeSpan.FromSeconds(1);
            var txStats = new TransactionMetricStatsCollection(new TransactionMetricName("WebTransaction", "myTx"));

            // ACT
            var attributes = _transactionAttributeMaker.GetAttributes(immutableTransaction, transactionMetricName, apdexT, totalTime, txStats);

            // ACQUIRE
            var transactionAttributes = attributes.ToDictionary();

            // ASSERT
            NrAssert.Multiple(
                () => Assert.AreEqual(11, transactionAttributes.Count()),  // Assert that only these attributes are generated
                () => Assert.AreEqual("Transaction", transactionAttributes["type"]),
                () => Assert.AreEqual(expectedStartTime.ToUnixTimeMilliseconds(), transactionAttributes["timestamp"]),
                () => Assert.AreEqual("WebTransaction/TransactionName", transactionAttributes["name"]),
                () => Assert.AreEqual("WebTransaction/TransactionName", transactionAttributes["transactionName"]),
                () => Assert.AreEqual(0.5f, transactionAttributes["duration"]),
                () => Assert.AreEqual(1, transactionAttributes["totalTime"]),
                () => Assert.NotNull(transactionAttributes["nr.tripId"]),
                () => Assert.NotNull(transactionAttributes["trip_id"]),
                () => Assert.AreEqual("/Unknown", transactionAttributes["request.uri"]),
                () => Assert.Contains("host.displayName", transactionAttributes.Keys)
            );
        }

        [Test]
        public void GetAttributes_ReturnsOneDatabase()
        {
            // ARRANGE
            var timer = Mock.Create<ITimer>();
            var expectedStartTime = DateTime.Now;
            var expectedDuration = TimeSpan.FromMilliseconds(500);
            Mock.Arrange(() => timer.Duration).Returns(expectedDuration);

            var priority = 0.5f;
            var transaction = new Transaction(_configuration, TransactionName.ForOtherTransaction("transactionCategory", "transactionName"), timer, expectedStartTime, Mock.Create<ICallStackManager>(), _databaseService, priority, Mock.Create<IDatabaseStatementParser>(), _distributedTracePayloadHandler, _errorService, _attribDefs);
            var immutableTransaction = transaction.ConvertToImmutableTransaction();
            var transactionMetricName = new TransactionMetricName("WebTransaction", "TransactionName");
            var apdexT = null as TimeSpan?;
            var totalTime = TimeSpan.FromSeconds(1);
            var txStats = new TransactionMetricStatsCollection(new TransactionMetricName("WebTransaction", "myTx"));
            txStats.MergeUnscopedStats(MetricNames.DatastoreAll, MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5)));
            // ACT
            var attributes = _transactionAttributeMaker.GetAttributes(immutableTransaction, transactionMetricName, apdexT, totalTime, txStats);

            // ACQUIRE
            var transactionAttributes = attributes.ToDictionary();

            // ASSERT
            NrAssert.Multiple(
                () => Assert.AreEqual(13, transactionAttributes.Count()),  // Assert that only these attributes are generated
                () => Assert.AreEqual("Transaction", transactionAttributes["type"]),
                () => Assert.AreEqual(expectedStartTime.ToUnixTimeMilliseconds(), transactionAttributes["timestamp"]),
                () => Assert.AreEqual("WebTransaction/TransactionName", transactionAttributes["name"]),
                () => Assert.AreEqual("WebTransaction/TransactionName", transactionAttributes["transactionName"]),
                () => Assert.AreEqual(0.5f, transactionAttributes["duration"]),
                () => Assert.AreEqual(1, transactionAttributes["totalTime"]),
                () => Assert.NotNull(transactionAttributes["nr.tripId"]),
                () => Assert.NotNull(transactionAttributes["databaseDuration"]),
                () => Assert.AreEqual(5, transactionAttributes["databaseDuration"]),
                () => Assert.NotNull(transactionAttributes["databaseCallCount"]),
                () => Assert.AreEqual(1, transactionAttributes["databaseCallCount"]),
                () => Assert.AreEqual("/Unknown", transactionAttributes["request.uri"]),
                () => Assert.Contains("host.displayName", transactionAttributes.Keys)
            );
        }

        [Test]
        public void GetAttributes_ReturnsMultipleDatabase()
        {
            // ARRANGE
            var timer = Mock.Create<ITimer>();
            var expectedStartTime = DateTime.Now;
            var expectedDuration = TimeSpan.FromMilliseconds(500);
            Mock.Arrange(() => timer.Duration).Returns(expectedDuration);

            var priority = 0.5f;
            var transaction = new Transaction(_configuration, TransactionName.ForOtherTransaction("transactionCategory", "transactionName"), timer, expectedStartTime, Mock.Create<ICallStackManager>(), _databaseService, priority, Mock.Create<IDatabaseStatementParser>(), _distributedTracePayloadHandler, _errorService, _attribDefs);
            var immutableTransaction = transaction.ConvertToImmutableTransaction();
            var transactionMetricName = new TransactionMetricName("WebTransaction", "TransactionName");
            var apdexT = null as TimeSpan?;
            var totalTime = TimeSpan.FromSeconds(1);
            var txStats = new TransactionMetricStatsCollection(new TransactionMetricName("WebTransaction", "myTx"));
            txStats.MergeUnscopedStats(MetricNames.DatastoreAll, MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5)));
            txStats.MergeUnscopedStats(MetricNames.DatastoreAll, MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(4)));
            txStats.MergeUnscopedStats(MetricNames.DatastoreAll, MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2)));

            // ACT
            var attributes = _transactionAttributeMaker.GetAttributes(immutableTransaction, transactionMetricName, apdexT, totalTime, txStats);

            // ACQUIRE
            var transactionAttributes = attributes.ToDictionary();

            // ASSERT
            NrAssert.Multiple(
                () => Assert.AreEqual(13, transactionAttributes.Count),  // Assert that only these attributes are generated
                () => Assert.AreEqual("Transaction", transactionAttributes["type"]),
                () => Assert.AreEqual(expectedStartTime.ToUnixTimeMilliseconds(), transactionAttributes["timestamp"]),
                () => Assert.AreEqual("WebTransaction/TransactionName", transactionAttributes["name"]),
                () => Assert.AreEqual("WebTransaction/TransactionName", transactionAttributes["transactionName"]),
                () => Assert.AreEqual(0.5f, transactionAttributes["duration"]),
                () => Assert.AreEqual(1, transactionAttributes["totalTime"]),
                () => Assert.NotNull(transactionAttributes["nr.tripId"]),
                () => Assert.NotNull(transactionAttributes["databaseDuration"]),
                () => Assert.AreEqual(11, transactionAttributes["databaseDuration"]),
                () => Assert.NotNull(transactionAttributes["databaseCallCount"]),
                () => Assert.AreEqual(3, transactionAttributes["databaseCallCount"]),
                () => Assert.AreEqual("/Unknown", transactionAttributes["request.uri"]),
                () => Assert.Contains("host.displayName", transactionAttributes.Keys)
            );
        }

        [Test]
        public void GetAttributes_ReturnsOneExternal()
        {
            // ARRANGE
            var timer = Mock.Create<ITimer>();
            var expectedStartTime = DateTime.Now;
            var expectedDuration = TimeSpan.FromMilliseconds(500);
            Mock.Arrange(() => timer.Duration).Returns(expectedDuration);

            var priority = 0.5f;
            var transaction = new Transaction(_configuration, TransactionName.ForOtherTransaction("transactionCategory", "transactionName"), timer, expectedStartTime, Mock.Create<ICallStackManager>(), _databaseService, priority, Mock.Create<IDatabaseStatementParser>(), _distributedTracePayloadHandler, _errorService, _attribDefs);
            var immutableTransaction = transaction.ConvertToImmutableTransaction();
            var transactionMetricName = new TransactionMetricName("WebTransaction", "TransactionName");
            var apdexT = null as TimeSpan?;
            var totalTime = TimeSpan.FromSeconds(1);
            var txStats = new TransactionMetricStatsCollection(new TransactionMetricName("WebTransaction", "myTx"));
            txStats.MergeUnscopedStats(MetricNames.ExternalAll, MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5)));
            // ACT
            var attributes = _transactionAttributeMaker.GetAttributes(immutableTransaction, transactionMetricName, apdexT, totalTime, txStats);

            // ACQUIRE
            var transactionAttributes = attributes.ToDictionary();

            // ASSERT
            NrAssert.Multiple(
                () => Assert.AreEqual(13, transactionAttributes.Count),  // Assert that only these attributes are generated
                () => Assert.AreEqual("Transaction", transactionAttributes["type"]),
                () => Assert.AreEqual(expectedStartTime.ToUnixTimeMilliseconds(), transactionAttributes["timestamp"]),
                () => Assert.AreEqual("WebTransaction/TransactionName", transactionAttributes["name"]),
                () => Assert.AreEqual("WebTransaction/TransactionName", transactionAttributes["transactionName"]),
                () => Assert.AreEqual(0.5f, transactionAttributes["duration"]),
                () => Assert.AreEqual(1, transactionAttributes["totalTime"]),
                () => Assert.NotNull(transactionAttributes["nr.tripId"]),
                () => Assert.NotNull(transactionAttributes["externalDuration"]),
                () => Assert.AreEqual(5, transactionAttributes["externalDuration"]),
                () => Assert.NotNull(transactionAttributes["externalCallCount"]),
                () => Assert.AreEqual(1, transactionAttributes["externalCallCount"]),
                () => Assert.AreEqual("/Unknown", transactionAttributes["request.uri"]),
                () => Assert.Contains("host.displayName", transactionAttributes.Keys)
            );
        }

        [Test]
        public void GetAttributes_ReturnsMultipleExternal()
        {
            // ARRANGE
            var timer = Mock.Create<ITimer>();
            var expectedStartTime = DateTime.Now;
            var expectedDuration = TimeSpan.FromMilliseconds(500);
            Mock.Arrange(() => timer.Duration).Returns(expectedDuration);

            var priority = 0.5f;
            var transaction = new Transaction(_configuration, TransactionName.ForOtherTransaction("transactionCategory", "transactionName"), timer, expectedStartTime, Mock.Create<ICallStackManager>(), _databaseService, priority, Mock.Create<IDatabaseStatementParser>(), _distributedTracePayloadHandler, _errorService, _attribDefs);
            var immutableTransaction = transaction.ConvertToImmutableTransaction();
            var transactionMetricName = new TransactionMetricName("WebTransaction", "TransactionName");
            var apdexT = null as TimeSpan?;
            var totalTime = TimeSpan.FromSeconds(1);
            var txStats = new TransactionMetricStatsCollection(new TransactionMetricName("WebTransaction", "myTx"));
            txStats.MergeUnscopedStats(MetricNames.ExternalAll, MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5)));
            txStats.MergeUnscopedStats(MetricNames.ExternalAll, MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(4)));
            txStats.MergeUnscopedStats(MetricNames.ExternalAll, MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2)));

            // ACT
            var attributes = _transactionAttributeMaker.GetAttributes(immutableTransaction, transactionMetricName, apdexT, totalTime, txStats);

            // ACQUIRE
            var transactionAttributes = attributes.ToDictionary();

            // ASSERT
            NrAssert.Multiple(
                () => Assert.AreEqual(13, transactionAttributes.Count),  // Assert that only these attributes are generated
                () => Assert.AreEqual("Transaction", transactionAttributes["type"]),
                () => Assert.AreEqual(expectedStartTime.ToUnixTimeMilliseconds(), transactionAttributes["timestamp"]),
                () => Assert.AreEqual("WebTransaction/TransactionName", transactionAttributes["name"]),
                () => Assert.AreEqual("WebTransaction/TransactionName", transactionAttributes["transactionName"]),
                () => Assert.AreEqual(0.5f, transactionAttributes["duration"]),
                () => Assert.AreEqual(1, transactionAttributes["totalTime"]),
                () => Assert.NotNull(transactionAttributes["nr.tripId"]),
                () => Assert.NotNull(transactionAttributes["externalDuration"]),
                () => Assert.AreEqual(11, transactionAttributes["externalDuration"]),
                () => Assert.NotNull(transactionAttributes["externalCallCount"]),
                () => Assert.AreEqual(3, transactionAttributes["externalCallCount"]),
                () => Assert.AreEqual("/Unknown", transactionAttributes["request.uri"]),
                () => Assert.Contains("host.displayName", transactionAttributes.Keys)
            );
        }

        [Test]
        public void GetAttributes_ReturnsAllAttributesThatHaveValues()
        {
            // ARRANGE
            var timer = Mock.Create<ITimer>();
            var expectedStartTime = DateTime.Now;
            var expectedDuration = TimeSpan.FromMilliseconds(500);
            Mock.Arrange(() => timer.Duration).Returns(expectedDuration);
            var transactionMetricName = new TransactionMetricName("WebTransaction", "TransactionName");
            var apdexT = TimeSpan.FromSeconds(2);

            var priority = 0.5f;
            var transaction = new Transaction(_configuration, TransactionName.ForWebTransaction("transactionCategory", "transactionName"), timer, expectedStartTime, Mock.Create<ICallStackManager>(), _databaseService, priority, Mock.Create<IDatabaseStatementParser>(), _distributedTracePayloadHandler, _errorService, _attribDefs);
            transaction.AddCustomAttribute("userAttributeKey", "userAttributeValue");
            transaction.SetRequestParameters(new[]
            {
                new KeyValuePair<string,string>("requestParameterKey", "requestParameterValue"),

            });

            var headerCollection = new Dictionary<string, string>()
            {
                { "key1", "value1" },
                { "key2", "value2" },
                { "key3", ""},
                { "Key4", "value4"},
                { "Referer", "/index.html?a=b&x=y" },
                { "Location", "/index.html?a=b&x=y"},
                { "Refresh", "/index.html?a=b&x=y"}
            };

            string GetHeaderValue(Dictionary<string, string> headers, string key)
            {
                return headers[key];
            }

            transaction.SetRequestHeaders(headerCollection, new[] { "key1", "key2", "key3", "Key4", "Referer", "Location", "Refresh" }, GetHeaderValue);
            
            transaction.SetHttpResponseStatusCode(400, null);
            transaction.TransactionMetadata.SetOriginalUri("originalUri");
            transaction.TransactionMetadata.SetQueueTime(TimeSpan.FromSeconds(1));
            transaction.TransactionMetadata.SetReferrerUri("referrerUri");
            transaction.TransactionMetadata.SetUri("uri");
            transaction.TransactionMetadata.SetCrossApplicationReferrerTripId("referrerTripId");
            transaction.TransactionMetadata.SetCrossApplicationReferrerProcessId("referrerProcessId");
            transaction.TransactionMetadata.SetCrossApplicationPathHash("pathHash");
            transaction.TransactionMetadata.SetCrossApplicationPathHash("pathHash2");
            transaction.TransactionMetadata.SetCrossApplicationReferrerPathHash("referringPathHash");
            transaction.TransactionMetadata.SetCrossApplicationReferrerTransactionGuid("referringTransactionGuid");
            var immutableTransaction = transaction.ConvertToImmutableTransaction();

            var txStats = new TransactionMetricStatsCollection(new TransactionMetricName("WebTransaction", "myTx"));
            txStats.MergeUnscopedStats(MetricNames.ExternalAll, MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2)));

            var totalTime = TimeSpan.FromSeconds(1);

            // ACT
            var attributes = _transactionAttributeMaker.GetAttributes(immutableTransaction, transactionMetricName, apdexT, totalTime, txStats);

            // ACQUIRE
            var transactionAttributes = attributes;

            // ASSERT
            NrAssert.Multiple(
                () => Assert.AreEqual(44, GetCount(transactionAttributes)),  // Assert that only these attributes are generated
                () => Assert.AreEqual("Transaction", GetAttributeValue(attributes, "type", AttributeDestinations.TransactionEvent)),
                () => Assert.AreEqual("TransactionError", GetAttributeValue(attributes, "type", AttributeDestinations.ErrorEvent)),
                () => Assert.AreEqual(expectedStartTime.ToUnixTimeMilliseconds(), GetAttributeValue(attributes, "timestamp", AttributeDestinations.TransactionEvent)),
                () => Assert.AreEqual(immutableTransaction.TransactionMetadata.ReadOnlyTransactionErrorState.ErrorData.NoticedAt.ToUnixTimeMilliseconds(), GetAttributeValue(transactionAttributes, "timestamp", AttributeDestinations.ErrorEvent)),
                () => Assert.AreEqual("WebTransaction/TransactionName", GetAttributeValue(transactionAttributes, "name")),
                () => Assert.AreEqual("WebTransaction/TransactionName", GetAttributeValue(transactionAttributes, "transactionName")),
                () => Assert.AreEqual(immutableTransaction.Guid, GetAttributeValue(transactionAttributes, "nr.guid")),
                () => Assert.AreEqual(0.5f, GetAttributeValue(transactionAttributes, "duration")),
                () => Assert.AreEqual(1, GetAttributeValue(transactionAttributes, "totalTime")),
                () => Assert.AreEqual(0.5, GetAttributeValue(transactionAttributes, "webDuration")),
                () => Assert.AreEqual(1, GetAttributeValue(transactionAttributes, "queueDuration")),
                () => Assert.AreEqual(2, GetAttributeValue(transactionAttributes, "externalDuration")),
                () => Assert.True(DoAttributesContain(transactionAttributes, "externalCallCount")),
                () => Assert.True(DoAttributesContain(transactionAttributes, "nr.apdexPerfZone")),
                () => Assert.AreEqual("originalUri", GetAttributeValue(transactionAttributes, "original_url")),
                () => Assert.AreEqual("uri", GetAttributeValue(transactionAttributes, "request.uri")),
                () => Assert.AreEqual("referrerUri", GetAttributeValue(transactionAttributes, "request.referer")),
                () => Assert.AreEqual("1000", GetAttributeValue(transactionAttributes, "queue_wait_time_ms")),
                () => Assert.AreEqual("400", GetAttributeValue(transactionAttributes, "response.status")),
                () => Assert.AreEqual(400, GetAttributeValue(transactionAttributes, "http.statusCode")),
                () => Assert.AreEqual("requestParameterValue", GetAttributeValue(transactionAttributes, "request.parameters.requestParameterKey")),
                () => Assert.AreEqual("userAttributeValue", GetAttributeValue(transactionAttributes, "userAttributeKey")),
                () => Assert.AreEqual("referrerProcessId", GetAttributeValue(transactionAttributes, "client_cross_process_id")),
                () => Assert.AreEqual("referrerTripId", GetAttributeValue(transactionAttributes, "trip_id")),
                () => Assert.AreEqual("referrerTripId", GetAttributeValue(transactionAttributes, "nr.tripId")),
                () => Assert.AreEqual("pathHash2", GetAttributeValue(transactionAttributes, "path_hash")),
                () => Assert.AreEqual("pathHash2", GetAttributeValue(transactionAttributes, "nr.pathHash")),
                () => Assert.AreEqual("referringPathHash", GetAttributeValue(transactionAttributes, "nr.referringPathHash")),
                () => Assert.AreEqual("referringTransactionGuid", GetAttributeValue(transactionAttributes, "referring_transaction_guid")),
                () => Assert.AreEqual("referringTransactionGuid", GetAttributeValue(transactionAttributes, "nr.referringTransactionGuid")),
                () => Assert.AreEqual("pathHash", GetAttributeValue(transactionAttributes, "nr.alternatePathHashes")),
                () => Assert.AreEqual("400", GetAttributeValue(transactionAttributes, "error.class")),
                () => Assert.AreEqual("400", GetAttributeValue(transactionAttributes, "errorType")),
#if NET
                () => Assert.AreEqual("400", GetAttributeValue(transactionAttributes, "errorMessage")),
                () => Assert.AreEqual("400", GetAttributeValue(transactionAttributes, "error.message")),
#else
                () => Assert.AreEqual("Bad Request", GetAttributeValue(transactionAttributes, "errorMessage")),
                () => Assert.AreEqual("Bad Request", GetAttributeValue(transactionAttributes, "error.message")),
#endif
                () => Assert.AreEqual(true, GetAttributeValue(transactionAttributes, "error")),
                () => Assert.True(DoAttributesContain(transactionAttributes, "host.displayName")),
                () => Assert.AreEqual("value1", GetAttributeValue(transactionAttributes, "request.headers.key1")),
                () => Assert.AreEqual("value2", GetAttributeValue(transactionAttributes, "request.headers.key2")),
                () => Assert.AreEqual("", GetAttributeValue(transactionAttributes, "request.headers.key3")),
                () => Assert.AreEqual("value4", GetAttributeValue(transactionAttributes, "request.headers.key4")),
                () => Assert.AreEqual("/index.html", GetAttributeValue(transactionAttributes, "request.headers.referer")), //test to make sure query string is removed.
                () => Assert.AreEqual("/index.html", GetAttributeValue(transactionAttributes, "request.headers.location")), //test to make sure query string is removed.
                () => Assert.AreEqual("/index.html", GetAttributeValue(transactionAttributes, "request.headers.refresh")) //test to make sure query string is removed.
            );
        }

        [Test]
        public void GetAttributes_SetRequestHeaders_HighSecurityModeEnabled()
        {
            // ARRANGE
            _localConfig.highSecurity.enabled = true;
            UpdateConfiguration();

            var timer = Mock.Create<ITimer>();
            var expectedStartTime = DateTime.Now;
            var expectedDuration = TimeSpan.FromMilliseconds(500);
            Mock.Arrange(() => timer.Duration).Returns(expectedDuration);
            var transactionMetricName = new TransactionMetricName("WebTransaction", "TransactionName");
            var apdexT = TimeSpan.FromSeconds(2);

            var priority = 0.5f;
            var transaction = new Transaction(_configuration, TransactionName.ForWebTransaction("transactionCategory", "transactionName"), timer, expectedStartTime, Mock.Create<ICallStackManager>(), _databaseService, priority, Mock.Create<IDatabaseStatementParser>(), _distributedTracePayloadHandler, _errorService, _attribDefs);

            var headerCollection = new Dictionary<string, string>()
            {
                { "key1", "value1" },
                { "key2", "value2" },
                { "key3", ""},
                { "Key4", "value4"}
            };

            string GetHeaderValue(Dictionary<string, string> headers, string key)
            {
                return headers[key];
            }

            transaction.SetRequestHeaders(headerCollection, new[] { "key1", "key2", "key3", "Key4" }, GetHeaderValue);

            var immutableTransaction = transaction.ConvertToImmutableTransaction();

            var txStats = new TransactionMetricStatsCollection(new TransactionMetricName("WebTransaction", "myTx"));
            txStats.MergeUnscopedStats(MetricNames.ExternalAll, MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2)));

            var totalTime = TimeSpan.FromSeconds(1);

            // ACT
            var attributes = _transactionAttributeMaker.GetAttributes(immutableTransaction, transactionMetricName, apdexT, totalTime, txStats);

            // ACQUIRE
            var transactionAttributes = attributes;

            // ASSERT
            NrAssert.Multiple(
                () => Assert.AreEqual(15, GetCount(transactionAttributes)),  // Assert that only these attributes are generated

                () => Assert.False(DoAttributesContain(transactionAttributes, "request.headers.key1")),
                () => Assert.False(DoAttributesContain(transactionAttributes, "request.headers.key2")),
                () => Assert.False(DoAttributesContain(transactionAttributes, "request.headers.key3")),
                () => Assert.False(DoAttributesContain(transactionAttributes, "request.headers.key4"))
            );
        }

        [Test]
        public void GetAttributes_ReturnsCatAttsWithoutCrossAppId()
        {
            // ARRANGE
            var timer = Mock.Create<ITimer>();
            var expectedStartTime = DateTime.Now;
            var expectedDuration = TimeSpan.FromMilliseconds(500);
            Mock.Arrange(() => timer.Duration).Returns(expectedDuration);
            var transactionMetricName = new TransactionMetricName("WebTransaction", "TransactionName");
            var apdexT = TimeSpan.FromSeconds(2);

            var priority = 0.5f;
            var transaction = new Transaction(_configuration, TransactionName.ForWebTransaction("transactionCategory", "transactionName"), timer, expectedStartTime, Mock.Create<ICallStackManager>(), _databaseService, priority, Mock.Create<IDatabaseStatementParser>(), _distributedTracePayloadHandler, _errorService, _attribDefs);
            transaction.SetRequestParameters(new[] { new KeyValuePair<string, string>("requestParameterKey", "requestParameterValue") });
            transaction.AddCustomAttribute("userAttributeKey", "userAttributeValue");
            transaction.SetHttpResponseStatusCode(200, null);
            transaction.TransactionMetadata.SetOriginalUri("originalUri");
            transaction.TransactionMetadata.SetQueueTime(TimeSpan.FromSeconds(1));
            transaction.TransactionMetadata.SetReferrerUri("referrerUri");
            transaction.TransactionMetadata.SetUri("uri");
            transaction.TransactionMetadata.SetCrossApplicationPathHash("pathHash");
            transaction.TransactionMetadata.SetCrossApplicationPathHash("pathHash2");
            transaction.TransactionMetadata.MarkHasCatResponseHeaders();
            var immutableTransaction = transaction.ConvertToImmutableTransaction();
            var txStats = new TransactionMetricStatsCollection(new TransactionMetricName("WebTransaction", "myTx"));
            txStats.MergeUnscopedStats(MetricNames.ExternalAll, MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2)));

            var totalTime = TimeSpan.FromSeconds(1);

            // ACT
            var attributes = _transactionAttributeMaker.GetAttributes(immutableTransaction, transactionMetricName, apdexT, totalTime, txStats);

            // ACQUIRE
            var transactionAttributes = attributes;

            var tripId = immutableTransaction.Guid;
            // ASSERT
            NrAssert.Multiple(
                () => Assert.AreEqual(27, GetCount(attributes)),  // Assert that only these attributes are generated
                () => Assert.AreEqual("Transaction", GetAttributeValue(transactionAttributes, "type", AttributeDestinations.TransactionEvent)),
                () => Assert.AreEqual(expectedStartTime.ToUnixTimeMilliseconds(), GetAttributeValue(transactionAttributes, "timestamp", AttributeDestinations.TransactionEvent)),
                () => Assert.AreEqual("WebTransaction/TransactionName", GetAttributeValue(transactionAttributes, "name")),
                () => Assert.AreEqual("WebTransaction/TransactionName", GetAttributeValue(transactionAttributes, "transactionName")),
                () => Assert.AreEqual(immutableTransaction.Guid, GetAttributeValue(transactionAttributes, "nr.guid")),
                () => Assert.AreEqual(0.5f, GetAttributeValue(transactionAttributes, "duration")),
                () => Assert.AreEqual(1, GetAttributeValue(transactionAttributes, "totalTime")),
                () => Assert.AreEqual(0.5, GetAttributeValue(transactionAttributes, "webDuration")),
                () => Assert.AreEqual(1, GetAttributeValue(transactionAttributes, "queueDuration")),
                () => Assert.AreEqual(2, GetAttributeValue(transactionAttributes, "externalDuration")),
                () => Assert.True(DoAttributesContain(transactionAttributes, "externalCallCount")),
                () => Assert.True(DoAttributesContain(transactionAttributes, "nr.apdexPerfZone")),
                () => Assert.AreEqual("originalUri", GetAttributeValue(transactionAttributes, "original_url")),
                () => Assert.AreEqual("uri", GetAttributeValue(transactionAttributes, "request.uri")),
                () => Assert.AreEqual("referrerUri", GetAttributeValue(transactionAttributes, "request.referer")),
                () => Assert.AreEqual("1000", GetAttributeValue(transactionAttributes, "queue_wait_time_ms")),
                () => Assert.AreEqual("200", GetAttributeValue(transactionAttributes, "response.status")),
                () => Assert.AreEqual(200, GetAttributeValue(transactionAttributes, "http.statusCode")),
                () => Assert.AreEqual("requestParameterValue", GetAttributeValue(transactionAttributes, "request.parameters.requestParameterKey")),
                () => Assert.AreEqual("userAttributeValue", GetAttributeValue(transactionAttributes, "userAttributeKey")),
                () => Assert.AreEqual(tripId, GetAttributeValue(transactionAttributes, "trip_id")),
                () => Assert.AreEqual(tripId, GetAttributeValue(transactionAttributes, "nr.tripId")),
                () => Assert.AreEqual("pathHash2", GetAttributeValue(transactionAttributes, "path_hash")),
                () => Assert.AreEqual("pathHash2", GetAttributeValue(transactionAttributes, "nr.pathHash")),
                () => Assert.AreEqual("pathHash", GetAttributeValue(transactionAttributes, "nr.alternatePathHashes")),
                () => Assert.True(DoAttributesContain(transactionAttributes, "host.displayName"))
            );
        }

        [Test]
        public void GetAttributes_DoesNotIncludeOriginalUri_IfSameValueAsUei()
        {
            // ARRANGE
            var timer = Mock.Create<ITimer>();
            var expectedStartTime = DateTime.Now;
            var expectedDuration = TimeSpan.FromMilliseconds(500);
            Mock.Arrange(() => timer.Duration).Returns(expectedDuration);
            var transactionMetricName = new TransactionMetricName("WebTransaction", "TransactionName");
            var apdexT = TimeSpan.FromSeconds(2);

            var priority = 0.5f;
            var transaction = new Transaction(_configuration, TransactionName.ForWebTransaction("transactionCategory", "transactionName"), timer, expectedStartTime, Mock.Create<ICallStackManager>(), _databaseService, priority, Mock.Create<IDatabaseStatementParser>(), _distributedTracePayloadHandler, _errorService, _attribDefs);
            transaction.TransactionMetadata.SetOriginalUri("SameUri");
            transaction.TransactionMetadata.SetUri("SameUri");
            var immutableTransaction = transaction.ConvertToImmutableTransaction();
            var totalTime = TimeSpan.FromSeconds(1);
            var txStats = new TransactionMetricStatsCollection(new TransactionMetricName("WebTransaction", "myTx"));

            // ACT
            var attributes = _transactionAttributeMaker.GetAttributes(immutableTransaction, transactionMetricName, apdexT, totalTime, txStats);

            // ACQUIRE
            var transactionAttributes = attributes;


            // ASSERT
            Assert.False(DoAttributesContain(transactionAttributes, "originalUri"));
        }

        [Test]
        public void GetAttributes_SendsAttributesToCorrectLocations()
        {
            // ARRANGE
            var timer = Mock.Create<ITimer>();
            var expectedStartTime = DateTime.Now;
            var expectedDuration = TimeSpan.FromMilliseconds(500);
            Mock.Arrange(() => timer.Duration).Returns(expectedDuration);
            var transactionMetricName = new TransactionMetricName("WebTransaction", "TransactionName");
            var apdexT = TimeSpan.FromSeconds(2);

            var priority = 0.5f;
            var transaction = new Transaction(_configuration, TransactionName.ForWebTransaction("transactionCategory", "transactionName"), timer, expectedStartTime, Mock.Create<ICallStackManager>(), _databaseService, priority, Mock.Create<IDatabaseStatementParser>(), _distributedTracePayloadHandler, _errorService, _attribDefs);
            transaction.SetRequestParameters(new[] { new KeyValuePair<string, string>("requestParameterKey", "requestParameterValue") });
            transaction.AddCustomAttribute("userAttributeKey", "userAttributeValue");
            transaction.TransactionMetadata.TransactionErrorState.AddCustomErrorData(MakeErrorData());
            transaction.SetHttpResponseStatusCode(400, null);
            transaction.TransactionMetadata.SetOriginalUri("originalUri");
            transaction.TransactionMetadata.SetQueueTime(TimeSpan.FromSeconds(1));
            transaction.TransactionMetadata.SetReferrerUri("referrerUri");
            transaction.TransactionMetadata.SetUri("uri");
            transaction.TransactionMetadata.SetCrossApplicationReferrerTripId("referrerTripId");
            transaction.TransactionMetadata.SetCrossApplicationPathHash("pathHash");
            transaction.TransactionMetadata.SetCrossApplicationPathHash("pathHash2");
            transaction.TransactionMetadata.SetCrossApplicationReferrerProcessId("referrerProcessId");
            transaction.TransactionMetadata.SetCrossApplicationReferrerPathHash("referringPathHash");
            transaction.TransactionMetadata.SetCrossApplicationReferrerTransactionGuid("referringTransactionGuid");
            var immutableTransaction = transaction.ConvertToImmutableTransaction();

            var totalTime = TimeSpan.FromSeconds(1);
            var txStats = new TransactionMetricStatsCollection(new TransactionMetricName("WebTransaction", "myTx"));
            txStats.MergeUnscopedStats(MetricNames.DatastoreAll, MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(3)));
            // ACT
            var attributes = _transactionAttributeMaker.GetAttributes(immutableTransaction, transactionMetricName, apdexT, totalTime, txStats);

            // ACQUIRE

            // ASSERT
            NrAssert.Multiple(
                () => Assert.AreEqual(38, GetCount(attributes)),  // Assert that only these attributes are generated
                () => AssertAttributeShouldBeAvailableFor(attributes, "type", AttributeDestinations.TransactionEvent, AttributeDestinations.ErrorEvent),
                () => AssertAttributeShouldBeAvailableFor(attributes, "timestamp", AttributeDestinations.TransactionEvent, AttributeDestinations.CustomEvent, AttributeDestinations.ErrorEvent),
                () => AssertAttributeShouldBeAvailableFor(attributes, "name", AttributeDestinations.TransactionEvent),
                () => AssertAttributeShouldBeAvailableFor(attributes, "transactionName", AttributeDestinations.ErrorEvent),
                () => AssertAttributeShouldBeAvailableFor(attributes, "nr.guid", AttributeDestinations.TransactionEvent , AttributeDestinations.ErrorEvent),
                () => AssertAttributeShouldBeAvailableFor(attributes, "duration", AttributeDestinations.TransactionEvent , AttributeDestinations.ErrorEvent),
                () => AssertAttributeShouldBeAvailableFor(attributes, "webDuration", AttributeDestinations.TransactionEvent),
                () => AssertAttributeShouldBeAvailableFor(attributes, "totalTime", AttributeDestinations.TransactionEvent , AttributeDestinations.TransactionTrace),
                () => AssertAttributeShouldBeAvailableFor(attributes, "databaseDuration", AttributeDestinations.TransactionEvent , AttributeDestinations.ErrorEvent),
                () => AssertAttributeShouldBeAvailableFor(attributes, "databaseCallCount", AttributeDestinations.TransactionEvent , AttributeDestinations.ErrorEvent),
                () => AssertAttributeShouldBeAvailableFor(attributes, "nr.apdexPerfZone", AttributeDestinations.TransactionEvent),
                () => AssertAttributeShouldBeAvailableFor(attributes, "client_cross_process_id", AttributeDestinations.ErrorTrace , AttributeDestinations.TransactionTrace),
                () => AssertAttributeShouldBeAvailableFor(attributes, "trip_id", AttributeDestinations.ErrorTrace , AttributeDestinations.TransactionTrace),
                () => AssertAttributeShouldBeAvailableFor(attributes, "nr.tripId", AttributeDestinations.TransactionEvent),
                () => AssertAttributeShouldBeAvailableFor(attributes, "path_hash", AttributeDestinations.ErrorTrace , AttributeDestinations.TransactionTrace),
                () => AssertAttributeShouldBeAvailableFor(attributes, "nr.pathHash", AttributeDestinations.TransactionEvent),
                () => AssertAttributeShouldBeAvailableFor(attributes, "nr.referringPathHash", AttributeDestinations.TransactionEvent),
                () => AssertAttributeShouldBeAvailableFor(attributes, "referring_transaction_guid", AttributeDestinations.ErrorTrace , AttributeDestinations.TransactionTrace),
                () => AssertAttributeShouldBeAvailableFor(attributes, "nr.referringTransactionGuid", AttributeDestinations.TransactionEvent , AttributeDestinations.ErrorEvent),
                () => AssertAttributeShouldBeAvailableFor(attributes, "nr.alternatePathHashes", AttributeDestinations.TransactionEvent),
                () => AssertAttributeShouldBeAvailableFor(attributes, "original_url", AttributeDestinations.ErrorTrace , AttributeDestinations.TransactionTrace , AttributeDestinations.ErrorEvent),
                () => AssertAttributeShouldBeAvailableFor(attributes, "request.uri", AttributeDestinations.TransactionEvent , AttributeDestinations.ErrorEvent , AttributeDestinations.ErrorTrace , AttributeDestinations.TransactionTrace , AttributeDestinations.SqlTrace),
                () => AssertAttributeShouldBeAvailableFor(attributes, "request.referer", AttributeDestinations.ErrorTrace , AttributeDestinations.TransactionTrace , AttributeDestinations.ErrorEvent),
                () => AssertAttributeShouldBeAvailableFor(attributes, "queue_wait_time_ms", AttributeDestinations.ErrorTrace , AttributeDestinations.TransactionTrace , AttributeDestinations.ErrorEvent),
                () => AssertAttributeShouldBeAvailableFor(attributes, "response.status", AttributeDestinations.TransactionEvent , AttributeDestinations.ErrorTrace , AttributeDestinations.TransactionTrace , AttributeDestinations.ErrorEvent),
                () => AssertAttributeShouldBeAvailableFor(attributes, "http.statusCode", AttributeDestinations.TransactionEvent , AttributeDestinations.ErrorTrace , AttributeDestinations.TransactionTrace , AttributeDestinations.ErrorEvent),
                () => DoAttributesContain(attributes, "request.parameters.requestParameterKey"),
                () => AssertAttributeShouldBeAvailableFor(attributes, "userErrorAttributeKey", AttributeDestinations.ErrorEvent , AttributeDestinations.ErrorTrace),
                () => AssertAttributeShouldBeAvailableFor(attributes, "error.class", AttributeDestinations.ErrorEvent),
                () => AssertAttributeShouldBeAvailableFor(attributes, "errorType", AttributeDestinations.TransactionEvent),
                () => AssertAttributeShouldBeAvailableFor(attributes, "errorMessage", AttributeDestinations.TransactionEvent),
                () => AssertAttributeShouldBeAvailableFor(attributes, "error.message", AttributeDestinations.ErrorEvent),
                () => AssertAttributeShouldBeAvailableFor(attributes, "error", AttributeDestinations.TransactionEvent),
                () => AssertAttributeShouldBeAvailableFor(attributes, "host.displayName", AttributeDestinations.TransactionTrace , AttributeDestinations.TransactionEvent , AttributeDestinations.ErrorTrace , AttributeDestinations.ErrorEvent)
            );
        }

        [Test]
        public void GetAttributes_AssignsCorrectClassificationToAttributes_ExternalOnly()
        {
            // ARRANGE
            var timer = Mock.Create<ITimer>();
            var expectedStartTime = DateTime.Now;
            var expectedDuration = TimeSpan.FromMilliseconds(500);
            Mock.Arrange(() => timer.Duration).Returns(expectedDuration);
            var transactionMetricName = new TransactionMetricName("WebTransaction", "TransactionName");
            var apdexT = TimeSpan.FromSeconds(2);

            var priority = 0.5f;
            var transaction = new Transaction(_configuration, TransactionName.ForWebTransaction("transactionCategory", "transactionName"), timer, expectedStartTime, Mock.Create<ICallStackManager>(), _databaseService, priority, Mock.Create<IDatabaseStatementParser>(), _distributedTracePayloadHandler, _errorService, _attribDefs);
            transaction.SetRequestParameters(new[] { new KeyValuePair<string, string>("requestParameterKey", "requestParameterValue") });
            transaction.AddCustomAttribute("userAttributeKey", "userAttributeValue");
            transaction.SetHttpResponseStatusCode(200, null);
            transaction.TransactionMetadata.SetOriginalUri("originalUri");
            transaction.TransactionMetadata.SetQueueTime(TimeSpan.FromSeconds(1));
            transaction.TransactionMetadata.SetReferrerUri("referrerUri");
            transaction.TransactionMetadata.SetUri("uri");
            transaction.TransactionMetadata.SetCrossApplicationReferrerTripId("referrerTripId");
            transaction.TransactionMetadata.SetCrossApplicationReferrerProcessId("referrerProcessId");
            transaction.TransactionMetadata.SetCrossApplicationPathHash("pathHash");
            transaction.TransactionMetadata.SetCrossApplicationPathHash("pathHash2");
            transaction.TransactionMetadata.SetCrossApplicationReferrerPathHash("referringPathHash");
            transaction.TransactionMetadata.SetCrossApplicationReferrerTransactionGuid("referringTransactionGuid");
            var immutableTransaction = transaction.ConvertToImmutableTransaction();

            var totalTime = TimeSpan.FromSeconds(1);
            var txStats = new TransactionMetricStatsCollection(new TransactionMetricName("WebTransaction", "myTx"));
            txStats.MergeUnscopedStats(MetricNames.ExternalAll, MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2)));

            // ACT
            var attributes = _transactionAttributeMaker.GetAttributes(immutableTransaction, transactionMetricName, apdexT, totalTime, txStats);

            // ACQUIRE
            var transactionAttributes = attributes.ToDictionary().Keys;

            // ASSERT
            NrAssert.Multiple
            (
                () => Assert.AreEqual(31, GetCount(attributes)),  // Assert that only these attributes are generated
                () => CollectionAssert.Contains(transactionAttributes, "type"),
                () => CollectionAssert.Contains(transactionAttributes, "timestamp"),
                () => CollectionAssert.Contains(transactionAttributes, "name"),
                () => CollectionAssert.Contains(transactionAttributes, "transactionName"),
                () => CollectionAssert.Contains(transactionAttributes, "nr.guid"),
                () => CollectionAssert.Contains(transactionAttributes, "duration"),
                () => CollectionAssert.Contains(transactionAttributes, "totalTime"),
                () => CollectionAssert.Contains(transactionAttributes, "webDuration"),
                () => CollectionAssert.Contains(transactionAttributes, "queueDuration"),
                () => CollectionAssert.Contains(transactionAttributes, "externalDuration"),
                () => CollectionAssert.Contains(transactionAttributes, "externalCallCount"),
                () => CollectionAssert.Contains(transactionAttributes, "nr.apdexPerfZone"),
                () => CollectionAssert.Contains(transactionAttributes, "original_url"),
                () => CollectionAssert.Contains(transactionAttributes, "request.uri"),
                () => CollectionAssert.Contains(transactionAttributes, "request.referer"),
                () => CollectionAssert.Contains(transactionAttributes, "queue_wait_time_ms"),
                () => CollectionAssert.Contains(transactionAttributes, "response.status"),
                () => CollectionAssert.Contains(transactionAttributes, "http.statusCode"),
                () => CollectionAssert.Contains(transactionAttributes, "request.parameters.requestParameterKey"),
                () => CollectionAssert.Contains(transactionAttributes, "host.displayName"),
                () => CollectionAssert.Contains(transactionAttributes, "userAttributeKey"),
                () => CollectionAssert.Contains(transactionAttributes, "client_cross_process_id"),
                () => CollectionAssert.Contains(transactionAttributes, "trip_id"),
                () => CollectionAssert.Contains(transactionAttributes, "nr.tripId"),
                () => CollectionAssert.Contains(transactionAttributes, "path_hash"),
                () => CollectionAssert.Contains(transactionAttributes, "nr.pathHash"),
                () => CollectionAssert.Contains(transactionAttributes, "nr.referringPathHash"),
                () => CollectionAssert.Contains(transactionAttributes, "referring_transaction_guid"),
                () => CollectionAssert.Contains(transactionAttributes, "nr.referringTransactionGuid"),
                () => CollectionAssert.Contains(transactionAttributes, "nr.alternatePathHashes")
            );


        }

        [Test]
        public void GetAttributes_AssignsCorrectClassificationToAttributes_ExternalAndDB()
        {
            // ARRANGE
            var timer = Mock.Create<ITimer>();
            var expectedStartTime = DateTime.Now;
            var expectedDuration = TimeSpan.FromMilliseconds(500);
            Mock.Arrange(() => timer.Duration).Returns(expectedDuration);
            var transactionMetricName = new TransactionMetricName("WebTransaction", "TransactionName");
            var apdexT = TimeSpan.FromSeconds(2);

            var priority = 0.5f;
            var transaction = new Transaction(_configuration, TransactionName.ForWebTransaction("transactionCategory", "transactionName"), timer, expectedStartTime, Mock.Create<ICallStackManager>(), _databaseService, priority, Mock.Create<IDatabaseStatementParser>(), _distributedTracePayloadHandler, _errorService, _attribDefs);
            transaction.SetRequestParameters(new[] { new KeyValuePair<string, string>("requestParameterKey", "requestParameterValue") });
            transaction.AddCustomAttribute("userAttributeKey", "userAttributeValue");
            transaction.SetHttpResponseStatusCode(200, null);
            transaction.TransactionMetadata.SetOriginalUri("originalUri");
            transaction.TransactionMetadata.SetQueueTime(TimeSpan.FromSeconds(1));
            transaction.TransactionMetadata.SetReferrerUri("referrerUri");
            transaction.TransactionMetadata.SetUri("uri");
            transaction.TransactionMetadata.SetCrossApplicationReferrerTripId("referrerTripId");
            transaction.TransactionMetadata.SetCrossApplicationReferrerProcessId("referrerProcessId");
            transaction.TransactionMetadata.SetCrossApplicationPathHash("pathHash");
            transaction.TransactionMetadata.SetCrossApplicationPathHash("pathHash2");
            transaction.TransactionMetadata.SetCrossApplicationReferrerPathHash("referringPathHash");
            transaction.TransactionMetadata.SetCrossApplicationReferrerTransactionGuid("referringTransactionGuid");
            var immutableTransaction = transaction.ConvertToImmutableTransaction();

            var totalTime = TimeSpan.FromSeconds(1);
            var txStats = new TransactionMetricStatsCollection(new TransactionMetricName("WebTransaction", "myTx"));
            txStats.MergeUnscopedStats(MetricNames.ExternalAll, MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2)));
            txStats.MergeUnscopedStats(MetricNames.DatastoreAll, MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(3)));

            // ACT
            var attributes = _transactionAttributeMaker.GetAttributes(immutableTransaction, transactionMetricName, apdexT, totalTime, txStats);

            // ACQUIRE
            var intrinsicAttributes = attributes.ToDictionary(AttributeClassification.Intrinsics).Keys;
            var agentAttributes = attributes.ToDictionary(AttributeClassification.AgentAttributes).Keys;


            // ASSERT
            NrAssert.Multiple(
                () => Assert.AreEqual(33, GetCount(attributes)),  // Assert that only these attributes are generated
                () => AssertAttributeShouldBeAvailableFor(attributes, "type", AttributeDestinations.TransactionEvent),
                () => AssertAttributeShouldBeAvailableFor(attributes, "timestamp", AttributeDestinations.TransactionEvent, AttributeDestinations.SpanEvent, AttributeDestinations.CustomEvent),
                () => CollectionAssert.Contains(intrinsicAttributes, "name"),
                () => CollectionAssert.Contains(intrinsicAttributes, "transactionName"),
                () => CollectionAssert.Contains(intrinsicAttributes, "nr.guid"),
                () => CollectionAssert.Contains(intrinsicAttributes, "duration"),
                () => CollectionAssert.Contains(intrinsicAttributes, "totalTime"),
                () => CollectionAssert.Contains(intrinsicAttributes, "webDuration"),
                () => CollectionAssert.Contains(intrinsicAttributes, "queueDuration"),
                () => CollectionAssert.Contains(intrinsicAttributes, "externalDuration"),
                () => CollectionAssert.Contains(intrinsicAttributes, "externalCallCount"),
                () => CollectionAssert.Contains(intrinsicAttributes, "databaseDuration"),
                () => CollectionAssert.Contains(intrinsicAttributes, "databaseCallCount"),
                () => CollectionAssert.Contains(intrinsicAttributes, "nr.apdexPerfZone"),
                () => CollectionAssert.Contains(agentAttributes, "original_url"),
                () => CollectionAssert.Contains(agentAttributes, "request.referer"),
                () => CollectionAssert.Contains(agentAttributes, "queue_wait_time_ms"),
                () => CollectionAssert.Contains(agentAttributes, "response.status"),
                () => CollectionAssert.Contains(agentAttributes, "http.statusCode"),
                () => CollectionAssert.Contains(agentAttributes, "request.parameters.requestParameterKey"),
                () => CollectionAssert.Contains(agentAttributes, "host.displayName"),
                () => CollectionAssert.Contains(intrinsicAttributes, "client_cross_process_id"),
                () => CollectionAssert.Contains(intrinsicAttributes, "trip_id"),
                () => CollectionAssert.Contains(intrinsicAttributes, "nr.tripId"),
                () => CollectionAssert.Contains(intrinsicAttributes, "path_hash"),
                () => CollectionAssert.Contains(intrinsicAttributes, "nr.pathHash"),
                () => CollectionAssert.Contains(intrinsicAttributes, "nr.referringPathHash"),
                () => CollectionAssert.Contains(intrinsicAttributes, "referring_transaction_guid"),
                () => CollectionAssert.Contains(intrinsicAttributes, "nr.referringTransactionGuid"),
                () => CollectionAssert.Contains(intrinsicAttributes, "nr.alternatePathHashes")
            );
        }

        [Test]
        public void GetAttributes_DoesNotReturnWebDurationAttribute_IfNonWebTransaction()
        {
            // ARRANGE
            var priority = 0.5f;
            var transactionBuilder = new Transaction(_configuration, TransactionName.ForOtherTransaction("transactionCategory", "transactionName"), Mock.Create<ITimer>(), DateTime.UtcNow, Mock.Create<ICallStackManager>(), _databaseService, priority, Mock.Create<IDatabaseStatementParser>(), _distributedTracePayloadHandler, _errorService, _attribDefs);
            var transaction = transactionBuilder.ConvertToImmutableTransaction();

            var transactionMetricName = new TransactionMetricName("WebTransaction", "TransactionName");
            var apdexT = TimeSpan.FromSeconds(2);
            var totalTime = TimeSpan.FromSeconds(1);
            var txStats = new TransactionMetricStatsCollection(new TransactionMetricName("WebTransaction", "myTx"));
            // ACT
            var attributes = _transactionAttributeMaker.GetAttributes(transaction, transactionMetricName, apdexT, totalTime, txStats);

            // ACQUIRE
            var transactionAttributes = attributes.ToDictionary();

            // ASSERT
            Assert.False(DoAttributesContain(attributes,"webDuration"));
        }

        [Test]
        public void GetAttributes_ErrorAttributesNotIncluded_IfErrorCollectorDisabled()
        {
            // ARRANGE
            var timer = Mock.Create<ITimer>();
            var expectedStartTime = DateTime.Now;
            var expectedDuration = TimeSpan.FromMilliseconds(500);
            Mock.Arrange(() => timer.Duration).Returns(expectedDuration);
            var transactionMetricName = new TransactionMetricName("WebTransaction", "TransactionName");
            var apdexT = TimeSpan.FromSeconds(2);
            _localConfig.errorCollector.enabled = false;
            UpdateConfiguration();

            var priority = 0.5f;
            var transaction = new Transaction(_configuration, TransactionName.ForWebTransaction("transactionCategory", "transactionName"), timer, expectedStartTime, Mock.Create<ICallStackManager>(), _databaseService, priority, Mock.Create<IDatabaseStatementParser>(), _distributedTracePayloadHandler, _errorService, _attribDefs);
            transaction.SetHttpResponseStatusCode(400, null);
            var immutableTransaction = transaction.ConvertToImmutableTransaction();

            var txStats = new TransactionMetricStatsCollection(new TransactionMetricName("WebTransaction", "myTx"));
            txStats.MergeUnscopedStats(MetricNames.ExternalAll, MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2)));

            var totalTime = TimeSpan.FromSeconds(1);

            // ACT
            var transactionAttributes = _transactionAttributeMaker.GetAttributes(immutableTransaction, transactionMetricName, apdexT, totalTime, txStats);

            // ASSERT
            NrAssert.Multiple(
                () => Assert.True(DoAttributesContain(transactionAttributes, "nr.apdexPerfZone")),
                () => Assert.False(DoAttributesContain(transactionAttributes, "error.class")),
                () => Assert.False(DoAttributesContain(transactionAttributes, "errorType")),
                () => Assert.False(DoAttributesContain(transactionAttributes, "errorMessage")),
                () => Assert.False(DoAttributesContain(transactionAttributes, "error.message")),
                () => Assert.False(DoAttributesContain(transactionAttributes, "error"))
            );
        }

        [Test]
        public void GetAttributes_FalseErrorAttributeIncluded_WithNoError()
        {
            // ARRANGE
            var timer = Mock.Create<ITimer>();
            var expectedStartTime = DateTime.Now;
            var expectedDuration = TimeSpan.FromMilliseconds(500);
            Mock.Arrange(() => timer.Duration).Returns(expectedDuration);
            var transactionMetricName = new TransactionMetricName("WebTransaction", "TransactionName");
            var apdexT = TimeSpan.FromSeconds(2);
            UpdateConfiguration();

            var priority = 0.5f;
            var transaction = new Transaction(_configuration, TransactionName.ForWebTransaction("transactionCategory", "transactionName"), timer, expectedStartTime, Mock.Create<ICallStackManager>(), _databaseService, priority, Mock.Create<IDatabaseStatementParser>(), _distributedTracePayloadHandler, _errorService, _attribDefs);
            var immutableTransaction = transaction.ConvertToImmutableTransaction();

            var txStats = new TransactionMetricStatsCollection(new TransactionMetricName("WebTransaction", "myTx"));
            txStats.MergeUnscopedStats(MetricNames.ExternalAll, MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2)));

            var totalTime = TimeSpan.FromSeconds(1);

            // ACT
            var transactionAttributes = _transactionAttributeMaker.GetAttributes(immutableTransaction, transactionMetricName, apdexT, totalTime, txStats);

            // ASSERT
            NrAssert.Multiple(
                () => Assert.AreEqual(false, GetAttributeValue(transactionAttributes, "error"))
            );
        }

        [TestCase(true)]
        [TestCase(false)]
        public void GetAttributes_ExpecedErrorAttribute_SentToCorrectDestinations(bool isErrorExpected)
        {
            // ARRANGE
            var timer = Mock.Create<ITimer>();
            var expectedStartTime = DateTime.Now;
            var transactionMetricName = new TransactionMetricName("WebTransaction", "TransactionName");
            var apdexT = TimeSpan.FromSeconds(2);

            var priority = 0.5f;
            var transaction = new Transaction(_configuration, TransactionName.ForWebTransaction("transactionCategory", "transactionName"), timer, expectedStartTime, Mock.Create<ICallStackManager>(), _databaseService, priority, Mock.Create<IDatabaseStatementParser>(), _distributedTracePayloadHandler, _errorService, _attribDefs);
            transaction.TransactionMetadata.TransactionErrorState.AddCustomErrorData(MakeErrorData(isErrorExpected: isErrorExpected));
            var immutableTransaction = transaction.ConvertToImmutableTransaction();

            var txStats = new TransactionMetricStatsCollection(new TransactionMetricName("WebTransaction", "myTx"));
            txStats.MergeUnscopedStats(MetricNames.ExternalAll, MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2)));

            var totalTime = TimeSpan.FromSeconds(1);

            // ACT
            var attributes = _transactionAttributeMaker.GetAttributes(immutableTransaction, transactionMetricName, apdexT, totalTime, txStats);

            // ASSERT
            if (isErrorExpected)
            {
                AssertAttributeShouldBeAvailableFor(attributes, "error.expected", AttributeDestinations.ErrorEvent, AttributeDestinations.ErrorTrace);
            }
            else
            {
                Assert.False(DoAttributesContain(attributes, "error.expected"));
            }
        }

        #region Distributed Trace

        private const DistributedTracingParentType IncomingType = DistributedTracingParentType.App;
        private const string IncomingAcctId = "incomingAcctId";
        private const string IncomingAppId = "incomingAppId";
        private const string IncomingGuid = "incomingGuid";
        private const string IncomingParentId = "incomingParentId";
        private const bool IncomingSampled = false;
        private const string IncomingTraceId = "incomingTraceId";
        private const float IncomingPriority = 0.5f;
        private const float Priority = 0.65f;
        private const string IncomingTransactionId = "transactionId";
        private const string IncomingTransportType = "HTTP";
        private static DateTime IncomingTimestamp = DateTime.UtcNow;
        private const string TrustKey = "1234";
        private const string ParentSpanIdAttributeName = "parentSpanId";
        private const string TransactionGuid = "guid";

        [Test]
        public void ShouldNotIncludeTripIdWhenDistributedTracingEnabled()
        {
            _localConfig.distributedTracing.enabled = true;
            _serverConfig.TrustedAccountKey = "1234";
            UpdateConfiguration();

            var transactionMetricName = new TransactionMetricName("WebTransaction", "TransactionName");

            var immutableTransaction = BuildTestImmutableTransaction(sampled: IncomingSampled, guid: TransactionGuid, traceId: IncomingTraceId);

            var txStats = new TransactionMetricStatsCollection(new TransactionMetricName("WebTransaction", "myTx"));
            var totalTime = TimeSpan.FromSeconds(1);
            var apdexT = TimeSpan.FromSeconds(2);

            // ACT
            var attributes = _transactionAttributeMaker.GetAttributes(immutableTransaction, transactionMetricName, apdexT, totalTime, txStats);

            // ACQUIRE
            var intrinsicAttribs = attributes.GetAttributeValuesDic(AttributeClassification.Intrinsics);

            // ASSERT
            NrAssert.Multiple(
                () => Assert.False(intrinsicAttribs.ContainsKey("nr.tripId"), "nr.tripId should not have been included"),
                () => Assert.False(intrinsicAttribs.ContainsKey("trip_id"), "trip_id should not have been included")
            );
        }

        [Test]
        public void GetAttributes_ReturnsDistributedTraceAttrs_IfDistributedTraceEnabledAndReceivedPayload()
        {
            _localConfig.distributedTracing.enabled = true;
            _serverConfig.TrustedAccountKey = TrustKey;
            UpdateConfiguration();

            var transactionMetricName = new TransactionMetricName("WebTransaction", "TransactionName");

            var immutableTransaction = BuildTestImmutableTransaction(sampled: IncomingSampled, guid: TransactionGuid, traceId: IncomingTraceId);

            var txStats = new TransactionMetricStatsCollection(new TransactionMetricName("WebTransaction", "myTx"));
            var totalTime = TimeSpan.FromSeconds(1);
            var apdexT = TimeSpan.FromSeconds(2);

            // ACT
            var attributes = _transactionAttributeMaker.GetAttributes(immutableTransaction, transactionMetricName, apdexT, totalTime, txStats);

            // ACQUIRE
            var intrinsicAttribValues = attributes.GetAttributeValuesDic(AttributeClassification.Intrinsics);
            var agentAttribValues = attributes.GetAttributeValuesDic(AttributeClassification.AgentAttributes);

            // ASSERT
            NrAssert.Multiple(
                () => Assert.AreEqual(27, GetCount(attributes)),  // Assert that only these attributes are generated
                () => Assert.AreEqual("Transaction", intrinsicAttribValues["type"]),
                () => Assert.True(intrinsicAttribValues.ContainsKey("timestamp")),
                () => Assert.AreEqual("WebTransaction/TransactionName", intrinsicAttribValues["name"]),
                () => Assert.AreEqual("WebTransaction/TransactionName", intrinsicAttribValues["transactionName"]),
                () => Assert.True(intrinsicAttribValues.ContainsKey("duration")),
                () => Assert.AreEqual(1, intrinsicAttribValues["totalTime"]),
                () => Assert.True(intrinsicAttribValues.ContainsKey("webDuration")),
                () => Assert.True(intrinsicAttribValues.ContainsKey("nr.apdexPerfZone")),
                () => Assert.AreEqual("/Unknown", agentAttribValues["request.uri"]),
                () => Assert.AreEqual(IncomingTraceId, intrinsicAttribValues["traceId"]),
                () => Assert.AreEqual(IncomingType.ToString(), intrinsicAttribValues["parent.type"]),
                () => Assert.AreEqual(IncomingAppId, intrinsicAttribValues["parent.app"]),
                () => Assert.AreEqual(IncomingAcctId, intrinsicAttribValues["parent.account"]),
                () => Assert.AreEqual(IncomingTransportType, intrinsicAttribValues["parent.transportType"]),
                () => Assert.True(intrinsicAttribValues.ContainsKey("parent.transportDuration")),

                () => Assert.AreEqual(IncomingType.ToString(), agentAttribValues["parent.type"]),
                () => Assert.AreEqual(IncomingAppId, agentAttribValues["parent.app"]),
                () => Assert.AreEqual(IncomingAcctId, agentAttribValues["parent.account"]),
                () => Assert.AreEqual(IncomingTransportType, agentAttribValues["parent.transportType"]),
                () => Assert.True(agentAttribValues.ContainsKey("parent.transportDuration")),

                () => Assert.AreEqual(IncomingTransactionId, intrinsicAttribValues["parentId"]),
                () => Assert.AreEqual(immutableTransaction.Guid, intrinsicAttribValues["guid"]),
                () => Assert.AreEqual(IncomingPriority, intrinsicAttribValues["priority"]),
                () => Assert.AreEqual(IncomingSampled, intrinsicAttribValues["sampled"]),
                () => Assert.AreEqual(IncomingGuid, intrinsicAttribValues["parentSpanId"]),
                () => Assert.True(agentAttribValues.ContainsKey("host.displayName"))
            );
        }

        [Test]
        public void GetAttributes_DoesNotReturnDistributedTraceAttrs_IfDistributedTraceEnabledAndDidNotReceivePayload()
        {
            _localConfig.distributedTracing.enabled = true;

            UpdateConfiguration();

            var timer = Mock.Create<ITimer>();
            var expectedStartTime = DateTime.Now;
            var transactionMetricName = new TransactionMetricName("WebTransaction", "TransactionName");

            var transaction = new Transaction(_configuration, TransactionName.ForWebTransaction("transactionCategory", "transactionName"), timer, expectedStartTime, Mock.Create<ICallStackManager>(), _databaseService, Priority, Mock.Create<IDatabaseStatementParser>(), _distributedTracePayloadHandler, _errorService, _attribDefs);

            var immutableTransaction = transaction.ConvertToImmutableTransaction();
            var txStats = new TransactionMetricStatsCollection(new TransactionMetricName("WebTransaction", "myTx"));
            var totalTime = TimeSpan.FromSeconds(1);
            var apdexT = TimeSpan.FromSeconds(2);

            // ACT
            var attributes = _transactionAttributeMaker.GetAttributes(immutableTransaction, transactionMetricName, apdexT, totalTime, txStats);

            // ACQUIRE
            var transactionAttributes = attributes.ToDictionary();

            // ASSERT
            NrAssert.Multiple(
                () => Assert.False(transactionAttributes.ContainsKey("parent.type")),
                () => Assert.False(transactionAttributes.ContainsKey("parent.app")),
                () => Assert.False(transactionAttributes.ContainsKey("parent.account")),
                () => Assert.False(transactionAttributes.ContainsKey("parent.transportType")),
                () => Assert.False(transactionAttributes.ContainsKey("parent.transportDuration")),
                () => Assert.False(transactionAttributes.ContainsKey("parentId"))
            );
        }

        [Test]
        public void GetAttributes_DoesNotReturnDistributedTraceAttrs_IfDistributedTraceEnabledAndIngestErrorsAreFatal()
        {
            _localConfig.distributedTracing.enabled = true;
            UpdateConfiguration();

            var transactionMetricName = new TransactionMetricName("WebTransaction", "TransactionName");

            var immutableTransaction = BuildTestImmutableTransaction(sampled: IncomingSampled, guid: TransactionGuid, traceId: IncomingTraceId, hasFatalError: true);

            var txStats = new TransactionMetricStatsCollection(new TransactionMetricName("WebTransaction", "myTx"));
            var totalTime = TimeSpan.FromSeconds(1);
            var apdexT = TimeSpan.FromSeconds(2);

            // ACT
            var attributes = _transactionAttributeMaker.GetAttributes(immutableTransaction, transactionMetricName, apdexT, totalTime, txStats);

            // ACQUIRE
            var transactionIntrinsicsAttributes = attributes.ToDictionary(AttributeClassification.Intrinsics);

            // ASSERT
            NrAssert.Multiple(
                () => Assert.False(transactionIntrinsicsAttributes.ContainsKey("parent.type")),
                () => Assert.False(transactionIntrinsicsAttributes.ContainsKey("parent.app")),
                () => Assert.False(transactionIntrinsicsAttributes.ContainsKey("parent.account")),
                () => Assert.False(transactionIntrinsicsAttributes.ContainsKey("parent.transportType")),
                () => Assert.False(transactionIntrinsicsAttributes.ContainsKey("parent.transportDuration")),
                () => Assert.False(transactionIntrinsicsAttributes.ContainsKey("parentId")),
                () => Assert.False(transactionIntrinsicsAttributes.ContainsKey("parentSpanId"))
            );
        }

        [Test]
        public void GetAttributes_AssignsAttributesToCorrectDestinations_DistributedTracePayloadReceived()
        {
            _localConfig.distributedTracing.enabled = true;
            UpdateConfiguration();

            var transactionMetricName = new TransactionMetricName("WebTransaction", "TransactionName");

            var immutableTransaction = BuildTestImmutableTransaction(sampled: IncomingSampled, guid: TransactionGuid, traceId: IncomingTraceId);

            var txStats = new TransactionMetricStatsCollection(new TransactionMetricName("WebTransaction", "myTx"));
            var totalTime = TimeSpan.FromSeconds(1);
            var apdexT = TimeSpan.FromSeconds(2);

            // ACT
            var attributes = _transactionAttributeMaker.GetAttributes(immutableTransaction, transactionMetricName, apdexT, totalTime, txStats);

            // ASSERT
            NrAssert.Multiple(
                () => Assert.AreEqual(27, GetCount(attributes)),  // Assert that only these attributes are generated
                () => AssertAttributeShouldBeAvailableFor(attributes,"type", AttributeDestinations.TransactionEvent),
                () => AssertAttributeShouldBeAvailableFor(attributes,"timestamp", AttributeDestinations.TransactionEvent, AttributeDestinations.SpanEvent, AttributeDestinations.CustomEvent),
                () => AssertAttributeShouldBeAvailableFor(attributes,"name", AttributeDestinations.TransactionEvent),
                () => AssertAttributeShouldBeAvailableFor(attributes,"transactionName", AttributeDestinations.ErrorEvent),
                () => AssertAttributeShouldBeAvailableFor(attributes,"duration", AttributeDestinations.TransactionEvent, AttributeDestinations.ErrorEvent, AttributeDestinations.SpanEvent),
                () => AssertAttributeShouldBeAvailableFor(attributes,"webDuration", AttributeDestinations.TransactionEvent),
                () => AssertAttributeShouldBeAvailableFor(attributes,"totalTime", AttributeDestinations.TransactionEvent, AttributeDestinations.TransactionTrace),
                () => AssertAttributeShouldBeAvailableFor(attributes,"nr.apdexPerfZone", AttributeDestinations.TransactionEvent),
                () => AssertAttributeShouldBeAvailableFor(attributes,"parent.type", AttributeDestinations.TransactionTrace, AttributeDestinations.ErrorTrace, AttributeDestinations.SqlTrace, AttributeDestinations.TransactionEvent, AttributeDestinations.ErrorEvent),
                () => AssertAttributeShouldBeAvailableFor(attributes,"parent.app", AttributeDestinations.TransactionTrace, AttributeDestinations.ErrorTrace, AttributeDestinations.SqlTrace, AttributeDestinations.TransactionEvent, AttributeDestinations.ErrorEvent),
                () => AssertAttributeShouldBeAvailableFor(attributes,"parent.account", AttributeDestinations.TransactionTrace, AttributeDestinations.ErrorTrace, AttributeDestinations.SqlTrace, AttributeDestinations.TransactionEvent, AttributeDestinations.ErrorEvent),
                () => AssertAttributeShouldBeAvailableFor(attributes,"parent.transportType", AttributeDestinations.TransactionTrace, AttributeDestinations.ErrorTrace, AttributeDestinations.SqlTrace, AttributeDestinations.TransactionEvent, AttributeDestinations.ErrorEvent),
                () => AssertAttributeShouldBeAvailableFor(attributes,"parent.transportDuration", AttributeDestinations.TransactionTrace, AttributeDestinations.ErrorTrace, AttributeDestinations.SqlTrace, AttributeDestinations.TransactionEvent, AttributeDestinations.ErrorEvent),
                () => AssertAttributeShouldBeAvailableFor(attributes,ParentSpanIdAttributeName, AttributeDestinations.TransactionEvent),
                () => AssertAttributeShouldBeAvailableFor(attributes,"parentId", AttributeDestinations.TransactionEvent, AttributeDestinations.SpanEvent),
                () => AssertAttributeShouldBeAvailableFor(attributes,"priority", AttributeDestinations.TransactionTrace, AttributeDestinations.ErrorTrace, AttributeDestinations.SqlTrace, AttributeDestinations.TransactionEvent, AttributeDestinations.ErrorEvent, AttributeDestinations.SpanEvent),
                () => AssertAttributeShouldBeAvailableFor(attributes,"sampled", AttributeDestinations.TransactionTrace, AttributeDestinations.ErrorTrace, AttributeDestinations.SqlTrace, AttributeDestinations.TransactionEvent, AttributeDestinations.ErrorEvent, AttributeDestinations.SpanEvent),
                () => AssertAttributeShouldBeAvailableFor(attributes,"parentSpanId", AttributeDestinations.TransactionEvent),
                () => AssertAttributeShouldBeAvailableFor(attributes, "request.uri", AttributeDestinations.TransactionEvent, AttributeDestinations.ErrorEvent, AttributeDestinations.ErrorTrace, AttributeDestinations.TransactionTrace, AttributeDestinations.SqlTrace)
            );
        }

        [Test]
        public void GetAttributes_AssignsCorrectClassificationToAttributes_DistributedTracePayloadReceived()
        {
            _localConfig.distributedTracing.enabled = true;
            UpdateConfiguration();

            var transactionMetricName = new TransactionMetricName("WebTransaction", "TransactionName");

            var immutableTransaction = BuildTestImmutableTransaction(sampled: IncomingSampled, guid: TransactionGuid, traceId: IncomingTraceId);

            var txStats = new TransactionMetricStatsCollection(new TransactionMetricName("WebTransaction", "myTx"));
            var totalTime = TimeSpan.FromSeconds(1);
            var apdexT = TimeSpan.FromSeconds(2);

            // ACT
            var attributes = _transactionAttributeMaker.GetAttributes(immutableTransaction, transactionMetricName, apdexT, totalTime, txStats);

            // ACQUIRE
            var intrinsicAttributes = attributes.ToDictionary(AttributeClassification.Intrinsics).Keys;
            var agentAttributes = attributes.ToDictionary(AttributeClassification.AgentAttributes).Keys;

            // ASSERT
            NrAssert.Multiple(
                () => Assert.AreEqual(27, GetCount(attributes)),  // Assert that only these attributes are generated
                () => CollectionAssert.Contains(intrinsicAttributes, "type"),
                () => CollectionAssert.Contains(intrinsicAttributes, "timestamp"),
                () => CollectionAssert.Contains(intrinsicAttributes, "name"),
                () => CollectionAssert.Contains(intrinsicAttributes, "transactionName"),
                () => CollectionAssert.Contains(intrinsicAttributes, "duration"),
                () => CollectionAssert.Contains(intrinsicAttributes, "totalTime"),
                () => CollectionAssert.Contains(intrinsicAttributes, "webDuration"),
                () => CollectionAssert.Contains(intrinsicAttributes, "nr.apdexPerfZone"),
                () => CollectionAssert.Contains(agentAttributes, "request.uri"),
                () => CollectionAssert.Contains(agentAttributes, "host.displayName"),
                () => CollectionAssert.Contains(intrinsicAttributes, "traceId"),
                () => CollectionAssert.Contains(intrinsicAttributes, "parent.type"),
                () => CollectionAssert.Contains(intrinsicAttributes, "parent.app"),
                () => CollectionAssert.Contains(intrinsicAttributes, "parent.account"),
                () => CollectionAssert.Contains(intrinsicAttributes, "parent.transportType"),
                () => CollectionAssert.Contains(intrinsicAttributes, "parent.transportDuration"),
                () => CollectionAssert.Contains(intrinsicAttributes, ParentSpanIdAttributeName),
                () => CollectionAssert.Contains(intrinsicAttributes, "guid"),
                () => CollectionAssert.Contains(intrinsicAttributes, "priority"),
                () => CollectionAssert.Contains(intrinsicAttributes, "sampled"),
                () => CollectionAssert.Contains(intrinsicAttributes, "parentId"),
                () => CollectionAssert.Contains(intrinsicAttributes,  "parentSpanId")
            );
        }

        [Test]
        public void ParentSpanIdIsGuidWhenDistributedTraceGuidInPayloadAndNoParentIdInPayload()
        {
            _localConfig.distributedTracing.enabled = true;
            UpdateConfiguration();

            var transactionMetricName = new TransactionMetricName("WebTransaction", "TransactionName");

            var immutableTransaction = BuildTestImmutableTransaction(sampled: IncomingSampled, guid: TransactionGuid, traceId: IncomingTraceId);

            var txStats = new TransactionMetricStatsCollection(new TransactionMetricName("WebTransaction", "myTx"));
            var totalTime = TimeSpan.FromSeconds(1);
            var apdexT = TimeSpan.FromSeconds(2);

            // ACT
            var attributes = _transactionAttributeMaker.GetAttributes(immutableTransaction, transactionMetricName, apdexT, totalTime, txStats);
            
            NrAssert.Multiple
            (
                () => Assert.IsTrue(DoAttributesContain(attributes, ParentSpanIdAttributeName, AttributeClassification.Intrinsics)),
                () => Assert.IsTrue(DoAttributesContain(attributes, ParentSpanIdAttributeName, AttributeDestinations.TransactionEvent)),
                () => Assert.AreEqual(IncomingGuid, GetAttributeValue(attributes,ParentSpanIdAttributeName))
            );
        }

        [Test]
        public void ParentSpanIdIsParentIdWhenDistributedTraceParentIdInPayload()
        {
            _localConfig.distributedTracing.enabled = true;
            UpdateConfiguration();

            var transactionMetricName = new TransactionMetricName("WebTransaction", "TransactionName");

            var immutableTransaction = BuildTestImmutableTransaction(sampled: IncomingSampled, guid: TransactionGuid, traceId: IncomingTraceId, parentIdInPayload: true);

            var txStats = new TransactionMetricStatsCollection(new TransactionMetricName("WebTransaction", "myTx"));
            var totalTime = TimeSpan.FromSeconds(1);
            var apdexT = TimeSpan.FromSeconds(2);

            // ACT
            var attributes = _transactionAttributeMaker.GetAttributes(immutableTransaction, transactionMetricName, apdexT, totalTime, txStats);

            NrAssert.Multiple
            (
               () => Assert.IsTrue(DoAttributesContain(attributes, ParentSpanIdAttributeName, AttributeClassification.Intrinsics)),
               () => Assert.IsTrue(DoAttributesContain(attributes, ParentSpanIdAttributeName, AttributeDestinations.TransactionEvent)),
               () => Assert.AreEqual(IncomingParentId, GetAttributeValue(attributes, ParentSpanIdAttributeName))
            );

        }

        [Test]
        public void ShouldNotCreateParentSpanIdWhenDistributedTraceGuidNotInPayloadAndParentIdNotInPayload()
        {
            _localConfig.distributedTracing.enabled = true;
            UpdateConfiguration();

            var transactionMetricName = new TransactionMetricName("WebTransaction", "TransactionName");

            var immutableTransaction = BuildTestImmutableTransaction(sampled: IncomingSampled, guid: TransactionGuid, traceId: IncomingTraceId, guidInPayload: false, parentIdInPayload: false);

            var txStats = new TransactionMetricStatsCollection(new TransactionMetricName("WebTransaction", "myTx"));
            var totalTime = TimeSpan.FromSeconds(1);
            var apdexT = TimeSpan.FromSeconds(2);

            // ACT
            var attributes = _transactionAttributeMaker.GetAttributes(immutableTransaction, transactionMetricName, apdexT, totalTime, txStats);

            Assert.False(DoAttributesContain(attributes,ParentSpanIdAttributeName));
        }

        private static DistributedTracePayload BuildSampleDistributedTracePayload(string guid = IncomingGuid)
        {
            const DistributedTracingParentType type = IncomingType;
            const string accountId = IncomingAcctId;
            const string appId = IncomingAppId;
            const string traceId = IncomingTraceId;
            const string trustKey = TrustKey;
            const float priority = IncomingPriority;
            const bool sampled = IncomingSampled;
            DateTime _timestamp = DateTime.UtcNow;
            const string transactionId = IncomingTransactionId;

            return DistributedTracePayload.TryBuildOutgoingPayload(
                    type.ToString(),
                    accountId,
                    appId,
                    guid,
                    traceId,
                    trustKey,
                    priority,
                    sampled,
                    _timestamp,
                    transactionId);
        }

        private ImmutableTransaction BuildTestImmutableTransaction(bool isWebTransaction = true, string guid = null, float priority = 0.5f, bool sampled = false, string traceId = "traceId", bool guidInPayload = true, bool parentIdInPayload = false, bool hasFatalError = false)
        {
            var name = TransactionName.ForWebTransaction("category", "name");

            var segments = Enumerable.Empty<Segment>();

            var placeholderMetadataBuilder = new TransactionMetadata(guid);
            var placeholderMetadata = placeholderMetadataBuilder.ConvertToImmutableMetadata();

            var immutableTransaction = new ImmutableTransaction(name, segments, placeholderMetadata, DateTime.Now, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10), guid, false, false, false, priority, sampled, traceId, BuildMockTracingState(guidInPayload: guidInPayload, parentIdInPayload: parentIdInPayload, hasFatalError), _attribDefs);

            return immutableTransaction;
        }

        private static ITracingState BuildMockTracingState(bool guidInPayload = true, bool parentIdInPayload = false, bool hasFatalError = false)
        {
            var tracingState = Mock.Create<ITracingState>();

            Mock.Arrange(() => tracingState.Type).Returns(IncomingType);
            Mock.Arrange(() => tracingState.AppId).Returns(IncomingAppId);
            Mock.Arrange(() => tracingState.AccountId).Returns(IncomingAcctId);
            Mock.Arrange(() => tracingState.TransportType).Returns(TransportType.HTTP);
            Mock.Arrange(() => tracingState.HasDataForParentAttributes).Returns(hasFatalError ? false : true);
            Mock.Arrange(() => tracingState.HasDataForAttributes).Returns(hasFatalError ? false : true);

            if (guidInPayload)
            {
                Mock.Arrange(() => tracingState.Guid).Returns(IncomingGuid);
            }
            else
            {
                string nullGuid = null;
                Mock.Arrange(() => tracingState.Guid).Returns(nullGuid);
            }

            if (parentIdInPayload)
            {
                Mock.Arrange(() => tracingState.ParentId).Returns(IncomingParentId);
            }
            else
            {
                string nullGuid = null;
                Mock.Arrange(() => tracingState.ParentId).Returns(nullGuid);
            }

            if (hasFatalError)
            {
                var ingestErrors = new List<IngestErrorType>() { IngestErrorType.TraceParentParseException };
                Mock.Arrange(() => tracingState.IngestErrors).Returns(ingestErrors);
            }

            Mock.Arrange(() => tracingState.Timestamp).Returns(IncomingTimestamp);
            Mock.Arrange(() => tracingState.TraceId).Returns(IncomingTraceId);
            Mock.Arrange(() => tracingState.TransactionId).Returns(IncomingTransactionId);
            Mock.Arrange(() => tracingState.Sampled).Returns(IncomingSampled);
            Mock.Arrange(() => tracingState.Priority).Returns(IncomingPriority);

            return tracingState;
        }
        #endregion Distributed Trace

        #endregion GetAttributes

        #region GetUserAndAgentAttributes

        [Test]
        public void GetUserAndAgentAttributes_ReturnsAllAttributesThatHaveValues()
        {
            // ARRANGE
            var timer = Mock.Create<ITimer>();
            var expectedStartTime = DateTime.Now;
            var expectedDuration = TimeSpan.FromMilliseconds(500);
            Mock.Arrange(() => timer.Duration).Returns(expectedDuration);

            var priority = 0.5f;
            var transaction = new Transaction(_configuration, TransactionName.ForWebTransaction("transactionCategory", "transactionName"), timer, expectedStartTime, Mock.Create<ICallStackManager>(), _databaseService, priority, Mock.Create<IDatabaseStatementParser>(), _distributedTracePayloadHandler, _errorService, _attribDefs);
            transaction.SetRequestParameters(new[] { new KeyValuePair<string, string>("requestParameterKey", "requestParameterValue") });
            transaction.AddCustomAttribute("userAttributeKey", "userAttributeValue");
            transaction.TransactionMetadata.TransactionErrorState.AddCustomErrorData(MakeErrorData());
            transaction.SetHttpResponseStatusCode(400, null);
            transaction.TransactionMetadata.SetOriginalUri("originalUri");
            transaction.TransactionMetadata.SetQueueTime(TimeSpan.FromSeconds(1));
            transaction.TransactionMetadata.SetReferrerUri("referrerUri");
            transaction.TransactionMetadata.SetUri("uri");
            transaction.TransactionMetadata.SetCrossApplicationReferrerTripId("referrerTripId");
            transaction.TransactionMetadata.SetCrossApplicationReferrerProcessId("referrerProcessId");
            transaction.TransactionMetadata.SetCrossApplicationPathHash("pathHash");
            transaction.TransactionMetadata.SetCrossApplicationPathHash("pathHash2");
            transaction.TransactionMetadata.SetCrossApplicationReferrerPathHash("referringPathHash");
            transaction.TransactionMetadata.SetCrossApplicationReferrerTransactionGuid("referringTransactionGuid");
            var immutableTransaction = transaction.ConvertToImmutableTransaction();

            
            // ACT
            var builderAttributes = new AttributeValueCollection(AttributeValueCollection.AllTargetModelTypes);
            _transactionAttributeMaker.SetUserAndAgentAttributes(builderAttributes, transaction.TransactionMetadata);


            var attributes = new AttributeValueCollection(AttributeValueCollection.AllTargetModelTypes);
            _transactionAttributeMaker.SetUserAndAgentAttributes(attributes, immutableTransaction.TransactionMetadata);

            // ACQUIRE
            var txBuilderAttributes = builderAttributes.ToDictionary();

            var transactionAttributes = attributes.ToDictionary();

            // ASSERT
            NrAssert.Multiple(
                () => Assert.AreEqual(10, GetCount(builderAttributes)),  // Assert that only these attributes are generated
                () => Assert.AreEqual("originalUri", txBuilderAttributes["original_url"]),
                () => Assert.AreEqual("uri", transactionAttributes["request.uri"]),
                () => Assert.AreEqual("referrerUri", txBuilderAttributes["request.referer"]),
                () => Assert.AreEqual("1000", txBuilderAttributes["queue_wait_time_ms"]),
                () => Assert.AreEqual("400", txBuilderAttributes["response.status"]),
                () => Assert.AreEqual(400, txBuilderAttributes["http.statusCode"]),
                () => Assert.AreEqual("requestParameterValue", txBuilderAttributes["request.parameters.requestParameterKey"]),
                () => Assert.AreEqual("userAttributeValue", txBuilderAttributes["userAttributeKey"]),
                () => Assert.AreEqual("userErrorAttributeValue", txBuilderAttributes["userErrorAttributeKey"]),
                () => Assert.Contains("host.displayName", txBuilderAttributes.Keys)
            );
            NrAssert.Multiple(
                () => Assert.AreEqual(10, GetCount(attributes)),  // Assert that only these attributes are generated
                () => Assert.AreEqual("originalUri", transactionAttributes["original_url"]),
                () => Assert.AreEqual("uri", transactionAttributes["request.uri"]),
                () => Assert.AreEqual("referrerUri", transactionAttributes["request.referer"]),
                () => Assert.AreEqual("1000", transactionAttributes["queue_wait_time_ms"]),
                () => Assert.AreEqual("400", transactionAttributes["response.status"]),
                () => Assert.AreEqual(400, transactionAttributes["http.statusCode"]),
                () => Assert.AreEqual("requestParameterValue", transactionAttributes["request.parameters.requestParameterKey"]),
                () => Assert.AreEqual("userAttributeValue", transactionAttributes["userAttributeKey"]),
                () => Assert.AreEqual("userErrorAttributeValue", transactionAttributes["userErrorAttributeKey"]),
                () => Assert.Contains("host.displayName", transactionAttributes.Keys)
            );
        }

        [Test]
        public void GetUserAndAgentAttributes_ExcludesErrorCustomAttributes_IfErrorCollectorDisabled()
        {
            // ARRANGE
            var timer = Mock.Create<ITimer>();
            var expectedStartTime = DateTime.Now;
            var expectedDuration = TimeSpan.FromMilliseconds(500);
            Mock.Arrange(() => timer.Duration).Returns(expectedDuration);
            _localConfig.errorCollector.enabled = false;
            UpdateConfiguration();

            var priority = 0.5f;
            var transaction = new Transaction(_configuration, TransactionName.ForWebTransaction("transactionCategory", "transactionName"), timer, expectedStartTime, Mock.Create<ICallStackManager>(), _databaseService, priority, Mock.Create<IDatabaseStatementParser>(), _distributedTracePayloadHandler, _errorService, _attribDefs);
            transaction.AddCustomAttribute("userAttributeKey", "userAttributeValue");
            transaction.TransactionMetadata.TransactionErrorState.AddCustomErrorData(MakeErrorData());
            transaction.SetHttpResponseStatusCode(400, null);
            var immutableTransaction = transaction.ConvertToImmutableTransaction();


            // ACT
            var attributes = new AttributeValueCollection(AttributeValueCollection.AllTargetModelTypes);
            _transactionAttributeMaker.SetUserAndAgentAttributes(attributes, immutableTransaction.TransactionMetadata);

            // ASSERT
            NrAssert.Multiple(
                () => Assert.True(DoAttributesContain(attributes, "userAttributeKey")),
                () => Assert.False(DoAttributesContain(attributes, "userErrorAttributeKey"))
            );
        }

        [Test]
        public void GetUserAndAgentAttributes_DoesNotIncludeOriginalUri_IfSameValueAsUei()
        {
            // ARRANGE
            var timer = Mock.Create<ITimer>();
            var expectedStartTime = DateTime.Now;
            var expectedDuration = TimeSpan.FromMilliseconds(500);
            Mock.Arrange(() => timer.Duration).Returns(expectedDuration);

            var priority = 0.5f;
            var transaction = new Transaction(_configuration, TransactionName.ForWebTransaction("transactionCategory", "transactionName"), timer, expectedStartTime, Mock.Create<ICallStackManager>(), _databaseService, priority, Mock.Create<IDatabaseStatementParser>(), _distributedTracePayloadHandler, _errorService, _attribDefs);
            transaction.TransactionMetadata.SetOriginalUri("SameUri");
            transaction.TransactionMetadata.SetUri("SameUri");
            var immutableTransaction = transaction.ConvertToImmutableTransaction();

            // ACT
            var builderAttributes = new AttributeValueCollection(AttributeValueCollection.AllTargetModelTypes);
            _transactionAttributeMaker.SetUserAndAgentAttributes(builderAttributes, transaction.TransactionMetadata);

            var attributes = new AttributeValueCollection(AttributeValueCollection.AllTargetModelTypes);
            _transactionAttributeMaker.SetUserAndAgentAttributes(attributes, immutableTransaction.TransactionMetadata);

            // ACQUIRE
            var txBuilderAttributes = builderAttributes.ToDictionary();

            var transactionAttributes = attributes.ToDictionary();

            // ASSERT
            Assert.False(txBuilderAttributes.ContainsKey("originalUri"));
            Assert.False(transactionAttributes.ContainsKey("originalUri"));
        }

        [Test]
        public void GetUserAndAgentAttributes_SendsAttributesToCorrectLocations()
        {
            // ARRANGE
            var timer = Mock.Create<ITimer>();
            var expectedStartTime = DateTime.Now;
            var expectedDuration = TimeSpan.FromMilliseconds(500);
            Mock.Arrange(() => timer.Duration).Returns(expectedDuration);

            _localConfig.distributedTracing.enabled = true;
            UpdateConfiguration();

            var priority = 0.5f;
            var transaction = new Transaction(_configuration, TransactionName.ForWebTransaction("transactionCategory", "transactionName"), timer, expectedStartTime, Mock.Create<ICallStackManager>(), _databaseService, priority, Mock.Create<IDatabaseStatementParser>(), _distributedTracePayloadHandler, _errorService, _attribDefs);
            transaction.SetRequestParameters(new[] { new KeyValuePair<string, string>("requestParameterKey", "requestParameterValue") });
            transaction.AddCustomAttribute("userAttributeKey", "userAttributeValue");
            transaction.TransactionMetadata.TransactionErrorState.AddCustomErrorData(MakeErrorData());
            transaction.SetHttpResponseStatusCode(400, null);
            transaction.TransactionMetadata.SetOriginalUri("originalUri");
            transaction.TransactionMetadata.SetQueueTime(TimeSpan.FromSeconds(1));
            transaction.TransactionMetadata.SetReferrerUri("referrerUri");
            transaction.TransactionMetadata.SetUri("uri");
            transaction.TransactionMetadata.SetCrossApplicationReferrerTripId("referrerTripId");
            transaction.TransactionMetadata.SetCrossApplicationPathHash("pathHash");
            transaction.TransactionMetadata.SetCrossApplicationPathHash("pathHash2");
            transaction.TransactionMetadata.SetCrossApplicationReferrerProcessId("referrerProcessId");
            transaction.TransactionMetadata.SetCrossApplicationReferrerPathHash("referringPathHash");
            transaction.TransactionMetadata.SetCrossApplicationReferrerTransactionGuid("referringTransactionGuid");
            var immutableTransaction = transaction.ConvertToImmutableTransaction();

            // ACT
            var builderAttributes = new AttributeValueCollection(AttributeValueCollection.AllTargetModelTypes);
            _transactionAttributeMaker.SetUserAndAgentAttributes(builderAttributes, transaction.TransactionMetadata);

            var attributes = new AttributeValueCollection(AttributeValueCollection.AllTargetModelTypes);
            _transactionAttributeMaker.SetUserAndAgentAttributes(attributes, immutableTransaction.TransactionMetadata);

            AssertAttributeShouldBeAvailableFor(builderAttributes, "http.statusCode", AttributeDestinations.TransactionEvent, AttributeDestinations.ErrorTrace, AttributeDestinations.TransactionTrace, AttributeDestinations.ErrorEvent, AttributeDestinations.SpanEvent);
            AssertAttributeShouldBeAvailableFor(builderAttributes, "request.parameters.requestParameterKey", AttributeDestinations.TransactionEvent, AttributeDestinations.ErrorTrace, AttributeDestinations.TransactionTrace, AttributeDestinations.SpanEvent);

            // ASSERT
            NrAssert.Multiple
            (
                () => Assert.AreEqual(10, GetCount(builderAttributes)),  // Assert that only these attributes are generated
                () => AssertAttributeShouldBeAvailableFor(builderAttributes, "original_url" , AttributeDestinations.ErrorTrace, AttributeDestinations.TransactionTrace, AttributeDestinations.ErrorEvent),
                () => AssertAttributeShouldBeAvailableFor(builderAttributes, "request.uri", AttributeDestinations.TransactionEvent, AttributeDestinations.ErrorEvent, AttributeDestinations.ErrorTrace, AttributeDestinations.TransactionTrace, AttributeDestinations.SqlTrace),
                () => AssertAttributeShouldBeAvailableFor(builderAttributes, "request.referer", AttributeDestinations.ErrorTrace, AttributeDestinations.TransactionTrace, AttributeDestinations.ErrorEvent),
                () => AssertAttributeShouldBeAvailableFor(builderAttributes, "queue_wait_time_ms", AttributeDestinations.ErrorTrace, AttributeDestinations.TransactionTrace, AttributeDestinations.ErrorEvent),
                () => AssertAttributeShouldBeAvailableFor(builderAttributes, "response.status", AttributeDestinations.TransactionEvent, AttributeDestinations.ErrorTrace, AttributeDestinations.TransactionTrace, AttributeDestinations.ErrorEvent),
                () => AssertAttributeShouldBeAvailableFor(builderAttributes, "http.statusCode", AttributeDestinations.TransactionEvent, AttributeDestinations.ErrorTrace, AttributeDestinations.TransactionTrace, AttributeDestinations.ErrorEvent, AttributeDestinations.SpanEvent),
                () => AssertAttributeShouldBeAvailableFor(builderAttributes, "request.parameters.requestParameterKey", AttributeDestinations.TransactionEvent, AttributeDestinations.ErrorTrace, AttributeDestinations.TransactionTrace, AttributeDestinations.SpanEvent),
                () => AssertAttributeShouldBeAvailableFor(builderAttributes, "userAttributeKey", AttributeDestinations.All),
                () => AssertAttributeShouldBeAvailableFor(builderAttributes, "userErrorAttributeKey", AttributeDestinations.ErrorEvent, AttributeDestinations.ErrorTrace),
                () => AssertAttributeShouldBeAvailableFor(builderAttributes, "host.displayName", AttributeDestinations.TransactionTrace, AttributeDestinations.TransactionEvent, AttributeDestinations.ErrorTrace, AttributeDestinations.ErrorEvent)
            );

            NrAssert.Multiple
            (
                () => Assert.AreEqual(10, GetCount(attributes)),  // Assert that only these attributes are generated
                () => AssertAttributeShouldBeAvailableFor(attributes, "original_url", AttributeDestinations.ErrorTrace, AttributeDestinations.TransactionTrace, AttributeDestinations.ErrorEvent),
                () => AssertAttributeShouldBeAvailableFor(attributes, "request.uri", AttributeDestinations.TransactionEvent, AttributeDestinations.ErrorEvent, AttributeDestinations.ErrorTrace, AttributeDestinations.TransactionTrace, AttributeDestinations.SqlTrace),
                () => AssertAttributeShouldBeAvailableFor(attributes, "request.referer", AttributeDestinations.ErrorTrace, AttributeDestinations.TransactionTrace, AttributeDestinations.ErrorEvent),
                () => AssertAttributeShouldBeAvailableFor(attributes, "queue_wait_time_ms", AttributeDestinations.ErrorTrace, AttributeDestinations.TransactionTrace, AttributeDestinations.ErrorEvent),
                () => AssertAttributeShouldBeAvailableFor(attributes, "response.status", AttributeDestinations.TransactionEvent, AttributeDestinations.ErrorTrace, AttributeDestinations.TransactionTrace, AttributeDestinations.ErrorEvent),
                () => AssertAttributeShouldBeAvailableFor(attributes, "http.statusCode", AttributeDestinations.TransactionEvent, AttributeDestinations.ErrorTrace, AttributeDestinations.TransactionTrace, AttributeDestinations.ErrorEvent, AttributeDestinations.SpanEvent),
                () => AssertAttributeShouldBeAvailableFor(builderAttributes, "request.parameters.requestParameterKey", AttributeDestinations.TransactionEvent, AttributeDestinations.ErrorTrace, AttributeDestinations.TransactionTrace, AttributeDestinations.SpanEvent),
                () => AssertAttributeShouldBeAvailableFor(attributes, "userAttributeKey", AttributeDestinations.All),
                () => AssertAttributeShouldBeAvailableFor(attributes, "userErrorAttributeKey", AttributeDestinations.ErrorEvent, AttributeDestinations.ErrorTrace),
                () => AssertAttributeShouldBeAvailableFor(attributes, "host.displayName", AttributeDestinations.TransactionTrace, AttributeDestinations.TransactionEvent, AttributeDestinations.ErrorTrace, AttributeDestinations.ErrorEvent)
            );
        }

        [Test]
        public void GetUserAndAgentAttributes_AssignsCorrectClassificationToAttributes()
        {
            // ARRANGE
            var timer = Mock.Create<ITimer>();
            var expectedStartTime = DateTime.Now;
            var expectedDuration = TimeSpan.FromMilliseconds(500);
            Mock.Arrange(() => timer.Duration).Returns(expectedDuration);

            var priority = 0.5f;
            var transaction = new Transaction(_configuration, TransactionName.ForWebTransaction("transactionCategory", "transactionName"), timer, expectedStartTime, Mock.Create<ICallStackManager>(), _databaseService, priority, Mock.Create<IDatabaseStatementParser>(), _distributedTracePayloadHandler, _errorService, _attribDefs);
            transaction.SetRequestParameters(new[] { new KeyValuePair<string, string>("requestParameterKey", "requestParameterValue") });
            transaction.AddCustomAttribute("userAttributeKey", "userAttributeValue");
            transaction.TransactionMetadata.TransactionErrorState.AddCustomErrorData(MakeErrorData());
            transaction.SetHttpResponseStatusCode(400, null);
            transaction.TransactionMetadata.SetOriginalUri("originalUri");
            transaction.TransactionMetadata.SetQueueTime(TimeSpan.FromSeconds(1));
            transaction.TransactionMetadata.SetReferrerUri("referrerUri");
            transaction.TransactionMetadata.SetUri("uri");
            transaction.TransactionMetadata.SetCrossApplicationReferrerTripId("referrerTripId");
            transaction.TransactionMetadata.SetCrossApplicationReferrerProcessId("referrerProcessId");
            transaction.TransactionMetadata.SetCrossApplicationPathHash("pathHash");
            transaction.TransactionMetadata.SetCrossApplicationPathHash("pathHash2");
            transaction.TransactionMetadata.SetCrossApplicationReferrerPathHash("referringPathHash");
            transaction.TransactionMetadata.SetCrossApplicationReferrerTransactionGuid("referringTransactionGuid");
            var immutableTransaction = transaction.ConvertToImmutableTransaction();

            // ACT
            var attributes = new AttributeValueCollection(AttributeValueCollection.AllTargetModelTypes);
            _transactionAttributeMaker.SetUserAndAgentAttributes(attributes, immutableTransaction.TransactionMetadata);

            // ACQUIRE
            var agentAttributes = attributes.ToDictionary(AttributeClassification.AgentAttributes);
            var userAttributes = attributes.ToDictionary(AttributeClassification.UserAttributes);


            // ASSERT
            NrAssert.Multiple(
                () => Assert.AreEqual(10, GetCount(attributes)),  // Assert that only these attributes are generated
                () => Assert.IsTrue(agentAttributes.ContainsKey("original_url")),
                () => Assert.IsTrue(agentAttributes.ContainsKey("request.uri")),
                () => Assert.IsTrue(agentAttributes.ContainsKey("request.referer")),
                () => Assert.IsTrue(agentAttributes.ContainsKey("queue_wait_time_ms")),
                () => Assert.IsTrue(agentAttributes.ContainsKey("response.status")),
                () => Assert.IsTrue(agentAttributes.ContainsKey("http.statusCode")),
                () => Assert.IsTrue(agentAttributes.ContainsKey("request.parameters.requestParameterKey")),
                () => Assert.IsTrue(agentAttributes.ContainsKey("host.displayName")),
                () => Assert.IsTrue(userAttributes.ContainsKey("userAttributeKey")),
                () => Assert.IsTrue(userAttributes.ContainsKey("userErrorAttributeKey"))
            );
        }

        [TestCase(null, null, ExpectedResult = "coconut")]
        [TestCase("blandAmericanCoconut", null, ExpectedResult = "blandAmericanCoconut")]
        [TestCase("blandAmericanCoconut", "vietnameseCoconut", ExpectedResult = "vietnameseCoconut")]
        [TestCase(null, "vietnameseCoconut", ExpectedResult = "vietnameseCoconut")]
        public object HostDisplayName_WithLocalConfigurationAndEnvironmentVariableSet(string localConfigurationValue, string environmentVariableValue)
        {
            // ARRANGE
            var environment = Mock.Create<IEnvironment>();
            var processStatic = Mock.Create<IProcessStatic>();
            var httpRuntimeStatic = Mock.Create<IHttpRuntimeStatic>();
            var configurationManagerStatic = Mock.Create<IConfigurationManagerStatic>();
            var localConfig = new configuration();
            var serverConfig = new ServerConfiguration();
            var runTimeConfig = new RunTimeConfiguration();
            var securityPoliciesConfiguration = new SecurityPoliciesConfiguration();
            var dnsStatic = Mock.Create<IDnsStatic>();

            _configuration = new TestableDefaultConfiguration(environment, localConfig, serverConfig, runTimeConfig, securityPoliciesConfiguration, processStatic, httpRuntimeStatic, configurationManagerStatic, dnsStatic);
            Mock.Arrange(() => _configurationService.Configuration).Returns(_configuration);

            Mock.Arrange(() => dnsStatic.GetHostName()).Returns("coconut");
            Mock.Arrange(() => environment.GetEnvironmentVariable("NEW_RELIC_PROCESS_HOST_DISPLAY_NAME")).Returns(environmentVariableValue);
            localConfig.processHost.displayName = localConfigurationValue;

            EventBus<ConfigurationUpdatedEvent>.Publish(new ConfigurationUpdatedEvent(_configuration, ConfigurationUpdateSource.Unknown));

            var transactionAttributeMaker = new TransactionAttributeMaker(_configurationService, _attribDefSvc);

            var timer = Mock.Create<ITimer>();
            var expectedStartTime = DateTime.Now;
            var expectedDuration = TimeSpan.FromMilliseconds(500);
            Mock.Arrange(() => timer.Duration).Returns(expectedDuration);

            var priority = 0.5f;
            var internalTransaction = new Transaction(_configuration, TransactionName.ForOtherTransaction("transactionCategory", "transactionName"), timer, expectedStartTime, Mock.Create<ICallStackManager>(), _databaseService, priority, Mock.Create<IDatabaseStatementParser>(), _distributedTracePayloadHandler, _errorService, _attribDefs);
            var immutableTransaction = internalTransaction.ConvertToImmutableTransaction();
            var transactionMetricName = new TransactionMetricName("WebTransaction", "TransactionName");
            var apdexT = null as TimeSpan?;
            var totalTime = TimeSpan.FromSeconds(1);
            var txStats = new TransactionMetricStatsCollection(new TransactionMetricName("WebTransaction", "myTx"));

            // ACT
            var attributes = transactionAttributeMaker.GetAttributes(immutableTransaction, transactionMetricName, apdexT, totalTime, txStats);

            // ACQUIRE
            return GetAttributeValue(attributes, "host.displayName");
        }

        #endregion GetUserAndAgentAttributes

        #region ResponseTime vs TransactionDuration tests

        [Test]
        public void ShouldUseResponseTimeForAttributes()
        {
            var transaction = new ImmutableTransactionBuilder()
                .IsWebTransaction("category", "name")
                .WithDuration(TimeSpan.FromSeconds(10))
                .WithResponseTime(TimeSpan.FromSeconds(5))
                .Build();

            var transactionMetricName = new TransactionMetricName("WebTransaction", "category/name");
            var transactionMetricStatsCollection = new TransactionMetricStatsCollection(transactionMetricName);

            var attributes = _transactionAttributeMaker.GetAttributes(transaction, transactionMetricName, apdexT: TimeSpan.FromSeconds(7), totalTime: TimeSpan.FromSeconds(10), txStats: transactionMetricStatsCollection);

            var expectedResponseTimeInSeconds = TimeSpan.FromSeconds(5).TotalSeconds;

            NrAssert.Multiple
            (
                () => Assert.AreEqual(expectedResponseTimeInSeconds, GetAttributeValue(attributes,"duration",AttributeClassification.Intrinsics)),
                () => Assert.AreEqual(expectedResponseTimeInSeconds, GetAttributeValue(attributes, "webDuration", AttributeClassification.Intrinsics)),
                () => Assert.AreEqual("S", GetAttributeValue(attributes, "nr.apdexPerfZone", AttributeClassification.Intrinsics))
            );
        }

        [Test]
        public void ShouldUseDurationForAttributes()
        {
            var transaction = new ImmutableTransactionBuilder()
                .IsOtherTransaction("category", "name")
                .WithDuration(TimeSpan.FromSeconds(10))
                .WithResponseTime(TimeSpan.FromSeconds(5))
                .Build();

            var transactionMetricName = new TransactionMetricName("OtherTransaction", "category/name");
            var transactionMetricStatsCollection = new TransactionMetricStatsCollection(transactionMetricName);

            var attributes = _transactionAttributeMaker.GetAttributes(transaction, transactionMetricName, apdexT: TimeSpan.FromSeconds(7), totalTime: TimeSpan.FromSeconds(10), txStats: transactionMetricStatsCollection);

            var expectedResponseTimeInSeconds = TimeSpan.FromSeconds(10).TotalSeconds;

            NrAssert.Multiple
            (
                () => Assert.AreEqual(expectedResponseTimeInSeconds, GetAttributeValue(attributes, "duration", AttributeClassification.Intrinsics)),
                //This is not a web transction
                () => Assert.AreEqual(null, GetAttributeValue(attributes, "webDuration", AttributeClassification.Intrinsics)),
                () => Assert.AreEqual("T", GetAttributeValue(attributes, "nr.apdexPerfZone", AttributeClassification.Intrinsics))
            );
        }

        #endregion

        private ErrorData MakeErrorData(bool isErrorExpected = false)
        {
            return new ErrorData("message", "type", "stacktrace", DateTime.UtcNow, new ReadOnlyDictionary<string, object>(new Dictionary<string, object>() { { "userErrorAttributeKey", "userErrorAttributeValue" } }), isErrorExpected, null);
        }

        private void AssertAttributeShouldBeAvailableFor(IAttributeValueCollection attribValues, string attribName, params AttributeDestinations[] destinations)
        {
            var notAvailableFor = new List<AttributeDestinations>();
            foreach(var destination in destinations)
            {
                var filteredAttribs = new AttributeValueCollection(attribValues, destination);
                var values = filteredAttribs.ToDictionary();

                if(!values.ContainsKey(attribName))
                {
                    notAvailableFor.Add(destination);
                }
            }

            if (notAvailableFor.Any())
            {
                throw new TestFailureException($"attrib '{attribName}' should have been available for {string.Join(", ", notAvailableFor)} but was not");
            }
        }

        private bool DoAttributesContain(IAttributeValueCollection attributes, string attribName)
        {
            var attribValues = attributes.GetAttributeValues(AttributeClassification.Intrinsics)
               .Union(attributes.GetAttributeValues(AttributeClassification.AgentAttributes))
               .Union(attributes.GetAttributeValues(AttributeClassification.UserAttributes));


            return attribValues.Any(x => x.AttributeDefinition.Name == attribName);
        }

        private bool DoAttributesContain(IAttributeValueCollection attributes, string attribName, AttributeClassification classification)
        {
            var attribValues = attributes.GetAttributeValues(classification);

            return attribValues.Any(x => x.AttributeDefinition.Name == attribName);
        }

        private bool DoAttributesContain(IAttributeValueCollection attributes, string attribName, AttributeDestinations destinations)
        {
            var attribValues = attributes.GetAttributeValues(AttributeClassification.Intrinsics)
               .Union(attributes.GetAttributeValues(AttributeClassification.AgentAttributes))
               .Union(attributes.GetAttributeValues(AttributeClassification.UserAttributes));


            return attribValues
                .Where(x => x.AttributeDefinition.IsAvailableForAny(destinations))
                .Any(x => x.AttributeDefinition.Name == attribName);
        }

        private bool DoAttributesContain(IAttributeValueCollection attributes, string attribName, AttributeDestinations destinations, AttributeClassification classification)
        {
            var attribValues = attributes.GetAttributeValues(classification);

            return attribValues
               .Where(x => x.AttributeDefinition.IsAvailableForAny(destinations))
               .Any(x => x.AttributeDefinition.Name == attribName);
        }

        private object GetCount(IAttributeValueCollection attributes)
        {
            var attribValues = attributes.GetAttributeValues(AttributeClassification.Intrinsics)
                .Union(attributes.GetAttributeValues(AttributeClassification.AgentAttributes))
                .Union(attributes.GetAttributeValues(AttributeClassification.UserAttributes));

            return attribValues.Count();

        }

        private object GetAttributeValue(IAttributeValueCollection attributes, string attribName, AttributeDestinations? destinations = null)
        {
            var attribValues = attributes.GetAttributeValues(AttributeClassification.Intrinsics)
                .Union(attributes.GetAttributeValues(AttributeClassification.AgentAttributes))
                .Union(attributes.GetAttributeValues(AttributeClassification.UserAttributes));

            return attribValues
                .Where(x => destinations == null || x.AttributeDefinition.IsAvailableForAny(destinations.Value))
                .Where(x => x.AttributeDefinition.Name == attribName)
                .Select(x => x.Value)
                .FirstOrDefault();
        }

        private object GetAttributeValue(IAttributeValueCollection attributes, string attribName, AttributeClassification classification, AttributeDestinations? destinations = null)
        {
            var attribValues = attributes.GetAttributeValues(classification);
               
            return attribValues
                .Where(x => destinations == null || x.AttributeDefinition.IsAvailableForAny(destinations.Value))
                .Where(x => x.AttributeDefinition.Name == attribName)
                .Select(x => x.Value)
                .FirstOrDefault();
        }

    }
}

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
using NewRelic.Agent.Core.Metrics;
using NewRelic.Agent.Core.Segments;
using NewRelic.Agent.Core.Time;
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
        private IBootstrapConfiguration _bootstrapConfiguration;

        private IEnvironment _environment;
        private IHttpRuntimeStatic _httpRuntimeStatic;
        private IProcessStatic _processStatic;
        private IConfigurationManagerStatic _configurationManagerStatic;
        private IDnsStatic _dnsStatic;

        private ConfigurationAutoResponder _configAutoResponder;

        private void UpdateConfiguration()
        {
            _configuration = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfiguration, _securityPoliciesConfiguration, _bootstrapConfiguration, _processStatic, _httpRuntimeStatic, _configurationManagerStatic, _dnsStatic);
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
            _bootstrapConfiguration = Mock.Create<IBootstrapConfiguration>();

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
            _databaseService.Dispose();
            _attribDefSvc.Dispose();
        }

        #region GetAttributes

        [Test]
        public void GetAttributes_ReturnsAllAttributesCreatedByTransactionAttributeMaker()
        {
            // ARRANGE
            var timer = Mock.Create<ISimpleTimer>();
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
                () => Assert.That(transactionAttributes.Count(), Is.EqualTo(12)),  // Assert that only these attributes are generated
                () => Assert.That(transactionAttributes["type"], Is.EqualTo("Transaction")),
                () => Assert.That(transactionAttributes["timestamp"], Is.EqualTo(expectedStartTime.ToUnixTimeMilliseconds())),
                () => Assert.That(transactionAttributes["name"], Is.EqualTo("WebTransaction/TransactionName")),
                () => Assert.That(transactionAttributes["transactionName"], Is.EqualTo("WebTransaction/TransactionName")),
                () => Assert.That(transactionAttributes["guid"], Is.EqualTo(immutableTransaction.Guid)),
                () => Assert.That(transactionAttributes["duration"], Is.EqualTo(0.5f)),
                () => Assert.That(transactionAttributes["totalTime"], Is.EqualTo(1)),
                () => Assert.That(transactionAttributes["nr.tripId"], Is.Not.Null),
                () => Assert.That(transactionAttributes["trip_id"], Is.Not.Null),
                () => Assert.That(transactionAttributes["request.uri"], Is.EqualTo("/Unknown")),
                () => Assert.That(transactionAttributes.Keys, Does.Contain("host.displayName"))
            );
        }

        [Test]
        public void GetAttributes_ReturnsOneDatabase()
        {
            // ARRANGE
            var timer = Mock.Create<ISimpleTimer>();
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
                () => Assert.That(transactionAttributes.Count(), Is.EqualTo(14)),  // Assert that only these attributes are generated
                () => Assert.That(transactionAttributes["type"], Is.EqualTo("Transaction")),
                () => Assert.That(transactionAttributes["timestamp"], Is.EqualTo(expectedStartTime.ToUnixTimeMilliseconds())),
                () => Assert.That(transactionAttributes["name"], Is.EqualTo("WebTransaction/TransactionName")),
                () => Assert.That(transactionAttributes["transactionName"], Is.EqualTo("WebTransaction/TransactionName")),
                () => Assert.That(transactionAttributes["guid"], Is.EqualTo(immutableTransaction.Guid)),
                () => Assert.That(transactionAttributes["duration"], Is.EqualTo(0.5f)),
                () => Assert.That(transactionAttributes["totalTime"], Is.EqualTo(1)),
                () => Assert.That(transactionAttributes["nr.tripId"], Is.Not.Null),
                () => Assert.That(transactionAttributes["databaseDuration"], Is.Not.Null),
                () => Assert.That(transactionAttributes["databaseDuration"], Is.EqualTo(5)),
                () => Assert.That(transactionAttributes["databaseCallCount"], Is.Not.Null),
                () => Assert.That(transactionAttributes["databaseCallCount"], Is.EqualTo(1)),
                () => Assert.That(transactionAttributes["request.uri"], Is.EqualTo("/Unknown")),
                () => Assert.That(transactionAttributes.Keys, Does.Contain("host.displayName"))
            );
        }

        [Test]
        public void GetAttributes_ReturnsMultipleDatabase()
        {
            // ARRANGE
            var timer = Mock.Create<ISimpleTimer>();
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
                () => Assert.That(transactionAttributes, Has.Count.EqualTo(14)),  // Assert that only these attributes are generated
                () => Assert.That(transactionAttributes["type"], Is.EqualTo("Transaction")),
                () => Assert.That(transactionAttributes["timestamp"], Is.EqualTo(expectedStartTime.ToUnixTimeMilliseconds())),
                () => Assert.That(transactionAttributes["name"], Is.EqualTo("WebTransaction/TransactionName")),
                () => Assert.That(transactionAttributes["transactionName"], Is.EqualTo("WebTransaction/TransactionName")),
                () => Assert.That(transactionAttributes["guid"], Is.EqualTo(immutableTransaction.Guid)),
                () => Assert.That(transactionAttributes["duration"], Is.EqualTo(0.5f)),
                () => Assert.That(transactionAttributes["totalTime"], Is.EqualTo(1)),
                () => Assert.That(transactionAttributes["nr.tripId"], Is.Not.Null),
                () => Assert.That(transactionAttributes["databaseDuration"], Is.Not.Null),
                () => Assert.That(transactionAttributes["databaseDuration"], Is.EqualTo(11)),
                () => Assert.That(transactionAttributes["databaseCallCount"], Is.Not.Null),
                () => Assert.That(transactionAttributes["databaseCallCount"], Is.EqualTo(3)),
                () => Assert.That(transactionAttributes["request.uri"], Is.EqualTo("/Unknown")),
                () => Assert.That(transactionAttributes.Keys, Does.Contain("host.displayName"))
            );
        }

        [Test]
        public void GetAttributes_ReturnsOneExternal()
        {
            // ARRANGE
            var timer = Mock.Create<ISimpleTimer>();
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
                () => Assert.That(transactionAttributes, Has.Count.EqualTo(14)),  // Assert that only these attributes are generated
                () => Assert.That(transactionAttributes["type"], Is.EqualTo("Transaction")),
                () => Assert.That(transactionAttributes["timestamp"], Is.EqualTo(expectedStartTime.ToUnixTimeMilliseconds())),
                () => Assert.That(transactionAttributes["name"], Is.EqualTo("WebTransaction/TransactionName")),
                () => Assert.That(transactionAttributes["transactionName"], Is.EqualTo("WebTransaction/TransactionName")),
                () => Assert.That(transactionAttributes["guid"], Is.EqualTo(immutableTransaction.Guid)),
                () => Assert.That(transactionAttributes["duration"], Is.EqualTo(0.5f)),
                () => Assert.That(transactionAttributes["totalTime"], Is.EqualTo(1)),
                () => Assert.That(transactionAttributes["nr.tripId"], Is.Not.Null),
                () => Assert.That(transactionAttributes["externalDuration"], Is.Not.Null),
                () => Assert.That(transactionAttributes["externalDuration"], Is.EqualTo(5)),
                () => Assert.That(transactionAttributes["externalCallCount"], Is.Not.Null),
                () => Assert.That(transactionAttributes["externalCallCount"], Is.EqualTo(1)),
                () => Assert.That(transactionAttributes["request.uri"], Is.EqualTo("/Unknown")),
                () => Assert.That(transactionAttributes.Keys, Does.Contain("host.displayName"))
            );
        }

        [Test]
        public void GetAttributes_ReturnsMultipleExternal()
        {
            // ARRANGE
            var timer = Mock.Create<ISimpleTimer>();
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
                () => Assert.That(transactionAttributes, Has.Count.EqualTo(14)),  // Assert that only these attributes are generated
                () => Assert.That(transactionAttributes["type"], Is.EqualTo("Transaction")),
                () => Assert.That(transactionAttributes["timestamp"], Is.EqualTo(expectedStartTime.ToUnixTimeMilliseconds())),
                () => Assert.That(transactionAttributes["name"], Is.EqualTo("WebTransaction/TransactionName")),
                () => Assert.That(transactionAttributes["transactionName"], Is.EqualTo("WebTransaction/TransactionName")),
                () => Assert.That(transactionAttributes["guid"], Is.EqualTo(immutableTransaction.Guid)),
                () => Assert.That(transactionAttributes["duration"], Is.EqualTo(0.5f)),
                () => Assert.That(transactionAttributes["totalTime"], Is.EqualTo(1)),
                () => Assert.That(transactionAttributes["nr.tripId"], Is.Not.Null),
                () => Assert.That(transactionAttributes["externalDuration"], Is.Not.Null),
                () => Assert.That(transactionAttributes["externalDuration"], Is.EqualTo(11)),
                () => Assert.That(transactionAttributes["externalCallCount"], Is.Not.Null),
                () => Assert.That(transactionAttributes["externalCallCount"], Is.EqualTo(3)),
                () => Assert.That(transactionAttributes["request.uri"], Is.EqualTo("/Unknown")),
                () => Assert.That(transactionAttributes.Keys, Does.Contain("host.displayName"))
            );
        }

        [Test]
        public void GetAttributes_ReturnsAllAttributesThatHaveValues()
        {
            // ARRANGE
            var timer = Mock.Create<ISimpleTimer>();
            var expectedStartTime = DateTime.Now;
            var expectedDuration = TimeSpan.FromMilliseconds(500);
            Mock.Arrange(() => timer.Duration).Returns(expectedDuration);
            var transactionMetricName = new TransactionMetricName("WebTransaction", "TransactionName");
            var apdexT = TimeSpan.FromSeconds(2);

            var priority = 0.5f;
            var transaction = new Transaction(_configuration, TransactionName.ForWebTransaction("transactionCategory", "transactionName"), timer, expectedStartTime, Mock.Create<ICallStackManager>(), _databaseService, priority, Mock.Create<IDatabaseStatementParser>(), _distributedTracePayloadHandler, _errorService, _attribDefs);
            transaction.AddCustomAttribute("userAttributeKey", "userAttributeValue");
            transaction.AddLambdaAttribute("lambdaAttributeKey", "lambdaAttributeValue");
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
                () => Assert.That(GetCount(transactionAttributes), Is.EqualTo(46)),  // Assert that only these attributes are generated
                () => Assert.That(GetAttributeValue(attributes, "type", AttributeDestinations.TransactionEvent), Is.EqualTo("Transaction")),
                () => Assert.That(GetAttributeValue(attributes, "type", AttributeDestinations.ErrorEvent), Is.EqualTo("TransactionError")),
                () => Assert.That(GetAttributeValue(attributes, "timestamp", AttributeDestinations.TransactionEvent), Is.EqualTo(expectedStartTime.ToUnixTimeMilliseconds())),
                () => Assert.That(GetAttributeValue(transactionAttributes, "timestamp", AttributeDestinations.ErrorEvent), Is.EqualTo(immutableTransaction.TransactionMetadata.ReadOnlyTransactionErrorState.ErrorData.NoticedAt.ToUnixTimeMilliseconds())),
                () => Assert.That(GetAttributeValue(transactionAttributes, "name"), Is.EqualTo("WebTransaction/TransactionName")),
                () => Assert.That(GetAttributeValue(transactionAttributes, "transactionName"), Is.EqualTo("WebTransaction/TransactionName")),
                () => Assert.That(GetAttributeValue(transactionAttributes, "guid"), Is.EqualTo(immutableTransaction.Guid)),
                () => Assert.That(GetAttributeValue(transactionAttributes, "nr.guid"), Is.EqualTo(immutableTransaction.Guid)),
                () => Assert.That(GetAttributeValue(transactionAttributes, "duration"), Is.EqualTo(0.5f)),
                () => Assert.That(GetAttributeValue(transactionAttributes, "totalTime"), Is.EqualTo(1)),
                () => Assert.That(GetAttributeValue(transactionAttributes, "webDuration"), Is.EqualTo(0.5)),
                () => Assert.That(GetAttributeValue(transactionAttributes, "queueDuration"), Is.EqualTo(1)),
                () => Assert.That(GetAttributeValue(transactionAttributes, "externalDuration"), Is.EqualTo(2)),
                () => Assert.That(DoAttributesContain(transactionAttributes, "externalCallCount"), Is.True),
                () => Assert.That(DoAttributesContain(transactionAttributes, "nr.apdexPerfZone"), Is.True),
                () => Assert.That(GetAttributeValue(transactionAttributes, "original_url"), Is.EqualTo("originalUri")),
                () => Assert.That(GetAttributeValue(transactionAttributes, "request.uri"), Is.EqualTo("uri")),
                () => Assert.That(GetAttributeValue(transactionAttributes, "request.referer"), Is.EqualTo("referrerUri")),
                () => Assert.That(GetAttributeValue(transactionAttributes, "queue_wait_time_ms"), Is.EqualTo("1000")),
                () => Assert.That(GetAttributeValue(transactionAttributes, "response.status"), Is.EqualTo("400")),
                () => Assert.That(GetAttributeValue(transactionAttributes, "http.statusCode"), Is.EqualTo(400)),
                () => Assert.That(GetAttributeValue(transactionAttributes, "request.parameters.requestParameterKey"), Is.EqualTo("requestParameterValue")),
                () => Assert.That(GetAttributeValue(transactionAttributes, "userAttributeKey"), Is.EqualTo("userAttributeValue")),
                () => Assert.That(GetAttributeValue(transactionAttributes, "lambdaAttributeKey"), Is.EqualTo("lambdaAttributeValue")),
                () => Assert.That(GetAttributeValue(transactionAttributes, "client_cross_process_id"), Is.EqualTo("referrerProcessId")),
                () => Assert.That(GetAttributeValue(transactionAttributes, "trip_id"), Is.EqualTo("referrerTripId")),
                () => Assert.That(GetAttributeValue(transactionAttributes, "nr.tripId"), Is.EqualTo("referrerTripId")),
                () => Assert.That(GetAttributeValue(transactionAttributes, "path_hash"), Is.EqualTo("pathHash2")),
                () => Assert.That(GetAttributeValue(transactionAttributes, "nr.pathHash"), Is.EqualTo("pathHash2")),
                () => Assert.That(GetAttributeValue(transactionAttributes, "nr.referringPathHash"), Is.EqualTo("referringPathHash")),
                () => Assert.That(GetAttributeValue(transactionAttributes, "referring_transaction_guid"), Is.EqualTo("referringTransactionGuid")),
                () => Assert.That(GetAttributeValue(transactionAttributes, "nr.referringTransactionGuid"), Is.EqualTo("referringTransactionGuid")),
                () => Assert.That(GetAttributeValue(transactionAttributes, "nr.alternatePathHashes"), Is.EqualTo("pathHash")),
                () => Assert.That(GetAttributeValue(transactionAttributes, "error.class"), Is.EqualTo("400")),
                () => Assert.That(GetAttributeValue(transactionAttributes, "errorType"), Is.EqualTo("400")),
#if NET
                () => Assert.That(GetAttributeValue(transactionAttributes, "errorMessage"), Is.EqualTo("400")),
                () => Assert.That(GetAttributeValue(transactionAttributes, "error.message"), Is.EqualTo("400")),
#else
                () => Assert.That(GetAttributeValue(transactionAttributes, "errorMessage"), Is.EqualTo("Bad Request")),
                () => Assert.That(GetAttributeValue(transactionAttributes, "error.message"), Is.EqualTo("Bad Request")),
#endif
                () => Assert.That(GetAttributeValue(transactionAttributes, "error"), Is.EqualTo(true)),
                () => Assert.That(DoAttributesContain(transactionAttributes, "host.displayName"), Is.True),
                () => Assert.That(GetAttributeValue(transactionAttributes, "request.headers.key1"), Is.EqualTo("value1")),
                () => Assert.That(GetAttributeValue(transactionAttributes, "request.headers.key2"), Is.EqualTo("value2")),
                () => Assert.That(GetAttributeValue(transactionAttributes, "request.headers.key3"), Is.EqualTo("")),
                () => Assert.That(GetAttributeValue(transactionAttributes, "request.headers.key4"), Is.EqualTo("value4")),
                () => Assert.That(GetAttributeValue(transactionAttributes, "request.headers.referer"), Is.EqualTo("/index.html")), //test to make sure query string is removed.
                () => Assert.That(GetAttributeValue(transactionAttributes, "request.headers.location"), Is.EqualTo("/index.html")), //test to make sure query string is removed.
                () => Assert.That(GetAttributeValue(transactionAttributes, "request.headers.refresh"), Is.EqualTo("/index.html")) //test to make sure query string is removed.
            );
        }

        [Test]
        public void GetAttributes_SetRequestHeaders_HighSecurityModeEnabled()
        {
            // ARRANGE
            _localConfig.highSecurity.enabled = true;
            UpdateConfiguration();

            var timer = Mock.Create<ISimpleTimer>();
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
                () => Assert.That(GetCount(transactionAttributes), Is.EqualTo(16)),  // Assert that only these attributes are generated

                () => Assert.That(DoAttributesContain(transactionAttributes, "request.headers.key1"), Is.False),
                () => Assert.That(DoAttributesContain(transactionAttributes, "request.headers.key2"), Is.False),
                () => Assert.That(DoAttributesContain(transactionAttributes, "request.headers.key3"), Is.False),
                () => Assert.That(DoAttributesContain(transactionAttributes, "request.headers.key4"), Is.False)
            );
        }

        [Test]
        public void GetAttributes_ReturnsCatAttsWithoutCrossAppId()
        {
            // ARRANGE
            var timer = Mock.Create<ISimpleTimer>();
            var expectedStartTime = DateTime.Now;
            var expectedDuration = TimeSpan.FromMilliseconds(500);
            Mock.Arrange(() => timer.Duration).Returns(expectedDuration);
            var transactionMetricName = new TransactionMetricName("WebTransaction", "TransactionName");
            var apdexT = TimeSpan.FromSeconds(2);

            var priority = 0.5f;
            var transaction = new Transaction(_configuration, TransactionName.ForWebTransaction("transactionCategory", "transactionName"), timer, expectedStartTime, Mock.Create<ICallStackManager>(), _databaseService, priority, Mock.Create<IDatabaseStatementParser>(), _distributedTracePayloadHandler, _errorService, _attribDefs);
            transaction.SetRequestParameters(new[] { new KeyValuePair<string, string>("requestParameterKey", "requestParameterValue") });
            transaction.AddCustomAttribute("userAttributeKey", "userAttributeValue");
            transaction.AddLambdaAttribute("lambdaAttributeKey", "lambdaAttributeValue");
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
                () => Assert.That(GetCount(attributes), Is.EqualTo(29)),  // Assert that only these attributes are generated
                () => Assert.That(GetAttributeValue(transactionAttributes, "type", AttributeDestinations.TransactionEvent), Is.EqualTo("Transaction")),
                () => Assert.That(GetAttributeValue(transactionAttributes, "timestamp", AttributeDestinations.TransactionEvent), Is.EqualTo(expectedStartTime.ToUnixTimeMilliseconds())),
                () => Assert.That(GetAttributeValue(transactionAttributes, "name"), Is.EqualTo("WebTransaction/TransactionName")),
                () => Assert.That(GetAttributeValue(transactionAttributes, "transactionName"), Is.EqualTo("WebTransaction/TransactionName")),
                () => Assert.That(GetAttributeValue(transactionAttributes, "guid"), Is.EqualTo(immutableTransaction.Guid)),
                () => Assert.That(GetAttributeValue(transactionAttributes, "nr.guid"), Is.EqualTo(immutableTransaction.Guid)),
                () => Assert.That(GetAttributeValue(transactionAttributes, "duration"), Is.EqualTo(0.5f)),
                () => Assert.That(GetAttributeValue(transactionAttributes, "totalTime"), Is.EqualTo(1)),
                () => Assert.That(GetAttributeValue(transactionAttributes, "webDuration"), Is.EqualTo(0.5)),
                () => Assert.That(GetAttributeValue(transactionAttributes, "queueDuration"), Is.EqualTo(1)),
                () => Assert.That(GetAttributeValue(transactionAttributes, "externalDuration"), Is.EqualTo(2)),
                () => Assert.That(DoAttributesContain(transactionAttributes, "externalCallCount"), Is.True),
                () => Assert.That(DoAttributesContain(transactionAttributes, "nr.apdexPerfZone"), Is.True),
                () => Assert.That(GetAttributeValue(transactionAttributes, "original_url"), Is.EqualTo("originalUri")),
                () => Assert.That(GetAttributeValue(transactionAttributes, "request.uri"), Is.EqualTo("uri")),
                () => Assert.That(GetAttributeValue(transactionAttributes, "request.referer"), Is.EqualTo("referrerUri")),
                () => Assert.That(GetAttributeValue(transactionAttributes, "queue_wait_time_ms"), Is.EqualTo("1000")),
                () => Assert.That(GetAttributeValue(transactionAttributes, "response.status"), Is.EqualTo("200")),
                () => Assert.That(GetAttributeValue(transactionAttributes, "http.statusCode"), Is.EqualTo(200)),
                () => Assert.That(GetAttributeValue(transactionAttributes, "request.parameters.requestParameterKey"), Is.EqualTo("requestParameterValue")),
                () => Assert.That(GetAttributeValue(transactionAttributes, "userAttributeKey"), Is.EqualTo("userAttributeValue")),
                () => Assert.That(GetAttributeValue(transactionAttributes, "trip_id"), Is.EqualTo(tripId)),
                () => Assert.That(GetAttributeValue(transactionAttributes, "nr.tripId"), Is.EqualTo(tripId)),
                () => Assert.That(GetAttributeValue(transactionAttributes, "path_hash"), Is.EqualTo("pathHash2")),
                () => Assert.That(GetAttributeValue(transactionAttributes, "nr.pathHash"), Is.EqualTo("pathHash2")),
                () => Assert.That(GetAttributeValue(transactionAttributes, "nr.alternatePathHashes"), Is.EqualTo("pathHash")),
                () => Assert.That(DoAttributesContain(transactionAttributes, "host.displayName"), Is.True),
                () => Assert.That(GetAttributeValue(transactionAttributes, "lambdaAttributeKey"), Is.EqualTo("lambdaAttributeValue"))
            );
        }

        [Test]
        public void GetAttributes_DoesNotIncludeOriginalUri_IfSameValueAsUei()
        {
            // ARRANGE
            var timer = Mock.Create<ISimpleTimer>();
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
            Assert.That(DoAttributesContain(transactionAttributes, "originalUri"), Is.False);
        }

        [Test]
        public void GetAttributes_SendsAttributesToCorrectLocations()
        {
            // ARRANGE
            var timer = Mock.Create<ISimpleTimer>();
            var expectedStartTime = DateTime.Now;
            var expectedDuration = TimeSpan.FromMilliseconds(500);
            Mock.Arrange(() => timer.Duration).Returns(expectedDuration);
            var transactionMetricName = new TransactionMetricName("WebTransaction", "TransactionName");
            var apdexT = TimeSpan.FromSeconds(2);

            var priority = 0.5f;
            var transaction = new Transaction(_configuration, TransactionName.ForWebTransaction("transactionCategory", "transactionName"), timer, expectedStartTime, Mock.Create<ICallStackManager>(), _databaseService, priority, Mock.Create<IDatabaseStatementParser>(), _distributedTracePayloadHandler, _errorService, _attribDefs);
            transaction.SetRequestParameters(new[] { new KeyValuePair<string, string>("requestParameterKey", "requestParameterValue") });
            transaction.AddCustomAttribute("userAttributeKey", "userAttributeValue");
            transaction.AddLambdaAttribute("lambdaAttributeKey", "lambdaAttributeValue");
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
                () => Assert.That(GetCount(attributes), Is.EqualTo(40)),  // Assert that only these attributes are generated
                () => AssertAttributeShouldBeAvailableFor(attributes, "type", AttributeDestinations.TransactionEvent, AttributeDestinations.ErrorEvent),
                () => AssertAttributeShouldBeAvailableFor(attributes, "timestamp", AttributeDestinations.TransactionEvent, AttributeDestinations.CustomEvent, AttributeDestinations.ErrorEvent),
                () => AssertAttributeShouldBeAvailableFor(attributes, "name", AttributeDestinations.TransactionEvent),
                () => AssertAttributeShouldBeAvailableFor(attributes, "transactionName", AttributeDestinations.ErrorEvent),
                () => AssertAttributeShouldBeAvailableFor(attributes, "guid", AttributeDestinations.ErrorEvent, AttributeDestinations.ErrorTrace),
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
                () => AssertAttributeShouldBeAvailableFor(attributes, "host.displayName", AttributeDestinations.TransactionTrace , AttributeDestinations.TransactionEvent , AttributeDestinations.ErrorTrace , AttributeDestinations.ErrorEvent),
                () => AssertAttributeShouldBeAvailableFor(attributes, "lambdaAttributeKey", AttributeDestinations.TransactionEvent, AttributeDestinations.TransactionTrace)
            );
        }

        [Test]
        public void GetAttributes_AssignsCorrectClassificationToAttributes_ExternalOnly()
        {
            // ARRANGE
            var timer = Mock.Create<ISimpleTimer>();
            var expectedStartTime = DateTime.Now;
            var expectedDuration = TimeSpan.FromMilliseconds(500);
            Mock.Arrange(() => timer.Duration).Returns(expectedDuration);
            var transactionMetricName = new TransactionMetricName("WebTransaction", "TransactionName");
            var apdexT = TimeSpan.FromSeconds(2);

            var priority = 0.5f;
            var transaction = new Transaction(_configuration, TransactionName.ForWebTransaction("transactionCategory", "transactionName"), timer, expectedStartTime, Mock.Create<ICallStackManager>(), _databaseService, priority, Mock.Create<IDatabaseStatementParser>(), _distributedTracePayloadHandler, _errorService, _attribDefs);
            transaction.SetRequestParameters(new[] { new KeyValuePair<string, string>("requestParameterKey", "requestParameterValue") });
            transaction.AddCustomAttribute("userAttributeKey", "userAttributeValue");
            transaction.AddLambdaAttribute("lambdaAttributeKey", "lambdaAttributeValue");
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
                () => Assert.That(GetCount(attributes), Is.EqualTo(33)),  // Assert that only these attributes are generated
                () => Assert.That(transactionAttributes, Has.Member("type")),
                () => Assert.That(transactionAttributes, Has.Member("timestamp")),
                () => Assert.That(transactionAttributes, Has.Member("name")),
                () => Assert.That(transactionAttributes, Has.Member("transactionName")),
                () => Assert.That(transactionAttributes, Has.Member("guid")),
                () => Assert.That(transactionAttributes, Has.Member("nr.guid")),
                () => Assert.That(transactionAttributes, Has.Member("duration")),
                () => Assert.That(transactionAttributes, Has.Member("totalTime")),
                () => Assert.That(transactionAttributes, Has.Member("webDuration")),
                () => Assert.That(transactionAttributes, Has.Member("queueDuration")),
                () => Assert.That(transactionAttributes, Has.Member("externalDuration")),
                () => Assert.That(transactionAttributes, Has.Member("externalCallCount")),
                () => Assert.That(transactionAttributes, Has.Member("nr.apdexPerfZone")),
                () => Assert.That(transactionAttributes, Has.Member("original_url")),
                () => Assert.That(transactionAttributes, Has.Member("request.uri")),
                () => Assert.That(transactionAttributes, Has.Member("request.referer")),
                () => Assert.That(transactionAttributes, Has.Member("queue_wait_time_ms")),
                () => Assert.That(transactionAttributes, Has.Member("response.status")),
                () => Assert.That(transactionAttributes, Has.Member("http.statusCode")),
                () => Assert.That(transactionAttributes, Has.Member("request.parameters.requestParameterKey")),
                () => Assert.That(transactionAttributes, Has.Member("host.displayName")),
                () => Assert.That(transactionAttributes, Has.Member("userAttributeKey")),
                () => Assert.That(transactionAttributes, Has.Member("client_cross_process_id")),
                () => Assert.That(transactionAttributes, Has.Member("trip_id")),
                () => Assert.That(transactionAttributes, Has.Member("nr.tripId")),
                () => Assert.That(transactionAttributes, Has.Member("path_hash")),
                () => Assert.That(transactionAttributes, Has.Member("nr.pathHash")),
                () => Assert.That(transactionAttributes, Has.Member("nr.referringPathHash")),
                () => Assert.That(transactionAttributes, Has.Member("referring_transaction_guid")),
                () => Assert.That(transactionAttributes, Has.Member("nr.referringTransactionGuid")),
                () => Assert.That(transactionAttributes, Has.Member("nr.alternatePathHashes")),
                () => Assert.That(transactionAttributes, Has.Member("lambdaAttributeKey"))
            );


        }

        [Test]
        public void GetAttributes_AssignsCorrectClassificationToAttributes_ExternalAndDB()
        {
            // ARRANGE
            var timer = Mock.Create<ISimpleTimer>();
            var expectedStartTime = DateTime.Now;
            var expectedDuration = TimeSpan.FromMilliseconds(500);
            Mock.Arrange(() => timer.Duration).Returns(expectedDuration);
            var transactionMetricName = new TransactionMetricName("WebTransaction", "TransactionName");
            var apdexT = TimeSpan.FromSeconds(2);

            var priority = 0.5f;
            var transaction = new Transaction(_configuration, TransactionName.ForWebTransaction("transactionCategory", "transactionName"), timer, expectedStartTime, Mock.Create<ICallStackManager>(), _databaseService, priority, Mock.Create<IDatabaseStatementParser>(), _distributedTracePayloadHandler, _errorService, _attribDefs);
            transaction.SetRequestParameters(new[] { new KeyValuePair<string, string>("requestParameterKey", "requestParameterValue") });
            transaction.AddCustomAttribute("userAttributeKey", "userAttributeValue");
            transaction.AddLambdaAttribute("lambdaAttributeKey", "lambdaAttributeValue");
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
                () => Assert.That(GetCount(attributes), Is.EqualTo(35)),  // Assert that only these attributes are generated
                () => AssertAttributeShouldBeAvailableFor(attributes, "type", AttributeDestinations.TransactionEvent),
                () => AssertAttributeShouldBeAvailableFor(attributes, "timestamp", AttributeDestinations.TransactionEvent, AttributeDestinations.SpanEvent, AttributeDestinations.CustomEvent),
                () => Assert.That(intrinsicAttributes, Has.Member("name")),
                () => Assert.That(intrinsicAttributes, Has.Member("transactionName")),
                () => Assert.That(intrinsicAttributes, Has.Member("guid")),
                () => Assert.That(intrinsicAttributes, Has.Member("nr.guid")),
                () => Assert.That(intrinsicAttributes, Has.Member("duration")),
                () => Assert.That(intrinsicAttributes, Has.Member("totalTime")),
                () => Assert.That(intrinsicAttributes, Has.Member("webDuration")),
                () => Assert.That(intrinsicAttributes, Has.Member("queueDuration")),
                () => Assert.That(intrinsicAttributes, Has.Member("externalDuration")),
                () => Assert.That(intrinsicAttributes, Has.Member("externalCallCount")),
                () => Assert.That(intrinsicAttributes, Has.Member("databaseDuration")),
                () => Assert.That(intrinsicAttributes, Has.Member("databaseCallCount")),
                () => Assert.That(intrinsicAttributes, Has.Member("nr.apdexPerfZone")),
                () => Assert.That(agentAttributes, Has.Member("original_url")),
                () => Assert.That(agentAttributes, Has.Member("request.referer")),
                () => Assert.That(agentAttributes, Has.Member("queue_wait_time_ms")),
                () => Assert.That(agentAttributes, Has.Member("response.status")),
                () => Assert.That(agentAttributes, Has.Member("http.statusCode")),
                () => Assert.That(agentAttributes, Has.Member("request.parameters.requestParameterKey")),
                () => Assert.That(agentAttributes, Has.Member("host.displayName")),
                () => Assert.That(agentAttributes, Has.Member("lambdaAttributeKey")),
                () => Assert.That(intrinsicAttributes, Has.Member("client_cross_process_id")),
                () => Assert.That(intrinsicAttributes, Has.Member("trip_id")),
                () => Assert.That(intrinsicAttributes, Has.Member("nr.tripId")),
                () => Assert.That(intrinsicAttributes, Has.Member("path_hash")),
                () => Assert.That(intrinsicAttributes, Has.Member("nr.pathHash")),
                () => Assert.That(intrinsicAttributes, Has.Member("nr.referringPathHash")),
                () => Assert.That(intrinsicAttributes, Has.Member("referring_transaction_guid")),
                () => Assert.That(intrinsicAttributes, Has.Member("nr.referringTransactionGuid")),
                () => Assert.That(intrinsicAttributes, Has.Member("nr.alternatePathHashes"))
            );
        }

        [Test]
        public void GetAttributes_DoesNotReturnWebDurationAttribute_IfNonWebTransaction()
        {
            // ARRANGE
            var priority = 0.5f;
            var transactionBuilder = new Transaction(_configuration, TransactionName.ForOtherTransaction("transactionCategory", "transactionName"), Mock.Create<ISimpleTimer>(), DateTime.UtcNow, Mock.Create<ICallStackManager>(), _databaseService, priority, Mock.Create<IDatabaseStatementParser>(), _distributedTracePayloadHandler, _errorService, _attribDefs);
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
            Assert.That(DoAttributesContain(attributes,"webDuration"), Is.False);
        }

        [Test]
        public void GetAttributes_ErrorAttributesNotIncluded_IfErrorCollectorDisabled()
        {
            // ARRANGE
            var timer = Mock.Create<ISimpleTimer>();
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
                () => Assert.That(DoAttributesContain(transactionAttributes, "nr.apdexPerfZone"), Is.True),
                () => Assert.That(DoAttributesContain(transactionAttributes, "error.class"), Is.False),
                () => Assert.That(DoAttributesContain(transactionAttributes, "errorType"), Is.False),
                () => Assert.That(DoAttributesContain(transactionAttributes, "errorMessage"), Is.False),
                () => Assert.That(DoAttributesContain(transactionAttributes, "error.message"), Is.False),
                () => Assert.That(DoAttributesContain(transactionAttributes, "error"), Is.False)
            );
        }

        [Test]
        public void GetAttributes_FalseErrorAttributeIncluded_WithNoError()
        {
            // ARRANGE
            var timer = Mock.Create<ISimpleTimer>();
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
                () => Assert.That(GetAttributeValue(transactionAttributes, "error"), Is.EqualTo(false))
            );
        }

        [TestCase(true)]
        [TestCase(false)]
        public void GetAttributes_ExpecedErrorAttribute_SentToCorrectDestinations(bool isErrorExpected)
        {
            // ARRANGE
            var timer = Mock.Create<ISimpleTimer>();
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
                Assert.That(DoAttributesContain(attributes, "error.expected"), Is.False);
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
                () => Assert.That(intrinsicAttribs.ContainsKey("nr.tripId"), Is.False, "nr.tripId should not have been included"),
                () => Assert.That(intrinsicAttribs.ContainsKey("trip_id"), Is.False, "trip_id should not have been included")
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
                () => Assert.That(GetCount(attributes), Is.EqualTo(27)),  // Assert that only these attributes are generated
                () => Assert.That(intrinsicAttribValues["type"], Is.EqualTo("Transaction")),
                () => Assert.That(intrinsicAttribValues.ContainsKey("timestamp"), Is.True),
                () => Assert.That(intrinsicAttribValues["name"], Is.EqualTo("WebTransaction/TransactionName")),
                () => Assert.That(intrinsicAttribValues["transactionName"], Is.EqualTo("WebTransaction/TransactionName")),
                () => Assert.That(intrinsicAttribValues.ContainsKey("duration"), Is.True),
                () => Assert.That(intrinsicAttribValues["totalTime"], Is.EqualTo(1)),
                () => Assert.That(intrinsicAttribValues.ContainsKey("webDuration"), Is.True),
                () => Assert.That(intrinsicAttribValues.ContainsKey("nr.apdexPerfZone"), Is.True),
                () => Assert.That(agentAttribValues["request.uri"], Is.EqualTo("/Unknown")),
                () => Assert.That(intrinsicAttribValues["traceId"], Is.EqualTo(IncomingTraceId)),
                () => Assert.That(intrinsicAttribValues["parent.type"], Is.EqualTo(IncomingType.ToString())),
                () => Assert.That(intrinsicAttribValues["parent.app"], Is.EqualTo(IncomingAppId)),
                () => Assert.That(intrinsicAttribValues["parent.account"], Is.EqualTo(IncomingAcctId)),
                () => Assert.That(intrinsicAttribValues["parent.transportType"], Is.EqualTo(IncomingTransportType)),
                () => Assert.That(intrinsicAttribValues.ContainsKey("parent.transportDuration"), Is.True),

                () => Assert.That(agentAttribValues["parent.type"], Is.EqualTo(IncomingType.ToString())),
                () => Assert.That(agentAttribValues["parent.app"], Is.EqualTo(IncomingAppId)),
                () => Assert.That(agentAttribValues["parent.account"], Is.EqualTo(IncomingAcctId)),
                () => Assert.That(agentAttribValues["parent.transportType"], Is.EqualTo(IncomingTransportType)),
                () => Assert.That(agentAttribValues.ContainsKey("parent.transportDuration"), Is.True),

                () => Assert.That(intrinsicAttribValues["parentId"], Is.EqualTo(IncomingTransactionId)),
                () => Assert.That(intrinsicAttribValues["guid"], Is.EqualTo(immutableTransaction.Guid)),
                () => Assert.That(intrinsicAttribValues["priority"], Is.EqualTo(IncomingPriority)),
                () => Assert.That(intrinsicAttribValues["sampled"], Is.EqualTo(IncomingSampled)),
                () => Assert.That(intrinsicAttribValues["parentSpanId"], Is.EqualTo(IncomingGuid)),
                () => Assert.That(agentAttribValues.ContainsKey("host.displayName"), Is.True)
            );
        }

        [Test]
        public void GetAttributes_DoesNotReturnDistributedTraceAttrs_IfDistributedTraceEnabledAndDidNotReceivePayload()
        {
            _localConfig.distributedTracing.enabled = true;

            UpdateConfiguration();

            var timer = Mock.Create<ISimpleTimer>();
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
                () => Assert.That(transactionAttributes.ContainsKey("parent.type"), Is.False),
                () => Assert.That(transactionAttributes.ContainsKey("parent.app"), Is.False),
                () => Assert.That(transactionAttributes.ContainsKey("parent.account"), Is.False),
                () => Assert.That(transactionAttributes.ContainsKey("parent.transportType"), Is.False),
                () => Assert.That(transactionAttributes.ContainsKey("parent.transportDuration"), Is.False),
                () => Assert.That(transactionAttributes.ContainsKey("parentId"), Is.False)
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
                () => Assert.That(transactionIntrinsicsAttributes.ContainsKey("parent.type"), Is.False),
                () => Assert.That(transactionIntrinsicsAttributes.ContainsKey("parent.app"), Is.False),
                () => Assert.That(transactionIntrinsicsAttributes.ContainsKey("parent.account"), Is.False),
                () => Assert.That(transactionIntrinsicsAttributes.ContainsKey("parent.transportType"), Is.False),
                () => Assert.That(transactionIntrinsicsAttributes.ContainsKey("parent.transportDuration"), Is.False),
                () => Assert.That(transactionIntrinsicsAttributes.ContainsKey("parentId"), Is.False),
                () => Assert.That(transactionIntrinsicsAttributes.ContainsKey("parentSpanId"), Is.False)
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
                () => Assert.That(GetCount(attributes), Is.EqualTo(27)),  // Assert that only these attributes are generated
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
                () => Assert.That(GetCount(attributes), Is.EqualTo(27)),  // Assert that only these attributes are generated
                () => Assert.That(intrinsicAttributes, Has.Member("type")),
                () => Assert.That(intrinsicAttributes, Has.Member("timestamp")),
                () => Assert.That(intrinsicAttributes, Has.Member("name")),
                () => Assert.That(intrinsicAttributes, Has.Member("transactionName")),
                () => Assert.That(intrinsicAttributes, Has.Member("duration")),
                () => Assert.That(intrinsicAttributes, Has.Member("totalTime")),
                () => Assert.That(intrinsicAttributes, Has.Member("webDuration")),
                () => Assert.That(intrinsicAttributes, Has.Member("nr.apdexPerfZone")),
                () => Assert.That(agentAttributes, Has.Member("request.uri")),
                () => Assert.That(agentAttributes, Has.Member("host.displayName")),
                () => Assert.That(intrinsicAttributes, Has.Member("traceId")),
                () => Assert.That(intrinsicAttributes, Has.Member("parent.type")),
                () => Assert.That(intrinsicAttributes, Has.Member("parent.app")),
                () => Assert.That(intrinsicAttributes, Has.Member("parent.account")),
                () => Assert.That(intrinsicAttributes, Has.Member("parent.transportType")),
                () => Assert.That(intrinsicAttributes, Has.Member("parent.transportDuration")),
                () => Assert.That(intrinsicAttributes, Has.Member(ParentSpanIdAttributeName)),
                () => Assert.That(intrinsicAttributes, Has.Member("guid")),
                () => Assert.That(intrinsicAttributes, Has.Member("priority")),
                () => Assert.That(intrinsicAttributes, Has.Member("sampled")),
                () => Assert.That(intrinsicAttributes, Has.Member("parentId")),
                () => Assert.That(intrinsicAttributes, Has.Member("parentSpanId"))
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
                () => Assert.That(DoAttributesContain(attributes, ParentSpanIdAttributeName, AttributeClassification.Intrinsics), Is.True),
                () => Assert.That(DoAttributesContain(attributes, ParentSpanIdAttributeName, AttributeDestinations.TransactionEvent), Is.True),
                () => Assert.That(GetAttributeValue(attributes,ParentSpanIdAttributeName), Is.EqualTo(IncomingGuid))
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
               () => Assert.That(DoAttributesContain(attributes, ParentSpanIdAttributeName, AttributeClassification.Intrinsics), Is.True),
               () => Assert.That(DoAttributesContain(attributes, ParentSpanIdAttributeName, AttributeDestinations.TransactionEvent), Is.True),
               () => Assert.That(GetAttributeValue(attributes, ParentSpanIdAttributeName), Is.EqualTo(IncomingParentId))
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

            Assert.That(DoAttributesContain(attributes,ParentSpanIdAttributeName), Is.False);
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
            var timer = Mock.Create<ISimpleTimer>();
            var expectedStartTime = DateTime.Now;
            var expectedDuration = TimeSpan.FromMilliseconds(500);
            Mock.Arrange(() => timer.Duration).Returns(expectedDuration);

            var priority = 0.5f;
            var transaction = new Transaction(_configuration, TransactionName.ForWebTransaction("transactionCategory", "transactionName"), timer, expectedStartTime, Mock.Create<ICallStackManager>(), _databaseService, priority, Mock.Create<IDatabaseStatementParser>(), _distributedTracePayloadHandler, _errorService, _attribDefs);
            transaction.SetRequestParameters(new[] { new KeyValuePair<string, string>("requestParameterKey", "requestParameterValue") });
            transaction.AddCustomAttribute("userAttributeKey", "userAttributeValue");
            transaction.AddLambdaAttribute("lambdaAttributeKey", "lambdaAttributeValue");
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
            transaction.TransactionMetadata.SetLlmTransaction(true);
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
                () => Assert.That(GetCount(builderAttributes), Is.EqualTo(12)),  // Assert that only these attributes are generated
                () => Assert.That(txBuilderAttributes["original_url"], Is.EqualTo("originalUri")),
                () => Assert.That(transactionAttributes["request.uri"], Is.EqualTo("uri")),
                () => Assert.That(txBuilderAttributes["request.referer"], Is.EqualTo("referrerUri")),
                () => Assert.That(txBuilderAttributes["queue_wait_time_ms"], Is.EqualTo("1000")),
                () => Assert.That(txBuilderAttributes["response.status"], Is.EqualTo("400")),
                () => Assert.That(txBuilderAttributes["http.statusCode"], Is.EqualTo(400)),
                () => Assert.That(txBuilderAttributes["request.parameters.requestParameterKey"], Is.EqualTo("requestParameterValue")),
                () => Assert.That(txBuilderAttributes["userAttributeKey"], Is.EqualTo("userAttributeValue")),
                () => Assert.That(txBuilderAttributes["userErrorAttributeKey"], Is.EqualTo("userErrorAttributeValue")),
                () => Assert.That(txBuilderAttributes["llm"], Is.EqualTo(true)),
                () => Assert.That(txBuilderAttributes.Keys, Does.Contain("host.displayName")),
                () => Assert.That(txBuilderAttributes["lambdaAttributeKey"], Is.EqualTo("lambdaAttributeValue"))
            );
            NrAssert.Multiple(
                () => Assert.That(GetCount(attributes), Is.EqualTo(12)),  // Assert that only these attributes are generated
                () => Assert.That(transactionAttributes["original_url"], Is.EqualTo("originalUri")),
                () => Assert.That(transactionAttributes["request.uri"], Is.EqualTo("uri")),
                () => Assert.That(transactionAttributes["request.referer"], Is.EqualTo("referrerUri")),
                () => Assert.That(transactionAttributes["queue_wait_time_ms"], Is.EqualTo("1000")),
                () => Assert.That(transactionAttributes["response.status"], Is.EqualTo("400")),
                () => Assert.That(transactionAttributes["http.statusCode"], Is.EqualTo(400)),
                () => Assert.That(transactionAttributes["request.parameters.requestParameterKey"], Is.EqualTo("requestParameterValue")),
                () => Assert.That(transactionAttributes["userAttributeKey"], Is.EqualTo("userAttributeValue")),
                () => Assert.That(transactionAttributes["userErrorAttributeKey"], Is.EqualTo("userErrorAttributeValue")),
                () => Assert.That(transactionAttributes["lambdaAttributeKey"], Is.EqualTo("lambdaAttributeValue")),
                () => Assert.That(transactionAttributes["llm"], Is.EqualTo(true)),
                () => Assert.That(transactionAttributes.Keys, Does.Contain("host.displayName"))
            );
        }

        [Test]
        public void GetUserAndAgentAttributes_ExcludesErrorCustomAttributes_IfErrorCollectorDisabled()
        {
            // ARRANGE
            var timer = Mock.Create<ISimpleTimer>();
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
                () => Assert.That(DoAttributesContain(attributes, "userAttributeKey"), Is.True),
                () => Assert.That(DoAttributesContain(attributes, "userErrorAttributeKey"), Is.False)
            );
        }

        [Test]
        public void GetUserAndAgentAttributes_DoesNotIncludeOriginalUri_IfSameValueAsUei()
        {
            // ARRANGE
            var timer = Mock.Create<ISimpleTimer>();
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

            Assert.Multiple(() =>
            {
                // ASSERT
                Assert.That(txBuilderAttributes.ContainsKey("originalUri"), Is.False);
                Assert.That(transactionAttributes.ContainsKey("originalUri"), Is.False);
            });
        }

        [Test]
        public void GetUserAndAgentAttributes_SendsAttributesToCorrectLocations()
        {
            // ARRANGE
            var timer = Mock.Create<ISimpleTimer>();
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
                () => Assert.That(GetCount(builderAttributes), Is.EqualTo(10)),  // Assert that only these attributes are generated
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
                () => Assert.That(GetCount(attributes), Is.EqualTo(10)),  // Assert that only these attributes are generated
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
            var timer = Mock.Create<ISimpleTimer>();
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
                () => Assert.That(GetCount(attributes), Is.EqualTo(10)),  // Assert that only these attributes are generated
                () => Assert.That(agentAttributes.ContainsKey("original_url"), Is.True),
                () => Assert.That(agentAttributes.ContainsKey("request.uri"), Is.True),
                () => Assert.That(agentAttributes.ContainsKey("request.referer"), Is.True),
                () => Assert.That(agentAttributes.ContainsKey("queue_wait_time_ms"), Is.True),
                () => Assert.That(agentAttributes.ContainsKey("response.status"), Is.True),
                () => Assert.That(agentAttributes.ContainsKey("http.statusCode"), Is.True),
                () => Assert.That(agentAttributes.ContainsKey("request.parameters.requestParameterKey"), Is.True),
                () => Assert.That(agentAttributes.ContainsKey("host.displayName"), Is.True),
                () => Assert.That(userAttributes.ContainsKey("userAttributeKey"), Is.True),
                () => Assert.That(userAttributes.ContainsKey("userErrorAttributeKey"), Is.True)
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
            var bootstrapConfiguration = Mock.Create<IBootstrapConfiguration>();

            _configuration = new TestableDefaultConfiguration(environment, localConfig, serverConfig, runTimeConfig, securityPoliciesConfiguration, bootstrapConfiguration, processStatic, httpRuntimeStatic, configurationManagerStatic, dnsStatic);
            Mock.Arrange(() => _configurationService.Configuration).Returns(_configuration);

            Mock.Arrange(() => dnsStatic.GetHostName()).Returns("coconut");
            Mock.Arrange(() => environment.GetEnvironmentVariable("NEW_RELIC_PROCESS_HOST_DISPLAY_NAME")).Returns(environmentVariableValue);
            localConfig.processHost.displayName = localConfigurationValue;

            EventBus<ConfigurationUpdatedEvent>.Publish(new ConfigurationUpdatedEvent(_configuration, ConfigurationUpdateSource.Unknown));

            var transactionAttributeMaker = new TransactionAttributeMaker(_configurationService, _attribDefSvc);

            var timer = Mock.Create<ISimpleTimer>();
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
                () => Assert.That(GetAttributeValue(attributes,"duration",AttributeClassification.Intrinsics), Is.EqualTo(expectedResponseTimeInSeconds)),
                () => Assert.That(GetAttributeValue(attributes, "webDuration", AttributeClassification.Intrinsics), Is.EqualTo(expectedResponseTimeInSeconds)),
                () => Assert.That(GetAttributeValue(attributes, "nr.apdexPerfZone", AttributeClassification.Intrinsics), Is.EqualTo("S"))
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
                () => Assert.That(GetAttributeValue(attributes, "duration", AttributeClassification.Intrinsics), Is.EqualTo(expectedResponseTimeInSeconds)),
                //This is not a web transction
                () => Assert.That(GetAttributeValue(attributes, "webDuration", AttributeClassification.Intrinsics), Is.EqualTo(null)),
                () => Assert.That(GetAttributeValue(attributes, "nr.apdexPerfZone", AttributeClassification.Intrinsics), Is.EqualTo("T"))
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

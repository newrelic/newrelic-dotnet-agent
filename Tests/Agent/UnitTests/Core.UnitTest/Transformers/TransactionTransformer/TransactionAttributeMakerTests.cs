using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.Aggregators;
using NewRelic.Agent.Core.Attributes;
using NewRelic.Agent.Core.CallStack;
using NewRelic.Agent.Core.Config;
using NewRelic.Agent.Core.Configuration;
using NewRelic.Agent.Core.Configuration.UnitTest;
using NewRelic.Agent.Core.Database;
using NewRelic.Agent.Core.DistributedTracing;
using NewRelic.Agent.Core.Errors;
using NewRelic.Agent.Core.Metric;
using NewRelic.Agent.Core.Segments;
using NewRelic.Agent.Core.Timing;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.WireModels;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Core;
using NewRelic.Core.DistributedTracing;
using NewRelic.SystemInterfaces;
using NewRelic.SystemInterfaces.Web;
using NewRelic.Testing.Assertions;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using Telerik.JustMock;
using Attribute = NewRelic.Agent.Core.Attributes.Attribute;

namespace NewRelic.Agent.Core.Transformers.TransactionTransformer
{
	[TestFixture]
	public class TransactionAttributeMakerTests
	{
		private IConfiguration _configuration;
		private IConfigurationService _configurationService;
		private TransactionAttributeMaker _transactionAttributeMaker;
		private IDatabaseService _databaseService;
		private IErrorService _errorService;
		private IDistributedTracePayloadHandler _distributedTracePayloadHandler;

		[SetUp]
		public void SetUp()
		{
			_configuration = Mock.Create<IConfiguration>();
			_configurationService = Mock.Create<IConfigurationService>();
			Mock.Arrange(() => _configurationService.Configuration).Returns(_configuration);
			_databaseService = new DatabaseService(Mock.Create<ICacheStatsReporter>());
			_transactionAttributeMaker = new TransactionAttributeMaker(_configurationService);
			_errorService = new ErrorService(_configurationService);
			_distributedTracePayloadHandler = Mock.Create<IDistributedTracePayloadHandler>();
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
			var internalTransaction = new Transaction(_configuration, TransactionName.ForOtherTransaction("transactionCategory", "transactionName"), timer, expectedStartTime, Mock.Create<ICallStackManager>(), _databaseService, priority, Mock.Create<IDatabaseStatementParser>(), _distributedTracePayloadHandler, _errorService);
			var immutableTransaction = internalTransaction.ConvertToImmutableTransaction();
			var transactionMetricName = new TransactionMetricName("WebTransaction", "TransactionName");
			var apdexT = null as TimeSpan?;
			var totalTime = TimeSpan.FromSeconds(1);
			var txStats = new TransactionMetricStatsCollection(new TransactionMetricName("WebTransaction", "myTx"));

			// ACT
			var attributes = _transactionAttributeMaker.GetAttributes(immutableTransaction, transactionMetricName, apdexT, totalTime, txStats);

			// ACQUIRE
			var transactionAttributes = attributes.GetIntrinsics()
				.Concat(attributes.GetAgentAttributes())
				.Concat(attributes.GetUserAttributes())
				.ToDictionary(attr => attr.Key, attr => attr.Value);

			// ASSERT
			NrAssert.Multiple(
				() => Assert.AreEqual(10, attributes.Count()),  // Assert that only these attributes are generated
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
			var transaction = new Transaction(_configuration, TransactionName.ForOtherTransaction("transactionCategory", "transactionName"), timer, expectedStartTime, Mock.Create<ICallStackManager>(), _databaseService, priority, Mock.Create<IDatabaseStatementParser>(), _distributedTracePayloadHandler, _errorService);
			var immutableTransaction = transaction.ConvertToImmutableTransaction();
			var transactionMetricName = new TransactionMetricName("WebTransaction", "TransactionName");
			var apdexT = null as TimeSpan?;
			var totalTime = TimeSpan.FromSeconds(1);
			var txStats = new TransactionMetricStatsCollection(new TransactionMetricName("WebTransaction", "myTx"));
			txStats.MergeUnscopedStats(MetricNames.DatastoreAll, MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5)));
			// ACT
			var attributes = _transactionAttributeMaker.GetAttributes(immutableTransaction, transactionMetricName, apdexT, totalTime, txStats);

			// ACQUIRE
			var transactionAttributes = attributes.GetIntrinsics()
				.Concat(attributes.GetAgentAttributes())
				.Concat(attributes.GetUserAttributes())
				.ToDictionary(attr => attr.Key, attr => attr.Value);

			// ASSERT
			NrAssert.Multiple(
				() => Assert.AreEqual(12, attributes.Count()),  // Assert that only these attributes are generated
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
			var transaction = new Transaction(_configuration, TransactionName.ForOtherTransaction("transactionCategory", "transactionName"), timer, expectedStartTime, Mock.Create<ICallStackManager>(), _databaseService, priority, Mock.Create<IDatabaseStatementParser>(), _distributedTracePayloadHandler, _errorService);
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
			var transactionAttributes = attributes.GetIntrinsics()
				.Concat(attributes.GetAgentAttributes())
				.Concat(attributes.GetUserAttributes())
				.ToDictionary(attr => attr.Key, attr => attr.Value);

			// ASSERT
			NrAssert.Multiple(
				() => Assert.AreEqual(12, attributes.Count()),  // Assert that only these attributes are generated
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
			var transaction = new Transaction(_configuration, TransactionName.ForOtherTransaction("transactionCategory", "transactionName"), timer, expectedStartTime, Mock.Create<ICallStackManager>(), _databaseService, priority, Mock.Create<IDatabaseStatementParser>(), _distributedTracePayloadHandler, _errorService);
			var immutableTransaction = transaction.ConvertToImmutableTransaction();
			var transactionMetricName = new TransactionMetricName("WebTransaction", "TransactionName");
			var apdexT = null as TimeSpan?;
			var totalTime = TimeSpan.FromSeconds(1);
			var txStats = new TransactionMetricStatsCollection(new TransactionMetricName("WebTransaction", "myTx"));
			txStats.MergeUnscopedStats(MetricNames.ExternalAll, MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5)));
			// ACT
			var attributes = _transactionAttributeMaker.GetAttributes(immutableTransaction, transactionMetricName, apdexT, totalTime, txStats);

			// ACQUIRE
			var transactionAttributes = attributes.GetIntrinsics()
				.Concat(attributes.GetAgentAttributes())
				.Concat(attributes.GetUserAttributes())
				.ToDictionary(attr => attr.Key, attr => attr.Value);

			// ASSERT
			NrAssert.Multiple(
				() => Assert.AreEqual(12, attributes.Count()),  // Assert that only these attributes are generated
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
			var transaction = new Transaction(_configuration, TransactionName.ForOtherTransaction("transactionCategory", "transactionName"), timer, expectedStartTime, Mock.Create<ICallStackManager>(), _databaseService, priority, Mock.Create<IDatabaseStatementParser>(), _distributedTracePayloadHandler, _errorService);
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
			var transactionAttributes = attributes.GetIntrinsics()
				.Concat(attributes.GetAgentAttributes())
				.Concat(attributes.GetUserAttributes())
				.ToDictionary(attr => attr.Key, attr => attr.Value);

			// ASSERT
			NrAssert.Multiple(
				() => Assert.AreEqual(12, attributes.Count()),  // Assert that only these attributes are generated
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
			var transaction = new Transaction(_configuration, TransactionName.ForWebTransaction("transactionCategory", "transactionName"), timer, expectedStartTime, Mock.Create<ICallStackManager>(), _databaseService, priority, Mock.Create<IDatabaseStatementParser>(), _distributedTracePayloadHandler, _errorService);
			transaction.TransactionMetadata.AddRequestParameter("requestParameterKey", "requestParameterValue");
			transaction.TransactionMetadata.AddUserAttribute("userAttributeKey", "userAttributeValue");
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
			var transactionAttributes = attributes.GetIntrinsics()
				.Concat(attributes.GetAgentAttributes())
				.Concat(attributes.GetUserAttributes())
				.ToList();

			// ASSERT
			NrAssert.Multiple(
				() => Assert.AreEqual(37, attributes.Count()),  // Assert that only these attributes are generated
				() => Assert.AreEqual("Transaction", GetAttributeValue(transactionAttributes, "type", AttributeDestinations.TransactionEvent)),
				() => Assert.AreEqual("TransactionError", GetAttributeValue(transactionAttributes, "type", AttributeDestinations.ErrorEvent)),
				() => Assert.AreEqual(expectedStartTime.ToUnixTimeMilliseconds(), GetAttributeValue(transactionAttributes, "timestamp", AttributeDestinations.TransactionEvent | AttributeDestinations.SpanEvent | AttributeDestinations.CustomEvent)),
				() => Assert.AreEqual(immutableTransaction.TransactionMetadata.ErrorData.NoticedAt.ToUnixTimeMilliseconds(), GetAttributeValue(transactionAttributes, "timestamp", AttributeDestinations.ErrorEvent)),
				() => Assert.AreEqual("WebTransaction/TransactionName", GetAttributeValue(transactionAttributes, "name")),
				() => Assert.AreEqual("WebTransaction/TransactionName", GetAttributeValue(transactionAttributes, "transactionName")),
				() => Assert.AreEqual(immutableTransaction.Guid, GetAttributeValue(transactionAttributes, "nr.guid")),
				() => Assert.AreEqual(0.5f, GetAttributeValue(transactionAttributes, "duration")),
				() => Assert.AreEqual(1, GetAttributeValue(transactionAttributes, "totalTime")),
				() => Assert.AreEqual(0.5, GetAttributeValue(transactionAttributes, "webDuration")),
				() => Assert.AreEqual(1, GetAttributeValue(transactionAttributes, "queueDuration")),
				() => Assert.AreEqual(2, GetAttributeValue(transactionAttributes, "externalDuration")),
				() => Assert.True(DoAttributesContain(transactionAttributes,"externalCallCount")),
				() => Assert.True(DoAttributesContain(transactionAttributes,"nr.apdexPerfZone")),
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
				() => Assert.AreEqual("Bad Request", GetAttributeValue(transactionAttributes, "errorMessage")),
				() => Assert.AreEqual("Bad Request", GetAttributeValue(transactionAttributes, "error.message")),
				() => Assert.AreEqual(true, GetAttributeValue(transactionAttributes, "error")),
				() => Assert.True(DoAttributesContain(transactionAttributes,"host.displayName"))
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
			var transaction = new Transaction(_configuration, TransactionName.ForWebTransaction("transactionCategory", "transactionName"), timer, expectedStartTime, Mock.Create<ICallStackManager>(), _databaseService, priority, Mock.Create<IDatabaseStatementParser>(), _distributedTracePayloadHandler, _errorService);
			transaction.TransactionMetadata.AddRequestParameter("requestParameterKey", "requestParameterValue");
			transaction.TransactionMetadata.AddUserAttribute("userAttributeKey", "userAttributeValue");
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
			var transactionAttributes = attributes.GetIntrinsics()
				.Concat(attributes.GetAgentAttributes())
				.Concat(attributes.GetUserAttributes())
				.ToList();

			var tripId = immutableTransaction.Guid;
			// ASSERT
			NrAssert.Multiple(
				() => Assert.AreEqual(26, attributes.Count()),  // Assert that only these attributes are generated
				() => Assert.AreEqual("Transaction", GetAttributeValue(transactionAttributes, "type", AttributeDestinations.TransactionEvent)),
				() => Assert.AreEqual(expectedStartTime.ToUnixTimeMilliseconds(), GetAttributeValue(transactionAttributes, "timestamp", AttributeDestinations.TransactionEvent | AttributeDestinations.SpanEvent | AttributeDestinations.CustomEvent)),
				() => Assert.AreEqual("WebTransaction/TransactionName", GetAttributeValue(transactionAttributes, "name")),
				() => Assert.AreEqual("WebTransaction/TransactionName", GetAttributeValue(transactionAttributes, "transactionName")),
				() => Assert.AreEqual(immutableTransaction.Guid, GetAttributeValue(transactionAttributes, "nr.guid")),
				() => Assert.AreEqual(0.5f, GetAttributeValue(transactionAttributes, "duration")),
				() => Assert.AreEqual(1, GetAttributeValue(transactionAttributes, "totalTime")),
				() => Assert.AreEqual(0.5, GetAttributeValue(transactionAttributes, "webDuration")),
				() => Assert.AreEqual(1, GetAttributeValue(transactionAttributes, "queueDuration")),
				() => Assert.AreEqual(2, GetAttributeValue(transactionAttributes, "externalDuration")),
				() => Assert.True(DoAttributesContain(transactionAttributes,"externalCallCount")),
				() => Assert.True(DoAttributesContain(transactionAttributes,"nr.apdexPerfZone")),
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
				() => Assert.True(DoAttributesContain(transactionAttributes,"host.displayName"))
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
			var transaction = new Transaction(_configuration, TransactionName.ForWebTransaction("transactionCategory", "transactionName"), timer, expectedStartTime, Mock.Create<ICallStackManager>(), _databaseService, priority, Mock.Create<IDatabaseStatementParser>(), _distributedTracePayloadHandler, _errorService);
			transaction.TransactionMetadata.SetOriginalUri("SameUri");
			transaction.TransactionMetadata.SetUri("SameUri");
			var immutableTransaction = transaction.ConvertToImmutableTransaction();
			var totalTime = TimeSpan.FromSeconds(1);
			var txStats = new TransactionMetricStatsCollection(new TransactionMetricName("WebTransaction", "myTx"));

			// ACT
			var attributes = _transactionAttributeMaker.GetAttributes(immutableTransaction, transactionMetricName, apdexT, totalTime, txStats);

			// ACQUIRE
			var transactionAttributes = attributes.GetIntrinsics()
				.Concat(attributes.GetAgentAttributes())
				.Concat(attributes.GetUserAttributes())
				.ToList();


			// ASSERT
			Assert.False(DoAttributesContain(transactionAttributes,"originalUri"));
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
			var transaction = new Transaction(_configuration, TransactionName.ForWebTransaction("transactionCategory", "transactionName"), timer, expectedStartTime, Mock.Create<ICallStackManager>(), _databaseService, priority, Mock.Create<IDatabaseStatementParser>(), _distributedTracePayloadHandler, _errorService);
			transaction.TransactionMetadata.AddRequestParameter("requestParameterKey", "requestParameterValue");
			transaction.TransactionMetadata.AddUserAttribute("userAttributeKey", "userAttributeValue");
			transaction.TransactionMetadata.AddCustomErrorData(MakeErrorData());
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
			var transactionAttributes = attributes.GetIntrinsics()
				.Concat(attributes.GetAgentAttributes())
				.Concat(attributes.GetUserAttributes())
				.ToList();

			// ASSERT
			NrAssert.Multiple(
				() => Assert.AreEqual(38, attributes.Count()),  // Assert that only these attributes are generated
				() => Assert.True(DoAttributesContain(transactionAttributes, "type", (AttributeDestinations.TransactionEvent))),
				() => Assert.True(DoAttributesContain(transactionAttributes, "type", (AttributeDestinations.ErrorEvent))),
				() => Assert.True(DoAttributesContain(transactionAttributes, "timestamp", (AttributeDestinations.TransactionEvent | AttributeDestinations.SpanEvent | AttributeDestinations.CustomEvent))),
				() => Assert.True(DoAttributesContain(transactionAttributes, "timestamp", (AttributeDestinations.ErrorEvent))),
				() => Assert.True(DoAttributesContain(transactionAttributes, "name", (AttributeDestinations.TransactionEvent))),
				() => Assert.True(DoAttributesContain(transactionAttributes, "transactionName", (AttributeDestinations.ErrorEvent))),
				() => Assert.True(DoAttributesContain(transactionAttributes, "nr.guid", (AttributeDestinations.TransactionEvent | AttributeDestinations.ErrorEvent))),
				() => Assert.True(DoAttributesContain(transactionAttributes, "duration", (AttributeDestinations.TransactionEvent | AttributeDestinations.ErrorEvent | AttributeDestinations.SpanEvent))),
				() => Assert.True(DoAttributesContain(transactionAttributes, "webDuration", (AttributeDestinations.TransactionEvent))),
				() => Assert.True(DoAttributesContain(transactionAttributes, "totalTime", (AttributeDestinations.TransactionEvent | AttributeDestinations.TransactionTrace))),
				() => Assert.True(DoAttributesContain(transactionAttributes, "databaseDuration", (AttributeDestinations.TransactionEvent | AttributeDestinations.ErrorEvent))),
				() => Assert.True(DoAttributesContain(transactionAttributes, "databaseCallCount", (AttributeDestinations.TransactionEvent | AttributeDestinations.ErrorEvent))),
				() => Assert.True(DoAttributesContain(transactionAttributes, "nr.apdexPerfZone", (AttributeDestinations.TransactionEvent))),
				() => Assert.True(DoAttributesContain(transactionAttributes, "client_cross_process_id", (AttributeDestinations.ErrorTrace | AttributeDestinations.TransactionTrace))),
				() => Assert.True(DoAttributesContain(transactionAttributes, "trip_id", (AttributeDestinations.ErrorTrace | AttributeDestinations.TransactionTrace))),
				() => Assert.True(DoAttributesContain(transactionAttributes, "nr.tripId", (AttributeDestinations.TransactionEvent))),
				() => Assert.True(DoAttributesContain(transactionAttributes, "path_hash", (AttributeDestinations.ErrorTrace | AttributeDestinations.TransactionTrace))),
				() => Assert.True(DoAttributesContain(transactionAttributes, "nr.pathHash", (AttributeDestinations.TransactionEvent))),
				() => Assert.True(DoAttributesContain(transactionAttributes, "nr.referringPathHash", (AttributeDestinations.TransactionEvent))),
				() => Assert.True(DoAttributesContain(transactionAttributes, "referring_transaction_guid", (AttributeDestinations.ErrorTrace | AttributeDestinations.TransactionTrace))),
				() => Assert.True(DoAttributesContain(transactionAttributes, "nr.referringTransactionGuid", (AttributeDestinations.TransactionEvent | AttributeDestinations.ErrorEvent))),
				() => Assert.True(DoAttributesContain(transactionAttributes, "nr.alternatePathHashes", (AttributeDestinations.TransactionEvent))),
				() => Assert.True(DoAttributesContain(transactionAttributes, "original_url", (AttributeDestinations.ErrorTrace | AttributeDestinations.TransactionTrace | AttributeDestinations.ErrorEvent))),
				() => Assert.True(DoAttributesContain(transactionAttributes, "request.uri", (AttributeDestinations.TransactionEvent | AttributeDestinations.ErrorEvent | AttributeDestinations.ErrorTrace | AttributeDestinations.TransactionTrace | AttributeDestinations.SqlTrace))),
				() => Assert.True(DoAttributesContain(transactionAttributes, "request.referer", (AttributeDestinations.ErrorTrace | AttributeDestinations.TransactionTrace | AttributeDestinations.ErrorEvent))),
				() => Assert.True(DoAttributesContain(transactionAttributes, "queue_wait_time_ms", (AttributeDestinations.ErrorTrace | AttributeDestinations.TransactionTrace | AttributeDestinations.ErrorEvent))),
				() => Assert.True(DoAttributesContain(transactionAttributes, "response.status", (AttributeDestinations.TransactionEvent | AttributeDestinations.ErrorTrace | AttributeDestinations.TransactionTrace | AttributeDestinations.ErrorEvent))),
				() => Assert.True(DoAttributesContain(transactionAttributes, "http.statusCode", (AttributeDestinations.TransactionEvent | AttributeDestinations.ErrorTrace | AttributeDestinations.TransactionTrace | AttributeDestinations.ErrorEvent | AttributeDestinations.SpanEvent))),
				() => Assert.True(DoAttributesContain(transactionAttributes, "request.parameters.requestParameterKey", (AttributeDestinations.None))),
				() => Assert.True(DoAttributesContain(transactionAttributes, "userAttributeKey", (AttributeDestinations.All))),
				() => Assert.True(DoAttributesContain(transactionAttributes, "userErrorAttributeKey", (AttributeDestinations.ErrorEvent | AttributeDestinations.ErrorTrace))),
				() => Assert.True(DoAttributesContain(transactionAttributes, "error.class", (AttributeDestinations.ErrorEvent | AttributeDestinations.SpanEvent))),
				() => Assert.True(DoAttributesContain(transactionAttributes, "errorType", (AttributeDestinations.TransactionEvent))),
				() => Assert.True(DoAttributesContain(transactionAttributes, "errorMessage", (AttributeDestinations.TransactionEvent))),
				() => Assert.True(DoAttributesContain(transactionAttributes, "error.message", (AttributeDestinations.ErrorEvent | AttributeDestinations.SpanEvent))),
				() => Assert.True(DoAttributesContain(transactionAttributes, "error", (AttributeDestinations.TransactionEvent))),
				() => Assert.True(DoAttributesContain(transactionAttributes, "host.displayName", (AttributeDestinations.TransactionTrace | AttributeDestinations.TransactionEvent | AttributeDestinations.ErrorTrace | AttributeDestinations.ErrorEvent)))
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
			var transaction = new Transaction(_configuration, TransactionName.ForWebTransaction("transactionCategory", "transactionName"), timer, expectedStartTime, Mock.Create<ICallStackManager>(), _databaseService, priority, Mock.Create<IDatabaseStatementParser>(), _distributedTracePayloadHandler, _errorService);
			transaction.TransactionMetadata.AddRequestParameter("requestParameterKey", "requestParameterValue");
			transaction.TransactionMetadata.AddUserAttribute("userAttributeKey", "userAttributeValue");
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
			var transactionAttributes = attributes.GetIntrinsics()
				.Concat(attributes.GetAgentAttributes())
				.Concat(attributes.GetUserAttributes())
				.ToList();


			// ASSERT
			NrAssert.Multiple(
				() => Assert.AreEqual(30, attributes.Count()),  // Assert that only these attributes are generated
				() => Assert.True(DoAttributesContain(transactionAttributes, "type", AttributeDestinations.TransactionEvent, (AttributeClassification.Intrinsics))),
				() => Assert.True(DoAttributesContain(transactionAttributes, "timestamp", AttributeDestinations.TransactionEvent | AttributeDestinations.SpanEvent | AttributeDestinations.CustomEvent, (AttributeClassification.Intrinsics))),
				() => Assert.True(DoAttributesContain(transactionAttributes, "name", (AttributeClassification.Intrinsics))),
				() => Assert.True(DoAttributesContain(transactionAttributes, "transactionName", (AttributeClassification.Intrinsics))),
				() => Assert.True(DoAttributesContain(transactionAttributes, "nr.guid", (AttributeClassification.Intrinsics))),
				() => Assert.True(DoAttributesContain(transactionAttributes, "duration", (AttributeClassification.Intrinsics))),
				() => Assert.True(DoAttributesContain(transactionAttributes, "totalTime", (AttributeClassification.Intrinsics | AttributeClassification.Intrinsics))),
				() => Assert.True(DoAttributesContain(transactionAttributes, "webDuration", (AttributeClassification.Intrinsics))),
				() => Assert.True(DoAttributesContain(transactionAttributes, "queueDuration", (AttributeClassification.Intrinsics))),
				() => Assert.True(DoAttributesContain(transactionAttributes, "externalDuration", (AttributeClassification.Intrinsics))),
				() => Assert.True(DoAttributesContain(transactionAttributes, "externalCallCount", (AttributeClassification.Intrinsics))),
				() => Assert.True(DoAttributesContain(transactionAttributes, "nr.apdexPerfZone", (AttributeClassification.Intrinsics))),
				() => Assert.True(DoAttributesContain(transactionAttributes, "original_url", (AttributeClassification.AgentAttributes))),
				() => Assert.True(DoAttributesContain(transactionAttributes, "request.uri", (AttributeClassification.AgentAttributes))),
				() => Assert.True(DoAttributesContain(transactionAttributes, "request.referer", (AttributeClassification.AgentAttributes))),
				() => Assert.True(DoAttributesContain(transactionAttributes, "queue_wait_time_ms", (AttributeClassification.AgentAttributes))),
				() => Assert.True(DoAttributesContain(transactionAttributes, "response.status", (AttributeClassification.AgentAttributes))),
				() => Assert.True(DoAttributesContain(transactionAttributes, "http.statusCode", (AttributeClassification.AgentAttributes))),
				() => Assert.True(DoAttributesContain(transactionAttributes, "request.parameters.requestParameterKey", (AttributeClassification.AgentAttributes))),
				() => Assert.True(DoAttributesContain(transactionAttributes, "host.displayName", (AttributeClassification.AgentAttributes))),
				() => Assert.True(DoAttributesContain(transactionAttributes, "userAttributeKey", (AttributeClassification.UserAttributes))),
				() => Assert.True(DoAttributesContain(transactionAttributes, "client_cross_process_id", (AttributeClassification.Intrinsics))),
				() => Assert.True(DoAttributesContain(transactionAttributes, "trip_id", (AttributeClassification.Intrinsics))),
				() => Assert.True(DoAttributesContain(transactionAttributes, "nr.tripId", (AttributeClassification.Intrinsics))),
				() => Assert.True(DoAttributesContain(transactionAttributes, "path_hash", (AttributeClassification.Intrinsics))),
				() => Assert.True(DoAttributesContain(transactionAttributes, "nr.pathHash", (AttributeClassification.Intrinsics))),
				() => Assert.True(DoAttributesContain(transactionAttributes, "nr.referringPathHash", (AttributeClassification.Intrinsics))),
				() => Assert.True(DoAttributesContain(transactionAttributes, "referring_transaction_guid", (AttributeClassification.Intrinsics))),
				() => Assert.True(DoAttributesContain(transactionAttributes, "nr.referringTransactionGuid", (AttributeClassification.Intrinsics))),
				() => Assert.True(DoAttributesContain(transactionAttributes, "nr.alternatePathHashes", (AttributeClassification.Intrinsics)))
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
			var transaction = new Transaction(_configuration, TransactionName.ForWebTransaction("transactionCategory", "transactionName"), timer, expectedStartTime, Mock.Create<ICallStackManager>(), _databaseService, priority, Mock.Create<IDatabaseStatementParser>(), _distributedTracePayloadHandler, _errorService);
			transaction.TransactionMetadata.AddRequestParameter("requestParameterKey", "requestParameterValue");
			transaction.TransactionMetadata.AddUserAttribute("userAttributeKey", "userAttributeValue");
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
			var transactionAttributes = attributes.GetIntrinsics()
				.Concat(attributes.GetAgentAttributes())
				.Concat(attributes.GetUserAttributes())
				.ToList();

			// ASSERT
			NrAssert.Multiple(
				() => Assert.AreEqual(32, attributes.Count()),  // Assert that only these attributes are generated
				() => Assert.True(DoAttributesContain(transactionAttributes, "type", AttributeDestinations.TransactionEvent, (AttributeClassification.Intrinsics))),
				() => Assert.True(DoAttributesContain(transactionAttributes, "timestamp", AttributeDestinations.TransactionEvent | AttributeDestinations.SpanEvent | AttributeDestinations.CustomEvent,(AttributeClassification.Intrinsics))),
				() => Assert.True(DoAttributesContain(transactionAttributes, "name", (AttributeClassification.Intrinsics))),
				() => Assert.True(DoAttributesContain(transactionAttributes, "transactionName", (AttributeClassification.Intrinsics))),
				() => Assert.True(DoAttributesContain(transactionAttributes, "nr.guid", (AttributeClassification.Intrinsics))),
				() => Assert.True(DoAttributesContain(transactionAttributes, "duration", (AttributeClassification.Intrinsics))),
				() => Assert.True(DoAttributesContain(transactionAttributes, "totalTime", (AttributeClassification.Intrinsics | AttributeClassification.Intrinsics))),
				() => Assert.True(DoAttributesContain(transactionAttributes, "webDuration", (AttributeClassification.Intrinsics))),
				() => Assert.True(DoAttributesContain(transactionAttributes, "queueDuration", (AttributeClassification.Intrinsics))),
				() => Assert.True(DoAttributesContain(transactionAttributes, "externalDuration", (AttributeClassification.Intrinsics))),
				() => Assert.True(DoAttributesContain(transactionAttributes, "externalCallCount", (AttributeClassification.Intrinsics))),
				() => Assert.True(DoAttributesContain(transactionAttributes, "databaseDuration", (AttributeClassification.Intrinsics))),
				() => Assert.True(DoAttributesContain(transactionAttributes, "databaseCallCount", (AttributeClassification.Intrinsics))),
				() => Assert.True(DoAttributesContain(transactionAttributes, "nr.apdexPerfZone", (AttributeClassification.Intrinsics))),
				() => Assert.True(DoAttributesContain(transactionAttributes, "original_url", (AttributeClassification.AgentAttributes))),
				() => Assert.True(DoAttributesContain(transactionAttributes, "request.referer", (AttributeClassification.AgentAttributes))),
				() => Assert.True(DoAttributesContain(transactionAttributes, "queue_wait_time_ms", (AttributeClassification.AgentAttributes))),
				() => Assert.True(DoAttributesContain(transactionAttributes, "response.status", (AttributeClassification.AgentAttributes))),
				() => Assert.True(DoAttributesContain(transactionAttributes, "http.statusCode", (AttributeClassification.AgentAttributes))),
				() => Assert.True(DoAttributesContain(transactionAttributes, "request.parameters.requestParameterKey", (AttributeClassification.AgentAttributes))),
				() => Assert.True(DoAttributesContain(transactionAttributes, "host.displayName", (AttributeClassification.AgentAttributes))),
				() => Assert.True(DoAttributesContain(transactionAttributes, "client_cross_process_id", (AttributeClassification.Intrinsics))),
				() => Assert.True(DoAttributesContain(transactionAttributes, "trip_id", (AttributeClassification.Intrinsics))),
				() => Assert.True(DoAttributesContain(transactionAttributes, "nr.tripId", (AttributeClassification.Intrinsics))),
				() => Assert.True(DoAttributesContain(transactionAttributes, "path_hash", (AttributeClassification.Intrinsics))),
				() => Assert.True(DoAttributesContain(transactionAttributes, "nr.pathHash", (AttributeClassification.Intrinsics))),
				() => Assert.True(DoAttributesContain(transactionAttributes, "nr.referringPathHash", (AttributeClassification.Intrinsics))),
				() => Assert.True(DoAttributesContain(transactionAttributes, "referring_transaction_guid", (AttributeClassification.Intrinsics))),
				() => Assert.True(DoAttributesContain(transactionAttributes, "nr.referringTransactionGuid", (AttributeClassification.Intrinsics))),
				() => Assert.True(DoAttributesContain(transactionAttributes, "nr.alternatePathHashes", (AttributeClassification.Intrinsics)))
			);
		}

		[Test]
		public void GetAttributes_DoesNotReturnWebDurationAttribute_IfNonWebTransaction()
		{
			// ARRANGE
			var priority = 0.5f;
			var transactionBuilder = new Transaction(_configuration, TransactionName.ForOtherTransaction("transactionCategory", "transactionName"), Mock.Create<ITimer>(), DateTime.UtcNow, Mock.Create<ICallStackManager>(), _databaseService, priority, Mock.Create<IDatabaseStatementParser>(), _distributedTracePayloadHandler, _errorService);
			var transaction = transactionBuilder.ConvertToImmutableTransaction();

			var transactionMetricName = new TransactionMetricName("WebTransaction", "TransactionName");
			var apdexT = TimeSpan.FromSeconds(2);
			var totalTime = TimeSpan.FromSeconds(1);
			var txStats = new TransactionMetricStatsCollection(new TransactionMetricName("WebTransaction", "myTx"));
			// ACT
			var attributes = _transactionAttributeMaker.GetAttributes(transaction, transactionMetricName, apdexT, totalTime, txStats);

			// ACQUIRE
			var transactionAttributes = attributes.GetIntrinsics()
				.Concat(attributes.GetAgentAttributes())
				.Concat(attributes.GetUserAttributes())
				.ToDictionary(attr => attr.Key, attr => attr.Value);

			// ASSERT
			Assert.False(transactionAttributes.ContainsKey("webDuration"));
		}

		#region Distributed Trace

		private const DistributedTracingParentType IncomingType = DistributedTracingParentType.App;
		private const string IncomingAcctId = "incomingAcctId";
		private const string IncomingAppId = "incomingAppId";
		private const string IncomingGuid = "incomingGuid";
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
			Mock.Arrange(() => _configuration.DistributedTracingEnabled).Returns(true);
			Mock.Arrange(() => _configuration.TrustedAccountKey).Returns("1234");
			var timer = Mock.Create<ITimer>();
			var expectedStartTime = DateTime.Now;
			var transactionMetricName = new TransactionMetricName("WebTransaction", "TransactionName");

			var immutableTransaction = BuildTestImmutableTransaction(sampled: IncomingSampled, guid: TransactionGuid, traceId: IncomingTraceId);

			var txStats = new TransactionMetricStatsCollection(new TransactionMetricName("WebTransaction", "myTx"));
			var totalTime = TimeSpan.FromSeconds(1);
			var apdexT = TimeSpan.FromSeconds(2);

			// ACT
			var attributes = _transactionAttributeMaker.GetAttributes(immutableTransaction, transactionMetricName, apdexT, totalTime, txStats);

			// ACQUIRE
			var transactionAttributes = attributes.GetIntrinsics()
				.Concat(attributes.GetAgentAttributes())
				.Concat(attributes.GetUserAttributes())
				.ToDictionary(attr => attr.Key, attr => attr.Value);

			// ASSERT
			NrAssert.Multiple(
				() => Assert.False(transactionAttributes.ContainsKey("nr.tripId"), "nr.tripId should not have been included"),
				() => Assert.False(transactionAttributes.ContainsKey("trip_id"), "trip_id should not have been included")
			);
		}

		[Test]
		public void GetAttributes_ReturnsDistributedTraceAttrs_IfDistributedTraceEnabledAndReceivedPayload()
		{
			Mock.Arrange(() => _configuration.DistributedTracingEnabled).Returns(true);
			Mock.Arrange(() => _configuration.TrustedAccountKey).Returns(TrustKey);
			var timer = Mock.Create<ITimer>();
			var expectedStartTime = DateTime.Now;
			var transactionMetricName = new TransactionMetricName("WebTransaction", "TransactionName");

			var immutableTransaction = BuildTestImmutableTransaction(sampled: IncomingSampled, guid: TransactionGuid, traceId: IncomingTraceId);

			var txStats = new TransactionMetricStatsCollection(new TransactionMetricName("WebTransaction", "myTx"));
			var totalTime = TimeSpan.FromSeconds(1);
			var apdexT = TimeSpan.FromSeconds(2);

			// ACT
			var attributes = _transactionAttributeMaker.GetAttributes(immutableTransaction, transactionMetricName, apdexT, totalTime, txStats);

			// ACQUIRE
			var transactionAttributes = attributes.GetIntrinsics()
				.Concat(attributes.GetAgentAttributes())
				.Concat(attributes.GetUserAttributes())
				.ToDictionary(attr => attr.Key, attr => attr.Value);

			// ASSERT
			NrAssert.Multiple(
				() => Assert.AreEqual(21, attributes.Count()),  // Assert that only these attributes are generated
				() => Assert.AreEqual("Transaction", transactionAttributes["type"]),
				() => Assert.True(transactionAttributes.ContainsKey("timestamp")),
				() => Assert.AreEqual("WebTransaction/TransactionName", transactionAttributes["name"]),
				() => Assert.AreEqual("WebTransaction/TransactionName", transactionAttributes["transactionName"]),
				() => Assert.True(transactionAttributes.ContainsKey("duration")),
				() => Assert.AreEqual(1, transactionAttributes["totalTime"]),
				() => Assert.True(transactionAttributes.ContainsKey("webDuration")),
				() => Assert.True(transactionAttributes.ContainsKey("nr.apdexPerfZone")),
				() => Assert.AreEqual("/Unknown", transactionAttributes["request.uri"]),
				() => Assert.AreEqual(IncomingTraceId, transactionAttributes["traceId"]),
				() => Assert.AreEqual(IncomingType.ToString(), transactionAttributes["parent.type"]),
				() => Assert.AreEqual(IncomingAppId, transactionAttributes["parent.app"]),
				() => Assert.AreEqual(IncomingAcctId, transactionAttributes["parent.account"]),
				() => Assert.AreEqual(IncomingTransportType, transactionAttributes["parent.transportType"]),
				() => Assert.True(transactionAttributes.ContainsKey("parent.transportDuration")),
				() => Assert.AreEqual(IncomingTransactionId, transactionAttributes["parentId"]),
				() => Assert.AreEqual(immutableTransaction.Guid, transactionAttributes["guid"]),
				() => Assert.AreEqual(IncomingPriority, transactionAttributes["priority"]),
				() => Assert.AreEqual(IncomingSampled, transactionAttributes["sampled"]),
				() => Assert.AreEqual(IncomingGuid, transactionAttributes["parentSpanId"]),
				() => Assert.Contains("host.displayName", transactionAttributes.Keys)
			);
		}

		[Test]
		public void GetAttributes_DoesNotReturnDistributedTraceAttrs_IfDistributedTraceEnabledAndDidNotReceivePayload()
		{
			Mock.Arrange(() => _configuration.DistributedTracingEnabled).Returns(true);
			var timer = Mock.Create<ITimer>();
			var expectedStartTime = DateTime.Now;
			var transactionMetricName = new TransactionMetricName("WebTransaction", "TransactionName");

			var transaction = new Transaction(_configuration, TransactionName.ForWebTransaction("transactionCategory", "transactionName"), timer, expectedStartTime, Mock.Create<ICallStackManager>(), _databaseService, Priority, Mock.Create<IDatabaseStatementParser>(), _distributedTracePayloadHandler, _errorService);

			var immutableTransaction = transaction.ConvertToImmutableTransaction();
			var txStats = new TransactionMetricStatsCollection(new TransactionMetricName("WebTransaction", "myTx"));
			var totalTime = TimeSpan.FromSeconds(1);
			var apdexT = TimeSpan.FromSeconds(2);

			// ACT
			var attributes = _transactionAttributeMaker.GetAttributes(immutableTransaction, transactionMetricName, apdexT, totalTime, txStats);

			// ACQUIRE
			var transactionAttributes = attributes.GetIntrinsics()
				.Concat(attributes.GetAgentAttributes())
				.Concat(attributes.GetUserAttributes())
				.ToDictionary(attr => attr.Key, attr => attr.Value);

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
		public void GetAttributes_AssignsAttributesToCorrectDestinations_DistributedTracePayloadReceived()
		{
			Mock.Arrange(() => _configuration.DistributedTracingEnabled).Returns(true);
			var timer = Mock.Create<ITimer>();
			var expectedStartTime = DateTime.Now;
			var transactionMetricName = new TransactionMetricName("WebTransaction", "TransactionName");

			var immutableTransaction = BuildTestImmutableTransaction(sampled: IncomingSampled, guid: TransactionGuid, traceId: IncomingTraceId);

			var txStats = new TransactionMetricStatsCollection(new TransactionMetricName("WebTransaction", "myTx"));
			var totalTime = TimeSpan.FromSeconds(1);
			var apdexT = TimeSpan.FromSeconds(2);

			// ACT
			var attributes = _transactionAttributeMaker.GetAttributes(immutableTransaction, transactionMetricName, apdexT, totalTime, txStats);

			// ACQUIRE
			var transactionAttributes = attributes.GetIntrinsics()
				.Concat(attributes.GetAgentAttributes())
				.Concat(attributes.GetUserAttributes())
				.ToList();

			// ASSERT
			NrAssert.Multiple(
				() => Assert.AreEqual(21, attributes.Count()),  // Assert that only these attributes are generated
				() => Assert.True(DoAttributesContain(transactionAttributes, "type", (AttributeDestinations.TransactionEvent))),
				() => Assert.True(DoAttributesContain(transactionAttributes, "timestamp", (AttributeDestinations.TransactionEvent | AttributeDestinations.SpanEvent | AttributeDestinations.CustomEvent))),
				() => Assert.True(DoAttributesContain(transactionAttributes, "name", (AttributeDestinations.TransactionEvent))),
				() => Assert.True(DoAttributesContain(transactionAttributes, "transactionName", (AttributeDestinations.ErrorEvent))),
				() => Assert.True(DoAttributesContain(transactionAttributes, "duration", (AttributeDestinations.TransactionEvent | AttributeDestinations.ErrorEvent | AttributeDestinations.SpanEvent))),
				() => Assert.True(DoAttributesContain(transactionAttributes, "webDuration", (AttributeDestinations.TransactionEvent))),
				() => Assert.True(DoAttributesContain(transactionAttributes, "totalTime", (AttributeDestinations.TransactionEvent | AttributeDestinations.TransactionTrace))),
				() => Assert.True(DoAttributesContain(transactionAttributes, "nr.apdexPerfZone", (AttributeDestinations.TransactionEvent))),
				() => Assert.True(DoAttributesContain(transactionAttributes, "parent.type", AttributeDestinations.TransactionTrace | AttributeDestinations.ErrorTrace | AttributeDestinations.SqlTrace | AttributeDestinations.TransactionEvent | AttributeDestinations.ErrorEvent)),
				() => Assert.True(DoAttributesContain(transactionAttributes, "parent.app", AttributeDestinations.TransactionTrace | AttributeDestinations.ErrorTrace | AttributeDestinations.SqlTrace | AttributeDestinations.TransactionEvent | AttributeDestinations.ErrorEvent)),
				() => Assert.True(DoAttributesContain(transactionAttributes, "parent.account", AttributeDestinations.TransactionTrace | AttributeDestinations.ErrorTrace | AttributeDestinations.SqlTrace | AttributeDestinations.TransactionEvent | AttributeDestinations.ErrorEvent)),
				() => Assert.True(DoAttributesContain(transactionAttributes, "parent.transportType", AttributeDestinations.TransactionTrace | AttributeDestinations.ErrorTrace | AttributeDestinations.SqlTrace | AttributeDestinations.TransactionEvent | AttributeDestinations.ErrorEvent)),
				() => Assert.True(DoAttributesContain(transactionAttributes, "parent.transportDuration", AttributeDestinations.TransactionTrace | AttributeDestinations.ErrorTrace | AttributeDestinations.SqlTrace | AttributeDestinations.TransactionEvent | AttributeDestinations.ErrorEvent)),
				() => Assert.True(DoAttributesContain(transactionAttributes, ParentSpanIdAttributeName, (AttributeDestinations.TransactionEvent))),
				() => Assert.True(DoAttributesContain(transactionAttributes, "parentId", (AttributeDestinations.TransactionEvent | AttributeDestinations.SpanEvent))),
				() => Assert.True(DoAttributesContain(transactionAttributes, "priority", AttributeDestinations.TransactionTrace | AttributeDestinations.ErrorTrace | AttributeDestinations.SqlTrace | AttributeDestinations.TransactionEvent | AttributeDestinations.ErrorEvent | AttributeDestinations.SpanEvent)),
				() => Assert.True(DoAttributesContain(transactionAttributes, "sampled", AttributeDestinations.TransactionTrace | AttributeDestinations.ErrorTrace | AttributeDestinations.SqlTrace | AttributeDestinations.TransactionEvent | AttributeDestinations.ErrorEvent | AttributeDestinations.SpanEvent)),
				() => Assert.True(DoAttributesContain(transactionAttributes, "parentSpanId", (AttributeDestinations.TransactionEvent))),
				() => Assert.True(DoAttributesContain(transactionAttributes, "request.uri", (AttributeDestinations.TransactionEvent | AttributeDestinations.ErrorEvent | AttributeDestinations.ErrorTrace | AttributeDestinations.TransactionTrace | AttributeDestinations.SqlTrace)))
			);
		}

		[Test]
		public void GetAttributes_AssignsCorrectClassificationToAttributes_DistributedTracePayloadReceived()
		{
			Mock.Arrange(() => _configuration.DistributedTracingEnabled).Returns(true);
			var timer = Mock.Create<ITimer>();
			var expectedStartTime = DateTime.Now;
			var transactionMetricName = new TransactionMetricName("WebTransaction", "TransactionName");

			var immutableTransaction = BuildTestImmutableTransaction(sampled: IncomingSampled, guid: TransactionGuid, traceId: IncomingTraceId);

			var txStats = new TransactionMetricStatsCollection(new TransactionMetricName("WebTransaction", "myTx"));
			var totalTime = TimeSpan.FromSeconds(1);
			var apdexT = TimeSpan.FromSeconds(2);

			// ACT
			var attributes = _transactionAttributeMaker.GetAttributes(immutableTransaction, transactionMetricName, apdexT, totalTime, txStats);

			// ACQUIRE
			var transactionAttributes = attributes.GetIntrinsics()
				.Concat(attributes.GetAgentAttributes())
				.Concat(attributes.GetUserAttributes())
				.ToDictionary(attr => attr.Key, attr => attr.Classification);

			// ASSERT
			NrAssert.Multiple(
				() => Assert.AreEqual(21, attributes.Count()),  // Assert that only these attributes are generated
				() => Assert.AreEqual(AttributeClassification.Intrinsics, transactionAttributes["type"]),
				() => Assert.AreEqual(AttributeClassification.Intrinsics, transactionAttributes["timestamp"]),
				() => Assert.AreEqual(AttributeClassification.Intrinsics, transactionAttributes["name"]),
				() => Assert.AreEqual(AttributeClassification.Intrinsics, transactionAttributes["transactionName"]),
				() => Assert.AreEqual(AttributeClassification.Intrinsics, transactionAttributes["duration"]),
				() => Assert.AreEqual(AttributeClassification.Intrinsics, transactionAttributes["totalTime"]),
				() => Assert.AreEqual(AttributeClassification.Intrinsics, transactionAttributes["webDuration"]),
				() => Assert.AreEqual(AttributeClassification.Intrinsics, transactionAttributes["nr.apdexPerfZone"]),
				() => Assert.AreEqual(AttributeClassification.AgentAttributes, transactionAttributes["request.uri"]),
				() => Assert.AreEqual(AttributeClassification.AgentAttributes, transactionAttributes["host.displayName"]),
				() => Assert.AreEqual(AttributeClassification.Intrinsics, transactionAttributes["traceId"]),
				() => Assert.AreEqual(AttributeClassification.Intrinsics, transactionAttributes["parent.type"]),
				() => Assert.AreEqual(AttributeClassification.Intrinsics, transactionAttributes["parent.app"]),
				() => Assert.AreEqual(AttributeClassification.Intrinsics, transactionAttributes["parent.account"]),
				() => Assert.AreEqual(AttributeClassification.Intrinsics, transactionAttributes["parent.transportType"]),
				() => Assert.AreEqual(AttributeClassification.Intrinsics, transactionAttributes["parent.transportDuration"]),
				() => Assert.AreEqual(AttributeClassification.Intrinsics, transactionAttributes[ParentSpanIdAttributeName]),
				() => Assert.AreEqual(AttributeClassification.Intrinsics, transactionAttributes["guid"]),
				() => Assert.AreEqual(AttributeClassification.Intrinsics, transactionAttributes["priority"]),
				() => Assert.AreEqual(AttributeClassification.Intrinsics, transactionAttributes["sampled"]),
				() => Assert.AreEqual(AttributeClassification.Intrinsics, transactionAttributes["parentId"]),
				() => Assert.AreEqual(AttributeClassification.Intrinsics, transactionAttributes["parentSpanId"])
			);
		}

		[Test]
		public void ShouldCreateParentSpanIdWhenDistributedTraceGuidInPayload()
		{
			Mock.Arrange(() => _configuration.DistributedTracingEnabled).Returns(true);
			var timer = Mock.Create<ITimer>();
			var expectedStartTime = DateTime.Now;
			var transactionMetricName = new TransactionMetricName("WebTransaction", "TransactionName");

			var immutableTransaction = BuildTestImmutableTransaction(sampled: IncomingSampled, guid: TransactionGuid, traceId: IncomingTraceId);

			var txStats = new TransactionMetricStatsCollection(new TransactionMetricName("WebTransaction", "myTx"));
			var totalTime = TimeSpan.FromSeconds(1);
			var apdexT = TimeSpan.FromSeconds(2);

			// ACT
			var attributes = _transactionAttributeMaker.GetAttributes(immutableTransaction, transactionMetricName, apdexT, totalTime, txStats);

			var transactionAttributes = attributes.GetIntrinsics()
				.Concat(attributes.GetAgentAttributes())
				.Concat(attributes.GetUserAttributes())
				.ToDictionary(attr => attr.Key, attr => attr);

			
			Assert.True(transactionAttributes.ContainsKey(ParentSpanIdAttributeName), "Failed to find attribute: \"{0}\"", ParentSpanIdAttributeName);

			var parentSpanIdAttribute = transactionAttributes[ParentSpanIdAttributeName];

			NrAssert.Multiple
			(
				() => Assert.AreEqual(AttributeClassification.Intrinsics, parentSpanIdAttribute.Classification),
				() => Assert.AreEqual(AttributeDestinations.TransactionEvent, parentSpanIdAttribute.DefaultDestinations),
				() => Assert.AreEqual(IncomingGuid, parentSpanIdAttribute.Value)
			);
		}

		[Test]
		public void ShouldNotCreateParentSpanIdWhenDistributedTraceGuidNotInPayload()
		{
			Mock.Arrange(() => _configuration.DistributedTracingEnabled).Returns(true);
			var timer = Mock.Create<ITimer>();
			var expectedStartTime = DateTime.Now;
			var transactionMetricName = new TransactionMetricName("WebTransaction", "TransactionName");

			var immutableTransaction = BuildTestImmutableTransaction(sampled: IncomingSampled, guid: TransactionGuid, traceId: IncomingTraceId, guidInPayload: false);

			var txStats = new TransactionMetricStatsCollection(new TransactionMetricName("WebTransaction", "myTx"));
			var totalTime = TimeSpan.FromSeconds(1);
			var apdexT = TimeSpan.FromSeconds(2);

			// ACT
			var attributes = _transactionAttributeMaker.GetAttributes(immutableTransaction, transactionMetricName, apdexT, totalTime, txStats);

			var transactionAttributes = attributes.GetIntrinsics()
				.Concat(attributes.GetAgentAttributes())
				.Concat(attributes.GetUserAttributes())
				.ToDictionary(attr => attr.Key, attr => attr);

			Assert.False(transactionAttributes.ContainsKey(ParentSpanIdAttributeName));
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

		private ImmutableTransaction BuildTestImmutableTransaction(bool isWebTransaction = true, string guid = null, float priority = 0.5f, bool sampled = false, string traceId = "traceId", bool guidInPayload = true)
		{
			var name = TransactionName.ForWebTransaction("category", "name");

			var segments = Enumerable.Empty<Segment>();

			var placeholderMetadataBuilder = new TransactionMetadata();
			var placeholderMetadata = placeholderMetadataBuilder.ConvertToImmutableMetadata();

			var immutableTransaction = new ImmutableTransaction(name, segments, placeholderMetadata, DateTime.Now, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10), guid, false, false, false, priority, sampled, traceId, BuildMockTracingState(guidInPayload: guidInPayload));

			return immutableTransaction;
		}

		private static ITracingState BuildMockTracingState(bool guidInPayload = true)
		{
			var tracingState = Mock.Create<ITracingState>();

			Mock.Arrange(() => tracingState.Type).Returns(IncomingType);
			Mock.Arrange(() => tracingState.AppId).Returns(IncomingAppId);
			Mock.Arrange(() => tracingState.AccountId).Returns(IncomingAcctId);
			Mock.Arrange(() => tracingState.TransportType).Returns(TransportType.HTTP);
			if (guidInPayload)
			{
				Mock.Arrange(() => tracingState.Guid).Returns(IncomingGuid);
			}
			else
			{
				string nullGuid = null;
				Mock.Arrange(() => tracingState.Guid).Returns(nullGuid);
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
			var transaction = new Transaction(_configuration, TransactionName.ForWebTransaction("transactionCategory", "transactionName"), timer, expectedStartTime, Mock.Create<ICallStackManager>(), _databaseService, priority, Mock.Create<IDatabaseStatementParser>(), _distributedTracePayloadHandler, _errorService);
			transaction.TransactionMetadata.AddRequestParameter("requestParameterKey", "requestParameterValue");
			transaction.TransactionMetadata.AddUserAttribute("userAttributeKey", "userAttributeValue");
			transaction.TransactionMetadata.AddCustomErrorData(MakeErrorData());
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
			var builderAttributes = _transactionAttributeMaker.GetUserAndAgentAttributes(transaction.TransactionMetadata);
			var attributes = _transactionAttributeMaker.GetUserAndAgentAttributes(immutableTransaction.TransactionMetadata);

			// ACQUIRE
			var txBuilderAttributes = builderAttributes.GetIntrinsics()
				.Concat(attributes.GetAgentAttributes())
				.Concat(attributes.GetUserAttributes())
				.ToDictionary(attr => attr.Key, attr => attr.Value);

			var transactionAttributes = attributes.GetIntrinsics()
				.Concat(attributes.GetAgentAttributes())
				.Concat(attributes.GetUserAttributes())
				.ToDictionary(attr => attr.Key, attr => attr.Value);

			// ASSERT
			NrAssert.Multiple(
				() => Assert.AreEqual(10, builderAttributes.Count()),  // Assert that only these attributes are generated
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
				() => Assert.AreEqual(10, attributes.Count()),  // Assert that only these attributes are generated
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
		public void GetUserAndAgentAttributes_DoesNotIncludeOriginalUri_IfSameValueAsUei()
		{
			// ARRANGE
			var timer = Mock.Create<ITimer>();
			var expectedStartTime = DateTime.Now;
			var expectedDuration = TimeSpan.FromMilliseconds(500);
			Mock.Arrange(() => timer.Duration).Returns(expectedDuration);

			var priority = 0.5f;
			var transaction = new Transaction(_configuration, TransactionName.ForWebTransaction("transactionCategory", "transactionName"), timer, expectedStartTime, Mock.Create<ICallStackManager>(), _databaseService, priority, Mock.Create<IDatabaseStatementParser>(), _distributedTracePayloadHandler, _errorService);
			transaction.TransactionMetadata.SetOriginalUri("SameUri");
			transaction.TransactionMetadata.SetUri("SameUri");
			var immutableTransaction = transaction.ConvertToImmutableTransaction();

			// ACT
			var builderAttributes = _transactionAttributeMaker.GetUserAndAgentAttributes(transaction.TransactionMetadata);
			var attributes = _transactionAttributeMaker.GetUserAndAgentAttributes(immutableTransaction.TransactionMetadata);

			// ACQUIRE
			var txBuilderAttributes = builderAttributes.GetIntrinsics()
				.Concat(attributes.GetAgentAttributes())
				.Concat(attributes.GetUserAttributes())
				.ToDictionary(attr => attr.Key, attr => attr.Value);

			var transactionAttributes = attributes.GetIntrinsics()
				.Concat(attributes.GetAgentAttributes())
				.Concat(attributes.GetUserAttributes())
				.ToDictionary(attr => attr.Key, attr => attr.Value);

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

			var priority = 0.5f;
			var transaction = new Transaction(_configuration, TransactionName.ForWebTransaction("transactionCategory", "transactionName"), timer, expectedStartTime, Mock.Create<ICallStackManager>(), _databaseService, priority, Mock.Create<IDatabaseStatementParser>(), _distributedTracePayloadHandler, _errorService);
			transaction.TransactionMetadata.AddRequestParameter("requestParameterKey", "requestParameterValue");
			transaction.TransactionMetadata.AddUserAttribute("userAttributeKey", "userAttributeValue");
			transaction.TransactionMetadata.AddCustomErrorData(MakeErrorData());
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
			var builderAttributes = _transactionAttributeMaker.GetUserAndAgentAttributes(transaction.TransactionMetadata);
			var attributes = _transactionAttributeMaker.GetUserAndAgentAttributes(immutableTransaction.TransactionMetadata);

			// ACQUIRE
			var txBuilderAttributes = builderAttributes.GetIntrinsics()
				.Concat(attributes.GetAgentAttributes())
				.Concat(attributes.GetUserAttributes())
				.ToDictionary(attr => attr.Key, attr => attr.DefaultDestinations);
			var transactionAttributes = attributes.GetIntrinsics()
				.Concat(attributes.GetAgentAttributes())
				.Concat(attributes.GetUserAttributes())
				.ToDictionary(attr => attr.Key, attr => attr.DefaultDestinations);

			// ASSERT
			NrAssert.Multiple(
				() => Assert.AreEqual(10, builderAttributes.Count()),  // Assert that only these attributes are generated
				() => Assert.AreEqual(AttributeDestinations.ErrorTrace | AttributeDestinations.TransactionTrace | AttributeDestinations.ErrorEvent, txBuilderAttributes["original_url"]),
				() => Assert.AreEqual(AttributeDestinations.TransactionEvent | AttributeDestinations.ErrorEvent | AttributeDestinations.ErrorTrace | AttributeDestinations.TransactionTrace | AttributeDestinations.SqlTrace, txBuilderAttributes["request.uri"]),
				() => Assert.AreEqual(AttributeDestinations.ErrorTrace | AttributeDestinations.TransactionTrace | AttributeDestinations.ErrorEvent, txBuilderAttributes["request.referer"]),
				() => Assert.AreEqual(AttributeDestinations.ErrorTrace | AttributeDestinations.TransactionTrace | AttributeDestinations.ErrorEvent, txBuilderAttributes["queue_wait_time_ms"]),
				() => Assert.AreEqual(AttributeDestinations.TransactionEvent | AttributeDestinations.ErrorTrace | AttributeDestinations.TransactionTrace | AttributeDestinations.ErrorEvent, txBuilderAttributes["response.status"]),
				() => Assert.AreEqual(AttributeDestinations.TransactionEvent | AttributeDestinations.ErrorTrace | AttributeDestinations.TransactionTrace | AttributeDestinations.ErrorEvent | AttributeDestinations.SpanEvent, txBuilderAttributes["http.statusCode"]),
				() => Assert.AreEqual(AttributeDestinations.None, txBuilderAttributes["request.parameters.requestParameterKey"]),
				() => Assert.AreEqual(AttributeDestinations.All, txBuilderAttributes["userAttributeKey"]),
				() => Assert.AreEqual(AttributeDestinations.ErrorEvent | AttributeDestinations.ErrorTrace, txBuilderAttributes["userErrorAttributeKey"]),
				() => Assert.AreEqual(AttributeDestinations.TransactionTrace | AttributeDestinations.TransactionEvent | AttributeDestinations.ErrorTrace | AttributeDestinations.ErrorEvent, txBuilderAttributes["host.displayName"])
			);
			NrAssert.Multiple(
				() => Assert.AreEqual(10, attributes.Count()),  // Assert that only these attributes are generated
				() => Assert.AreEqual(AttributeDestinations.ErrorTrace | AttributeDestinations.TransactionTrace | AttributeDestinations.ErrorEvent, transactionAttributes["original_url"]),
				() => Assert.AreEqual(AttributeDestinations.TransactionEvent | AttributeDestinations.ErrorEvent | AttributeDestinations.ErrorTrace | AttributeDestinations.TransactionTrace | AttributeDestinations.SqlTrace, txBuilderAttributes["request.uri"]),
				() => Assert.AreEqual(AttributeDestinations.ErrorTrace | AttributeDestinations.TransactionTrace | AttributeDestinations.ErrorEvent, transactionAttributes["request.referer"]),
				() => Assert.AreEqual(AttributeDestinations.ErrorTrace | AttributeDestinations.TransactionTrace | AttributeDestinations.ErrorEvent, transactionAttributes["queue_wait_time_ms"]),
				() => Assert.AreEqual(AttributeDestinations.TransactionEvent | AttributeDestinations.ErrorTrace | AttributeDestinations.TransactionTrace | AttributeDestinations.ErrorEvent, transactionAttributes["response.status"]),
				() => Assert.AreEqual(AttributeDestinations.TransactionEvent | AttributeDestinations.ErrorTrace | AttributeDestinations.TransactionTrace | AttributeDestinations.ErrorEvent | AttributeDestinations.SpanEvent, transactionAttributes["http.statusCode"]),
				() => Assert.AreEqual(AttributeDestinations.None, transactionAttributes["request.parameters.requestParameterKey"]),
				() => Assert.AreEqual(AttributeDestinations.All, transactionAttributes["userAttributeKey"]),
				() => Assert.AreEqual(AttributeDestinations.ErrorEvent | AttributeDestinations.ErrorTrace, transactionAttributes["userErrorAttributeKey"]),
				() => Assert.AreEqual(AttributeDestinations.TransactionTrace | AttributeDestinations.TransactionEvent | AttributeDestinations.ErrorTrace | AttributeDestinations.ErrorEvent, txBuilderAttributes["host.displayName"])
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
			var transaction = new Transaction(_configuration, TransactionName.ForWebTransaction("transactionCategory", "transactionName"), timer, expectedStartTime, Mock.Create<ICallStackManager>(), _databaseService, priority, Mock.Create<IDatabaseStatementParser>(), _distributedTracePayloadHandler, _errorService);
			transaction.TransactionMetadata.AddRequestParameter("requestParameterKey", "requestParameterValue");
			transaction.TransactionMetadata.AddUserAttribute("userAttributeKey", "userAttributeValue");
			transaction.TransactionMetadata.AddCustomErrorData(MakeErrorData());
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
			var attributes = _transactionAttributeMaker.GetUserAndAgentAttributes(immutableTransaction.TransactionMetadata);

			// ACQUIRE
			var transactionAttributes = attributes.GetIntrinsics()
				.Concat(attributes.GetAgentAttributes())
				.Concat(attributes.GetUserAttributes())
				.ToDictionary(attr => attr.Key, attr => attr.Classification);

			// ASSERT
			NrAssert.Multiple(
				() => Assert.AreEqual(10, attributes.Count()),  // Assert that only these attributes are generated
				() => Assert.AreEqual(AttributeClassification.AgentAttributes, transactionAttributes["original_url"]),
				() => Assert.AreEqual(AttributeClassification.AgentAttributes, transactionAttributes["request.uri"]),
				() => Assert.AreEqual(AttributeClassification.AgentAttributes, transactionAttributes["request.referer"]),
				() => Assert.AreEqual(AttributeClassification.AgentAttributes, transactionAttributes["queue_wait_time_ms"]),
				() => Assert.AreEqual(AttributeClassification.AgentAttributes, transactionAttributes["response.status"]),
				() => Assert.AreEqual(AttributeClassification.AgentAttributes, transactionAttributes["http.statusCode"]),
				() => Assert.AreEqual(AttributeClassification.AgentAttributes, transactionAttributes["request.parameters.requestParameterKey"]),
				() => Assert.AreEqual(AttributeClassification.AgentAttributes, transactionAttributes["host.displayName"]),
				() => Assert.AreEqual(AttributeClassification.UserAttributes, transactionAttributes["userAttributeKey"]),
				() => Assert.AreEqual(AttributeClassification.UserAttributes, transactionAttributes["userErrorAttributeKey"])
			);
		}

		[TestCase(null, null, ExpectedResult = "coconut")]
		[TestCase("blandAmericanCoconut", null, ExpectedResult = "blandAmericanCoconut")]
		[TestCase("blandAmericanCoconut", "vietnameseCoconut", ExpectedResult = "vietnameseCoconut")]
		[TestCase(null, "vietnameseCoconut", ExpectedResult = "vietnameseCoconut")]
		public string HostDisplayName_WithLocalConfigurationAndEnvironmentVariableSet(string localConfigurationValue, string environmentVariableValue)
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

			var configuration = new TestableDefaultConfiguration(environment, localConfig, serverConfig, runTimeConfig, securityPoliciesConfiguration, processStatic, httpRuntimeStatic, configurationManagerStatic, dnsStatic);
			Mock.Arrange(() => _configurationService.Configuration).Returns(configuration);
			Mock.Arrange(() => dnsStatic.GetHostName()).Returns("coconut");
			Mock.Arrange(() => environment.GetEnvironmentVariable("NEW_RELIC_PROCESS_HOST_DISPLAY_NAME")).Returns(environmentVariableValue);
			localConfig.processHost.displayName = localConfigurationValue;

			var transactionAttributeMaker = new TransactionAttributeMaker(_configurationService);

			var timer = Mock.Create<ITimer>();
			var expectedStartTime = DateTime.Now;
			var expectedDuration = TimeSpan.FromMilliseconds(500);
			Mock.Arrange(() => timer.Duration).Returns(expectedDuration);

			var priority = 0.5f;
			var internalTransaction = new Transaction(_configuration, TransactionName.ForOtherTransaction("transactionCategory", "transactionName"), timer, expectedStartTime, Mock.Create<ICallStackManager>(), _databaseService, priority, Mock.Create<IDatabaseStatementParser>(), _distributedTracePayloadHandler, _errorService);
			var immutableTransaction = internalTransaction.ConvertToImmutableTransaction();
			var transactionMetricName = new TransactionMetricName("WebTransaction", "TransactionName");
			var apdexT = null as TimeSpan?;
			var totalTime = TimeSpan.FromSeconds(1);
			var txStats = new TransactionMetricStatsCollection(new TransactionMetricName("WebTransaction", "myTx"));

			// ACT
			var attributes = transactionAttributeMaker.GetAttributes(immutableTransaction, transactionMetricName, apdexT, totalTime, txStats);

			// ACQUIRE
			var transactionAttributes = attributes.GetIntrinsics()
				.Concat(attributes.GetAgentAttributes())
				.Concat(attributes.GetUserAttributes())
				.ToDictionary(attr => attr.Key, attr => attr.Value);

			return (string) transactionAttributes["host.displayName"];
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

			var attributes = _transactionAttributeMaker.GetAttributes(transaction, transactionMetricName, apdexT:TimeSpan.FromSeconds(7), totalTime: TimeSpan.FromSeconds(10), txStats: transactionMetricStatsCollection);

			var intrinsicAttributes = attributes.GetIntrinsicsDictionary();
			var expectedResponseTimeInSeconds = TimeSpan.FromSeconds(5).TotalSeconds;

			NrAssert.Multiple(
					() => Assert.AreEqual(expectedResponseTimeInSeconds, intrinsicAttributes["duration"]),
					() => Assert.AreEqual(expectedResponseTimeInSeconds, intrinsicAttributes["webDuration"]),
					() => Assert.AreEqual("S", intrinsicAttributes["nr.apdexPerfZone"])
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

			var intrinsicAttributes = attributes.GetIntrinsicsDictionary();
			var expectedResponseTimeInSeconds = TimeSpan.FromSeconds(10).TotalSeconds;

			NrAssert.Multiple(
					() => Assert.AreEqual(expectedResponseTimeInSeconds, intrinsicAttributes["duration"]),
					() => Assert.AreEqual("T", intrinsicAttributes["nr.apdexPerfZone"]),
					() => CollectionAssert.DoesNotContain(intrinsicAttributes.Keys, "webDuration")
				);
		}

		#endregion

		private ErrorData MakeErrorData()
		{
			return new ErrorData("message", "type", "stacktrace", DateTime.UtcNow, new ReadOnlyDictionary<string, object>(new Dictionary<string, object>() { { "userErrorAttributeKey", "userErrorAttributeValue" } }));
		}

		private bool DoAttributesContain(IEnumerable<Attribute> attributes, string attribName, AttributeDestinations destinations)
		{
			return attributes
				.Where(x => (x.DefaultDestinations | destinations) == destinations)
				.Any(x => x.Key == attribName);

		}

		private bool DoAttributesContain(IEnumerable<Attribute> attributes, string attribName, AttributeDestinations destinations, AttributeClassification classification)
		{
			return attributes
				.Where(x => (x.DefaultDestinations | destinations) == destinations)
				.Where(x => (x.Classification | classification) == classification)
				.Any(x => x.Key == attribName);

		}

		private bool DoAttributesContain(IEnumerable<Attribute> attributes, string attribName, AttributeClassification classification)
		{
			return attributes
				.Where(x => (x.Classification | classification) == classification)
				.Any(x => x.Key == attribName);

		}

		private bool DoAttributesContain(IEnumerable<Attribute> attributes, string attribName)
		{
			return attributes.Any(x => x.Key == attribName);
		}

		private object GetAttributeValue(IEnumerable<Attribute> attributes, string attribName, AttributeDestinations destinations)
		{
			return attributes
				.Where(x => (x.DefaultDestinations | destinations) == destinations)
				.Where(x => x.Key == attribName)
				.Select(x => x.Value)
				.FirstOrDefault();
		}

		private object GetAttributeValue(IEnumerable<Attribute> attributes, string attribName)
		{
			return attributes
				.Where(x => x.Key == attribName)
				.Select(x => x.Value)
				.FirstOrDefault();
		}
	}
}

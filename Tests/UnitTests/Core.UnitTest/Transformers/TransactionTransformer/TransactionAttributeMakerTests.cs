using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Errors;
using NewRelic.Agent.Core.Timing;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.Transactions.TransactionNames;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Core.WireModels;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Data;
using NewRelic.Testing.Assertions;
using NUnit.Framework;
using Telerik.JustMock;
using Attribute = NewRelic.Agent.Core.Transactions.Attribute;
using NewRelic.Agent.Core.Aggregators;
using NewRelic.Agent.Core.Metric;
using NewRelic.Agent.Core.CallStack;
using NewRelic.Agent.Core.Database;

namespace NewRelic.Agent.Core.Transformers.TransactionTransformer
{
	[TestFixture]
	public class TransactionAttributeMakerTests
	{
		[NotNull]
		private IConfiguration _configuration;

		[NotNull]
		private IConfigurationService _configurationService;

		[NotNull]
		private TransactionAttributeMaker _transactionAttributeMaker;

		[SetUp]
		public void SetUp()
		{

			_configuration = Mock.Create<IConfiguration>();
			_configurationService = Mock.Create<IConfigurationService>();
			Mock.Arrange(() => _configurationService.Configuration).Returns(_configuration);

			_transactionAttributeMaker = new TransactionAttributeMaker();
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

			var internalTransaction = new Transaction(_configuration, new OtherTransactionName("transactionCategory", "transactionName"), timer, expectedStartTime, Mock.Create<ICallStackManager>(), SqlObfuscator.GetObfuscatingSqlObfuscator());
			var immutableTransaction = internalTransaction.ConvertToImmutableTransaction();
			var errorData = ErrorData.TryGetErrorData(immutableTransaction, _configurationService);
			var transactionMetricName = new TransactionMetricName("WebTransaction", "TransactionName");
			var apdexT = null as TimeSpan?;
			var totalTime = TimeSpan.FromSeconds(1);
			var txStats = new TransactionMetricStatsCollection(new TransactionMetricName("WebTransaction", "myTx"));

			// ACT
			var attributes = _transactionAttributeMaker.GetAttributes(immutableTransaction, transactionMetricName, apdexT, totalTime, errorData, txStats);

			// ACQUIRE
			var transactionAttributes = attributes.GetIntrinsics()
				.Concat(attributes.GetAgentAttributes())
				.Concat(attributes.GetUserAttributes())
				.ToDictionary(attr => attr.Key, attr => attr.Value);

			// ASSERT
			NrAssert.Multiple(
				() => Assert.AreEqual(8, attributes.Count()),  // Assert that only these attributes are generated
				() => Assert.AreEqual("Transaction", transactionAttributes["type"]),
				() => Assert.AreEqual(expectedStartTime.ToUnixTime(), transactionAttributes["timestamp"]),
				() => Assert.AreEqual("WebTransaction/TransactionName", transactionAttributes["name"]),
				() => Assert.AreEqual("WebTransaction/TransactionName", transactionAttributes["transactionName"]),
				() => Assert.AreEqual(0.5, transactionAttributes["duration"]),
				() => Assert.AreEqual(1, transactionAttributes["totalTime"]),
				() => Assert.NotNull(transactionAttributes["nr.tripId"])
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

			var transaction = new Transaction(_configuration, new OtherTransactionName("transactionCategory", "transactionName"), timer, expectedStartTime, Mock.Create<ICallStackManager>(), SqlObfuscator.GetObfuscatingSqlObfuscator());
			var immutableTransaction = transaction.ConvertToImmutableTransaction();
			var errorData = ErrorData.TryGetErrorData(immutableTransaction, _configurationService);
			var transactionMetricName = new TransactionMetricName("WebTransaction", "TransactionName");
			var apdexT = null as TimeSpan?;
			var totalTime = TimeSpan.FromSeconds(1);
			var txStats = new TransactionMetricStatsCollection(new TransactionMetricName("WebTransaction", "myTx"));
			txStats.MergeUnscopedStats(MetricNames.DatastoreAll, MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5)));
			// ACT
			var attributes = _transactionAttributeMaker.GetAttributes(immutableTransaction, transactionMetricName, apdexT, totalTime, errorData, txStats);

			// ACQUIRE
			var transactionAttributes = attributes.GetIntrinsics()
				.Concat(attributes.GetAgentAttributes())
				.Concat(attributes.GetUserAttributes())
				.ToDictionary(attr => attr.Key, attr => attr.Value);

			// ASSERT
			NrAssert.Multiple(
				() => Assert.AreEqual(10, attributes.Count()),  // Assert that only these attributes are generated
				() => Assert.AreEqual("Transaction", transactionAttributes["type"]),
				() => Assert.AreEqual(expectedStartTime.ToUnixTime(), transactionAttributes["timestamp"]),
				() => Assert.AreEqual("WebTransaction/TransactionName", transactionAttributes["name"]),
				() => Assert.AreEqual("WebTransaction/TransactionName", transactionAttributes["transactionName"]),
				() => Assert.AreEqual(0.5, transactionAttributes["duration"]),
				() => Assert.AreEqual(1, transactionAttributes["totalTime"]),
				() => Assert.NotNull(transactionAttributes["nr.tripId"]),
				() => Assert.NotNull(transactionAttributes["databaseDuration"]),
				() => Assert.AreEqual(5, transactionAttributes["databaseDuration"]),
				() => Assert.NotNull(transactionAttributes["databaseCallCount"]),
				() => Assert.AreEqual(1, transactionAttributes["databaseCallCount"])
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

			var transaction = new Transaction(_configuration, new OtherTransactionName("transactionCategory", "transactionName"), timer, expectedStartTime, Mock.Create<ICallStackManager>(), SqlObfuscator.GetObfuscatingSqlObfuscator());
			var immutableTransaction = transaction.ConvertToImmutableTransaction();
			var errorData = ErrorData.TryGetErrorData(immutableTransaction, _configurationService);
			var transactionMetricName = new TransactionMetricName("WebTransaction", "TransactionName");
			var apdexT = null as TimeSpan?;
			var totalTime = TimeSpan.FromSeconds(1);
			var txStats = new TransactionMetricStatsCollection(new TransactionMetricName("WebTransaction", "myTx"));
			txStats.MergeUnscopedStats(MetricNames.DatastoreAll, MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5)));
			txStats.MergeUnscopedStats(MetricNames.DatastoreAll, MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(4)));
			txStats.MergeUnscopedStats(MetricNames.DatastoreAll, MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2)));

			// ACT
			var attributes = _transactionAttributeMaker.GetAttributes(immutableTransaction, transactionMetricName, apdexT, totalTime, errorData, txStats);

			// ACQUIRE
			var transactionAttributes = attributes.GetIntrinsics()
				.Concat(attributes.GetAgentAttributes())
				.Concat(attributes.GetUserAttributes())
				.ToDictionary(attr => attr.Key, attr => attr.Value);

			// ASSERT
			NrAssert.Multiple(
				() => Assert.AreEqual(10, attributes.Count()),  // Assert that only these attributes are generated
				() => Assert.AreEqual("Transaction", transactionAttributes["type"]),
				() => Assert.AreEqual(expectedStartTime.ToUnixTime(), transactionAttributes["timestamp"]),
				() => Assert.AreEqual("WebTransaction/TransactionName", transactionAttributes["name"]),
				() => Assert.AreEqual("WebTransaction/TransactionName", transactionAttributes["transactionName"]),
				() => Assert.AreEqual(0.5, transactionAttributes["duration"]),
				() => Assert.AreEqual(1, transactionAttributes["totalTime"]),
				() => Assert.NotNull(transactionAttributes["nr.tripId"]),
				() => Assert.NotNull(transactionAttributes["databaseDuration"]),
				() => Assert.AreEqual(11, transactionAttributes["databaseDuration"]),
				() => Assert.NotNull(transactionAttributes["databaseCallCount"]),
				() => Assert.AreEqual(3, transactionAttributes["databaseCallCount"])
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
			var transaction = new Transaction(_configuration, new OtherTransactionName("transactionCategory", "transactionName"), timer, expectedStartTime, Mock.Create<ICallStackManager>(), SqlObfuscator.GetObfuscatingSqlObfuscator());
			var immutableTransaction = transaction.ConvertToImmutableTransaction();
			var errorData = ErrorData.TryGetErrorData(immutableTransaction, _configurationService);
			var transactionMetricName = new TransactionMetricName("WebTransaction", "TransactionName");
			var apdexT = null as TimeSpan?;
			var totalTime = TimeSpan.FromSeconds(1);
			var txStats = new TransactionMetricStatsCollection(new TransactionMetricName("WebTransaction", "myTx"));
			txStats.MergeUnscopedStats(MetricNames.ExternalAll, MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5)));
			// ACT
			var attributes = _transactionAttributeMaker.GetAttributes(immutableTransaction, transactionMetricName, apdexT, totalTime, errorData, txStats);

			// ACQUIRE
			var transactionAttributes = attributes.GetIntrinsics()
				.Concat(attributes.GetAgentAttributes())
				.Concat(attributes.GetUserAttributes())
				.ToDictionary(attr => attr.Key, attr => attr.Value);

			// ASSERT
			NrAssert.Multiple(
				() => Assert.AreEqual(10, attributes.Count()),  // Assert that only these attributes are generated
				() => Assert.AreEqual("Transaction", transactionAttributes["type"]),
				() => Assert.AreEqual(expectedStartTime.ToUnixTime(), transactionAttributes["timestamp"]),
				() => Assert.AreEqual("WebTransaction/TransactionName", transactionAttributes["name"]),
				() => Assert.AreEqual("WebTransaction/TransactionName", transactionAttributes["transactionName"]),
				() => Assert.AreEqual(0.5, transactionAttributes["duration"]),
				() => Assert.AreEqual(1, transactionAttributes["totalTime"]),
				() => Assert.NotNull(transactionAttributes["nr.tripId"]),
				() => Assert.NotNull(transactionAttributes["externalDuration"]),
				() => Assert.AreEqual(5, transactionAttributes["externalDuration"]),
				() => Assert.NotNull(transactionAttributes["externalCallCount"]),
				() => Assert.AreEqual(1, transactionAttributes["externalCallCount"])
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

			var transaction = new Transaction(_configuration, new OtherTransactionName("transactionCategory", "transactionName"), timer, expectedStartTime, Mock.Create<ICallStackManager>(), SqlObfuscator.GetObfuscatingSqlObfuscator());
			var immutableTransaction = transaction.ConvertToImmutableTransaction();
			var errorData = ErrorData.TryGetErrorData(immutableTransaction, _configurationService);
			var transactionMetricName = new TransactionMetricName("WebTransaction", "TransactionName");
			var apdexT = null as TimeSpan?;
			var totalTime = TimeSpan.FromSeconds(1);
			var txStats = new TransactionMetricStatsCollection(new TransactionMetricName("WebTransaction", "myTx"));
			txStats.MergeUnscopedStats(MetricNames.ExternalAll, MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5)));
			txStats.MergeUnscopedStats(MetricNames.ExternalAll, MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(4)));
			txStats.MergeUnscopedStats(MetricNames.ExternalAll, MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2)));

			// ACT
			var attributes = _transactionAttributeMaker.GetAttributes(immutableTransaction, transactionMetricName, apdexT, totalTime, errorData, txStats);

			// ACQUIRE
			var transactionAttributes = attributes.GetIntrinsics()
				.Concat(attributes.GetAgentAttributes())
				.Concat(attributes.GetUserAttributes())
				.ToDictionary(attr => attr.Key, attr => attr.Value);

			// ASSERT
			NrAssert.Multiple(
				() => Assert.AreEqual(10, attributes.Count()),  // Assert that only these attributes are generated
				() => Assert.AreEqual("Transaction", transactionAttributes["type"]),
				() => Assert.AreEqual(expectedStartTime.ToUnixTime(), transactionAttributes["timestamp"]),
				() => Assert.AreEqual("WebTransaction/TransactionName", transactionAttributes["name"]),
				() => Assert.AreEqual("WebTransaction/TransactionName", transactionAttributes["transactionName"]),
				() => Assert.AreEqual(0.5, transactionAttributes["duration"]),
				() => Assert.AreEqual(1, transactionAttributes["totalTime"]),
				() => Assert.NotNull(transactionAttributes["nr.tripId"]),
				() => Assert.NotNull(transactionAttributes["externalDuration"]),
				() => Assert.AreEqual(11, transactionAttributes["externalDuration"]),
				() => Assert.NotNull(transactionAttributes["externalCallCount"]),
				() => Assert.AreEqual(3, transactionAttributes["externalCallCount"])
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
			var transaction = new Transaction(_configuration, new WebTransactionName("transactionCategory", "transactionName"), timer, expectedStartTime, Mock.Create<ICallStackManager>(), SqlObfuscator.GetObfuscatingSqlObfuscator());
			transaction.TransactionMetadata.AddServiceParameter("serviceRequestKey", "serviceRequestValue");
			transaction.TransactionMetadata.AddRequestParameter("requestParameterKey", "requestParameterValue");
			transaction.TransactionMetadata.AddUserAttribute("userAttributeKey", "userAttributeValue");
			transaction.TransactionMetadata.SetHttpResponseStatusCode(400, null);
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
			var errorData = ErrorData.TryGetErrorData(immutableTransaction, _configurationService);

			var totalTime = TimeSpan.FromSeconds(1);

			// ACT
			var attributes = _transactionAttributeMaker.GetAttributes(immutableTransaction, transactionMetricName, apdexT, totalTime, errorData, txStats);

			// ACQUIRE
			var transactionAttributes = attributes.GetIntrinsics()
				.Concat(attributes.GetAgentAttributes())
				.Concat(attributes.GetUserAttributes())
				.ToDictionary(attr => attr.Key, attr => attr.Value);

			// ASSERT
			NrAssert.Multiple(
				() => Assert.AreEqual(32, attributes.Count()),  // Assert that only these attributes are generated
				() => Assert.AreEqual("Transaction", transactionAttributes["type"]),
				() => Assert.AreEqual((expectedStartTime + expectedDuration).ToUnixTime(), transactionAttributes["timestamp"]),
				() => Assert.AreEqual("WebTransaction/TransactionName", transactionAttributes["name"]),
				() => Assert.AreEqual("WebTransaction/TransactionName", transactionAttributes["transactionName"]),
				() => Assert.AreEqual(immutableTransaction.Guid, transactionAttributes["nr.guid"]),
				() => Assert.AreEqual(0.5, transactionAttributes["duration"]),
				() => Assert.AreEqual(1, transactionAttributes["totalTime"]),
				() => Assert.AreEqual(0.5, transactionAttributes["webDuration"]),
				() => Assert.AreEqual(1, transactionAttributes["queueDuration"]),
				() => Assert.AreEqual(2, transactionAttributes["externalDuration"]),
				() => Assert.True(transactionAttributes.ContainsKey("externalCallCount")),
				() => Assert.True(transactionAttributes.ContainsKey("nr.apdexPerfZone")),
				() => Assert.AreEqual("originalUri", transactionAttributes["original_url"]),
				() => Assert.AreEqual("uri", transactionAttributes["request_uri"]),
				() => Assert.AreEqual("referrerUri", transactionAttributes["request.referer"]),
				() => Assert.AreEqual("1000", transactionAttributes["queue_wait_time_ms"]),
				() => Assert.AreEqual("400", transactionAttributes["response.status"]),
				() => Assert.AreEqual("requestParameterValue", transactionAttributes["request.parameters.requestParameterKey"]),
				() => Assert.AreEqual("serviceRequestValue", transactionAttributes["service.request.serviceRequestKey"]),
				() => Assert.AreEqual("userAttributeValue", transactionAttributes["userAttributeKey"]),
				() => Assert.AreEqual("referrerProcessId", transactionAttributes["client_cross_process_id"]),
				() => Assert.AreEqual("referrerTripId", transactionAttributes["trip_id"]),
				() => Assert.AreEqual("referrerTripId", transactionAttributes["nr.tripId"]),
				() => Assert.AreEqual("pathHash2", transactionAttributes["path_hash"]),
				() => Assert.AreEqual("pathHash2", transactionAttributes["nr.pathHash"]),
				() => Assert.AreEqual("referringPathHash", transactionAttributes["nr.referringPathHash"]),
				() => Assert.AreEqual("referringTransactionGuid", transactionAttributes["referring_transaction_guid"]),
				() => Assert.AreEqual("referringTransactionGuid", transactionAttributes["nr.referringTransactionGuid"]),
				() => Assert.AreEqual("pathHash", transactionAttributes["nr.alternatePathHashes"]),
				() => Assert.AreEqual("400", transactionAttributes["error.class"]),
				() => Assert.AreEqual("400", transactionAttributes["errorType"]),
				() => Assert.AreEqual("Bad Request", transactionAttributes["errorMessage"])
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
			var transaction = new Transaction(_configuration, new WebTransactionName("transactionCategory", "transactionName"), timer, expectedStartTime, Mock.Create<ICallStackManager>(), SqlObfuscator.GetObfuscatingSqlObfuscator());
			transaction.TransactionMetadata.AddServiceParameter("serviceRequestKey", "serviceRequestValue");
			transaction.TransactionMetadata.AddRequestParameter("requestParameterKey", "requestParameterValue");
			transaction.TransactionMetadata.AddUserAttribute("userAttributeKey", "userAttributeValue");
			transaction.TransactionMetadata.SetHttpResponseStatusCode(400, null);
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
			var errorData = ErrorData.TryGetErrorData(immutableTransaction, _configurationService);

			var totalTime = TimeSpan.FromSeconds(1);

			// ACT
			var attributes = _transactionAttributeMaker.GetAttributes(immutableTransaction, transactionMetricName, apdexT, totalTime, errorData, txStats);

			// ACQUIRE
			var transactionAttributes = attributes.GetIntrinsics()
				.Concat(attributes.GetAgentAttributes())
				.Concat(attributes.GetUserAttributes())
				.ToDictionary(attr => attr.Key, attr => attr.Value);

			var tripId = immutableTransaction.Guid;
			// ASSERT
			NrAssert.Multiple(
				() => Assert.AreEqual(28, attributes.Count()),  // Assert that only these attributes are generated
				() => Assert.AreEqual("Transaction", transactionAttributes["type"]),
				() => Assert.AreEqual((expectedStartTime + expectedDuration).ToUnixTime(), transactionAttributes["timestamp"]),
				() => Assert.AreEqual("WebTransaction/TransactionName", transactionAttributes["name"]),
				() => Assert.AreEqual("WebTransaction/TransactionName", transactionAttributes["transactionName"]),
				() => Assert.AreEqual(immutableTransaction.Guid, transactionAttributes["nr.guid"]),
				() => Assert.AreEqual(0.5, transactionAttributes["duration"]),
				() => Assert.AreEqual(1, transactionAttributes["totalTime"]),
				() => Assert.AreEqual(0.5, transactionAttributes["webDuration"]),
				() => Assert.AreEqual(1, transactionAttributes["queueDuration"]),
				() => Assert.AreEqual(2, transactionAttributes["externalDuration"]),
				() => Assert.True(transactionAttributes.ContainsKey("externalCallCount")),
				() => Assert.True(transactionAttributes.ContainsKey("nr.apdexPerfZone")),
				() => Assert.AreEqual("originalUri", transactionAttributes["original_url"]),
				() => Assert.AreEqual("uri", transactionAttributes["request_uri"]),
				() => Assert.AreEqual("referrerUri", transactionAttributes["request.referer"]),
				() => Assert.AreEqual("1000", transactionAttributes["queue_wait_time_ms"]),
				() => Assert.AreEqual("400", transactionAttributes["response.status"]),
				() => Assert.AreEqual("requestParameterValue", transactionAttributes["request.parameters.requestParameterKey"]),
				() => Assert.AreEqual("serviceRequestValue", transactionAttributes["service.request.serviceRequestKey"]),
				() => Assert.AreEqual("userAttributeValue", transactionAttributes["userAttributeKey"]),
				() => Assert.AreEqual(tripId, transactionAttributes["trip_id"]),
				() => Assert.AreEqual(tripId, transactionAttributes["nr.tripId"]),
				() => Assert.AreEqual("pathHash2", transactionAttributes["path_hash"]),
				() => Assert.AreEqual("pathHash2", transactionAttributes["nr.pathHash"]),
				() => Assert.AreEqual("pathHash", transactionAttributes["nr.alternatePathHashes"]),
				() => Assert.AreEqual("400", transactionAttributes["error.class"]),
				() => Assert.AreEqual("400", transactionAttributes["errorType"]),
				() => Assert.AreEqual("Bad Request", transactionAttributes["errorMessage"])
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

			var transaction = new Transaction(_configuration, new WebTransactionName("transactionCategory", "transactionName"), timer, expectedStartTime, Mock.Create<ICallStackManager>(), SqlObfuscator.GetObfuscatingSqlObfuscator());
			transaction.TransactionMetadata.SetOriginalUri("SameUri");
			transaction.TransactionMetadata.SetUri("SameUri");
			var immutableTransaction = transaction.ConvertToImmutableTransaction();
			var errorData = ErrorData.TryGetErrorData(immutableTransaction, _configurationService);
			var totalTime = TimeSpan.FromSeconds(1);
			var txStats = new TransactionMetricStatsCollection(new TransactionMetricName("WebTransaction", "myTx"));

			// ACT
			var attributes = _transactionAttributeMaker.GetAttributes(immutableTransaction, transactionMetricName, apdexT, totalTime, errorData, txStats);

			// ACQUIRE
			var transactionAttributes = attributes.GetIntrinsics()
				.Concat(attributes.GetAgentAttributes())
				.Concat(attributes.GetUserAttributes())
				.ToDictionary(attr => attr.Key, attr => attr.Value);

			// ASSERT
			Assert.False(transactionAttributes.ContainsKey("originalUri"));
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

			var transaction = new Transaction(_configuration, new WebTransactionName("transactionCategory", "transactionName"), timer, expectedStartTime, Mock.Create<ICallStackManager>(), SqlObfuscator.GetObfuscatingSqlObfuscator());
			transaction.TransactionMetadata.AddServiceParameter("serviceRequestKey", "serviceRequestValue");
			transaction.TransactionMetadata.AddRequestParameter("requestParameterKey", "requestParameterValue");
			transaction.TransactionMetadata.AddUserAttribute("userAttributeKey", "userAttributeValue");
			transaction.TransactionMetadata.AddUserErrorAttribute("userErrorAttributeKey", "userErrorAttributeValue");
			transaction.TransactionMetadata.SetHttpResponseStatusCode(400, null);
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
			var errorData = ErrorData.TryGetErrorData(immutableTransaction, _configurationService);

			var totalTime = TimeSpan.FromSeconds(1);
			var txStats = new TransactionMetricStatsCollection(new TransactionMetricName("WebTransaction", "myTx"));
			txStats.MergeUnscopedStats(MetricNames.DatastoreAll, MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(3)));
			// ACT
			var attributes = _transactionAttributeMaker.GetAttributes(immutableTransaction, transactionMetricName, apdexT, totalTime, errorData, txStats);

			// ACQUIRE
			var transactionAttributes = attributes.GetIntrinsics()
				.Concat(attributes.GetAgentAttributes())
				.Concat(attributes.GetUserAttributes())
				.ToDictionary(attr => attr.Key, attr => attr.DefaultDestinations);

			// ASSERT
			NrAssert.Multiple(
				() => Assert.AreEqual(33, attributes.Count()),  // Assert that only these attributes are generated
				() => Assert.AreEqual(AttributeDestinations.TransactionEvent, transactionAttributes["type"]),
				() => Assert.AreEqual(AttributeDestinations.TransactionEvent | AttributeDestinations.ErrorEvent, transactionAttributes["timestamp"]),
				() => Assert.AreEqual(AttributeDestinations.TransactionEvent, transactionAttributes["name"]),
				() => Assert.AreEqual(AttributeDestinations.ErrorEvent, transactionAttributes["transactionName"]),
				() => Assert.AreEqual(AttributeDestinations.TransactionEvent | AttributeDestinations.ErrorEvent, transactionAttributes["nr.guid"]),
				() => Assert.AreEqual(AttributeDestinations.TransactionEvent | AttributeDestinations.ErrorEvent, transactionAttributes["duration"]),
				() => Assert.AreEqual(AttributeDestinations.TransactionEvent, transactionAttributes["webDuration"]),
				() => Assert.AreEqual(AttributeDestinations.TransactionEvent | AttributeDestinations.TransactionTrace, transactionAttributes["totalTime"]),
				() => Assert.AreEqual(AttributeDestinations.TransactionEvent | AttributeDestinations.ErrorEvent, transactionAttributes["databaseDuration"]),
				() => Assert.AreEqual(AttributeDestinations.TransactionEvent | AttributeDestinations.ErrorEvent, transactionAttributes["databaseCallCount"]),
				() => Assert.AreEqual(AttributeDestinations.TransactionEvent, transactionAttributes["nr.apdexPerfZone"]),
				() => Assert.AreEqual(AttributeDestinations.ErrorTrace | AttributeDestinations.TransactionTrace, transactionAttributes["client_cross_process_id"]),
				() => Assert.AreEqual(AttributeDestinations.ErrorTrace | AttributeDestinations.TransactionTrace, transactionAttributes["trip_id"]),
				() => Assert.AreEqual(AttributeDestinations.TransactionEvent, transactionAttributes["nr.tripId"]),
				() => Assert.AreEqual(AttributeDestinations.ErrorTrace | AttributeDestinations.TransactionTrace, transactionAttributes["path_hash"]),
				() => Assert.AreEqual(AttributeDestinations.TransactionEvent, transactionAttributes["nr.pathHash"]),
				() => Assert.AreEqual(AttributeDestinations.TransactionEvent, transactionAttributes["nr.referringPathHash"]),
				() => Assert.AreEqual(AttributeDestinations.ErrorTrace | AttributeDestinations.TransactionTrace, transactionAttributes["referring_transaction_guid"]),
				() => Assert.AreEqual(AttributeDestinations.TransactionEvent | AttributeDestinations.ErrorEvent, transactionAttributes["nr.referringTransactionGuid"]),
				() => Assert.AreEqual(AttributeDestinations.TransactionEvent, transactionAttributes["nr.alternatePathHashes"]),
				() => Assert.AreEqual(AttributeDestinations.ErrorTrace | AttributeDestinations.TransactionTrace | AttributeDestinations.ErrorEvent, transactionAttributes["original_url"]),
				() => Assert.AreEqual(AttributeDestinations.TransactionEvent | AttributeDestinations.ErrorEvent, transactionAttributes["request_uri"]),
				() => Assert.AreEqual(AttributeDestinations.ErrorTrace | AttributeDestinations.TransactionTrace | AttributeDestinations.ErrorEvent, transactionAttributes["request.referer"]),
				() => Assert.AreEqual(AttributeDestinations.ErrorTrace | AttributeDestinations.TransactionTrace | AttributeDestinations.ErrorEvent, transactionAttributes["queue_wait_time_ms"]),
				() => Assert.AreEqual(AttributeDestinations.TransactionEvent | AttributeDestinations.ErrorTrace | AttributeDestinations.TransactionTrace | AttributeDestinations.ErrorEvent, transactionAttributes["response.status"]),
				() => Assert.AreEqual(AttributeDestinations.None, transactionAttributes["request.parameters.requestParameterKey"]),
				() => Assert.AreEqual(AttributeDestinations.None, transactionAttributes["service.request.serviceRequestKey"]),
				() => Assert.AreEqual(AttributeDestinations.All, transactionAttributes["userAttributeKey"]),
				() => Assert.AreEqual(AttributeDestinations.ErrorEvent | AttributeDestinations.ErrorTrace, transactionAttributes["userErrorAttributeKey"]),
				() => Assert.AreEqual(AttributeDestinations.ErrorEvent, transactionAttributes["error.class"]),
				() => Assert.AreEqual(AttributeDestinations.TransactionEvent, transactionAttributes["errorType"]),
				() => Assert.AreEqual(AttributeDestinations.TransactionEvent, transactionAttributes["errorMessage"])
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

			var transaction = new Transaction(_configuration, new WebTransactionName("transactionCategory", "transactionName"), timer, expectedStartTime, Mock.Create<ICallStackManager>(), SqlObfuscator.GetObfuscatingSqlObfuscator());
			transaction.TransactionMetadata.AddServiceParameter("serviceRequestKey", "serviceRequestValue");
			transaction.TransactionMetadata.AddRequestParameter("requestParameterKey", "requestParameterValue");
			transaction.TransactionMetadata.AddUserAttribute("userAttributeKey", "userAttributeValue");
			transaction.TransactionMetadata.AddUserErrorAttribute("userErrorAttributeKey", "userErrorAttributeValue");
			transaction.TransactionMetadata.SetHttpResponseStatusCode(400, null);
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
			var errorData = ErrorData.TryGetErrorData(immutableTransaction, _configurationService);

			var totalTime = TimeSpan.FromSeconds(1);
			var txStats = new TransactionMetricStatsCollection(new TransactionMetricName("WebTransaction", "myTx"));
			txStats.MergeUnscopedStats(MetricNames.ExternalAll, MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2)));

			// ACT
			var attributes = _transactionAttributeMaker.GetAttributes(immutableTransaction, transactionMetricName, apdexT, totalTime, errorData, txStats);

			// ACQUIRE
			var transactionAttributes = attributes.GetIntrinsics()
				.Concat(attributes.GetAgentAttributes())
				.Concat(attributes.GetUserAttributes())
				.ToDictionary(attr => attr.Key, attr => attr.Classification);

			// ASSERT
			NrAssert.Multiple(
				() => Assert.AreEqual(33, attributes.Count()),  // Assert that only these attributes are generated
				() => Assert.AreEqual(AttributeClassification.Intrinsics, transactionAttributes["type"]),
				() => Assert.AreEqual(AttributeClassification.Intrinsics, transactionAttributes["timestamp"]),
				() => Assert.AreEqual(AttributeClassification.Intrinsics, transactionAttributes["name"]),
				() => Assert.AreEqual(AttributeClassification.Intrinsics, transactionAttributes["transactionName"]),
				() => Assert.AreEqual(AttributeClassification.Intrinsics, transactionAttributes["nr.guid"]),
				() => Assert.AreEqual(AttributeClassification.Intrinsics, transactionAttributes["duration"]),
				() => Assert.AreEqual(AttributeClassification.Intrinsics | AttributeClassification.Intrinsics, transactionAttributes["totalTime"]),
				() => Assert.AreEqual(AttributeClassification.Intrinsics, transactionAttributes["webDuration"]),
				() => Assert.AreEqual(AttributeClassification.Intrinsics, transactionAttributes["queueDuration"]),
				() => Assert.AreEqual(AttributeClassification.Intrinsics, transactionAttributes["externalDuration"]),
				() => Assert.AreEqual(AttributeClassification.Intrinsics, transactionAttributes["externalCallCount"]),
				() => Assert.AreEqual(AttributeClassification.Intrinsics, transactionAttributes["nr.apdexPerfZone"]),
				() => Assert.AreEqual(AttributeClassification.AgentAttributes, transactionAttributes["original_url"]),
				() => Assert.AreEqual(AttributeClassification.AgentAttributes, transactionAttributes["request_uri"]),
				() => Assert.AreEqual(AttributeClassification.AgentAttributes, transactionAttributes["request.referer"]),
				() => Assert.AreEqual(AttributeClassification.AgentAttributes, transactionAttributes["queue_wait_time_ms"]),
				() => Assert.AreEqual(AttributeClassification.AgentAttributes, transactionAttributes["response.status"]),
				() => Assert.AreEqual(AttributeClassification.AgentAttributes, transactionAttributes["request.parameters.requestParameterKey"]),
				() => Assert.AreEqual(AttributeClassification.AgentAttributes, transactionAttributes["service.request.serviceRequestKey"]),
				() => Assert.AreEqual(AttributeClassification.UserAttributes, transactionAttributes["userAttributeKey"]),
				() => Assert.AreEqual(AttributeClassification.UserAttributes, transactionAttributes["userErrorAttributeKey"]),
				() => Assert.AreEqual(AttributeClassification.Intrinsics, transactionAttributes["client_cross_process_id"]),
				() => Assert.AreEqual(AttributeClassification.Intrinsics, transactionAttributes["trip_id"]),
				() => Assert.AreEqual(AttributeClassification.Intrinsics, transactionAttributes["nr.tripId"]),
				() => Assert.AreEqual(AttributeClassification.Intrinsics, transactionAttributes["path_hash"]),
				() => Assert.AreEqual(AttributeClassification.Intrinsics, transactionAttributes["nr.pathHash"]),
				() => Assert.AreEqual(AttributeClassification.Intrinsics, transactionAttributes["nr.referringPathHash"]),
				() => Assert.AreEqual(AttributeClassification.Intrinsics, transactionAttributes["referring_transaction_guid"]),
				() => Assert.AreEqual(AttributeClassification.Intrinsics, transactionAttributes["nr.referringTransactionGuid"]),
				() => Assert.AreEqual(AttributeClassification.Intrinsics, transactionAttributes["nr.alternatePathHashes"]),
				() => Assert.AreEqual(AttributeClassification.Intrinsics, transactionAttributes["error.class"]),
				() => Assert.AreEqual(AttributeClassification.Intrinsics, transactionAttributes["errorType"]),
				() => Assert.AreEqual(AttributeClassification.Intrinsics, transactionAttributes["errorMessage"])
			);
		}

		public void GetAttributes_AssignsCorrectClassificationToAttributes_ExternalAndDB()
		{
			// ARRANGE
			var timer = Mock.Create<ITimer>();
			var expectedStartTime = DateTime.Now;
			var expectedDuration = TimeSpan.FromMilliseconds(500);
			Mock.Arrange(() => timer.Duration).Returns(expectedDuration);
			var transactionMetricName = new TransactionMetricName("WebTransaction", "TransactionName");
			var apdexT = TimeSpan.FromSeconds(2);

			var transaction = new Transaction(_configuration, new WebTransactionName("transactionCategory", "transactionName"), timer, expectedStartTime, Mock.Create<ICallStackManager>(), SqlObfuscator.GetObfuscatingSqlObfuscator());
			transaction.TransactionMetadata.AddServiceParameter("serviceRequestKey", "serviceRequestValue");
			transaction.TransactionMetadata.AddRequestParameter("requestParameterKey", "requestParameterValue");
			transaction.TransactionMetadata.AddUserAttribute("userAttributeKey", "userAttributeValue");
			transaction.TransactionMetadata.AddUserErrorAttribute("userErrorAttributeKey", "userErrorAttributeValue");
			transaction.TransactionMetadata.SetHttpResponseStatusCode(400, null);
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
			var errorData = ErrorData.TryGetErrorData(immutableTransaction, _configurationService);

			var totalTime = TimeSpan.FromSeconds(1);
			var txStats = new TransactionMetricStatsCollection(new TransactionMetricName("WebTransaction", "myTx"));
			txStats.MergeUnscopedStats(MetricNames.ExternalAll, MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2)));
			txStats.MergeUnscopedStats(MetricNames.DatastoreAll, MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(3)));

			// ACT
			var attributes = _transactionAttributeMaker.GetAttributes(immutableTransaction, transactionMetricName, apdexT, totalTime, errorData, txStats);

			// ACQUIRE
			var transactionAttributes = attributes.GetIntrinsics()
				.Concat(attributes.GetAgentAttributes())
				.Concat(attributes.GetUserAttributes())
				.ToDictionary(attr => attr.Key, attr => attr.Classification);

			// ASSERT
			NrAssert.Multiple(
				() => Assert.AreEqual(34, attributes.Count()),  // Assert that only these attributes are generated
				() => Assert.AreEqual(AttributeClassification.Intrinsics, transactionAttributes["type"]),
				() => Assert.AreEqual(AttributeClassification.Intrinsics, transactionAttributes["timestamp"]),
				() => Assert.AreEqual(AttributeClassification.Intrinsics, transactionAttributes["name"]),
				() => Assert.AreEqual(AttributeClassification.Intrinsics, transactionAttributes["transactionName"]),
				() => Assert.AreEqual(AttributeClassification.Intrinsics, transactionAttributes["nr.guid"]),
				() => Assert.AreEqual(AttributeClassification.Intrinsics, transactionAttributes["duration"]),
				() => Assert.AreEqual(AttributeClassification.Intrinsics | AttributeClassification.Intrinsics, transactionAttributes["totalTime"]),
				() => Assert.AreEqual(AttributeClassification.Intrinsics, transactionAttributes["webDuration"]),
				() => Assert.AreEqual(AttributeClassification.Intrinsics, transactionAttributes["queueDuration"]),
				() => Assert.AreEqual(AttributeClassification.Intrinsics, transactionAttributes["externalDuration"]),
				() => Assert.AreEqual(AttributeClassification.Intrinsics, transactionAttributes["externalCallCount"]),
				() => Assert.AreEqual(AttributeClassification.Intrinsics, transactionAttributes["databaseDuration"]),
				() => Assert.AreEqual(AttributeClassification.Intrinsics, transactionAttributes["databaseCallCount"]),
				() => Assert.AreEqual(AttributeClassification.Intrinsics, transactionAttributes["nr.apdexPerfZone"]),
				() => Assert.AreEqual(AttributeClassification.AgentAttributes, transactionAttributes["original_url"]),
				() => Assert.AreEqual(AttributeClassification.AgentAttributes, transactionAttributes["request.referer"]),
				() => Assert.AreEqual(AttributeClassification.AgentAttributes, transactionAttributes["queue_wait_time_ms"]),
				() => Assert.AreEqual(AttributeClassification.AgentAttributes, transactionAttributes["response.status"]),
				() => Assert.AreEqual(AttributeClassification.AgentAttributes, transactionAttributes["request.parameters.requestParameterKey"]),
				() => Assert.AreEqual(AttributeClassification.AgentAttributes, transactionAttributes["service.request.serviceRequestKey"]),
				() => Assert.AreEqual(AttributeClassification.UserAttributes, transactionAttributes["userAttributeKey"]),
				() => Assert.AreEqual(AttributeClassification.UserAttributes, transactionAttributes["userErrorAttributeKey"]),
				() => Assert.AreEqual(AttributeClassification.Intrinsics, transactionAttributes["client_cross_process_id"]),
				() => Assert.AreEqual(AttributeClassification.Intrinsics, transactionAttributes["trip_id"]),
				() => Assert.AreEqual(AttributeClassification.Intrinsics, transactionAttributes["nr.tripId"]),
				() => Assert.AreEqual(AttributeClassification.Intrinsics, transactionAttributes["path_hash"]),
				() => Assert.AreEqual(AttributeClassification.Intrinsics, transactionAttributes["nr.pathHash"]),
				() => Assert.AreEqual(AttributeClassification.Intrinsics, transactionAttributes["nr.referringPathHash"]),
				() => Assert.AreEqual(AttributeClassification.Intrinsics, transactionAttributes["referring_transaction_guid"]),
				() => Assert.AreEqual(AttributeClassification.Intrinsics, transactionAttributes["nr.referringTransactionGuid"]),
				() => Assert.AreEqual(AttributeClassification.Intrinsics, transactionAttributes["nr.alternatePathHashes"]),
				() => Assert.AreEqual(AttributeClassification.Intrinsics, transactionAttributes["error.class"]),
				() => Assert.AreEqual(AttributeClassification.Intrinsics, transactionAttributes["errorType"]),
				() => Assert.AreEqual(AttributeClassification.Intrinsics, transactionAttributes["errorMessage"])
			);
		}

		[Test]
		public void GetAttributes_DoesNotReturnWebDurationAttribute_IfNonWebTransaction()
		{
			// ARRANGE

			var tranasctionBuilder = new Transaction(_configuration, new OtherTransactionName("transactionCategory", "transactionName"), Mock.Create<ITimer>(), DateTime.UtcNow, Mock.Create<ICallStackManager>(), SqlObfuscator.GetObfuscatingSqlObfuscator());
			var transaction = tranasctionBuilder.ConvertToImmutableTransaction();

			var transactionMetricName = new TransactionMetricName("WebTransaction", "TransactionName");
			var apdexT = TimeSpan.FromSeconds(2);
			var totalTime = TimeSpan.FromSeconds(1);
			var txStats = new TransactionMetricStatsCollection(new TransactionMetricName("WebTransaction", "myTx"));
			// ACT
			var attributes = _transactionAttributeMaker.GetAttributes(transaction, transactionMetricName, apdexT, totalTime, new ErrorData(), txStats);

			// ACQUIRE
			var transactionAttributes = attributes.GetIntrinsics()
				.Concat(attributes.GetAgentAttributes())
				.Concat(attributes.GetUserAttributes())
				.ToDictionary(attr => attr.Key, attr => attr.Value);

			// ASSERT
			Assert.False(transactionAttributes.ContainsKey("webDuration"));
		}

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

			var transaction = new Transaction(_configuration, new WebTransactionName("transactionCategory", "transactionName"), timer, expectedStartTime, Mock.Create<ICallStackManager>(), SqlObfuscator.GetObfuscatingSqlObfuscator());
			transaction.TransactionMetadata.AddServiceParameter("serviceRequestKey", "serviceRequestValue");
			transaction.TransactionMetadata.AddRequestParameter("requestParameterKey", "requestParameterValue");
			transaction.TransactionMetadata.AddUserAttribute("userAttributeKey", "userAttributeValue");
			transaction.TransactionMetadata.AddUserErrorAttribute("userErrorAttributeKey", "userErrorAttributeValue");
			transaction.TransactionMetadata.SetHttpResponseStatusCode(400, null);
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
				() => Assert.AreEqual(9, builderAttributes.Count()),  // Assert that only these attributes are generated
				() => Assert.AreEqual("originalUri", txBuilderAttributes["original_url"]),
				() => Assert.AreEqual("uri", transactionAttributes["request_uri"]),
				() => Assert.AreEqual("referrerUri", txBuilderAttributes["request.referer"]),
				() => Assert.AreEqual("1000", txBuilderAttributes["queue_wait_time_ms"]),
				() => Assert.AreEqual("400", txBuilderAttributes["response.status"]),
				() => Assert.AreEqual("requestParameterValue", txBuilderAttributes["request.parameters.requestParameterKey"]),
				() => Assert.AreEqual("serviceRequestValue", txBuilderAttributes["service.request.serviceRequestKey"]),
				() => Assert.AreEqual("userAttributeValue", txBuilderAttributes["userAttributeKey"]),
				() => Assert.AreEqual("userErrorAttributeValue", txBuilderAttributes["userErrorAttributeKey"])
			);
			NrAssert.Multiple(
				() => Assert.AreEqual(9, attributes.Count()),  // Assert that only these attributes are generated
				() => Assert.AreEqual("originalUri", transactionAttributes["original_url"]),
				() => Assert.AreEqual("uri", transactionAttributes["request_uri"]),
				() => Assert.AreEqual("referrerUri", transactionAttributes["request.referer"]),
				() => Assert.AreEqual("1000", transactionAttributes["queue_wait_time_ms"]),
				() => Assert.AreEqual("400", transactionAttributes["response.status"]),
				() => Assert.AreEqual("requestParameterValue", transactionAttributes["request.parameters.requestParameterKey"]),
				() => Assert.AreEqual("serviceRequestValue", transactionAttributes["service.request.serviceRequestKey"]),
				() => Assert.AreEqual("userAttributeValue", transactionAttributes["userAttributeKey"]),
				() => Assert.AreEqual("userErrorAttributeValue", transactionAttributes["userErrorAttributeKey"])
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

			var transaction = new Transaction(_configuration, new WebTransactionName("transactionCategory", "transactionName"), timer, expectedStartTime, Mock.Create<ICallStackManager>(), SqlObfuscator.GetObfuscatingSqlObfuscator());
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
			var transaction = new Transaction(_configuration, new WebTransactionName("transactionCategory", "transactionName"), timer, expectedStartTime, Mock.Create<ICallStackManager>(), SqlObfuscator.GetObfuscatingSqlObfuscator());
			transaction.TransactionMetadata.AddServiceParameter("serviceRequestKey", "serviceRequestValue");
			transaction.TransactionMetadata.AddRequestParameter("requestParameterKey", "requestParameterValue");
			transaction.TransactionMetadata.AddUserAttribute("userAttributeKey", "userAttributeValue");
			transaction.TransactionMetadata.AddUserErrorAttribute("userErrorAttributeKey", "userErrorAttributeValue");
			transaction.TransactionMetadata.SetHttpResponseStatusCode(400, null);
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
				() => Assert.AreEqual(9, builderAttributes.Count()),  // Assert that only these attributes are generated
				() => Assert.AreEqual(AttributeDestinations.ErrorTrace | AttributeDestinations.TransactionTrace | AttributeDestinations.ErrorEvent, txBuilderAttributes["original_url"]),
				() => Assert.AreEqual(AttributeDestinations.TransactionEvent | AttributeDestinations.ErrorEvent, txBuilderAttributes["request_uri"]),
				() => Assert.AreEqual(AttributeDestinations.ErrorTrace | AttributeDestinations.TransactionTrace | AttributeDestinations.ErrorEvent, txBuilderAttributes["request.referer"]),
				() => Assert.AreEqual(AttributeDestinations.ErrorTrace | AttributeDestinations.TransactionTrace | AttributeDestinations.ErrorEvent, txBuilderAttributes["queue_wait_time_ms"]),
				() => Assert.AreEqual(AttributeDestinations.TransactionEvent | AttributeDestinations.ErrorTrace | AttributeDestinations.TransactionTrace | AttributeDestinations.ErrorEvent, txBuilderAttributes["response.status"]),
				() => Assert.AreEqual(AttributeDestinations.None, txBuilderAttributes["request.parameters.requestParameterKey"]),
				() => Assert.AreEqual(AttributeDestinations.None, txBuilderAttributes["service.request.serviceRequestKey"]),
				() => Assert.AreEqual(AttributeDestinations.All, txBuilderAttributes["userAttributeKey"]),
				() => Assert.AreEqual(AttributeDestinations.ErrorEvent | AttributeDestinations.ErrorTrace, txBuilderAttributes["userErrorAttributeKey"])
			);
			NrAssert.Multiple(
				() => Assert.AreEqual(9, attributes.Count()),  // Assert that only these attributes are generated
				() => Assert.AreEqual(AttributeDestinations.ErrorTrace | AttributeDestinations.TransactionTrace | AttributeDestinations.ErrorEvent, transactionAttributes["original_url"]),
				() => Assert.AreEqual(AttributeDestinations.TransactionEvent | AttributeDestinations.ErrorEvent, txBuilderAttributes["request_uri"]),
				() => Assert.AreEqual(AttributeDestinations.ErrorTrace | AttributeDestinations.TransactionTrace | AttributeDestinations.ErrorEvent, transactionAttributes["request.referer"]),
				() => Assert.AreEqual(AttributeDestinations.ErrorTrace | AttributeDestinations.TransactionTrace | AttributeDestinations.ErrorEvent, transactionAttributes["queue_wait_time_ms"]),
				() => Assert.AreEqual(AttributeDestinations.TransactionEvent | AttributeDestinations.ErrorTrace | AttributeDestinations.TransactionTrace | AttributeDestinations.ErrorEvent, transactionAttributes["response.status"]),
				() => Assert.AreEqual(AttributeDestinations.None, transactionAttributes["request.parameters.requestParameterKey"]),
				() => Assert.AreEqual(AttributeDestinations.None, transactionAttributes["service.request.serviceRequestKey"]),
				() => Assert.AreEqual(AttributeDestinations.All, transactionAttributes["userAttributeKey"]),
				() => Assert.AreEqual(AttributeDestinations.ErrorEvent | AttributeDestinations.ErrorTrace, transactionAttributes["userErrorAttributeKey"])
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
			var transaction = new Transaction(_configuration, new WebTransactionName("transactionCategory", "transactionName"), timer, expectedStartTime, Mock.Create<ICallStackManager>(), SqlObfuscator.GetObfuscatingSqlObfuscator());
			transaction.TransactionMetadata.AddServiceParameter("serviceRequestKey", "serviceRequestValue");
			transaction.TransactionMetadata.AddRequestParameter("requestParameterKey", "requestParameterValue");
			transaction.TransactionMetadata.AddUserAttribute("userAttributeKey", "userAttributeValue");
			transaction.TransactionMetadata.AddUserErrorAttribute("userErrorAttributeKey", "userErrorAttributeValue");
			transaction.TransactionMetadata.SetHttpResponseStatusCode(400, null);
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
			var builderAttributes = _transactionAttributeMaker.GetUserAndAgentAttributes(immutableTransaction.TransactionMetadata);
			var attributes = _transactionAttributeMaker.GetUserAndAgentAttributes(immutableTransaction.TransactionMetadata);

			// ACQUIRE
			var txBuilderAttributes = builderAttributes.GetIntrinsics()
				.Concat(attributes.GetAgentAttributes())
				.Concat(attributes.GetUserAttributes())
				.ToDictionary(attr => attr.Key, attr => attr.Classification);
			var transactionAttributes = attributes.GetIntrinsics()
				.Concat(attributes.GetAgentAttributes())
				.Concat(attributes.GetUserAttributes())
				.ToDictionary(attr => attr.Key, attr => attr.Classification);

			// ASSERT
			NrAssert.Multiple(
				() => Assert.AreEqual(9, builderAttributes.Count()),  // Assert that only these attributes are generated
				() => Assert.AreEqual(AttributeClassification.AgentAttributes, txBuilderAttributes["original_url"]),
				() => Assert.AreEqual(AttributeClassification.AgentAttributes, txBuilderAttributes["request_uri"]),
				() => Assert.AreEqual(AttributeClassification.AgentAttributes, txBuilderAttributes["request.referer"]),
				() => Assert.AreEqual(AttributeClassification.AgentAttributes, txBuilderAttributes["queue_wait_time_ms"]),
				() => Assert.AreEqual(AttributeClassification.AgentAttributes, txBuilderAttributes["response.status"]),
				() => Assert.AreEqual(AttributeClassification.AgentAttributes, txBuilderAttributes["request.parameters.requestParameterKey"]),
				() => Assert.AreEqual(AttributeClassification.AgentAttributes, txBuilderAttributes["service.request.serviceRequestKey"]),
				() => Assert.AreEqual(AttributeClassification.UserAttributes, txBuilderAttributes["userAttributeKey"]),
				() => Assert.AreEqual(AttributeClassification.UserAttributes, txBuilderAttributes["userErrorAttributeKey"])
			);
			NrAssert.Multiple(
				() => Assert.AreEqual(9, attributes.Count()),  // Assert that only these attributes are generated
				() => Assert.AreEqual(AttributeClassification.AgentAttributes, transactionAttributes["original_url"]),
				() => Assert.AreEqual(AttributeClassification.AgentAttributes, transactionAttributes["request_uri"]),
				() => Assert.AreEqual(AttributeClassification.AgentAttributes, transactionAttributes["request.referer"]),
				() => Assert.AreEqual(AttributeClassification.AgentAttributes, transactionAttributes["queue_wait_time_ms"]),
				() => Assert.AreEqual(AttributeClassification.AgentAttributes, transactionAttributes["response.status"]),
				() => Assert.AreEqual(AttributeClassification.AgentAttributes, transactionAttributes["request.parameters.requestParameterKey"]),
				() => Assert.AreEqual(AttributeClassification.AgentAttributes, transactionAttributes["service.request.serviceRequestKey"]),
				() => Assert.AreEqual(AttributeClassification.UserAttributes, transactionAttributes["userAttributeKey"]),
				() => Assert.AreEqual(AttributeClassification.UserAttributes, transactionAttributes["userErrorAttributeKey"])
			);
		}

		#endregion GetUserAndAgentAttributes

	}
}
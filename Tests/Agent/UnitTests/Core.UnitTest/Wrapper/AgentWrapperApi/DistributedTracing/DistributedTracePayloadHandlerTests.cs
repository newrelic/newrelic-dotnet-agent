using NewRelic.Agent.Api;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.Api;
using NewRelic.Agent.Core.DistributedTracing;
using NewRelic.Agent.Core.Segments;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Core.DistributedTracing;
using NewRelic.Testing.Assertions;
using NUnit.Framework;
using System;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.Wrapper.AgentWrapperApi.DistributedTracing
{
	[TestFixture]
	public class DistributedTracePayloadHandlerTests
	{
		private const string DistributedTraceHeaderName = "NewRelic";

		private const string DtTypeApp = "App";
		private const string IncomingDtType = "Mobile";
		private const string AgentAccountId = "273070";
		private const string IncomingAccountId = "222222";
		private const string AgentApplicationId = "238575";
		private const string IncomingApplicationId = "888888";
		private const string IncomingDtGuid = "incomingGuid";
		private const string IncomingDtTraceId = "incomingTraceId";
		private const string IncomingTrustKey = "12345";
		private const float Priority = 0.5f;
		private const float IncomingPriority = 0.75f;
		private const string TransactionId = "transactionId";

		private DistributedTracePayloadHandler _distributedTracePayloadHandler;
		private IConfiguration _configuration;
		private IAdaptiveSampler _adaptiveSampler;
		private IAgentHealthReporter _agentHealthReporter;

		[SetUp]
		public void SetUp()
		{
			_configuration = Mock.Create<IConfiguration>();
			_adaptiveSampler = Mock.Create<IAdaptiveSampler>();

			Mock.Arrange(() => _configuration.DistributedTracingEnabled).Returns(true);
			Mock.Arrange(() => _configuration.TransactionEventsEnabled).Returns(true);
			Mock.Arrange(() => _configuration.AccountId).Returns(AgentAccountId);
			Mock.Arrange(() => _configuration.PrimaryApplicationId).Returns(AgentApplicationId);
			Mock.Arrange(() => _configuration.TrustedAccountKey).Returns(IncomingTrustKey);

			var configurationService = Mock.Create<IConfigurationService>();
			Mock.Arrange(() => configurationService.Configuration).Returns(_configuration);
			
			_agentHealthReporter = Mock.Create<IAgentHealthReporter>();
			_distributedTracePayloadHandler = new DistributedTracePayloadHandler(configurationService, _agentHealthReporter, _adaptiveSampler);
		}

		#region Accept Incoming Request

		[Test]
		public void TryDecodeInboundSerializedDistributedTracePayload_ReturnsValidPayload()
		{
			// Arrange
			var payload = BuildSampleDistributedTracePayload();
			payload.TransactionId = TransactionId;
			payload.Guid = null;

			var encodedPayload = HeaderEncoder.SerializeAndEncodeDistributedTracePayload(payload);
			
			// Act
			var decodedPayload = _distributedTracePayloadHandler.TryDecodeInboundSerializedDistributedTracePayload(encodedPayload);

			// Assert
			Assert.IsNotNull(decodedPayload);

			NrAssert.Multiple(
				() => Assert.AreEqual(IncomingDtType, decodedPayload.Type),
				() => Assert.AreEqual(IncomingAccountId, decodedPayload.AccountId),
				() => Assert.AreEqual(AgentApplicationId, decodedPayload.AppId),
				() => Assert.AreEqual(null, decodedPayload.Guid),
				() => Assert.AreEqual(IncomingTrustKey, decodedPayload.TrustKey),
				() => Assert.AreEqual(IncomingPriority, decodedPayload.Priority),
				() => Assert.AreEqual(false, decodedPayload.Sampled),
				() => Assert.That(decodedPayload.Timestamp, Is.LessThan(DateTime.UtcNow)),
				() => Assert.AreEqual(TransactionId, decodedPayload.TransactionId)
			);
		}

		[Test]
		public void PayloadShouldBeNullWhenTrustKeyNotTrusted()
		{
			// Arrange
			Mock.Arrange(() => _configuration.TrustedAccountKey).Returns("NOPE");

			var payload = BuildSampleDistributedTracePayload();
			payload.TransactionId = TransactionId;

			var encodedPayload = HeaderEncoder.SerializeAndEncodeDistributedTracePayload(payload);

			// Act
			var decodedPayload = _distributedTracePayloadHandler.TryDecodeInboundSerializedDistributedTracePayload(encodedPayload);

			// Assert
			Assert.IsNull(decodedPayload);
		}

		[Test]
		public void PayloadShouldBePopulatedWhenTrustKeyTrusted()
		{
			// Arrange
			var payload = BuildSampleDistributedTracePayload();
			payload.TransactionId = TransactionId;

			var encodedPayload = HeaderEncoder.SerializeAndEncodeDistributedTracePayload(payload);
			
			// Act
			var decodedPayload = _distributedTracePayloadHandler.TryDecodeInboundSerializedDistributedTracePayload(encodedPayload);

			// Assert
			Assert.IsNotNull(decodedPayload);
		}

		[Test]
		public void PayloadShouldBeNullWhenTrustKeyNullAndAccountIdNotTrusted()
		{
			// Arrange
			Mock.Arrange(() => _configuration.TrustedAccountKey).Returns("NOPE");

			var payload = BuildSampleDistributedTracePayload();
			payload.TrustKey = null;
			payload.TransactionId = TransactionId;

			var encodedPayload = HeaderEncoder.SerializeAndEncodeDistributedTracePayload(payload);
			
			// Act
			var decodedPayload = _distributedTracePayloadHandler.TryDecodeInboundSerializedDistributedTracePayload(encodedPayload);

			// Assert
			Assert.IsNull(decodedPayload);
		}

		[Test]
		public void PayloadShouldBePopulatedWhenTrustKeyNullAndAccountIdTrusted()
		{
			// Arrange
			Mock.Arrange(() => _configuration.TrustedAccountKey).Returns(IncomingAccountId);

			var payload = BuildSampleDistributedTracePayload();
			payload.TrustKey = null;
			payload.TransactionId = TransactionId;

			var encodedPayload = HeaderEncoder.SerializeAndEncodeDistributedTracePayload(payload);
			
			// Act
			var decodedPayload = _distributedTracePayloadHandler.TryDecodeInboundSerializedDistributedTracePayload(encodedPayload);

			// Assert
			Assert.IsNotNull(decodedPayload);
		}


		[Test]
		public void TryDecodeInboundSerializedDistributedTracePayload_ReturnsNull_IfHigherMajorVersion()
		{
			// Arrange
			var payload = BuildSampleDistributedTracePayload();
			payload.Version = new[] {int.MaxValue, 1};
			payload.TransactionId = TransactionId;

			var encodedPayload = HeaderEncoder.SerializeAndEncodeDistributedTracePayload(payload);
			
			// Act
			var decodedPayload = _distributedTracePayloadHandler.TryDecodeInboundSerializedDistributedTracePayload(encodedPayload);

			// Assert
			Assert.IsNull(decodedPayload);
		}

		[Test]
		public void ShouldNotCreatePayloadWhenGuidAndTransactionIdNull()
		{
			// Arrange
			var payload = BuildSampleDistributedTracePayload();
			payload.Guid = null;

			var encodedPayload = HeaderEncoder.SerializeAndEncodeDistributedTracePayload(payload);
			
			// Act
			var decodedPayload = _distributedTracePayloadHandler.TryDecodeInboundSerializedDistributedTracePayload(encodedPayload);

			// Assert
			Assert.IsNull(decodedPayload);
		}

		[Test]
		public void ShouldGenerateParseExceptionMetricWhenGuidAndTransactionIdNull()
		{
			// Arrange
			var payload = BuildSampleDistributedTracePayload();
			payload.Guid = null;
			payload.TransactionId = null;

			var encodedPayload = HeaderEncoder.SerializeAndEncodeDistributedTracePayload(payload);
			
			// Act
			_distributedTracePayloadHandler.TryDecodeInboundSerializedDistributedTracePayload(encodedPayload);

			// Assert
			Mock.Assert(() => _agentHealthReporter.ReportSupportabilityDistributedTraceAcceptPayloadParseException(), Occurs.Once());
		}


		#endregion Accept Incoming Request

		#region Create Outbound Request

		[Test]
		public void TryGetOutboundDistributedTraceApiModel_ReturnsCorrectModel_IfFirstInChain()
		{
			// Arrange
			var transaction = BuildMockTransaction(hasIncomingPayload: false);

			// Act
			var model = _distributedTracePayloadHandler.TryGetOutboundDistributedTraceApiModel(transaction);
			var encodedJson = model.HttpSafe();

			DistributedTracePayload dtPayload = null;
			Assert.That(() => dtPayload = HeaderEncoder.TryDecodeAndDeserializeDistributedTracePayload(encodedJson), Is.Not.Null);

			// Assert
			NrAssert.Multiple(
				() => Assert.AreEqual(DtTypeApp, dtPayload.Type),
				() => Assert.AreEqual(AgentAccountId, dtPayload.AccountId),
				() => Assert.AreEqual(AgentApplicationId, dtPayload.AppId),
				() => Assert.AreEqual(null, dtPayload.Guid),
				() => Assert.AreEqual(transaction.Guid, dtPayload.TraceId),
				() => Assert.AreEqual(Priority, dtPayload.Priority),
				() => Assert.That(dtPayload.Timestamp, Is.LessThan(DateTime.UtcNow)),
				() => Assert.AreEqual($"{transaction.Guid}", dtPayload.TransactionId)
			);
		}

		[Test]
		public void TryGetOutboundDistributedTraceApiModel_ReturnsCorrectModel_IfNotFirstInChain()
		{
			// Arrange
			Mock.Arrange(() => _configuration.TrustedAccountKey).Returns(IncomingTrustKey);
			var transaction = BuildMockTransaction(hasIncomingPayload: true);

			// Act
			var model = _distributedTracePayloadHandler.TryGetOutboundDistributedTraceApiModel(transaction);
			var encodedJson = model.HttpSafe();

			DistributedTracePayload dtPayload = null;
			Assert.That(() => dtPayload = HeaderEncoder.TryDecodeAndDeserializeDistributedTracePayload(encodedJson), Is.Not.Null);

			// Assert
			NrAssert.Multiple(
				() => Assert.AreEqual(DtTypeApp, dtPayload.Type),
				() => Assert.AreEqual(AgentAccountId, dtPayload.AccountId),
				() => Assert.AreEqual(AgentApplicationId, dtPayload.AppId),
				() => Assert.AreEqual(null, dtPayload.Guid),
				() => Assert.AreEqual(IncomingDtTraceId, dtPayload.TraceId),
				() => Assert.AreEqual(IncomingTrustKey, dtPayload.TrustKey),
				() => Assert.AreEqual(IncomingPriority, dtPayload.Priority),
				() => Assert.That(dtPayload.Timestamp, Is.LessThan(DateTime.UtcNow)),
				() => Assert.AreEqual($"{transaction.Guid}", dtPayload.TransactionId)
			);
		}

		[Test]
		public void ShouldPopulateTrustKeyWhenTrustedAccountKeyDifferentThanAccountId()
		{
			// Arrange
			var transaction = BuildMockTransaction(hasIncomingPayload: true);

			// Act
			var model = _distributedTracePayloadHandler.TryGetOutboundDistributedTraceApiModel(transaction);
			var encodedJson = model.HttpSafe();
			var payload = HeaderEncoder.TryDecodeAndDeserializeDistributedTracePayload(encodedJson);

			// Assert
			Assert.NotNull(payload);
			Assert.AreEqual(IncomingTrustKey, payload.TrustKey);
		}

		[Test]
		public void ShouldNotPopulateTrustKeyWhenTrustedAccountKeySameAsAccountId()
		{
			// Arrange
			Mock.Arrange(() => _configuration.TrustedAccountKey).Returns(AgentAccountId);

			var transaction = BuildMockTransaction(hasIncomingPayload: true);

			// Act
			var model = _distributedTracePayloadHandler.TryGetOutboundDistributedTraceApiModel(transaction);
			var encodedJson = model.HttpSafe();
			var payload = HeaderEncoder.TryDecodeAndDeserializeDistributedTracePayload(encodedJson);

			// Assert
			Assert.NotNull(payload);
			Assert.IsNull(payload.TrustKey);
		}

		[Test]
		public void PayloadShouldHaveGuidWhenSpansEnabledAndTransactionSampled()
		{
			// Arrange
			Mock.Arrange(() => _configuration.SpanEventsEnabled).Returns(true);

			var transaction = BuildMockTransaction();
			transaction.TransactionMetadata.DistributedTraceSampled = true;

			const string expectedGuid = "expectedId";
			var segment = Mock.Create<ISegment>();
			Mock.Arrange(() => segment.SpanId).Returns(expectedGuid);

			// Act
			var model = _distributedTracePayloadHandler.TryGetOutboundDistributedTraceApiModel(transaction, segment);
			var encodedJson = model.HttpSafe();
			var payload = HeaderEncoder.TryDecodeAndDeserializeDistributedTracePayload(encodedJson);

			// Assert
			Assert.NotNull(payload.Guid);
			Assert.AreEqual(expectedGuid, payload.Guid);
		}

		[Test]
		public void PayloadShouldNotHaveGuidWhenSpansDisabled()
		{
			// Arrange
			Mock.Arrange(() => _configuration.SpanEventsEnabled).Returns(false);

			var transaction = BuildMockTransaction(hasIncomingPayload: true);

			const string expectedGuid = "expectedId";
			var segment = Mock.Create<ISegment>();
			Mock.Arrange(() => segment.SpanId).Returns(expectedGuid);

			// Act
			var model = _distributedTracePayloadHandler.TryGetOutboundDistributedTraceApiModel(transaction, segment);
			var encodedJson = model.HttpSafe();
			var payload = HeaderEncoder.TryDecodeAndDeserializeDistributedTracePayload(encodedJson);

			// Assert
			Assert.IsNull(payload.Guid);
		}

		[Test]
		public void PayloadShouldNotHaveGuidWhenSpansEnabledAndTransactionNotSampled()
		{
			// Arrange
			Mock.Arrange(() => _configuration.SpanEventsEnabled).Returns(true);

			var transaction = BuildMockTransaction();
			transaction.TransactionMetadata.DistributedTraceSampled = false;

			const string expectedGuid = "expectedId";
			var segment = Mock.Create<ISegment>();
			Mock.Arrange(() => segment.SpanId).Returns(expectedGuid);

			// Act
			var model = _distributedTracePayloadHandler.TryGetOutboundDistributedTraceApiModel(transaction, segment);
			var encodedJson = model.HttpSafe();
			var payload = HeaderEncoder.TryDecodeAndDeserializeDistributedTracePayload(encodedJson);

			// Assert
			Assert.IsNull(payload.Guid);
		}

		[Test]
		public void ShouldNotCreatePayloadWhenAccountIdNotReceivedFromServer()
		{
			// Arrange
			Mock.Arrange(() => _configuration.AccountId).Returns<string>(null);
			Mock.Arrange(() => _configuration.PrimaryApplicationId).Returns(AgentApplicationId);

			var transaction = BuildMockTransaction(hasIncomingPayload: true);

			// Act
			var payload = _distributedTracePayloadHandler.TryGetOutboundDistributedTraceApiModel(transaction);

			// Assert
			Assert.AreEqual(DistributedTraceApiModel.EmptyModel, payload);
		}

		[Test]
		public void ShouldNotCreatePayloadWhenPrimaryApplicationIdNotReceivedFromServer()
		{
			// Arrange
			Mock.Arrange(() => _configuration.AccountId).Returns(AgentAccountId);
			Mock.Arrange(() => _configuration.PrimaryApplicationId).Returns<string>(null);

			var transaction = BuildMockTransaction(hasIncomingPayload: true);

			// Act
			var payload = _distributedTracePayloadHandler.TryGetOutboundDistributedTraceApiModel(transaction);

			// Assert
			Assert.AreEqual(DistributedTraceApiModel.EmptyModel, payload);
		}

		[Test]
		public void ShouldNotCreatePayloadWhenSampledNotSet()
		{
			// Arrange
			var transaction = BuildMockTransaction();
			transaction.TransactionMetadata.DistributedTraceSampled = null;

			// Act
			var payload = _distributedTracePayloadHandler.TryGetOutboundDistributedTraceApiModel(transaction);

			// Assert
			Assert.AreEqual(DistributedTraceApiModel.EmptyModel, payload);
		}

		[Test]
		public void PayloadShouldNotHaveTransactionIdWhenTransactionEventsDisabled()
		{
			// Arrange
			Mock.Arrange(() => _configuration.SpanEventsEnabled).Returns(true);
			Mock.Arrange(() => _configuration.TransactionEventsEnabled).Returns(false);

			var segment = Mock.Create<Segment>();
			Mock.Arrange(() => segment.SpanId).Returns("56789");

			var transaction = BuildMockTransaction();
			transaction.TransactionMetadata.DistributedTraceGuid = "12345";
			transaction.TransactionMetadata.DistributedTraceSampled = true;

			// Act
			var model = _distributedTracePayloadHandler.TryGetOutboundDistributedTraceApiModel(transaction, segment);
			var encodedJson = model.HttpSafe();
			var payload = HeaderEncoder.TryDecodeAndDeserializeDistributedTracePayload(encodedJson);

			// Assert
			Assert.IsNull(payload.TransactionId);
		}

		[Test]
		public void PayloadShouldHaveTransactionIdWhenTransactionEventsEnabled()
		{
			// Arrange
			Mock.Arrange(() => _configuration.TransactionEventsEnabled).Returns(true);

			var transaction = BuildMockTransaction();

			// Act
			var model = _distributedTracePayloadHandler.TryGetOutboundDistributedTraceApiModel(transaction);
			var encodedJson = model.HttpSafe();
			var payload = HeaderEncoder.TryDecodeAndDeserializeDistributedTracePayload(encodedJson);

			// Assert
			Assert.NotNull(payload.TransactionId);
			Assert.AreEqual(transaction.Guid, payload.TransactionId);
		}

		[Test]
		public void ShouldNotCreatePayloadWhenSpanEventsDisabledAndTransactionEventsDisabled()
		{
			// Arrange
			Mock.Arrange(() => _configuration.SpanEventsEnabled).Returns(false);
			Mock.Arrange(() => _configuration.TransactionEventsEnabled).Returns(false);

			var transaction = BuildMockTransaction();

			// Act
			var payload = _distributedTracePayloadHandler.TryGetOutboundDistributedTraceApiModel(transaction);

			// Assert
			Assert.AreEqual(DistributedTraceApiModel.EmptyModel, payload);
		}

		#region TraceId Tests

		[Test]
		public void PayloadShouldHaveTraceIdWhenSpansEnabled()
		{
			// Arrange
			Mock.Arrange(() => _configuration.SpanEventsEnabled).Returns(true);

			var transaction = BuildMockTransaction(hasIncomingPayload: true);
			var segment = Mock.Create<ISegment>();

			// Act
			var model = _distributedTracePayloadHandler.TryGetOutboundDistributedTraceApiModel(transaction, segment);
			var encodedJson = model.HttpSafe();
			var payload = HeaderEncoder.TryDecodeAndDeserializeDistributedTracePayload(encodedJson);

			// Assert
			Assert.NotNull(payload.TraceId);
		}

		[Test]
		public void PayloadShouldHaveTraceIdWhenSpansDisabled()
		{
			// Arrange
			Mock.Arrange(() => _configuration.SpanEventsEnabled).Returns(false);

			var transaction = BuildMockTransaction(hasIncomingPayload: true);
			var segment = Mock.Create<ISegment>();

			// Act
			var model = _distributedTracePayloadHandler.TryGetOutboundDistributedTraceApiModel(transaction, segment);
			var encodedJson = model.HttpSafe();
			var payload = HeaderEncoder.TryDecodeAndDeserializeDistributedTracePayload(encodedJson);

			// Assert
			Assert.NotNull(payload.TraceId);
		}

		[Test]
		public void TraceIdShouldBeSameAsIncomingTraceIdWhenReceived()
		{
			var transaction = BuildMockTransaction(hasIncomingPayload: true);
			var segment = Mock.Create<ISegment>();

			// Act
			var model = _distributedTracePayloadHandler.TryGetOutboundDistributedTraceApiModel(transaction, segment);
			var encodedJson = model.HttpSafe();
			var payload = HeaderEncoder.TryDecodeAndDeserializeDistributedTracePayload(encodedJson);

			// Assert
			Assert.AreEqual(IncomingDtTraceId, payload.TraceId);
		}

		[Test]
		public void PayloadShouldHaveExpectedTraceIdValueWhenNoTraceIdReceived()
		{
			// Arrange
			var transaction = BuildMockTransaction();

			var segment = Mock.Create<ISegment>();
			var expectedTraceIdValue = transaction.Guid;

			// Act
			var model = _distributedTracePayloadHandler.TryGetOutboundDistributedTraceApiModel(transaction, segment);
			var encodedJson = model.HttpSafe();
			var payload = HeaderEncoder.TryDecodeAndDeserializeDistributedTracePayload(encodedJson);

			// Assert
			Assert.AreEqual(expectedTraceIdValue, payload.TraceId);
		}

		[Test]
		public void TraceIdShouldBeSameForAllSpansWhenNoTraceIdReceived()
		{
			// Arrange
			var transaction = BuildMockTransaction();

			var segment1 = Mock.Create<ISegment>();
			var segment2 = Mock.Create<ISegment>();

			// Act
			var firstHeaders = _distributedTracePayloadHandler.TryGetOutboundDistributedTraceApiModel(transaction, segment1);

			var firstEncodedJson = firstHeaders.HttpSafe();
			var payload1 = HeaderEncoder.TryDecodeAndDeserializeDistributedTracePayload(firstEncodedJson);

			var secondHeaders = _distributedTracePayloadHandler.TryGetOutboundDistributedTraceApiModel(transaction, segment2);
			var secondEncodedJson = secondHeaders.HttpSafe();
			var payload2 = HeaderEncoder.TryDecodeAndDeserializeDistributedTracePayload(secondEncodedJson);

			// Assert
			Assert.AreEqual(payload1.TraceId, payload2.TraceId);
		}

		#endregion TraceID Tests

		#endregion

		#region helpers

		private static IInternalTransaction BuildMockTransaction(bool hasIncomingPayload = false)
		{
			var transaction = Mock.Create<IInternalTransaction>();
			var transactionMetadata = Mock.Create<ITransactionMetadata>();
			Mock.Arrange(() => transaction.TransactionMetadata).Returns(transactionMetadata);
			
			var transactionGuid = Guid.NewGuid().ToString();
			Mock.Arrange(() => transaction.Guid).Returns(transactionGuid);

			transaction.TransactionMetadata.Priority = Priority;
			transaction.TransactionMetadata.DistributedTraceSampled = false;

			if (hasIncomingPayload)
			{
				transaction.TransactionMetadata.DistributedTraceType = IncomingDtType;
				transaction.TransactionMetadata.DistributedTraceAccountId = IncomingAccountId;
				transaction.TransactionMetadata.DistributedTraceAppId = IncomingApplicationId;
				transaction.TransactionMetadata.DistributedTraceGuid = IncomingDtGuid;
				transaction.TransactionMetadata.DistributedTraceTraceId = IncomingDtTraceId;
				transaction.TransactionMetadata.DistributedTraceTrustKey = IncomingTrustKey;
				transaction.TransactionMetadata.Priority = IncomingPriority;
			}
			else
			{
				transaction.TransactionMetadata.DistributedTraceType = null;
				transaction.TransactionMetadata.DistributedTraceAccountId = null;
				transaction.TransactionMetadata.DistributedTraceAppId = null;
				transaction.TransactionMetadata.DistributedTraceGuid = null;
				transaction.TransactionMetadata.DistributedTraceTraceId = null;
				transaction.TransactionMetadata.DistributedTraceTrustKey = null;
			}

			return transaction;
		}

		private static DistributedTracePayload BuildSampleDistributedTracePayload()
		{
			return DistributedTracePayload.TryBuildOutgoingPayload(
				IncomingDtType,
				IncomingAccountId,
				AgentApplicationId,
				IncomingDtGuid,
				IncomingDtTraceId,
				IncomingTrustKey,
				IncomingPriority,
				false,
				DateTime.UtcNow,
				null);				
		}

		#endregion helpers
	}
}

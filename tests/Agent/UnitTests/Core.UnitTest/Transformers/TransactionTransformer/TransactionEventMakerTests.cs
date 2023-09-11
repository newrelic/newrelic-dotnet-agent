// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Aggregators;
using NewRelic.Agent.Core.Attributes;
using NewRelic.Agent.Core.CallStack;
using NewRelic.Agent.Core.Database;
using NewRelic.Agent.Core.DistributedTracing;
using NewRelic.Agent.Core.Errors;
using NewRelic.Agent.Core.Segments;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Agent.TestUtilities;
using NewRelic.Core.DistributedTracing;
using NewRelic.Testing.Assertions;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.Transformers.TransactionTransformer.UnitTest
{
    [TestFixture]
    public class TransactionEventMakerTests
    {
        private TransactionEventMaker _transactionEventMaker;

        private static ISimpleTimerFactory _timerFactory;
        private IConfiguration _configuration;
        private IConfigurationService _configurationService;
        private TransactionAttributeMaker _transactionAttributeMaker;
        private ITransactionMetricNameMaker _transactionMetricNameMaker;
        private IErrorService _errorService;
        private IAttributeDefinitionService _attribDefSvc;
        private IAttributeDefinitions _attribDefs => _attribDefSvc.AttributeDefs;

        [SetUp]
        public void SetUp()
        {
            _timerFactory = Mock.Create<ISimpleTimerFactory>();
           

            _configuration = Mock.Create<IConfiguration>();
            Mock.Arrange(() => _configuration.ErrorCollectorEnabled).Returns(true);
            _configurationService = Mock.Create<IConfigurationService>();
            Mock.Arrange(() => _configurationService.Configuration).Returns(_configuration);

            _transactionMetricNameMaker = Mock.Create<ITransactionMetricNameMaker>();
            Mock.Arrange(() => _transactionMetricNameMaker.GetTransactionMetricName(Arg.IsAny<ITransactionName>()))
                .Returns(new TransactionMetricName("WebTransaction", "TransactionName"));


            _errorService = new ErrorService(_configurationService);
            _attribDefSvc = new AttributeDefinitionService((f) => new AttributeDefinitions(f));

            _transactionEventMaker = new TransactionEventMaker(_attribDefSvc);
            _transactionAttributeMaker = new TransactionAttributeMaker(_configurationService, _attribDefSvc);

        }

        [Test]
        public void GetTransactionEvent_ReturnsSyntheticEvent()
        {
            // ARRANGE
            var transaction = BuildTestTransaction(isSynthetics: true);

            var immutableTransaction = transaction.ConvertToImmutableTransaction();
            var transactionMetricName = _transactionMetricNameMaker.GetTransactionMetricName(immutableTransaction.TransactionName);
            var txStats = new TransactionMetricStatsCollection(transactionMetricName);
            var attributes = _transactionAttributeMaker.GetAttributes(immutableTransaction, transactionMetricName, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(15), txStats);

            // ACT
            var transactionEvent = _transactionEventMaker.GetTransactionEvent(immutableTransaction, attributes);

            // ASSERT
            Assert.NotNull(transactionEvent);
            Assert.IsTrue(transactionEvent.IsSynthetics);
        }


        [Test]
        public void GetTransactionEvent_ReturnsCorrectAttributes()
        {
            // ARRANGE
            var transaction = BuildTestTransaction(statusCode: 200, uri: "http://foo.com");
            transaction.AddCustomAttribute("foo", "bar");
            var errorData = MakeErrorData();
            transaction.TransactionMetadata.TransactionErrorState.AddCustomErrorData(errorData);

            var immutableTransaction = transaction.ConvertToImmutableTransaction();
            var transactionMetricName = _transactionMetricNameMaker.GetTransactionMetricName(immutableTransaction.TransactionName);
            var txStats = new TransactionMetricStatsCollection(transactionMetricName);
            var attributes = _transactionAttributeMaker.GetAttributes(immutableTransaction, transactionMetricName, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(15), txStats);

            // ACT
            var transactionEvent = _transactionEventMaker.GetTransactionEvent(immutableTransaction, attributes);
            var agentAttributes = transactionEvent.AgentAttributes();
            var intrinsicAttributes = transactionEvent.IntrinsicAttributes();
            var userAttributes = transactionEvent.UserAttributes();

            // Change Notes
            // Originally the test was looking for 29 attribute values.  These are not yet filtered, even though they are attacched to
            // the transaction event.  The equivalent here is the 'attributes' value, which is not filtered.  It has 31 attributes.
            // The two extra are type and timestamp for error events.


            var unfilteredIntrinsicsDic = attributes.GetAttributeValues(AttributeClassification.Intrinsics)
                .Where(x=>x.AttributeDefinition.IsAvailableForAny(AttributeDestinations.TransactionEvent))
                .GroupBy(x=>x.AttributeDefinition)
                .ToDictionary(x => x.Key, x=>x.Last());

            var filteredIntrinsicsDic = transactionEvent.AttributeValues.GetAttributeValues(AttributeClassification.Intrinsics)
                .GroupBy(x => x.AttributeDefinition)
                .ToDictionary(x => x.Key, x => x.Last());

            // ASSERT
            NrAssert.Multiple(
                () => CollectionAssert.AreEquivalent(unfilteredIntrinsicsDic.Keys, filteredIntrinsicsDic.Keys),
                () => Assert.AreEqual(19, intrinsicAttributes.Count),
                () => Assert.AreEqual("Transaction", intrinsicAttributes["type"]),
                () => Assert.AreEqual(4, agentAttributes.Count),
                () => Assert.AreEqual("200", agentAttributes["response.status"]),
                () => Assert.AreEqual(200, agentAttributes["http.statusCode"]),
                () => Assert.AreEqual("http://foo.com", agentAttributes["request.uri"]),
                () => Assert.IsTrue(agentAttributes.ContainsKey("host.displayName")),
                () => Assert.AreEqual(1, userAttributes.Count),
                () => Assert.AreEqual("bar", userAttributes["foo"])
                //This should be on the error event
                //() => Assert.AreEqual("baz", userAttributes["fiz"])
            );
        }

        [Test]
        public void GetTransactionEvent_DoesNotReturnsSyntheticEvent()
        {
            // ARRANGE
            var transaction = BuildTestTransaction(isSynthetics: false);

            var immutableTransaction = transaction.ConvertToImmutableTransaction();
            var transactionMetricName = _transactionMetricNameMaker.GetTransactionMetricName(immutableTransaction.TransactionName);
            var txStats = new TransactionMetricStatsCollection(transactionMetricName);
            var attributes = _transactionAttributeMaker.GetAttributes(immutableTransaction, transactionMetricName, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(15), txStats);

            // ACT
            var transactionEvent = _transactionEventMaker.GetTransactionEvent(immutableTransaction, attributes);

            // ASSERT
            Assert.NotNull(transactionEvent);
            Assert.IsFalse(transactionEvent.IsSynthetics);
        }

        [Test]
        public void GetTransactionEvent_ReturnsCorrectDistributedTraceAttributes()
        {
            // ARRANGE

            Mock.Arrange(() => _configurationService.Configuration.DistributedTracingEnabled).Returns(true);

            var immutableTransaction = BuildTestImmutableTransaction(sampled: true, guid: "guid", isDTParticipant: _configurationService.Configuration.DistributedTracingEnabled);

            var transactionMetricName = _transactionMetricNameMaker.GetTransactionMetricName(immutableTransaction.TransactionName);
            var txStats = new TransactionMetricStatsCollection(transactionMetricName);
            var attributes = _transactionAttributeMaker.GetAttributes(immutableTransaction, transactionMetricName, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(15), txStats);

            // ACT
            var transactionEvent = _transactionEventMaker.GetTransactionEvent(immutableTransaction, attributes);
            var intrinsicAttributes = transactionEvent.IntrinsicAttributes();

            // ASSERT
            NrAssert.Multiple(
                () => Assert.AreEqual(19, intrinsicAttributes.Count, "intrinsicAttributes.Count"),
                () => Assert.Contains("guid", intrinsicAttributes.Keys.ToArray(), "IntrinsicAttributes.Keys.Contains('guid')"),
                () => Assert.AreEqual(immutableTransaction.TracingState.Type.ToString(), intrinsicAttributes["parent.type"], "parent.type"),
                () => Assert.AreEqual(immutableTransaction.TracingState.AppId, intrinsicAttributes["parent.app"], "parent.app"),
                () => Assert.AreEqual(immutableTransaction.TracingState.AccountId, intrinsicAttributes["parent.account"], "parent.account"),
                () => Assert.AreEqual(EnumNameCache<TransportType>.GetName(immutableTransaction.TracingState.TransportType), intrinsicAttributes["parent.transportType"], "parent.transportType"),
                () => Assert.AreEqual(immutableTransaction.TracingState.TransportDuration.TotalSeconds, (double)intrinsicAttributes["parent.transportDuration"], 0.000001d, "parent.transportDuration"),
                () => Assert.AreEqual(immutableTransaction.TracingState.TransactionId, intrinsicAttributes["parentId"], "parentId"),
                () => Assert.AreEqual(immutableTransaction.TracingState.ParentId, intrinsicAttributes["parentSpanId"], "parentSpanId"),
                () => Assert.AreEqual(immutableTransaction.TraceId, intrinsicAttributes["traceId"], "traceId"),
                () => Assert.AreEqual(immutableTransaction.Priority, intrinsicAttributes["priority"], "priority"),
                () => Assert.AreEqual(immutableTransaction.Sampled, intrinsicAttributes["sampled"], "sampled")
            );
        }

        private ErrorData MakeErrorData()
        {
            return new ErrorData("message", "type", "stacktrace", DateTime.UtcNow, new ReadOnlyDictionary<string, object>(new Dictionary<string, object>() { { "fiz", "baz" } }), false, null);
        }

        private const DistributedTracingParentType Type = DistributedTracingParentType.App;
        private const string AppId = "appId";
        private const string AccountId = "accountId";
        private const string GetTransportType = "HTTPS";
        private const string Guid = "guid";
        private static DateTime Timestamp = DateTime.UtcNow;
        private const string TraceId = "traceId";
        private const string TransactionId = "transactionId";
        private const bool Sampled = true;
        private const float Priority = 1.56f;
        private const string traceparentParentId = "parentId";

        private ImmutableTransaction BuildTestImmutableTransaction(bool isWebTransaction = true, string guid = null, float priority = 0.5f, bool sampled = false, string traceId = "traceId", bool isDTParticipant = false)
        {
            var name = TransactionName.ForWebTransaction("category", "name");

            var segments = Enumerable.Empty<Segment>();

            var placeholderMetadataBuilder = new TransactionMetadata(guid);
            var placeholderMetadata = placeholderMetadataBuilder.ConvertToImmutableMetadata();

            var immutableTransaction = new ImmutableTransaction(name, segments, placeholderMetadata, DateTime.UtcNow, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10), guid, false, false, false, priority, sampled, traceId, BuildMockTracingState(isDTParticipant), _attribDefs);

            return immutableTransaction;
        }

        private IInternalTransaction BuildTestTransaction(bool isWebTransaction = true, string uri = null, string referrerUri = null, string guid = null, int? statusCode = null, int? subStatusCode = null, string referrerCrossProcessId = null, string transactionCategory = "defaultTxCategory", string transactionName = "defaultTxName", ErrorData exceptionData = null, ErrorData customErrorData = null, bool isSynthetics = true, bool isCAT = true, bool includeUserAttributes = false, float priority = 0.5f, bool sampled = false, string traceId = "traceId")
        {
            var name = isWebTransaction
                ? TransactionName.ForWebTransaction(transactionCategory, transactionName)
                : TransactionName.ForOtherTransaction(transactionCategory, transactionName);

            var segments = Enumerable.Empty<Segment>();

            var placeholderMetadataBuilder = new TransactionMetadata(guid);
            var placeholderMetadata = placeholderMetadataBuilder.ConvertToImmutableMetadata();

            var immutableTransaction = new ImmutableTransaction(name, segments, placeholderMetadata, DateTime.UtcNow, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10), guid, false, false, false, priority, sampled, traceId, null, _attribDefs);

            var internalTransaction = new Transaction(Mock.Create<IConfiguration>(), immutableTransaction.TransactionName, _timerFactory.StartNewTimer(), DateTime.UtcNow, Mock.Create<ICallStackManager>(), Mock.Create<IDatabaseService>(), priority, Mock.Create<IDatabaseStatementParser>(), Mock.Create<IDistributedTracePayloadHandler>(), Mock.Create<IErrorService>(), _attribDefs);

            var adaptiveSampler = Mock.Create<IAdaptiveSampler>();
            Mock.Arrange(() => adaptiveSampler.ComputeSampled(ref priority)).Returns(sampled);
            internalTransaction.SetSampled(adaptiveSampler);

            PopulateTransactionMetadataBuilder(internalTransaction, uri, statusCode, subStatusCode, referrerCrossProcessId, exceptionData, customErrorData, isSynthetics, isCAT, referrerUri, includeUserAttributes);

            return internalTransaction;
        }

        private void PopulateTransactionMetadataBuilder(IInternalTransaction transaction, string uri = null, int? statusCode = null, int? subStatusCode = null, string referrerCrossProcessId = null, ErrorData exceptionData = null, ErrorData customErrorData = null, bool isSynthetics = true, bool isCAT = true, string referrerUri = null, bool includeUserAttributes = false)
        {
            var metadata = transaction.TransactionMetadata;

            if (uri != null)
                metadata.SetUri(uri);
            if (statusCode != null)
                metadata.SetHttpResponseStatusCode(statusCode.Value, subStatusCode, _errorService);
            if (referrerCrossProcessId != null)
                metadata.SetCrossApplicationReferrerProcessId(referrerCrossProcessId);
            if (statusCode != null)
                metadata.SetHttpResponseStatusCode(statusCode.Value, subStatusCode, _errorService);
            if (exceptionData != null)
                metadata.TransactionErrorState.AddExceptionData((ErrorData)exceptionData);
            if (customErrorData != null)
                metadata.TransactionErrorState.AddCustomErrorData((ErrorData)customErrorData);
            if (referrerUri != null)
                metadata.SetReferrerUri(referrerUri);
            if (isCAT)
            {
                metadata.SetCrossApplicationReferrerProcessId("cross application process id");
                metadata.SetCrossApplicationReferrerTransactionGuid("transaction Guid");
            }

            metadata.SetQueueTime(TimeSpan.FromSeconds(10));
            metadata.SetOriginalUri("originalUri");
            metadata.SetCrossApplicationPathHash("crossApplicationPathHash");
            metadata.SetCrossApplicationReferrerContentLength(10000);
            metadata.SetCrossApplicationReferrerPathHash("crossApplicationReferrerPathHash");
            metadata.SetCrossApplicationReferrerTripId("crossApplicationReferrerTripId");

            if (includeUserAttributes)
            {
                transaction.AddCustomAttribute("sample.user.attribute", "user attribute string");
            }

            if (isSynthetics)
            {
                metadata.SetSyntheticsResourceId("syntheticsResourceId");
                metadata.SetSyntheticsJobId("syntheticsJobId");
                metadata.SetSyntheticsMonitorId("syntheticsMonitorId");
            }
        }
        private static ITracingState BuildMockTracingState(bool isDTParticipant)
        {
            var tracingState = Mock.Create<ITracingState>();

            Mock.Arrange(() => tracingState.Type).Returns(Type);
            Mock.Arrange(() => tracingState.AppId).Returns(AppId);
            Mock.Arrange(() => tracingState.AccountId).Returns(AccountId);
            Mock.Arrange(() => tracingState.TransportType).Returns(TransportType.HTTP);
            Mock.Arrange(() => tracingState.Guid).Returns(Guid);
            Mock.Arrange(() => tracingState.Timestamp).Returns(Timestamp);
            Mock.Arrange(() => tracingState.TraceId).Returns(TraceId);
            Mock.Arrange(() => tracingState.TransactionId).Returns(TransactionId);
            Mock.Arrange(() => tracingState.Sampled).Returns(Sampled);
            Mock.Arrange(() => tracingState.Priority).Returns(Priority);
            Mock.Arrange(() => tracingState.ParentId).Returns(traceparentParentId);
            Mock.Arrange(() => tracingState.HasDataForParentAttributes).Returns(true);
            Mock.Arrange(() => tracingState.HasDataForAttributes).Returns(isDTParticipant);

            return tracingState;
        }
    }
}

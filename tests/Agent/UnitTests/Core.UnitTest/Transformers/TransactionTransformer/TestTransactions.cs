// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.CallStack;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.Core.Database;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using Telerik.JustMock;
using NewRelic.Agent.Extensions.Parsing;
using NewRelic.Agent.Core.Segments;
using NewRelic.Agent.Core.Segments.Tests;
using NewRelic.Agent.Core.Errors;
using NewRelic.Agent.Core.DistributedTracing;
using NewRelic.Agent.Core.Attributes;

namespace NewRelic.Agent.Core.Transformers.TransactionTransformer
{
    public static class TestTransactions
    {
        private static IDatabaseService _databaseService = new DatabaseService();
        private static IAttributeDefinitionService _attribDefSvc = new AttributeDefinitionService((f) => new AttributeDefinitions(f));

        public static IConfiguration GetDefaultConfiguration()
        {
            var configuration = Mock.Create<IConfiguration>();

            Mock.Arrange(() => configuration.TransactionTracerMaxSegments).Returns(666);
            Mock.Arrange(() => configuration.TransactionEventsEnabled).Returns(true);
            Mock.Arrange(() => configuration.TransactionEventsMaximumSamplesStored).Returns(10000);
            Mock.Arrange(() => configuration.TransactionEventsTransactionsEnabled).Returns(true);
            Mock.Arrange(() => configuration.TransactionEventsAttributesEnabled).Returns(true);
            Mock.Arrange(() => configuration.ErrorCollectorEnabled).Returns(true);
            Mock.Arrange(() => configuration.ErrorCollectorCaptureEvents).Returns(true);
            Mock.Arrange(() => configuration.CaptureErrorCollectorAttributes).Returns(true);
            Mock.Arrange(() => configuration.TransactionTracerEnabled).Returns(true);
            Mock.Arrange(() => configuration.LogMetricsCollectorEnabled).Returns(true);
            Mock.Arrange(() => configuration.LogEventCollectorEnabled).Returns(true);
            Mock.Arrange(() => configuration.LogEventsMaxSamplesStored).Returns(2000);
            Mock.Arrange(() => configuration.LogEventsHarvestCycle).Returns(TimeSpan.FromSeconds(5));
            return configuration;
        }

        public static IInternalTransaction CreateDefaultTransaction(bool isWebTransaction = true, string uri = null,
            string guid = null, int? statusCode = null, int? subStatusCode = null, string referrerCrossProcessId = null,
            string transactionCategory = "defaultTxCategory", string transactionName = "defaultTxName", bool addSegment = true,
            IEnumerable<Segment> segments = null, bool sampled = false, IConfigurationService configurationService = null, Exception exception = null)
        {
            var name = isWebTransaction
                ? TransactionName.ForWebTransaction(transactionCategory, transactionName)
                : TransactionName.ForOtherTransaction(transactionCategory, transactionName);

            segments = segments ?? Enumerable.Empty<Segment>();

            var placeholderMetadataBuilder = new TransactionMetadata(guid);
            var placeholderMetadata = placeholderMetadataBuilder.ConvertToImmutableMetadata();

            var immutableTransaction = new ImmutableTransaction(name, segments, placeholderMetadata, DateTime.Now, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), guid, false, false, false, 0.5f, false, string.Empty, null, _attribDefSvc.AttributeDefs);
            var priority = 0.5f;

            var configuration = configurationService?.Configuration ?? GetDefaultConfiguration();
            var errorService = configurationService != null ? new ErrorService(configurationService) : new ErrorService(Mock.Create<IConfigurationService>());

            var internalTransaction = new Transaction(configuration, immutableTransaction.TransactionName, Mock.Create<ISimpleTimer>(), DateTime.UtcNow, Mock.Create<ICallStackManager>(),
                _databaseService, priority, Mock.Create<IDatabaseStatementParser>(), Mock.Create<IDistributedTracePayloadHandler>(),
                errorService, _attribDefSvc.AttributeDefs);

            if (exception != null)
            {
                internalTransaction.NoticeError(exception);
            }

            if (segments.Any())
            {
                foreach (var segment in segments)
                {
                    internalTransaction.Add(segment);
                }
            }
            else if (addSegment)
            {
                internalTransaction.Add(SimpleSegmentDataTestHelpers.CreateSimpleSegmentBuilder(TimeSpan.Zero, TimeSpan.Zero, 0, null, new MethodCallData("typeName", "methodName", 1), Enumerable.Empty<KeyValuePair<string, object>>(), "MyMockedRootNode", false));
            }

            var adaptiveSampler = Mock.Create<IAdaptiveSampler>();
            Mock.Arrange(() => adaptiveSampler.ComputeSampled(ref priority)).Returns(sampled);
            internalTransaction.SetSampled(adaptiveSampler);
            var transactionMetadata = internalTransaction.TransactionMetadata;
            PopulateTransactionMetadataBuilder(transactionMetadata, errorService, uri, statusCode, subStatusCode, referrerCrossProcessId);

            return internalTransaction;
        }

        public static ImmutableTransaction CreateTestTransactionWithSegments(IEnumerable<Segment> segments)
        {
            var uri = "sqlTrace/Uri";

            var transactionMetadata = new TransactionMetadata("transactionGuid");
            transactionMetadata.SetUri(uri);

            var name = TransactionName.ForWebTransaction("TxsWithSegments", "TxWithSegmentX");
            var metadata = transactionMetadata.ConvertToImmutableMetadata();
            var guid = Guid.NewGuid().ToString();

            var transaction = new ImmutableTransaction(name, segments, metadata, DateTime.UtcNow, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), guid, false, false, false, 0.5f, false, string.Empty, null, _attribDefSvc.AttributeDefs);
            return transaction;
        }

        public static Segment BuildSegment(ITransactionSegmentState txSegmentState, DatastoreVendor vendor, string model, string commandText, TimeSpan startTime = new TimeSpan(), TimeSpan? duration = null, string name = "", MethodCallData methodCallData = null, IEnumerable<KeyValuePair<string, object>> parameters = null, string host = null, string portPathOrId = null, string databaseName = null)
        {
            if (txSegmentState == null)
                txSegmentState = TransactionSegmentStateHelpers.GetItransactionSegmentState();

            methodCallData = methodCallData ?? new MethodCallData("typeName", "methodName", 1);
            var data = new DatastoreSegmentData(_databaseService, new ParsedSqlStatement(vendor, model, null), commandText, new ConnectionInfo("none", host, portPathOrId, databaseName));
            var segment = new Segment(txSegmentState, methodCallData);
            segment.SetSegmentData(data);

            return new Segment(startTime, duration, segment, parameters);
        }

        private static void PopulateTransactionMetadataBuilder(ITransactionMetadata metadata, IErrorService errorService, string uri = null, int? statusCode = null, int? subStatusCode = null, string referrerCrossProcessId = null)
        {
            if (uri != null)
                metadata.SetUri(uri);
            if (statusCode != null)
                metadata.SetHttpResponseStatusCode(statusCode.Value, subStatusCode, errorService);
            if (referrerCrossProcessId != null)
                metadata.SetCrossApplicationReferrerProcessId(referrerCrossProcessId);
            if (statusCode != null)
                metadata.SetHttpResponseStatusCode(statusCode.Value, subStatusCode, errorService);

            metadata.SetOriginalUri("originalUri");
            metadata.SetReferrerUri("referrerUri");
            metadata.SetCrossApplicationPathHash("crossApplicationPathHash");
            metadata.SetCrossApplicationReferrerContentLength(10000);
            metadata.SetCrossApplicationReferrerPathHash("crossApplicationReferrerPathHash");
            metadata.SetCrossApplicationReferrerTripId("crossApplicationReferrerTripId");
            metadata.SetSyntheticsResourceId("syntheticsResourceId");
            metadata.SetSyntheticsJobId("syntheticsJobId");
            metadata.SetSyntheticsMonitorId("syntheticsMonitorId");
        }
    }
}

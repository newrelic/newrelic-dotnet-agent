﻿using JetBrains.Annotations;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.CallStack;
using NewRelic.Agent.Core.Metric;
using NewRelic.Agent.Core.Timing;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.Transactions.TransactionNames;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.Core.Database;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using Telerik.JustMock;
using ITransaction = NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders.ITransaction;
using NewRelic.Collections;
using NewRelic.Agent.Core.Errors;

namespace NewRelic.Agent.Core.Transformers.TransactionTransformer
{
	public static class TestTransactions
	{
		[NotNull]
		public static IConfiguration GetDefaultConfiguration()
		{
			var configuration = Mock.Create<IConfiguration>();

			Mock.Arrange(() => configuration.TransactionTracerMaxSegments).Returns(666);
			Mock.Arrange(() => configuration.TransactionEventsEnabled).Returns(true);
			Mock.Arrange(() => configuration.TransactionEventsMaxSamplesStored).Returns(10000);
			Mock.Arrange(() => configuration.TransactionEventsTransactionsEnabled).Returns(true);
			Mock.Arrange(() => configuration.CaptureTransactionEventsAttributes).Returns(true);
			Mock.Arrange(() => configuration.ErrorCollectorEnabled).Returns(true);
			Mock.Arrange(() => configuration.ErrorCollectorCaptureEvents).Returns(true);
			Mock.Arrange(() => configuration.CaptureErrorCollectorAttributes).Returns(true);
			Mock.Arrange(() => configuration.TransactionTracerEnabled).Returns(true);
			return configuration;
		}

		[NotNull]
		public static ITransaction CreateDefaultTransaction(Boolean isWebTransaction = true, String uri = null, String guid = null, Int32? statusCode = null, Int32? subStatusCode = null, String referrerCrossProcessId = null, String transactionCategory = "defaultTxCategory", String transactionName = "defaultTxName", bool addSegment = true, IEnumerable<Segment> segments = null)
		{
			var name = isWebTransaction
				? new WebTransactionName(transactionCategory, transactionName)
				: new OtherTransactionName(transactionCategory, transactionName) as ITransactionName;
			segments = segments ?? Enumerable.Empty<Segment>();

			var placeholderMetadataBuilder = new TransactionMetadata();
			var placeholderMetadata = placeholderMetadataBuilder.ConvertToImmutableMetadata();
			
			var immutableTransaction = new ImmutableTransaction(name, segments, placeholderMetadata, DateTime.Now, TimeSpan.FromSeconds(1), guid, false, false, false, SqlObfuscator.GetObfuscatingSqlObfuscator());
			var internalTransaction = new Transaction(GetDefaultConfiguration(), immutableTransaction.TransactionName, Mock.Create<ITimer>(), DateTime.UtcNow, Mock.Create<ICallStackManager>(), SqlObfuscator.GetObfuscatingSqlObfuscator());
			if (segments.Any())
			{
				foreach (var segment in segments)
				{
					internalTransaction.Add(segment);
				}
			}
			else if (addSegment)
			{
				internalTransaction.Add(SimpleSegmentDataTests.createSimpleSegmentBuilder(TimeSpan.Zero, TimeSpan.Zero, 0, null, null, Enumerable.Empty<KeyValuePair<string, object>>(), "MyMockedRootNode", false));
			}
			var transactionMetadata = internalTransaction.TransactionMetadata;
			PopulateTransactionMetadataBuilder(transactionMetadata, uri, statusCode, subStatusCode, referrerCrossProcessId);

			return internalTransaction;
		}

		[NotNull]
		public static ImmutableTransaction CreateTestTransactionWithSegments(IEnumerable<Segment> segments)
		{
			var uri = "sqlTrace/Uri";

			var transactionMetadata = new TransactionMetadata();
			transactionMetadata.SetUri(uri);

			var name = new WebTransactionName("TxsWithSegments", "TxWithSegmentX");
			var metadata = transactionMetadata.ConvertToImmutableMetadata();
			var guid = Guid.NewGuid().ToString();

			var transaction = new ImmutableTransaction(name, segments, metadata, DateTime.UtcNow, TimeSpan.FromSeconds(1), guid, false, false, false, SqlObfuscator.GetObfuscatingSqlObfuscator());
			return transaction;
		}

		[NotNull]
		public static TypedSegment<DatastoreSegmentData> BuildSegment(ITransactionSegmentState txSegmentState, DatastoreVendor vendor, String model, String commandText, TimeSpan startTime = new TimeSpan(), TimeSpan? duration = null, String name = "", MethodCallData methodCallData = null, IEnumerable<KeyValuePair<String, Object>> parameters = null, String host = null, String portPathOrId = null, String databaseName = null)
		{
			if (txSegmentState == null)
				txSegmentState = Mock.Create<ITransactionSegmentState>();
			methodCallData = methodCallData ?? new MethodCallData("typeName", "methodName", 1);
			var data = new DatastoreSegmentData()
			{
				DatastoreVendorName = vendor,
				Model = model,
				CommandText = commandText,
				Host = host,
				PortPathOrId = portPathOrId,
				DatabaseName = databaseName
			};
			return new TypedSegment<DatastoreSegmentData>(startTime, duration, 
				new TypedSegment<DatastoreSegmentData>(txSegmentState, methodCallData, data, false));
		}
		private static void PopulateTransactionMetadataBuilder([NotNull] ITransactionMetadata metadata, String uri = null, Int32? statusCode = null, Int32? subStatusCode = null, String referrerCrossProcessId = null)
		{
			if (uri != null)
				metadata.SetUri(uri);
			if (statusCode != null)
				metadata.SetHttpResponseStatusCode(statusCode.Value, subStatusCode);
			if (referrerCrossProcessId != null)
				metadata.SetCrossApplicationReferrerProcessId(referrerCrossProcessId);
			if (statusCode != null)
				metadata.SetHttpResponseStatusCode(statusCode.Value, subStatusCode);

			metadata.SetOriginalUri("originalUri");
			metadata.SetPath("path");
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

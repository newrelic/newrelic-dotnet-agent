// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using NewRelic.Agent.Api;
using NewRelic.Agent.Core;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Agent.Tests.TestSerializationHelpers.Models;
using NewRelic.Agent.TestUtilities;
using Newtonsoft.Json;
using NUnit.Framework;
using NUnit.Framework.Constraints;

namespace CompositeTests
{
    [TestFixture]
    internal class ServerlessDataTransportTests
    {
        private CompositeTestAgent _compositeTestAgent;

        [SetUp]
        public void Setup()
        {
            _compositeTestAgent = new CompositeTestAgent(enableServerlessMode: true);

            _compositeTestAgent.LocalConfiguration.transactionTracer.explainThreshold = 0;
            _compositeTestAgent.LocalConfiguration.datastoreTracer.instanceReporting.enabled = true;
            _compositeTestAgent.LocalConfiguration.datastoreTracer.databaseNameReporting.enabled = true;
        }

        [TearDown]
        public void TearDown()
        {
            _compositeTestAgent.Dispose();
        }

        internal enum TestType
        {
            TransactionOnly,
            RecordMetric,
            NoticeError,
            RecordCustomEvent,
            DatastoreSegment,
            All
        }

        internal enum PayloadDataTypes
        {
            AnalyticEventData,
            CustomEventData,
            ErrorData,
            ErrorEventData,
            MetricData,
            SpanEventData,
            SqlTraceData,
            TransactionSampleData
        }

        [Test]
        [TestCase(TestType.TransactionOnly, new[] { PayloadDataTypes.AnalyticEventData, PayloadDataTypes.MetricData, PayloadDataTypes.SpanEventData, PayloadDataTypes.TransactionSampleData }, new[] { PayloadDataTypes.CustomEventData, PayloadDataTypes.ErrorData, PayloadDataTypes.ErrorEventData, PayloadDataTypes.SqlTraceData })]
        [TestCase(TestType.RecordMetric, new[] { PayloadDataTypes.AnalyticEventData, PayloadDataTypes.MetricData, PayloadDataTypes.SpanEventData, PayloadDataTypes.TransactionSampleData }, new[] { PayloadDataTypes.CustomEventData, PayloadDataTypes.ErrorData, PayloadDataTypes.ErrorEventData, PayloadDataTypes.SqlTraceData })]
        [TestCase(TestType.NoticeError, new[] { PayloadDataTypes.AnalyticEventData, PayloadDataTypes.ErrorData, PayloadDataTypes.ErrorEventData, PayloadDataTypes.MetricData, PayloadDataTypes.SpanEventData, PayloadDataTypes.TransactionSampleData }, new[] { PayloadDataTypes.CustomEventData, PayloadDataTypes.SqlTraceData })]
        [TestCase(TestType.RecordCustomEvent, new[] { PayloadDataTypes.AnalyticEventData, PayloadDataTypes.CustomEventData, PayloadDataTypes.MetricData, PayloadDataTypes.SpanEventData, PayloadDataTypes.TransactionSampleData }, new[] { PayloadDataTypes.ErrorData, PayloadDataTypes.ErrorEventData, PayloadDataTypes.SqlTraceData })]
        [TestCase(TestType.DatastoreSegment, new[] { PayloadDataTypes.AnalyticEventData, PayloadDataTypes.MetricData, PayloadDataTypes.SpanEventData, PayloadDataTypes.SqlTraceData, PayloadDataTypes.TransactionSampleData }, new[] { PayloadDataTypes.CustomEventData, PayloadDataTypes.ErrorData, PayloadDataTypes.ErrorEventData })]
        [TestCase(TestType.All, new[] { PayloadDataTypes.AnalyticEventData, PayloadDataTypes.CustomEventData, PayloadDataTypes.ErrorData, PayloadDataTypes.ErrorEventData, PayloadDataTypes.MetricData, PayloadDataTypes.SpanEventData, PayloadDataTypes.SqlTraceData, PayloadDataTypes.TransactionSampleData }, new PayloadDataTypes[] { })]
        public void ServerlessDataTransport_IncludesOnlyExpectedPayloadData(TestType testType, PayloadDataTypes[] expectedPayloadDataTypes, PayloadDataTypes[] unexpectedPayloadDataTypes)
        {
            // make sure the test case is configured correctly
            Assert.That(expectedPayloadDataTypes.Length + unexpectedPayloadDataTypes.Length, Is.EqualTo(8), "Expected and Unexpected payload arrays must contain a total of 8 elements between them");

            // ACT
            var transaction = _compositeTestAgent.GetAgent().CreateTransaction(
                isWeb: true,
                category: "Lambda",
                transactionDisplayName: "TransactionName",
                doNotTrackAsUnitOfWork: true);

            if (testType is TestType.RecordMetric or TestType.All)
                AgentApi.RecordMetric("MyCustomMetric", 1.4f);
            if (testType is TestType.NoticeError or TestType.All)
                AgentApi.NoticeError(new Exception("This is a new exception"));
            if (testType is TestType.RecordCustomEvent or TestType.All)
                AgentApi.RecordCustomEvent("MyCustomEvent", new Dictionary<string, object> { { "key1", "val1" }, { "key2", "val2" } });

            ISegment segment;
            segment = _compositeTestAgent.GetAgent().StartTransactionSegmentOrThrow("segment");
            segment.End();

            if (testType is TestType.DatastoreSegment or TestType.All)
            {
                segment = _compositeTestAgent.GetAgent().StartDatastoreRequestSegmentOrThrow("SELECT", DatastoreVendor.MSSQL, "Table1", "SELECT * FROM Table1", null, "myHost", "myPort", "myDatabase");
                segment.End();
            }
            transaction.End(); // In serverless mode, harvest happens automatically when the transaction ends

            // Assert
            var payloadJson = _compositeTestAgent.ServerlessPayload;
            var unzippedPayload = payloadJson.GetUnzippedPayload();

            var telemetryPayloads = JsonConvert.DeserializeObject<ServerlessTelemetryPayloads>(unzippedPayload);


            // if the serverless data transport didn't collect any of the following data types, they won't exist in the payload
            foreach (var payloadDataType in expectedPayloadDataTypes)
            {
                ValidatePayloadData(payloadDataType, telemetryPayloads, Is.Not.Null);
            }

            foreach (var unexpectedPayloadDataType in unexpectedPayloadDataTypes)
                ValidatePayloadData(unexpectedPayloadDataType, telemetryPayloads, Is.Null);
        }

        private static void ValidatePayloadData(PayloadDataTypes payloadDataType,
            ServerlessTelemetryPayloads telemetryPayloads, NullConstraint nullConstraint)
        {
            switch (payloadDataType)
            {
                case PayloadDataTypes.AnalyticEventData:
                    Assert.That(telemetryPayloads.TransactionEventsPayload, nullConstraint);
                    break;
                case PayloadDataTypes.CustomEventData:
                    Assert.That(telemetryPayloads.CustomEventsPayload, nullConstraint);
                    break;
                case PayloadDataTypes.ErrorData:
                    Assert.That(telemetryPayloads.ErrorTracePayload, nullConstraint);
                    break;
                case PayloadDataTypes.ErrorEventData:
                    Assert.That(telemetryPayloads.ErrorEventsPayload, nullConstraint);
                    break;
                case PayloadDataTypes.MetricData:
                    Assert.That(telemetryPayloads.MetricsPayload, nullConstraint);
                    break;
                case PayloadDataTypes.SpanEventData:
                    Assert.That(telemetryPayloads.SpanEventsPayload, nullConstraint);
                    break;
                case PayloadDataTypes.SqlTraceData:
                    Assert.That(telemetryPayloads.SqlTracePayload, nullConstraint);
                    break;
                case PayloadDataTypes.TransactionSampleData:
                    Assert.That(telemetryPayloads.TransactionTracePayload, nullConstraint); 
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}

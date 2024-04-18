// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using NewRelic.Agent.Api;
using NewRelic.Agent.Core;
using NewRelic.Agent.Core.Config;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Agent.TestUtilities;
using NUnit.Framework;

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

        internal enum TestType
        {
            TransactionOnly,
            RecordMetric,
            NoticeError,
            RecordCustomEvent,
            DatastoreSegment,
            All
        }

        [Test]
        [TestCase(TestType.TransactionOnly, new[] { "analytic_event_data", "metric_data", "span_event_data", "transaction_sample_data" }, new[] { "custom_event_data", "error_data", "error_event_data", "sql_trace_data" })]
        [TestCase(TestType.RecordMetric, new[] { "analytic_event_data", "metric_data", "span_event_data", "transaction_sample_data" }, new[] { "custom_event_data", "error_data", "error_event_data", "sql_trace_data" })]
        [TestCase(TestType.NoticeError, new[] { "analytic_event_data", "error_data", "error_event_data", "metric_data", "span_event_data", "transaction_sample_data" }, new[] { "custom_event_data", "sql_trace_data" })]
        [TestCase(TestType.RecordCustomEvent, new[] { "analytic_event_data", "custom_event_data", "metric_data", "span_event_data", "transaction_sample_data" }, new[] { "error_data", "error_event_data", "sql_trace_data" })]
        [TestCase(TestType.DatastoreSegment, new[] { "analytic_event_data", "metric_data", "span_event_data", "sql_trace_data", "transaction_sample_data" }, new[] { "custom_event_data", "error_data", "error_event_data" })]
        [TestCase(TestType.All, new[] { "analytic_event_data", "custom_event_data", "error_data", "error_event_data", "metric_data", "span_event_data", "sql_trace_data", "transaction_sample_data" }, new string[] {})]
        public void ServerlessDataTransport_IncludesOnlyExpectedPayloadData(TestType testType, string[] expectedPayloadDataTypes, string[] unexpectedPayloadDataTypes)
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

            // if the serverless data transport didn't collect any of the following data types, they won't exist in the payload
            foreach (var payloadDataType in expectedPayloadDataTypes)
                Assert.That(unzippedPayload, Does.Contain(payloadDataType));

            foreach (var unexpectedPayloadDataType in unexpectedPayloadDataTypes)
                Assert.That(unzippedPayload, Does.Not.Contain(unexpectedPayloadDataType));

            //Assert.That(unzippedPayload, Contains.Substring("error_data"));
            //Assert.That(unzippedPayload, Contains.Substring("error_event_data"));
            //Assert.That(unzippedPayload, Contains.Substring("analytic_event_data"));
            //Assert.That(unzippedPayload, Contains.Substring("span_event_data"));
            //Assert.That(unzippedPayload, Contains.Substring("custom_event_data"));
            //Assert.That(unzippedPayload, Contains.Substring("transaction_sample_data"));
            //Assert.That(unzippedPayload, Contains.Substring("sql_trace_data"));
        }
    }
}

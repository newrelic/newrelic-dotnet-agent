// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using NewRelic.Agent.Core;
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


        [Test]
        public void ServerlessDataTransport_TracksAndReportsExpectedData()
        {
            // ACT
            var transaction = _compositeTestAgent.GetAgent().CreateTransaction(
                isWeb: true,
                category: "Lambda",
                transactionDisplayName: "TransactionName",
                doNotTrackAsUnitOfWork: true);

            AgentApi.RecordMetric("MyCustomMetric", 1.4f);
            AgentApi.NoticeError(new Exception("This is a new exception"));
            AgentApi.RecordCustomEvent("MyCustomEvent", new Dictionary<string, object> { { "key1", "val1" }, { "key2", "val2" } });

            var segment = _compositeTestAgent.GetAgent().StartTransactionSegmentOrThrow("segment");
            segment.End();

            segment = _compositeTestAgent.GetAgent().StartDatastoreRequestSegmentOrThrow("SELECT", DatastoreVendor.MSSQL, "Table1", "SELECT * FROM Table1", null, "myHost", "myPort", "myDatabase");
            segment.End();

            transaction.End(); // In serverless mode, harvest happens automatically when the transaction ends

            // Assert
            var payloadJson = _compositeTestAgent.ServerlessPayload;
            var unzippedPayload = payloadJson.GetUnzippedPayload();

            // if the serverless data transport didn't collect any of the following data types, they won't exist in the payload
            Assert.That(unzippedPayload, Contains.Substring("metric_data"));
            Assert.That(unzippedPayload, Contains.Substring("error_data"));
            Assert.That(unzippedPayload, Contains.Substring("error_event_data"));
            Assert.That(unzippedPayload, Contains.Substring("analytic_event_data"));
            Assert.That(unzippedPayload, Contains.Substring("span_event_data"));
            Assert.That(unzippedPayload, Contains.Substring("custom_event_data"));
            Assert.That(unzippedPayload, Contains.Substring("transaction_sample_data"));
            Assert.That(unzippedPayload, Contains.Substring("sql_trace_data"));
        }
    }
}

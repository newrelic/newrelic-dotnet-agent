// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.DataTransport;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Agent.TestUtilities;
using Newtonsoft.Json;
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
        }


        [Test]
        public void ServerlessDataTransport_TracksAndReportsTransactionSampleData()
        {
            // ACT
            var transaction = _compositeTestAgent.GetAgent().CreateTransaction(
                isWeb: true,
                category: "Lambda",
                transactionDisplayName: "TransactionName",
                doNotTrackAsUnitOfWork: true);
            var segment = _compositeTestAgent.GetAgent().StartTransactionSegmentOrThrow("segment");
            segment.End();
            transaction.End();

            // Harvest happens automatically when the transaction ends

            // Assert
            var payloadJson = _compositeTestAgent.ServerlessPayload;
            var unzippedPayload = payloadJson.GetUnzippedPayload();
            dynamic payload = JsonConvert.DeserializeObject(unzippedPayload);
            var transactionData = payload["transaction_sample_data"];
            var transactionName = transactionData[1][0][2].Value;
            Assert.That(transactionName, Is.EqualTo("WebTransaction/Lambda/TransactionName"));
        }
    }
}

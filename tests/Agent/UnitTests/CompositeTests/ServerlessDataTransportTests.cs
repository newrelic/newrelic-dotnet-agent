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
        private IConfigurationService _configSvc;
        private IServerlessModeDataTransportService _dataTransportService;

        [SetUp]
        public void Setup()
        {
            _compositeTestAgent = new CompositeTestAgent(enableServerlessMode: true);

            _configSvc = _compositeTestAgent.Container.Resolve<IConfigurationService>();

            _dataTransportService = _compositeTestAgent.Container.Resolve<IServerlessModeDataTransportService>();
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

            _compositeTestAgent.Harvest();

            // Assert
            var payloadJson = _compositeTestAgent.ServerlessPayload;
            var unzippedPayload = payloadJson.GetUnzippedPayload();
            dynamic payload = JsonConvert.DeserializeObject(unzippedPayload);
            var transactionData = payload["transaction_sample_data"];
            Assert.That(transactionData[1][0][2].Value).IsEqualTo("WebTransaction/Lambda/TransactionName");
        }
    }
}

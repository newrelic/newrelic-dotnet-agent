// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTests.RemoteServiceFixtures;
using NewRelic.IntegrationTests.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.DataTransmission
{
    [NetFrameworkTest]
    public class ConnectResponseHandlingTests : NewRelicIntegrationTest<MvcWithCollectorFixture>
    {
        private readonly MvcWithCollectorFixture _fixture;
        private IEnumerable<CollectedRequest> _collectedRequests = null;
        private HeaderValidationData _requestHeaderMapValidationData = new HeaderValidationData();

        public ConnectResponseHandlingTests(MvcWithCollectorFixture fixture, ITestOutputHelper output) : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.AddActions
            (
                setupConfiguration: () =>
                {
                    var configModifier = new NewRelicConfigModifier(fixture.DestinationNewRelicConfigFilePath);
                    configModifier.EnableSpanEvents(true);
                    configModifier.ForceTransactionTraces();
                    configModifier.SetLogLevel("finest");
                },
                exerciseApplication: () =>
                {
                    _fixture.Get();
                    _fixture.AgentLog.WaitForLogLine(AgentLogFile.AgentConnectedLogLineRegex, TimeSpan.FromMinutes(1));
                    _fixture.AgentLog.WaitForLogLine(AgentLogFile.HarvestFinishedLogLineRegex, TimeSpan.FromMinutes(2));
                    _collectedRequests = _fixture.GetCollectedRequests();
                    _requestHeaderMapValidationData = _fixture.GetRequestHeaderMapValidationData();
                }
            );

            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            Assert.NotNull(_collectedRequests);
            var connectRequest = _collectedRequests.FirstOrDefault(x => x.Querystring.FirstOrDefault(y => y.Key == "method").Value == "connect");
            Assert.NotNull(connectRequest);

            var connectResponseData = _fixture.AgentLog.GetConnectResponseData();
            _fixture.TestLogger.WriteLine(JsonConvert.SerializeObject(connectResponseData));

            Assert.True(_requestHeaderMapValidationData.MetricDataHasMap);
            Assert.True(_requestHeaderMapValidationData.AnalyticEventDataHasMap);
            Assert.True(_requestHeaderMapValidationData.TransactionSampleDataHasMap);
            Assert.True(_requestHeaderMapValidationData.SpanEventDataHasMap);
        }
    }
}

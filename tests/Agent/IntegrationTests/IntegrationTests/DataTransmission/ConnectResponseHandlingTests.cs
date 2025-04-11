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
                    configModifier.ConfigureFasterMetricsHarvestCycle(10);
                    configModifier.ConfigureFasterTransactionTracesHarvestCycle(10);
                    configModifier.ConfigureFasterSpanEventsHarvestCycle(10);
                },
                exerciseApplication: () =>
                {
                    _fixture.Get();
                    _fixture.AgentLog.WaitForLogLine(AgentLogFile.AgentConnectedLogLineRegex, TimeSpan.FromMinutes(1));

                    // The test queries the mock collector to verify that all 4 of these data types have been sent, so wait for them.
                    _fixture.AgentLog.WaitForLogLine(AgentLogFile.MetricDataLogLineRegex, TimeSpan.FromMinutes(1));
                    _fixture.AgentLog.WaitForLogLine(AgentLogFile.AnalyticsEventDataLogLineRegex, TimeSpan.FromMinutes(1));
                    _fixture.AgentLog.WaitForLogLine(AgentLogFile.TransactionSampleLogLineRegex, TimeSpan.FromMinutes(1));
                    _fixture.AgentLog.WaitForLogLine(AgentLogFile.SpanEventDataLogLineRegex, TimeSpan.FromMinutes(1));

                    // Query mock collector
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

            Assert.True(_requestHeaderMapValidationData.MetricDataHasMap, "Metric data was missing.");
            Assert.True(_requestHeaderMapValidationData.AnalyticEventDataHasMap, "Analytic event data was missing.");
            Assert.True(_requestHeaderMapValidationData.TransactionSampleDataHasMap, "Transaction data was missing.");
            Assert.True(_requestHeaderMapValidationData.SpanEventDataHasMap, "Span event data was missing.");
        }
    }
}

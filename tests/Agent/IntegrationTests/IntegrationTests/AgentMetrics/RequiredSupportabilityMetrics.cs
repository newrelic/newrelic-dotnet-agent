// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NewRelic.Agent.IntegrationTestHelpers;
using Xunit;


namespace NewRelic.Agent.IntegrationTests.AgentMetrics
{
    // This test verifies that all supportability metrics are generated that are required by APM.
    [NetFrameworkTest]
    public class RequiredSupportabilityMetrics : NewRelicIntegrationTest<RemoteServiceFixtures.BasicMvcApplicationTestFixture>
    {
        private readonly RemoteServiceFixtures.BasicMvcApplicationTestFixture _fixture;

        public RequiredSupportabilityMetrics(RemoteServiceFixtures.BasicMvcApplicationTestFixture fixture, ITestOutputHelper output)
            : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var configModifier = new NewRelicConfigModifier(_fixture.DestinationNewRelicConfigFilePath);
                    configModifier.ConfigureFasterMetricsHarvestCycle(10);
                },
                exerciseApplication: () =>
                {
                    _fixture.Get();
                    _fixture.Get();
                    _fixture.Get();
                    _fixture.Get();

                    // The Supportability/AnalyticsEvents/TotalEventsSent metric won't be seen until a second harvest occurs, so we must wait up to 60 seconds for it
                    var startTime = DateTime.Now;
                    while (DateTime.Now <= startTime.AddSeconds(20) && !_fixture.AgentLog.GetMetrics().Any(metric => metric.MetricSpec.Name == "Supportability/AnalyticsEvents/TotalEventsSent"))
                    {
                        Thread.Sleep(TimeSpan.FromMilliseconds(500));
                    }
                }
            );
            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = @"Supportability/MetricHarvest/transmit", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"Supportability/AnalyticsEvents/TotalEventsSeen", CallCountAllHarvests = 4 },
                new Assertions.ExpectedMetric { metricName = @"Supportability/AnalyticsEvents/TotalEventsSent", CallCountAllHarvests = 4 },
            };

            var metrics = _fixture.AgentLog.GetMetrics().ToList();

            Assertions.MetricsExist(expectedMetrics, metrics);
        }
    }
}

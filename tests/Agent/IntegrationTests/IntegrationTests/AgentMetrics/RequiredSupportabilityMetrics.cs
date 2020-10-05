// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NewRelic.Agent.IntegrationTestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.AgentMetrics
{
    // This test verifies that all supportability metrics are generated that are required by APM.
    [NetFrameworkTest]
    public class RequiredSupportabilityMetrics : IClassFixture<RemoteServiceFixtures.BasicMvcApplicationTestFixture>
    {
        private readonly RemoteServiceFixtures.BasicMvcApplicationTestFixture _fixture;

        public RequiredSupportabilityMetrics(RemoteServiceFixtures.BasicMvcApplicationTestFixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var configModifier = new NewRelicConfigModifier(_fixture.DestinationNewRelicConfigFilePath);
                },
                exerciseApplication: () =>
                {
                    _fixture.Get();
                    _fixture.Get();
                    _fixture.Get();
                    _fixture.Get();

                    // The Supportability/AnalyticsEvents/TotalEventsSent metric won't be seen until a second harvest occurs, so we must wait up to 60 seconds for it
                    var startTime = DateTime.Now;
                    while (DateTime.Now <= startTime.AddSeconds(60) && !_fixture.AgentLog.GetMetrics().Any(metric => metric.MetricSpec.Name == "Supportability/AnalyticsEvents/TotalEventsSent"))
                    {
                        Thread.Sleep(TimeSpan.FromSeconds(5));
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
                new Assertions.ExpectedMetric { metricName = @"Supportability/AnalyticsEvents/TotalEventsSeen", callCount = 4 },
                new Assertions.ExpectedMetric { metricName = @"Supportability/AnalyticsEvents/TotalEventsSent", callCount = 4 },
            };

            var metrics = _fixture.AgentLog.GetMetrics().ToList();

            Assertions.MetricsExist(expectedMetrics, metrics);
        }
    }
}

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
    [NetFrameworkTest]
    public class CpuMvc : NewRelicIntegrationTest<RemoteServiceFixtures.BasicMvcApplicationTestFixture>
    {
        private readonly RemoteServiceFixtures.BasicMvcApplicationTestFixture _fixture;

        public CpuMvc(RemoteServiceFixtures.BasicMvcApplicationTestFixture fixture, ITestOutputHelper testLogger)
            : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = testLogger;
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
                    var startTime = DateTime.Now;
                    while (DateTime.Now <= startTime.AddSeconds(20))
                    {
                        if (_fixture.AgentLog.GetMetrics().Any(metric => metric.MetricSpec.Name == "CPU/User Time"))
                            break;
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
                new Assertions.ExpectedMetric {metricName = @"CPU/User Time"},
                new Assertions.ExpectedMetric {metricName = @"CPU/User/Utilization"}
            };

            var metrics = _fixture.AgentLog.GetMetrics().ToList();

            Assertions.MetricsExist(expectedMetrics, metrics);
        }
    }
}

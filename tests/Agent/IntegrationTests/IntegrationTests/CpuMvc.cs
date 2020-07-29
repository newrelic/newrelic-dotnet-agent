/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NewRelic.Agent.IntegrationTestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests
{
    public class CpuMvc : IClassFixture<RemoteServiceFixtures.BasicMvcApplication>
    {
        private readonly RemoteServiceFixtures.BasicMvcApplication _fixture;

        public CpuMvc(RemoteServiceFixtures.BasicMvcApplication fixture, ITestOutputHelper testLogger)
        {
            _fixture = fixture;
            _fixture.TestLogger = testLogger;
            _fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var configModifier = new NewRelicConfigModifier(_fixture.DestinationNewRelicConfigFilePath);
                },
                exerciseApplication: () =>
                {
                    _fixture.Get();
                    var startTime = DateTime.Now;
                    while (DateTime.Now <= startTime.AddSeconds(60))
                    {
                        if (_fixture.AgentLog.GetMetrics().Any(metric => metric.MetricSpec.Name == "CPU/User Time"))
                            break;
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
                new Assertions.ExpectedMetric {metricName = @"CPU/User Time"},
                new Assertions.ExpectedMetric {metricName = @"CPU/User/Utilization"}
            };

            var metrics = _fixture.AgentLog.GetMetrics().ToList();

            Assertions.MetricsExist(expectedMetrics, metrics);
        }
    }
}

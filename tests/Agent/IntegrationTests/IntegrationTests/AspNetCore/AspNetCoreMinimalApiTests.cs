// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.AspNetCore
{
    [NetCoreTest]
    public class AspNetCoreMinimalApiTests : NewRelicIntegrationTest<RemoteServiceFixtures.AspNetCoreMinimalApiTestsFixture>
    {
        private readonly RemoteServiceFixtures.AspNetCoreMinimalApiTestsFixture _fixture;

        private const int ExpectedTransactionCount = 2;

        public AspNetCoreMinimalApiTests(RemoteServiceFixtures.AspNetCoreMinimalApiTestsFixture fixture, ITestOutputHelper output)
            : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var configPath = fixture.DestinationNewRelicConfigFilePath;
                    var configModifier = new NewRelicConfigModifier(configPath);
                    configModifier.ForceTransactionTraces();
                    configModifier.ConfigureFasterMetricsHarvestCycle(10);
                    configModifier.ConfigureFasterTransactionTracesHarvestCycle(10);
                    configModifier.ConfigureFasterErrorTracesHarvestCycle(10);
                },
                exerciseApplication: () =>
                {
                    _fixture.MinimalApiGet();
                    _fixture.MinimalApiPost();
                }
            );
            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var metrics = _fixture.AgentLog.GetMetrics().ToList();
            Assert.NotNull(metrics);

            // verify that separate transactions were created for the GET and POST endpoint
            var list = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = @"WebTransaction/ASP/minimalapi (Get)", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"WebTransaction/ASP/minimalapi (Post)", callCount = 1 }
            };
            Assertions.MetricsExist(list, metrics);
        }
    }
}

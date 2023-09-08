// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.DataTransmission
{
    [NetFrameworkTest]
    public class FasterEventHarvestNetFrameworkTests : FasterEventHarvestTests<ConsoleDynamicMethodFixtureFWLatest>
    {
        public FasterEventHarvestNetFrameworkTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output) : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class FasterEventHarvestNetCoreTests : FasterEventHarvestTests<ConsoleDynamicMethodFixtureCoreOldest>
    {
        public FasterEventHarvestNetCoreTests(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output) : base(fixture, output)
        {
        }
    }

    public abstract class FasterEventHarvestTests<TFixture> : NewRelicIntegrationTest<TFixture> where TFixture : ConsoleDynamicMethodFixture
    {
        protected readonly TFixture Fixture;

        public FasterEventHarvestTests(TFixture fixture, ITestOutputHelper output) : base(fixture)
        {
            Fixture = fixture;
            Fixture.TestLogger = output;

            Fixture.AddCommand($"FasterEventHarvest Test");

            Fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var configModifier = new NewRelicConfigModifier(Fixture.DestinationNewRelicConfigFilePath);
                    configModifier.SetLogLevel("finest");
                }
            );

            Fixture.Initialize();
        }

        [Fact]
        public void ExpectedMetrics()
        {
            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric(){ metricName = "Supportability/EventHarvest/ReportPeriod" },
                new Assertions.ExpectedMetric(){ metricName = "Supportability/EventHarvest/ErrorEventData/HarvestLimit" },
                new Assertions.ExpectedMetric(){ metricName = "Supportability/EventHarvest/CustomEventData/HarvestLimit" },
                new Assertions.ExpectedMetric(){ metricName = "Supportability/EventHarvest/AnalyticEventData/HarvestLimit" },
                new Assertions.ExpectedMetric(){ metricName = "Supportability/SpanEvent/Limit" }

            };

            var actualMetrics = Fixture.AgentLog.GetMetrics().ToList();

            Assertions.MetricsExist(expectedMetrics, actualMetrics);
        }
    }
}

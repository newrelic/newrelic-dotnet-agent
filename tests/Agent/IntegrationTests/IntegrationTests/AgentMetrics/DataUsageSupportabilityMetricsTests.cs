// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using MultiFunctionApplicationHelpers;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Testing.Assertions;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.AgentMetrics
{

    [NetFrameworkTest]
    public class DataUsageSupportabilityMetricsTestsFW : DataUsageSupportabilityMetricsTests<ConsoleDynamicMethodFixtureFWLatest>
    {
        public DataUsageSupportabilityMetricsTestsFW(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class DataUsageSupportabilityMetricsTestsCore31 : DataUsageSupportabilityMetricsTests<ConsoleDynamicMethodFixtureCore31>
    {
        public DataUsageSupportabilityMetricsTestsCore31(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class DataUsageSupportabilityMetricsTestsCoreLatest : DataUsageSupportabilityMetricsTests<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public DataUsageSupportabilityMetricsTestsCoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    
    public abstract class DataUsageSupportabilityMetricsTests<TFixture> : NewRelicIntegrationTest<TFixture> where TFixture : ConsoleDynamicMethodFixture
    {
        protected readonly TFixture Fixture;

        public DataUsageSupportabilityMetricsTests(TFixture fixture, ITestOutputHelper output) : base(fixture)
        {
            Fixture = fixture;
            Fixture.TestLogger = output;
            Fixture.SetTimeout(TimeSpan.FromMinutes(2));

            // Logging commands
            Fixture.AddCommand($"LoggingTester SetFramework log4net");
            Fixture.AddCommand($"LoggingTester Configure");

            Fixture.AddCommand($"LoggingTester CreateSingleLogMessageInTransaction ThisIsADebugLogMessage DEBUG");

            // This is necessary to cause one harvest cycle to happen and cause the logging data endpoint to be called
            Fixture.AddCommand($"RootCommands DelaySeconds 10");



            Fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var configModifier = new NewRelicConfigModifier(fixture.DestinationNewRelicConfigFilePath);
                    configModifier.ConfigureFasterMetricsHarvestCycle(10);
                    configModifier.EnableLogForwarding()
                    .SetLogLevel("debug");
                }
            );

            Fixture.Initialize();
        }

        [Theory]
        [InlineData("Supportability/DotNET/Collector/Output/Bytes")]
        [InlineData("Supportability/DotNET/Collector/connect/Output/Bytes")]
        [InlineData("Supportability/DotNET/Collector/log_event_data/Output/Bytes")]
        public void ExpectedDataUsageMetric(string expectedMetricName)
        {
            var metrics = Fixture.AgentLog.GetMetrics().ToList();

            var dataUsageMetric = metrics.FirstOrDefault(x => x.MetricSpec.Name == expectedMetricName);
            Assert.NotNull(dataUsageMetric);

            Assert.NotEqual(0UL, dataUsageMetric.Values.CallCount);
            Assert.NotEqual(0, dataUsageMetric.Values.Total);
            Assert.NotEqual(0, dataUsageMetric.Values.TotalExclusive);
        }

    }
}

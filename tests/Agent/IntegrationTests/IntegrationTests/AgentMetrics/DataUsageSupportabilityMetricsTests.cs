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
        protected readonly TFixture _fixture;

        public DataUsageSupportabilityMetricsTests(TFixture fixture, ITestOutputHelper output) : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.SetTimeout(TimeSpan.FromMinutes(2));

            // Logging commands
            _fixture.AddCommand($"LoggingTester SetFramework log4net");
            _fixture.AddCommand($"LoggingTester ConfigureWithInfoLevelEnabled");

            _fixture.AddCommand($"LoggingTester CreateSingleLogMessageInTransaction ThisIsAInfoLogMessage INFO");

            // This is necessary to allow the logging data endpoint to be called before the application is shut down
            _fixture.AddCommand($"RootCommands DelaySeconds 5");



            _fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var configModifier = new NewRelicConfigModifier(fixture.DestinationNewRelicConfigFilePath);

                    configModifier.EnableLogForwarding()
                    .SetLogLevel("debug");
                }
            );

            _fixture.Initialize();
        }

        [Theory]
        [InlineData("Supportability/DotNET/Collector/Output/Bytes")]
        [InlineData("Supportability/DotNET/Collector/connect/Output/Bytes")]
        [InlineData("Supportability/DotNET/Collector/log_event_data/Output/Bytes")]
        public void ExpectedDataUsageMetric(string expectedMetricName)
        {
            var metrics = _fixture.AgentLog.GetMetrics().ToList();

            var dataUsageMetric = metrics.FirstOrDefault(x => x.MetricSpec.Name == expectedMetricName);
            Assert.NotNull(dataUsageMetric);

            Assert.NotEqual(0UL, dataUsageMetric.Values.CallCount);
            Assert.NotEqual(0, dataUsageMetric.Values.Total);
            Assert.NotEqual(0, dataUsageMetric.Values.TotalExclusive);
        }

    }
}

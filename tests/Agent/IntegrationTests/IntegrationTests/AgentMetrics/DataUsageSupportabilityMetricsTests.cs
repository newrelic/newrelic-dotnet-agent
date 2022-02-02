// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using MultiFunctionApplicationHelpers;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Testing.Assertions;
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
    public class DataUsageSupportabilityMetricsTestsCore21 : DataUsageSupportabilityMetricsTests<ConsoleDynamicMethodFixtureCore21>
    {
        public DataUsageSupportabilityMetricsTestsCore21(ConsoleDynamicMethodFixtureCore21 fixture, ITestOutputHelper output)
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

            // Note: I am re-using another test harness here to exercise the agent and gather unrelated metrics
            Fixture.AddCommand($"PerformanceMetrics Test 275 328");

            Fixture.Actions
            (
                setupConfiguration: () =>
                {
                    Fixture.RemoteApplication.NewRelicConfig.SetLogLevel("finest");
                    // NOTE: not sure I need this app setting...
                    Fixture.RemoteApplication.AddAppSetting("NewRelic.EventListenerSamplersEnabled", "true");
                }
            );

            Fixture.Initialize();
        }

        [Fact]
        public void ExpectedMetric_CollectorGlobalMetric()
        {
            const string expectedMetricName = "Supportability/DotNET/Collector/Output/Bytes";
            var metrics = Fixture.AgentLog.GetMetrics().ToList();

            var collectorGlobalMetric = metrics.FirstOrDefault(x => x.MetricSpec.Name == expectedMetricName);
            Assert.NotNull(collectorGlobalMetric);

            Assert.NotEqual(0UL, collectorGlobalMetric.Values.CallCount);
            Assert.NotEqual(0, collectorGlobalMetric.Values.Total);
            Assert.NotEqual(0, collectorGlobalMetric.Values.TotalExclusive);
        }

        [Fact]
        public void ExpectedMetric_CollectorConnectMetric()
        {
            const string expectedMetricName = "Supportability/DotNET/Collector/connect/Output/Bytes";
            var metrics = Fixture.AgentLog.GetMetrics().ToList();

            var collectorConnectMetric = metrics.FirstOrDefault(x => x.MetricSpec.Name == expectedMetricName);
            Assert.NotNull(collectorConnectMetric);

            Assert.NotEqual(0UL, collectorConnectMetric.Values.CallCount);
            Assert.NotEqual(0, collectorConnectMetric.Values.Total);
            Assert.NotEqual(0, collectorConnectMetric.Values.TotalExclusive);
        }
    }
}

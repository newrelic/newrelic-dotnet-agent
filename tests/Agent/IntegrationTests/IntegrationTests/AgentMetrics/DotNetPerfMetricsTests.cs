// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using NewRelic.Testing.Assertions;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.AgentMetrics
{

    [NetFrameworkTest]
    public class DotNetPerfMetricsTestsFW : DotNetPerfMetricsTests<ConsoleDynamicMethodFixtureFWLatest>
    {
        public DotNetPerfMetricsTestsFW(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }

        protected override string[] ExpectedMetricNames_GC => ExpectedMetricNames_GC_NetFramework;
    }

    [NetCoreTest]
    public class DotNetPerfMetricsTestsCoreOldest : DotNetPerfMetricsTests<ConsoleDynamicMethodFixtureCoreOldest>
    {
        public DotNetPerfMetricsTestsCoreOldest(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }

        protected override string[] ExpectedMetricNames_GC => ExpectedMetricNames_GC_NetCore;
    }

    [NetCoreTest]
    public class DotNetPerfMetricsTestsCoreLatest : DotNetPerfMetricsTests<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public DotNetPerfMetricsTestsCoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }

        protected override string[] ExpectedMetricNames_GC => ExpectedMetricNames_GC_NetCore;
    }

    
    public abstract class DotNetPerfMetricsTests<TFixture> : NewRelicIntegrationTest<TFixture> where TFixture : ConsoleDynamicMethodFixture
    {
        private const ulong COUNT_GC_INDUCED = 5;
        private const int THREADPOOL_WORKER_MAX = 275;
        private const int THREADPOOL_COMPLETION_MAX = 328;

        protected const string METRICNAME_THREADPOOL_WORKER_AVAILABLE = "Threadpool/Worker/Available";
        protected const string METRICNAME_THREADPOOL_WORKER_INUSE = "Threadpool/Worker/InUse";
        protected const string METRICNAME_THREADPOOL_COMPLETION_AVAILABLE = "Threadpool/Completion/Available";
        protected const string METRICNAME_THREADPOOL_COMPLETION_INUSE = "Threadpool/Completion/InUse";

        protected readonly TFixture Fixture;

        protected abstract string[] ExpectedMetricNames_GC { get; }
        protected string[] ExpectedMetricNames_GC_NetFramework => new string[]
        {
            "GC/Gen0/Size",
            "GC/Gen0/Promoted",
            "GC/Gen1/Size",
            "GC/Gen1/Promoted",
            "GC/Gen2/Size",
            "GC/LOH/Size",
            "GC/Handles",
            "GC/Induced",
            "GC/PercentTimeInGC",
            "GC/Gen0/Collections",
            "GC/Gen1/Collections",
            "GC/Gen2/Collections"
        };
        protected string[] ExpectedMetricNames_GC_NetCore => new string[]
        {
            "GC/Gen0/Size",
            "GC/Gen0/Promoted",
            "GC/Gen1/Size",
            "GC/Gen1/Promoted",
            "GC/Gen2/Size",
            "GC/Gen2/Survived",
            "GC/LOH/Size",
            "GC/LOH/Survived",
            "GC/Handles",
            "GC/Induced",
            "GC/Gen0/Collections",
            "GC/Gen1/Collections",
            "GC/Gen2/Collections"
        };
        protected string[] ExpectedMetricNames_Memory => new string[]
        {
            "Memory/Physical",
            "Memory/WorkingSet"
        };
        protected string[] ExpectedMetricNames_CPU => new string[]
        {
            "CPU/User/Utilization",
            "CPU/User Time"
        };

        protected string[] ExpectedMetricNames_Threadpool => new string[]
        {
            METRICNAME_THREADPOOL_WORKER_AVAILABLE,
            METRICNAME_THREADPOOL_WORKER_INUSE,
            METRICNAME_THREADPOOL_COMPLETION_AVAILABLE,
            METRICNAME_THREADPOOL_COMPLETION_INUSE,
            "Threadpool/Throughput/Requested",
            "Threadpool/Throughput/Started",
            "Threadpool/Throughput/QueueLength"
        };

        public DotNetPerfMetricsTests(TFixture fixture, ITestOutputHelper output) : base(fixture)
        {
            Fixture = fixture;
            Fixture.TestLogger = output;

            Fixture.AddCommand($"PerformanceMetrics Test {THREADPOOL_WORKER_MAX} {THREADPOOL_COMPLETION_MAX}");

            Fixture.Actions
            (
                setupConfiguration: () =>
                {
                    Fixture.RemoteApplication.NewRelicConfig.SetLogLevel("finest");
                    Fixture.RemoteApplication.AddAppSetting("NewRelic.EventListenerSamplersEnabled", "true");
                    Fixture.RemoteApplication.NewRelicConfig.ConfigureFasterMetricsHarvestCycle(10);
                }
            );

            Fixture.Initialize();
        }

        [Fact]
        public void ExpectedMetrics_GarbageCollection()
        {
            var metrics = Fixture.AgentLog.GetMetrics().ToList();
            var metricNames = metrics.Select(x => x.MetricSpec.Name).OrderBy(x => x).ToArray();

            TestMetrics("GC", metricNames, ExpectedMetricNames_GC);

            if (ExpectedMetricNames_GC.Length > 0)
            {
                // There was an issue where GC metrics were being sent without actually being hooked up to any data, this check verifies that we are getting any data at all
                var sumOfAllGcGen0Collections = metrics.Where(x => x.MetricSpec.Name == "GC/Gen0/Collections")
                    .Select(x => x.Values.CallCount).Aggregate((x, y) => x + y);

                Assert.NotEqual(0UL, sumOfAllGcGen0Collections);
            }
        }

        [Fact]
        public void ExpectedMetrics_Threadpool()
        {
            var metrics = Fixture.AgentLog.GetMetrics().ToList();
            var metricNames = metrics.Select(x => x.MetricSpec.Name).OrderBy(x => x).ToArray();

            TestMetrics("Threadpool", metricNames, ExpectedMetricNames_Threadpool);
        }

        [Fact]
        public void ExpectedMetricValues_Threadpool()
        {
            var metrics = Fixture.AgentLog.GetMetrics()
                .Where(x => string.IsNullOrWhiteSpace(x.MetricSpec.Scope)).ToList();

            var minInUseWrk = metrics.Where(x => x.MetricSpec.Name == METRICNAME_THREADPOOL_WORKER_INUSE).First().Values.Min;
            var maxAvailWrk = metrics.Where(x => x.MetricSpec.Name == METRICNAME_THREADPOOL_WORKER_AVAILABLE).First().Values.Max;

            var minInUseCmplt = metrics.Where(x => x.MetricSpec.Name == METRICNAME_THREADPOOL_COMPLETION_INUSE).First().Values.Min;
            var maxAvailCmplt = metrics.Where(x => x.MetricSpec.Name == METRICNAME_THREADPOOL_COMPLETION_AVAILABLE).First().Values.Max;

            NrAssert.Multiple
            (
                () => Assert.Equal(THREADPOOL_WORKER_MAX, minInUseWrk + maxAvailWrk),
                () => Assert.Equal(THREADPOOL_COMPLETION_MAX, minInUseCmplt + maxAvailCmplt)
            );
        }

        [Fact]
        public void ExpectedMetrics_Memory()
        {
            var metrics = Fixture.AgentLog.GetMetrics().ToList();
            var metricNames = metrics.Select(x => x.MetricSpec.Name).OrderBy(x => x).ToArray();

            TestMetrics("Memory", metricNames, ExpectedMetricNames_Memory);
        }

        [Fact]
        public void ExpectedMetrics_CPU()
        {
            var metrics = Fixture.AgentLog.GetMetrics().ToList();
            var metricNames = metrics.Select(x => x.MetricSpec.Name).OrderBy(x => x).ToArray();

            TestMetrics("CPU", metricNames, ExpectedMetricNames_CPU);
        }

        private void TestMetrics(string metricNamePrefix, string[] allMetricNames, string[] expectedMetricNames)
        {
            var foundMetrics = allMetricNames
                .Where(x => x.StartsWith(metricNamePrefix, System.StringComparison.OrdinalIgnoreCase))
                .ToArray();

            var extraMetrics = foundMetrics.Except(expectedMetricNames).ToArray();
            var missingMetrics = expectedMetricNames.Except(foundMetrics).ToArray();

            NrAssert.Multiple(
                () => Assert.True(!missingMetrics.Any(), $"The following metrics are missing: {string.Join(", ", missingMetrics)}"),
                () => Assert.True(!extraMetrics.Any(), $"The following extra metrics were reported: {string.Join(", ", extraMetrics)}")
            );
        }
    }
}

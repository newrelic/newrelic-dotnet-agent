// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTests.RemoteServiceFixtures;
using NewRelic.Testing.Assertions;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.AgentMetrics
{

    [NetFrameworkTest]
    public class DotNetPerfMetricsTestsFW : DotNetPerfMetricsTests<ConsoleDynamicMethodFixtureFW>
    {
        public DotNetPerfMetricsTestsFW(ConsoleDynamicMethodFixtureFW fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }

        protected override string[] ExpectedMetricNames_GC => new string[]
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

        protected override string[] ExpectedMetricNames_Memory => new string[]
        {
            "Memory/Physical",
            "Memory/WorkingSet"
        };

        protected override string[] ExpectedMetricNames_CPU => new string[]
        {
            "CPU/User/Utilization",
            "CPU/User Time"
        };
    }

    [NetCoreTest]
    public class DotNetPerfMetricsTestsCore22 : DotNetPerfMetricsTests<ConsoleDynamicMethodFixtureCore22>
    {
        public DotNetPerfMetricsTestsCore22(ConsoleDynamicMethodFixtureCore22 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }

        protected override string[] ExpectedMetricNames_GC => new string[]
        {
        };

        protected override string[] ExpectedMetricNames_Memory => new string[]
        {
            "Memory/Physical",
            "Memory/WorkingSet"
        };

        protected override string[] ExpectedMetricNames_CPU => new string[]
        {
            "CPU/User/Utilization",
            "CPU/User Time"
        };
    }

    [NetCoreTest]
    public class DotNetPerfMetricsTestsCoreLatest : DotNetPerfMetricsTests<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public DotNetPerfMetricsTestsCoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }

        protected override string[] ExpectedMetricNames_GC => new string[]
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
            "GC/Gen2/Collections",
        };

        protected override string[] ExpectedMetricNames_Memory => new string[]
        {
            "Memory/Physical",
            "Memory/WorkingSet"
        };

        protected override string[] ExpectedMetricNames_CPU => new string[]
        {
            "CPU/User/Utilization",
            "CPU/User Time"
        };
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
        protected abstract string[] ExpectedMetricNames_Memory { get; }
        protected abstract string[] ExpectedMetricNames_CPU { get; }

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
                .Where(x => string.IsNullOrWhiteSpace(x.MetricSpec.Scope))
                .ToDictionary(x => x.MetricSpec.Name, x => x.Values);

            var minInUseWrk = metrics[METRICNAME_THREADPOOL_WORKER_INUSE].Min;
            var maxAvailWrk = metrics[METRICNAME_THREADPOOL_WORKER_AVAILABLE].Max;

            var minInUseCmplt = metrics[METRICNAME_THREADPOOL_COMPLETION_INUSE].Min;
            var maxAvailCmplt = metrics[METRICNAME_THREADPOOL_COMPLETION_AVAILABLE].Max;

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

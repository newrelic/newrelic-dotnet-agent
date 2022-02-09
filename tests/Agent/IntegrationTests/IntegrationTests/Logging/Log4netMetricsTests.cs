// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using System.Linq;
using MultiFunctionApplicationHelpers;
using NewRelic.Agent.IntegrationTestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.Logging
{
    public abstract class Log4netMetricsTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
        where TFixture : ConsoleDynamicMethodFixture
    {
        private readonly TFixture _fixture;

        public Log4netMetricsTestsBase(TFixture fixture, ITestOutputHelper output) : base(fixture)
        {
            _fixture = fixture;
            _fixture.SetTimeout(TimeSpan.FromMinutes(3));
            _fixture.TestLogger = output;

            _fixture.AddCommand($"Log4netTester Configure");
            _fixture.AddCommand($"Log4netTester CreateSingleLogMessage LogMakerLogMakerMakeMeALog DEBUG");
            _fixture.AddCommand($"Log4netTester CreateSingleLogMessage FindMeALog INFO");
            _fixture.AddCommand($"Log4netTester CreateSingleLogMessage CatchMeALog WARN");
            _fixture.AddCommand($"Log4netTester CreateSingleLogMessage LogMakerLogMakerLookThroughYourLogBook ERROR");
            _fixture.AddCommand($"Log4netTester CreateSingleLogMessage MakeMeAPerfectLog FATAL");

            _fixture.AddCommand($"RootCommands DelaySeconds 90");

            _fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var configModifier = new NewRelicConfigModifier(fixture.DestinationNewRelicConfigFilePath);

                    configModifier.EnableLogMetrics(true)
                    .EnableDistributedTrace()
                    .SetLogLevel("debug");
                }
            );

            _fixture.Initialize();
        }

        [Fact]
        public void LogLinesPerLevelMetricsExist()
        {
            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = "Logging/lines/DEBUG", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = "Logging/lines/INFO", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = "Logging/lines/WARN", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = "Logging/lines/ERROR", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = "Logging/lines/FATAL", callCount = 1 },

                new Assertions.ExpectedMetric { metricName = "Logging/lines", callCount = 5 },
            };

            var actualMetrics = _fixture.AgentLog.GetMetrics();
            Assertions.MetricsExist(expectedMetrics, actualMetrics);
        }

        [Fact]
        public void SupportabilityDataUsageMetricsExist()
        {
            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = "Supportability/DotNET/Collector/Output/Bytes"},
                new Assertions.ExpectedMetric { metricName = "Supportability/DotNET/Collector/log_event_data/Output/Bytes"}
            };

            var actualMetrics = _fixture.AgentLog.GetMetrics();
            Assertions.MetricsExist(expectedMetrics, actualMetrics);

            var logEventDataMetrics = actualMetrics.Where(x => x.MetricSpec.Name == "Supportability/DotNET/Collector/log_event_data/Output/Bytes");
            foreach (var metric in logEventDataMetrics)
            {
                Assert.NotEqual(0UL, metric.Values.CallCount);
                Assert.NotEqual(0, metric.Values.Total);
            }
        }
    }

    [NetFrameworkTest]
    public class Log4netMetricsTestsFWLatestTests : Log4netMetricsTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public Log4netMetricsTestsFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetFrameworkTest]
    public class Log4netMetricsTestsFW471Tests : Log4netMetricsTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public Log4netMetricsTestsFW471Tests(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetFrameworkTest]
    public class Log4netMetricsTestsFW462Tests : Log4netMetricsTestsBase<ConsoleDynamicMethodFixtureFW462>
    {
        public Log4netMetricsTestsFW462Tests(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class Log4netMetricsTestsNetCoreLatestTests : Log4netMetricsTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public Log4netMetricsTestsNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class Log4netMetricsTestsNetCore50Tests : Log4netMetricsTestsBase<ConsoleDynamicMethodFixtureCore50>
    {
        public Log4netMetricsTestsNetCore50Tests(ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class Log4netMetricsTestsNetCore31Tests : Log4netMetricsTestsBase<ConsoleDynamicMethodFixtureCore31>
    {
        public Log4netMetricsTestsNetCore31Tests(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class Log4netMetricsTestsNetCore21Tests : Log4netMetricsTestsBase<ConsoleDynamicMethodFixtureCore21>
    {
        public Log4netMetricsTestsNetCore21Tests(ConsoleDynamicMethodFixtureCore21 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }
}

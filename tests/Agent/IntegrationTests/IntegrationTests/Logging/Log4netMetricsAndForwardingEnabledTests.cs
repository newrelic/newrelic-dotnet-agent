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
    public abstract class Log4NetMetricsAndForwardingEnabledTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
        where TFixture : ConsoleDynamicMethodFixture
    {
        private readonly TFixture _fixture;

        public Log4NetMetricsAndForwardingEnabledTestsBase(TFixture fixture, ITestOutputHelper output) : base(fixture)
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

                    configModifier.EnableLogMetrics()
                    .EnableLogForwarding()
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
    public class Log4NetMetricsAndForwardingEnabledTestsFWLatestTests : Log4NetMetricsAndForwardingEnabledTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public Log4NetMetricsAndForwardingEnabledTestsFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetFrameworkTest]
    public class Log4NetMetricsAndForwardingEnabledTestsFW471Tests : Log4NetMetricsAndForwardingEnabledTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public Log4NetMetricsAndForwardingEnabledTestsFW471Tests(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetFrameworkTest]
    public class Log4NetMetricsAndForwardingEnabledTestsFW462Tests : Log4NetMetricsAndForwardingEnabledTestsBase<ConsoleDynamicMethodFixtureFW462>
    {
        public Log4NetMetricsAndForwardingEnabledTestsFW462Tests(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class Log4NetMetricsAndForwardingEnabledTestsNetCoreLatestTests : Log4NetMetricsAndForwardingEnabledTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public Log4NetMetricsAndForwardingEnabledTestsNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class Log4NetMetricsAndForwardingEnabledTestsNetCore50Tests : Log4NetMetricsAndForwardingEnabledTestsBase<ConsoleDynamicMethodFixtureCore50>
    {
        public Log4NetMetricsAndForwardingEnabledTestsNetCore50Tests(ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class Log4NetMetricsAndForwardingEnabledTestsNetCore31Tests : Log4NetMetricsAndForwardingEnabledTestsBase<ConsoleDynamicMethodFixtureCore31>
    {
        public Log4NetMetricsAndForwardingEnabledTestsNetCore31Tests(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class Log4NetMetricsAndForwardingEnabledTestsNetCore21Tests : Log4NetMetricsAndForwardingEnabledTestsBase<ConsoleDynamicMethodFixtureCore21>
    {
        public Log4NetMetricsAndForwardingEnabledTestsNetCore21Tests(ConsoleDynamicMethodFixtureCore21 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }
}

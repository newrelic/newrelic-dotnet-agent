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
    public abstract class Log4NetMetricsEnabledAndForwardingDisabledTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
        where TFixture : ConsoleDynamicMethodFixture
    {
        private readonly TFixture _fixture;

        public Log4NetMetricsEnabledAndForwardingDisabledTestsBase(TFixture fixture, ITestOutputHelper output) : base(fixture)
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
                    .DisableLogForwarding()
                    .EnableDistributedTrace()
                    .SetLogLevel("debug");
                }
            );

            _fixture.Initialize();
        }

        [Fact]
        public void LogLinesPerLevelMetricsExist()
        {
            var unexpectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = "Logging/lines/DEBUG", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = "Logging/lines/INFO", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = "Logging/lines/WARN", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = "Logging/lines/ERROR", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = "Logging/lines/FATAL", callCount = 1 },

                new Assertions.ExpectedMetric { metricName = "Logging/lines", callCount = 5 },
            };

            var actualMetrics = _fixture.AgentLog.GetMetrics();
            Assertions.MetricsExist(unexpectedMetrics, actualMetrics);
        }

        [Fact]
        public void SupportabilityDataUsageMetricsDontExist()
        {
            var unexpectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = "Supportability/DotNET/Collector/log_event_data/Output/Bytes"}
            };

            var actualMetrics = _fixture.AgentLog.GetMetrics();
            Assertions.MetricsDoNotExist(unexpectedMetrics, actualMetrics);
        }
    }

    [NetFrameworkTest]
    public class Log4NetMetricsEnabledAndForwardingDisabledTestsFWLatestTests : Log4NetMetricsEnabledAndForwardingDisabledTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public Log4NetMetricsEnabledAndForwardingDisabledTestsFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetFrameworkTest]
    public class Log4NetMetricsEnabledAndForwardingDisabledTestsFW471Tests : Log4NetMetricsEnabledAndForwardingDisabledTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public Log4NetMetricsEnabledAndForwardingDisabledTestsFW471Tests(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetFrameworkTest]
    public class Log4NetMetricsEnabledAndForwardingDisabledTestsFW462Tests : Log4NetMetricsEnabledAndForwardingDisabledTestsBase<ConsoleDynamicMethodFixtureFW462>
    {
        public Log4NetMetricsEnabledAndForwardingDisabledTestsFW462Tests(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class Log4NetMetricsEnabledAndForwardingDisabledTestsNetCoreLatestTests : Log4NetMetricsEnabledAndForwardingDisabledTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public Log4NetMetricsEnabledAndForwardingDisabledTestsNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class Log4NetMetricsEnabledAndForwardingDisabledTestsNetCore50Tests : Log4NetMetricsEnabledAndForwardingDisabledTestsBase<ConsoleDynamicMethodFixtureCore50>
    {
        public Log4NetMetricsEnabledAndForwardingDisabledTestsNetCore50Tests(ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class Log4NetMetricsEnabledAndForwardingDisabledTestsNetCore31Tests : Log4NetMetricsEnabledAndForwardingDisabledTestsBase<ConsoleDynamicMethodFixtureCore31>
    {
        public Log4NetMetricsEnabledAndForwardingDisabledTestsNetCore31Tests(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class Log4NetMetricsEnabledAndForwardingDisabledTestsNetCore21Tests : Log4NetMetricsEnabledAndForwardingDisabledTestsBase<ConsoleDynamicMethodFixtureCore21>
    {
        public Log4NetMetricsEnabledAndForwardingDisabledTestsNetCore21Tests(ConsoleDynamicMethodFixtureCore21 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }
}

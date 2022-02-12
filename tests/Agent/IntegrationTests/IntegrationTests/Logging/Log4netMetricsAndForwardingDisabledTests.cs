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
    public abstract class Log4NetMetricsAndForwardingDisabledTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
        where TFixture : ConsoleDynamicMethodFixture
    {
        private readonly TFixture _fixture;

        public Log4NetMetricsAndForwardingDisabledTestsBase(TFixture fixture, ITestOutputHelper output) : base(fixture)
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

                    configModifier.DisableLogMetrics()
                    .DisableLogForwarding()
                    .EnableDistributedTrace()
                    .SetLogLevel("debug");
                }
            );

            _fixture.Initialize();
        }

        [Fact]
        public void LogLinesPerLevelMetricsDontExist()
        {
            var unexpectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = "Logging/lines/DEBUG" },
                new Assertions.ExpectedMetric { metricName = "Logging/lines/INFO" },
                new Assertions.ExpectedMetric { metricName = "Logging/lines/WARN" },
                new Assertions.ExpectedMetric { metricName = "Logging/lines/ERROR" },
                new Assertions.ExpectedMetric { metricName = "Logging/lines/FATAL" },

                new Assertions.ExpectedMetric { metricName = "Logging/lines" },
            };

            var actualMetrics = _fixture.AgentLog.GetMetrics();
            Assertions.MetricsDoNotExist(unexpectedMetrics, actualMetrics);
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
    public class Log4NetMetricsAndForwardingDisabledTestsFWLatestTests : Log4NetMetricsAndForwardingDisabledTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public Log4NetMetricsAndForwardingDisabledTestsFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetFrameworkTest]
    public class Log4NetMetricsAndForwardingDisabledTestsFW471Tests : Log4NetMetricsAndForwardingDisabledTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public Log4NetMetricsAndForwardingDisabledTestsFW471Tests(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetFrameworkTest]
    public class Log4NetMetricsAndForwardingDisabledTestsFW462Tests : Log4NetMetricsAndForwardingDisabledTestsBase<ConsoleDynamicMethodFixtureFW462>
    {
        public Log4NetMetricsAndForwardingDisabledTestsFW462Tests(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class Log4NetMetricsAndForwardingDisabledTestsNetCoreLatestTests : Log4NetMetricsAndForwardingDisabledTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public Log4NetMetricsAndForwardingDisabledTestsNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class Log4NetMetricsAndForwardingDisabledTestsNetCore50Tests : Log4NetMetricsAndForwardingDisabledTestsBase<ConsoleDynamicMethodFixtureCore50>
    {
        public Log4NetMetricsAndForwardingDisabledTestsNetCore50Tests(ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class Log4NetMetricsAndForwardingDisabledTestsNetCore31Tests : Log4NetMetricsAndForwardingDisabledTestsBase<ConsoleDynamicMethodFixtureCore31>
    {
        public Log4NetMetricsAndForwardingDisabledTestsNetCore31Tests(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class Log4NetMetricsAndForwardingDisabledTestsNetCore21Tests : Log4NetMetricsAndForwardingDisabledTestsBase<ConsoleDynamicMethodFixtureCore21>
    {
        public Log4NetMetricsAndForwardingDisabledTestsNetCore21Tests(ConsoleDynamicMethodFixtureCore21 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }
}

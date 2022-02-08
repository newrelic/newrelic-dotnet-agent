// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using MultiFunctionApplicationHelpers;
using NewRelic.Agent.IntegrationTestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.Logging
{
    public abstract class Log4netMetricsTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
        where TFixture : ConsoleDynamicMethodFixture
    {
        private const string InfoMessage = "testing123";
        private const string DebugMessage = "testing456";
        private readonly TFixture _fixture;

        public Log4netMetricsTestsBase(TFixture fixture, ITestOutputHelper output) : base(fixture)
        {
            _fixture = fixture;
            _fixture.SetTimeout(System.TimeSpan.FromMinutes(2));
            _fixture.TestLogger = output;

            _fixture.AddCommand($"Log4netTester Configure");
            _fixture.AddCommand($"Log4netTester CreateSingleLogMessage {InfoMessage} info");
            _fixture.AddCommand($"Log4netTester CreateSingleLogMessage {DebugMessage} debug");

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
        public void Test()
        {
            // Sending 1 info and 1 debug message, total 2 messages
            var expectedInfoMessages = 1;
            var expectedDebugMessages = 1;
            var expectedTotalMessages = 2;

            var actualMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = @"Logging/lines/INFO", callCount = expectedInfoMessages },
                new Assertions.ExpectedMetric { metricName = @"Logging/lines/DEBUG", callCount = expectedDebugMessages },
                new Assertions.ExpectedMetric { metricName = @"Logging/lines", callCount = expectedTotalMessages },
            };

            var metrics = _fixture.AgentLog.GetMetrics();
            Assertions.MetricsExist(actualMetrics, metrics);
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

// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.ContainerIntegrationTests.Fixtures;
using NewRelic.Agent.IntegrationTestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.ContainerIntegrationTests.Tests
{
    /// <summary>
    /// This test is meant to prevent any regressions from occurring when profiler log lines containing
    /// character codes outside of the ascii range are written to log files. Older profiler versions
    /// could trigger an error or crash when this happened. Before the profiler change, no transactions
    /// would be created by the test application, with the profiler change, the test transaction should be
    /// created successfully.
    /// </summary>
    public class LinuxUnicodeLogFileTest : NewRelicIntegrationTest<LinuxUnicodeLogFileTestFixture>
    {
        private readonly LinuxUnicodeLogFileTestFixture _fixture;

        public LinuxUnicodeLogFileTest(LinuxUnicodeLogFileTestFixture fixture, ITestOutputHelper output) : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;

            _fixture.Actions(setupConfiguration: () =>
            {
                var configModifier = new NewRelicConfigModifier(_fixture.DestinationNewRelicConfigFilePath);
                configModifier.ConfigureFasterMetricsHarvestCycle(10);
                // The original problem only seemed to occur with some of the finest level log lines
                // and it did not occur with console logs.
                configModifier.SetLogLevel("finest");
            },
                exerciseApplication: () =>
                {
                    _fixture.ExerciseApplication();

                    _fixture.Delay(11); // wait long enough to ensure a metric harvest occurs after we exercise the app
                    _fixture.AgentLog.WaitForLogLine(AgentLogBase.HarvestFinishedLogLineRegex, TimeSpan.FromSeconds(11));

                    // shut down the container and wait for the agent log to see it
                    _fixture.ShutdownRemoteApplication();
                    _fixture.AgentLog.WaitForLogLine(AgentLogBase.ShutdownLogLineRegex, TimeSpan.FromSeconds(10));
                });

            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var actualMetrics = _fixture.AgentLog.GetMetrics();

            Assert.Contains(actualMetrics, m => m.MetricSpec.Name.Equals("WebTransaction/MVC/WeatherForecast/Get"));
        }
    }
}

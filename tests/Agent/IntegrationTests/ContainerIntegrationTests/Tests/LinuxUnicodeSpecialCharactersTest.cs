// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.IO;
using System.Linq;
using System.Text;
using NewRelic.Agent.ContainerIntegrationTests.Fixtures;
using NewRelic.Agent.IntegrationTestHelpers;
using Xunit;

namespace NewRelic.Agent.ContainerIntegrationTests.Tests
{
    /// <summary>
    /// This test is meant to prevent any regressions from occurring when profiler reads instrumentation files with non-English unicode characters
    /// Older profiler versions could trigger an error or crash when this happened. Before the profiler change, no transactions/segments
    /// would be created by the test application, with the profiler change, the test transactions/segments should be
    /// created successfully.
    /// </summary>
    [Trait("Architecture", "amd64")]
    [Trait("Distro", "Ubuntu")]
    public class LinuxUnicodeSpecialCharactersTest : NewRelicIntegrationTest<LinuxUnicodeSpecialCharactersTestFixture>
    {
        private readonly LinuxUnicodeSpecialCharactersTestFixture _fixture;

        public LinuxUnicodeSpecialCharactersTest(LinuxUnicodeSpecialCharactersTestFixture fixture, ITestOutputHelper output) : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;

            _fixture.Actions(setupConfiguration: () =>
            {
                var configModifier = new NewRelicConfigModifier(_fixture.DestinationNewRelicConfigFilePath);
                configModifier.ConfigureFasterMetricsHarvestCycle(10);
                configModifier.SetLogLevel("finest");

                // Create the instrumentation xml to instrument the method with Japanese characters in the name
                _fixture.CreateJpnInstrumentationXml();
            },
                exerciseApplication: () =>
                {
                    _fixture.ExerciseApplication();

                    _fixture.Delay(11); // wait long enough to ensure a metric harvest occurs after we exercise the app
                    _fixture.AgentLog.WaitForLogLine(AgentLogBase.MetricDataLogLineRegex, TimeSpan.FromSeconds(11));

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
            Assert.Contains(actualMetrics, m => m.MetricSpec.Name.Equals("DotNet/ContainerizedAspNetCoreApp.Controllers.WeatherForecastController/何かをする"));
        }
    }
}

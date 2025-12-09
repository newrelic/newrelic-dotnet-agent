// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.IO;
using System.Text;
using NewRelic.Agent.ContainerIntegrationTests.Fixtures;
using NewRelic.Agent.IntegrationTestHelpers;
using Xunit;

namespace NewRelic.Agent.ContainerIntegrationTests.Tests
{
    /// <summary>
    /// This test is meant to prevent any regressions from occurring when profiler log lines containing
    /// character codes outside of the ascii range are written to log files. Older profiler versions
    /// could trigger an error or crash when this happened. Before the profiler change, no transactions
    /// would be created by the test application, with the profiler change, the test transaction should be
    /// created successfully.
    /// </summary>
    [Trait("Architecture", "amd64")]
    [Trait("Distro", "Ubuntu")]
    public class LinuxUnicodeByteOrderMarkTest : NewRelicIntegrationTest<LinuxUnicodeByteOrderMarkTestFixture>
    {
        private readonly LinuxUnicodeByteOrderMarkTestFixture _fixture;

        public LinuxUnicodeByteOrderMarkTest(LinuxUnicodeByteOrderMarkTestFixture fixture, ITestOutputHelper output) : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;

            _fixture.Actions(setupConfiguration: () =>
            {
                var configModifier = new NewRelicConfigModifier(_fixture.DestinationNewRelicConfigFilePath);
                configModifier.ConfigureFasterMetricsHarvestCycle(10);
                configModifier.SetLogLevel("finest");

                // Overwrite the newrelic.config to include a BOM
                configModifier.AddBomToConfig();
            },
                exerciseApplication: () =>
                {
                    if (HasBom(_fixture.DestinationNewRelicConfigFilePath))
                    {
                        _fixture.TestLogger.WriteLine("BOM found in file: " + _fixture.DestinationNewRelicConfigFilePath);
                    }
                    else
                    {
                        throw new InvalidOperationException("BOM missing from file: " + _fixture.DestinationNewRelicConfigFilePath);
                    }

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
        }

        private static bool HasBom(string filePath)
        {
            using FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            using StreamReader reader = new StreamReader(fs, Encoding.Default, detectEncodingFromByteOrderMarks: true);

            // Read a single character to trigger encoding detection
            reader.Peek();

            // Check if the detected encoding has a preamble (BOM)
            return reader.CurrentEncoding.GetPreamble().Length > 0;
        }
    }
}

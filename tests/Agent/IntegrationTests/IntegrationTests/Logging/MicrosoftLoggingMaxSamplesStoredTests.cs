// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Linq;
using MultiFunctionApplicationHelpers;
using NewRelic.Agent.IntegrationTestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.Logging
{
    public abstract class MicrosoftLoggingMaxSamplesStoredTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
        where TFixture : ConsoleDynamicMethodFixture
    {
        private readonly TFixture _fixture;

        public MicrosoftLoggingMaxSamplesStoredTestsBase(TFixture fixture, ITestOutputHelper output) : base(fixture)
        {
            _fixture = fixture;
            _fixture.SetTimeout(System.TimeSpan.FromMinutes(2));
            _fixture.TestLogger = output;

            _fixture.AddCommand($"LoggingTester SetFramework MICROSOFTLOGGING");
            _fixture.AddCommand($"LoggingTester Configure");
            _fixture.AddCommand($"LoggingTester CreateSingleLogMessage One DEBUG");
            _fixture.AddCommand($"LoggingTester CreateSingleLogMessage Two INFO");
            _fixture.AddCommand($"LoggingTester CreateSingleLogMessage Three WARN");
            _fixture.AddCommand($"LoggingTester CreateSingleLogMessage Four ERROR");
            _fixture.AddCommand($"LoggingTester CreateSingleLogMessage GetYourLogsOnTheDanceFloor FATAL");

            _fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var configModifier = new NewRelicConfigModifier(fixture.DestinationNewRelicConfigFilePath);

                    configModifier
                    .EnableApplicationLogging()
                    .EnableLogForwarding()
                    .EnableLogMetrics()
                    .SetLogForwardingMaxSamplesStored(12) // this is sent back from collector as 1 (60 second base harvest / 5 second faste harvest = 12)
                    .EnableDistributedTrace()
                    .SetLogLevel("debug");
                }
            );

            _fixture.Initialize();
        }

        [Fact]
        public void OnlyOneLogLineIsSent()
        {
            var logData = _fixture.AgentLog.GetLogEventData().FirstOrDefault();
            Assert.NotNull(logData);

            Assert.NotNull(logData.Common);
            Assert.NotNull(logData.Common.Attributes);
            Assert.False(string.IsNullOrWhiteSpace(logData.Common.Attributes.EntityGuid));
            Assert.False(string.IsNullOrWhiteSpace(logData.Common.Attributes.Hostname));

            // Since we set the maximum number of log lines stored to 1 in setupConfiguration, there should only be one log line
            Assert.Single(logData.Logs);
            var logLine = logData.Logs[0];
            Assert.False(string.IsNullOrWhiteSpace(logLine.Message));
            Assert.False(string.IsNullOrWhiteSpace(logLine.Level));
            Assert.NotEqual(0, logLine.Timestamp);

        }
    }

    [NetCoreTest]
    public class MicrosoftLoggingMaxSamplesStoredTestsNetCoreLatestTests : MicrosoftLoggingMaxSamplesStoredTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public MicrosoftLoggingMaxSamplesStoredTestsNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class MicrosoftLoggingMaxSamplesStoredTestsNetCore50Tests : MicrosoftLoggingMaxSamplesStoredTestsBase<ConsoleDynamicMethodFixtureCore50>
    {
        public MicrosoftLoggingMaxSamplesStoredTestsNetCore50Tests(ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class MicrosoftLoggingMaxSamplesStoredTestsNetCore31Tests : MicrosoftLoggingMaxSamplesStoredTestsBase<ConsoleDynamicMethodFixtureCore31>
    {
        public MicrosoftLoggingMaxSamplesStoredTestsNetCore31Tests(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class MicrosoftLoggingMaxSamplesStoredTestsNetCore22Tests : MicrosoftLoggingMaxSamplesStoredTestsBase<ConsoleDynamicMethodFixtureCore22>
    {
        public MicrosoftLoggingMaxSamplesStoredTestsNetCore22Tests(ConsoleDynamicMethodFixtureCore22 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class MicrosoftLoggingMaxSamplesStoredTestsNetCore21Tests : MicrosoftLoggingMaxSamplesStoredTestsBase<ConsoleDynamicMethodFixtureCore21>
    {
        public MicrosoftLoggingMaxSamplesStoredTestsNetCore21Tests(ConsoleDynamicMethodFixtureCore21 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }
}

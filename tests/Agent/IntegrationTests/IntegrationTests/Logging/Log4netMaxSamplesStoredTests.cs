// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Linq;
using MultiFunctionApplicationHelpers;
using NewRelic.Agent.IntegrationTestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.Logging
{
    public abstract class Log4netMaxSamplesStoredTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
        where TFixture : ConsoleDynamicMethodFixture
    {
        private readonly TFixture _fixture;

        public Log4netMaxSamplesStoredTestsBase(TFixture fixture, ITestOutputHelper output) : base(fixture)
        {
            _fixture = fixture;
            _fixture.SetTimeout(System.TimeSpan.FromMinutes(2));
            _fixture.TestLogger = output;

            _fixture.AddCommand($"Log4netTester Configure");
            _fixture.AddCommand($"Log4netTester CreateSingleLogMessage One DEBUG");
            _fixture.AddCommand($"Log4netTester CreateSingleLogMessage Two INFO");
            _fixture.AddCommand($"Log4netTester CreateSingleLogMessage Three WARN");
            _fixture.AddCommand($"Log4netTester CreateSingleLogMessage Four ERROR");
            _fixture.AddCommand($"Log4netTester CreateSingleLogMessage GetYourLogsOnTheDanceFloor FATAL");

            _fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var configModifier = new NewRelicConfigModifier(fixture.DestinationNewRelicConfigFilePath);

                    configModifier
                    .EnableApplicationLogging()
                    .EnableLogForwarding()
                    .EnableLogMetrics()
                    .SetLogForwardingMaxSamplesStored(1)
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

    [NetFrameworkTest]
    public class Log4netMaxSamplesStoredTestsFWLatestTests : Log4netMaxSamplesStoredTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public Log4netMaxSamplesStoredTestsFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetFrameworkTest]
    public class Log4netMaxSamplesStoredTestsFW471Tests : Log4netMaxSamplesStoredTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public Log4netMaxSamplesStoredTestsFW471Tests(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetFrameworkTest]
    public class Log4netMaxSamplesStoredTestsFW462Tests : Log4netMaxSamplesStoredTestsBase<ConsoleDynamicMethodFixtureFW462>
    {
        public Log4netMaxSamplesStoredTestsFW462Tests(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class Log4netMaxSamplesStoredTestsNetCoreLatestTests : Log4netMaxSamplesStoredTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public Log4netMaxSamplesStoredTestsNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class Log4netMaxSamplesStoredTestsNetCore50Tests : Log4netMaxSamplesStoredTestsBase<ConsoleDynamicMethodFixtureCore50>
    {
        public Log4netMaxSamplesStoredTestsNetCore50Tests(ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class Log4netMaxSamplesStoredTestsNetCore31Tests : Log4netMaxSamplesStoredTestsBase<ConsoleDynamicMethodFixtureCore31>
    {
        public Log4netMaxSamplesStoredTestsNetCore31Tests(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class Log4netMaxSamplesStoredTestsNetCore22Tests : Log4netMaxSamplesStoredTestsBase<ConsoleDynamicMethodFixtureCore22>
    {
        public Log4netMaxSamplesStoredTestsNetCore22Tests(ConsoleDynamicMethodFixtureCore22 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class Log4netMaxSamplesStoredTestsNetCore21Tests : Log4netMaxSamplesStoredTestsBase<ConsoleDynamicMethodFixtureCore21>
    {
        public Log4netMaxSamplesStoredTestsNetCore21Tests(ConsoleDynamicMethodFixtureCore21 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }
}

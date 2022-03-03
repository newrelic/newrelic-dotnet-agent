// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Linq;
using MultiFunctionApplicationHelpers;
using NewRelic.Agent.IntegrationTestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.Logging
{
    public abstract class SerilogMaxSamplesStoredTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
        where TFixture : ConsoleDynamicMethodFixture
    {
        private readonly TFixture _fixture;

        public SerilogMaxSamplesStoredTestsBase(TFixture fixture, ITestOutputHelper output) : base(fixture)
        {
            _fixture = fixture;
            _fixture.SetTimeout(System.TimeSpan.FromMinutes(2));
            _fixture.TestLogger = output;

            _fixture.AddCommand($"LoggingTester SetFramework Serilog");
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
    public class SerilogMaxSamplesStoredTestsFWLatestTests : SerilogMaxSamplesStoredTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public SerilogMaxSamplesStoredTestsFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetFrameworkTest]
    public class SerilogMaxSamplesStoredTestsFW471Tests : SerilogMaxSamplesStoredTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public SerilogMaxSamplesStoredTestsFW471Tests(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetFrameworkTest]
    public class SerilogMaxSamplesStoredTestsFW462Tests : SerilogMaxSamplesStoredTestsBase<ConsoleDynamicMethodFixtureFW462>
    {
        public SerilogMaxSamplesStoredTestsFW462Tests(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class SerilogMaxSamplesStoredTestsNetCoreLatestTests : SerilogMaxSamplesStoredTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public SerilogMaxSamplesStoredTestsNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class SerilogMaxSamplesStoredTestsNetCore50Tests : SerilogMaxSamplesStoredTestsBase<ConsoleDynamicMethodFixtureCore50>
    {
        public SerilogMaxSamplesStoredTestsNetCore50Tests(ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class SerilogMaxSamplesStoredTestsNetCore31Tests : SerilogMaxSamplesStoredTestsBase<ConsoleDynamicMethodFixtureCore31>
    {
        public SerilogMaxSamplesStoredTestsNetCore31Tests(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class SerilogMaxSamplesStoredTestsNetCore22Tests : SerilogMaxSamplesStoredTestsBase<ConsoleDynamicMethodFixtureCore22>
    {
        public SerilogMaxSamplesStoredTestsNetCore22Tests(ConsoleDynamicMethodFixtureCore22 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class SerilogMaxSamplesStoredTestsNetCore21Tests : SerilogMaxSamplesStoredTestsBase<ConsoleDynamicMethodFixtureCore21>
    {
        public SerilogMaxSamplesStoredTestsNetCore21Tests(ConsoleDynamicMethodFixtureCore21 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }
}

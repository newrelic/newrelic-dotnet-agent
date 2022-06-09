// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Linq;
using MultiFunctionApplicationHelpers;
using NewRelic.Agent.IntegrationTestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.Logging.MaxSamplesStored
{
    public abstract class MaxSamplesStoredTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
        where TFixture : ConsoleDynamicMethodFixture
    {
        private readonly TFixture _fixture;

        public MaxSamplesStoredTestsBase(TFixture fixture, ITestOutputHelper output, LoggingFramework loggingFramework) : base(fixture)
        {
            _fixture = fixture;
            _fixture.SetTimeout(System.TimeSpan.FromMinutes(2));
            _fixture.TestLogger = output;

            _fixture.AddCommand($"LoggingTester SetFramework {loggingFramework}");
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

                    // applicationLogging metrics and forwarding enabled by default
                    configModifier
                    .SetLogForwardingMaxSamplesStored(12)
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
            Assert.False(string.IsNullOrWhiteSpace(logLine.LogLevel));
            Assert.NotEqual(0, logLine.Timestamp);

        }
    }

    #region log4net

    [NetFrameworkTest]
    public class Log4netMaxSamplesStoredTestsFWLatestTests : MaxSamplesStoredTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public Log4netMaxSamplesStoredTestsFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Log4net)
        {
        }
    }

    [NetFrameworkTest]
    public class Log4netMaxSamplesStoredTestsFW471Tests : MaxSamplesStoredTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public Log4netMaxSamplesStoredTestsFW471Tests(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Log4net)
        {
        }
    }

    [NetFrameworkTest]
    public class Log4netMaxSamplesStoredTestsFW462Tests : MaxSamplesStoredTestsBase<ConsoleDynamicMethodFixtureFW462>
    {
        public Log4netMaxSamplesStoredTestsFW462Tests(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Log4net)
        {
        }
    }

    [NetCoreTest]
    public class Log4netMaxSamplesStoredTestsNetCoreLatestTests : MaxSamplesStoredTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public Log4netMaxSamplesStoredTestsNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Log4net)
        {
        }
    }

    [NetCoreTest]
    public class Log4netMaxSamplesStoredTestsNetCore50Tests : MaxSamplesStoredTestsBase<ConsoleDynamicMethodFixtureCore50>
    {
        public Log4netMaxSamplesStoredTestsNetCore50Tests(ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Log4net)
        {
        }
    }

    [NetCoreTest]
    public class Log4netMaxSamplesStoredTestsNetCore31Tests : MaxSamplesStoredTestsBase<ConsoleDynamicMethodFixtureCore31>
    {
        public Log4netMaxSamplesStoredTestsNetCore31Tests(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Log4net)
        {
        }
    }

    #endregion

    #region MicrosoftLogging

    [NetCoreTest]
    public class MicrosoftLoggingMaxSamplesStoredTestsNetCoreLatestTests : MaxSamplesStoredTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public MicrosoftLoggingMaxSamplesStoredTestsNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.MicrosoftLogging)
        {
        }
    }

    [NetCoreTest]
    public class MicrosoftLoggingMaxSamplesStoredTestsNetCore50Tests : MaxSamplesStoredTestsBase<ConsoleDynamicMethodFixtureCore50>
    {
        public MicrosoftLoggingMaxSamplesStoredTestsNetCore50Tests(ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.MicrosoftLogging)
        {
        }
    }

    [NetCoreTest]
    public class MicrosoftLoggingMaxSamplesStoredTestsNetCore31Tests : MaxSamplesStoredTestsBase<ConsoleDynamicMethodFixtureCore31>
    {
        public MicrosoftLoggingMaxSamplesStoredTestsNetCore31Tests(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.MicrosoftLogging)
        {
        }
    }

    #endregion

    #region Serilog

    [NetFrameworkTest]
    public class SerilogMaxSamplesStoredTestsFWLatestTests : MaxSamplesStoredTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public SerilogMaxSamplesStoredTestsFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Serilog)
        {
        }
    }

    [NetFrameworkTest]
    public class SerilogMaxSamplesStoredTestsFW471Tests : MaxSamplesStoredTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public SerilogMaxSamplesStoredTestsFW471Tests(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Serilog)
        {
        }
    }

    [NetFrameworkTest]
    public class SerilogMaxSamplesStoredTestsFW462Tests : MaxSamplesStoredTestsBase<ConsoleDynamicMethodFixtureFW462>
    {
        public SerilogMaxSamplesStoredTestsFW462Tests(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Serilog)
        {
        }
    }

    [NetCoreTest]
    public class SerilogMaxSamplesStoredTestsNetCoreLatestTests : MaxSamplesStoredTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public SerilogMaxSamplesStoredTestsNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Serilog)
        {
        }
    }

    [NetCoreTest]
    public class SerilogMaxSamplesStoredTestsNetCore50Tests : MaxSamplesStoredTestsBase<ConsoleDynamicMethodFixtureCore50>
    {
        public SerilogMaxSamplesStoredTestsNetCore50Tests(ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Serilog)
        {
        }
    }

    [NetCoreTest]
    public class SerilogMaxSamplesStoredTestsNetCore31Tests : MaxSamplesStoredTestsBase<ConsoleDynamicMethodFixtureCore31>
    {
        public SerilogMaxSamplesStoredTestsNetCore31Tests(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Serilog)
        {
        }
    }

    #endregion

    #region NLog

    [NetFrameworkTest]
    public class NLogMaxSamplesStoredTestsFWLatestTests : MaxSamplesStoredTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public NLogMaxSamplesStoredTestsFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.NLog)
        {
        }
    }

    [NetFrameworkTest]
    public class NLogMaxSamplesStoredTestsFW471Tests : MaxSamplesStoredTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public NLogMaxSamplesStoredTestsFW471Tests(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.NLog)
        {
        }
    }

    [NetFrameworkTest]
    public class NLogMaxSamplesStoredTestsFW462Tests : MaxSamplesStoredTestsBase<ConsoleDynamicMethodFixtureFW462>
    {
        public NLogMaxSamplesStoredTestsFW462Tests(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.NLog)
        {
        }
    }

    [NetCoreTest]
    public class NLogMaxSamplesStoredTestsNetCoreLatestTests : MaxSamplesStoredTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public NLogMaxSamplesStoredTestsNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.NLog)
        {
        }
    }

    [NetCoreTest]
    public class NLogMaxSamplesStoredTestsNetCore50Tests : MaxSamplesStoredTestsBase<ConsoleDynamicMethodFixtureCore50>
    {
        public NLogMaxSamplesStoredTestsNetCore50Tests(ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.NLog)
        {
        }
    }

    [NetCoreTest]
    public class NLogMaxSamplesStoredTestsNetCore31Tests : MaxSamplesStoredTestsBase<ConsoleDynamicMethodFixtureCore31>
    {
        public NLogMaxSamplesStoredTestsNetCore31Tests(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.NLog)
        {
        }
    }

    #endregion

}

// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Linq;
using MultiFunctionApplicationHelpers;
using NewRelic.Agent.IntegrationTestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.Logging.LogLevelDetection
{
    public abstract class LogLevelTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
        where TFixture : ConsoleDynamicMethodFixture
    {
        private readonly TFixture _fixture;
        private readonly LoggingFramework _loggingFramework;

        public LogLevelTestsBase(TFixture fixture, ITestOutputHelper output, LoggingFramework loggingFramework) : base(fixture)
        {
            _loggingFramework = loggingFramework;

            _fixture = fixture;
            _fixture.SetTimeout(TimeSpan.FromMinutes(2));
            _fixture.TestLogger = output;

            _fixture.AddCommand($"LoggingTester SetFramework {_loggingFramework}");
            _fixture.AddCommand($"LoggingTester ConfigureWithInfoLevelEnabled");
            _fixture.AddCommand($"LoggingTester CreateSingleLogMessage ShouldNotBeForwardedDebugMessage DEBUG");
            _fixture.AddCommand($"LoggingTester CreateSingleLogMessage ShouldBeForwardedInfoMessage INFO");
            _fixture.AddCommand($"LoggingTester CreateSingleLogMessage ShouldBeForwardedErrorMessage ERROR");

            _fixture.AddActions
            (
                setupConfiguration: () =>
                {
                    var configModifier = new NewRelicConfigModifier(fixture.DestinationNewRelicConfigFilePath);

                    // applicationLogging metrics and forwarding enabled by default
                    configModifier
                    .SetLogLevel("debug");
                },
                exerciseApplication: () =>
                {
                    _fixture.AgentLog.WaitForLogLine(AgentLogBase.LogDataLogLineRegex, TimeSpan.FromMinutes(2));
                }
            );

            _fixture.Initialize();
        }

        [Fact]
        public void OnlyTwoLogLinesAreSent()
        {
            // These assertions could be improved, but this does effectively verify that DEBUG messages are ignored
            var logData = _fixture.AgentLog.GetLogEventDataLogLines().ToArray();
            Assert.NotEmpty(logData);
            Assert.Equal(2, logData.Length);
        }

        [Fact]
        public void CorrectLogsWereForwarded()
        {
            var expectedLogLines = new Assertions.ExpectedLogLine[]
            {   
                new Assertions.ExpectedLogLine { Level = LogUtils.GetLevelName(_loggingFramework, "INFO"), LogMessage = "ShouldBeForwardedInfoMessage" },
                new Assertions.ExpectedLogLine { Level = LogUtils.GetLevelName(_loggingFramework, "ERROR"), LogMessage = "ShouldBeForwardedErrorMessage" }
            };

            var unexpectedLogLines = new Assertions.ExpectedLogLine { Level = LogUtils.GetLevelName(_loggingFramework, "DEBUG"), LogMessage = "ShouldNotBeForwardedDebugMessage" };

            var logLines = _fixture.AgentLog.GetLogEventDataLogLines().ToArray();

            Assertions.LogLinesExist(expectedLogLines, logLines);
            Assertions.LogLineDoesntExist(unexpectedLogLines, logLines);
        }
    }

    #region log4net

    [NetFrameworkTest]
    public class Log4netLogLevelFWLatestTests : LogLevelTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public Log4netLogLevelFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Log4net)
        {
        }
    }

    [NetFrameworkTest]
    public class Log4netLogLevelFW471Tests : LogLevelTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public Log4netLogLevelFW471Tests(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Log4net)
        {
        }
    }

    [NetFrameworkTest]
    public class Log4netLogLevelFW462Tests : LogLevelTestsBase<ConsoleDynamicMethodFixtureFW462>
    {
        public Log4netLogLevelFW462Tests(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Log4net)
        {
        }
    }

    [NetCoreTest]
    public class Log4netLogLevelNetCoreLatestTests : LogLevelTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public Log4netLogLevelNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Log4net)
        {
        }
    }

    [NetCoreTest]
    public class Log4netLogLevelTestsNetCoreOldestTests : LogLevelTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
    {
        public Log4netLogLevelTestsNetCoreOldestTests(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Log4net)
        {
        }
    }

    #endregion

    #region MicrosoftLogging

    [NetCoreTest]
    public class MicrosoftLoggingLogLevelTestsNetCoreLatestTests : LogLevelTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public MicrosoftLoggingLogLevelTestsNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.MicrosoftLogging)
        {
        }
    }

    [NetCoreTest]
    public class MicrosoftLoggingLogLevelTestsNetCoreOldestTests : LogLevelTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
    {
        public MicrosoftLoggingLogLevelTestsNetCoreOldestTests(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.MicrosoftLogging)
        {
        }
    }

    [NetFrameworkTest]
    public class MicrosoftLoggingLogLevelTestsFWLatestTests : LogLevelTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public MicrosoftLoggingLogLevelTestsFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.MicrosoftLogging)
        {
        }
    }

    #endregion

    #region Serilog

    [NetFrameworkTest]
    public class SerilogLogLevelTestsFWLatestTests : LogLevelTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public SerilogLogLevelTestsFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Serilog)
        {
        }
    }

    [NetFrameworkTest]
    public class SerilogLogLevelTestsFW471Tests : LogLevelTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public SerilogLogLevelTestsFW471Tests(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Serilog)
        {
        }
    }

    [NetFrameworkTest]
    public class SerilogLogLevelTestsFW462Tests : LogLevelTestsBase<ConsoleDynamicMethodFixtureFW462>
    {
        public SerilogLogLevelTestsFW462Tests(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Serilog)
        {
        }
    }

    [NetCoreTest]
    public class SerilogLogLevelTestsNetCoreLatestTests : LogLevelTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public SerilogLogLevelTestsNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Serilog)
        {
        }
    }

    [NetCoreTest]
    public class SerilogLogLevelTestsNetCoreOldestTests : LogLevelTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
    {
        public SerilogLogLevelTestsNetCoreOldestTests(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Serilog)
        {
        }
    }
    #endregion

    #region NLog

    [NetFrameworkTest]
    public class NLogLogLevelTestsFWLatestTests : LogLevelTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public NLogLogLevelTestsFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.NLog)
        {
        }
    }

    [NetFrameworkTest]
    public class NLogLogLevelTestsFW471Tests : LogLevelTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public NLogLogLevelTestsFW471Tests(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.NLog)
        {
        }
    }

    [NetFrameworkTest]
    public class NLogLogLevelTestsFW462Tests : LogLevelTestsBase<ConsoleDynamicMethodFixtureFW462>
    {
        public NLogLogLevelTestsFW462Tests(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.NLog)
        {
        }
    }

    [NetCoreTest]
    public class NLogLogLevelTestsNetCoreLatestTests : LogLevelTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public NLogLogLevelTestsNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.NLog)
        {
        }
    }

    [NetCoreTest]
    public class NLogLogLevelTestsNetCoreOldestTests : LogLevelTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
    {
        public NLogLogLevelTestsNetCoreOldestTests(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.NLog)
        {
        }
    }

    #endregion

    #region Sitecore

    [NetFrameworkTest]
    public class SitecoreLogLevelTestsFWLatestTests : LogLevelTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public SitecoreLogLevelTestsFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Sitecore)
        {
        }
    }

    [NetFrameworkTest]
    public class SitecoreLogLevelTestsFW480Tests : LogLevelTestsBase<ConsoleDynamicMethodFixtureFW48>
    {
        public SitecoreLogLevelTestsFW480Tests(ConsoleDynamicMethodFixtureFW48 fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Sitecore)
        {
        }
    }

    #endregion
}

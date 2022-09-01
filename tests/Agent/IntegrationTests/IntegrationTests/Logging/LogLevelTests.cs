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

        public LogLevelTestsBase(TFixture fixture, ITestOutputHelper output, LoggingFramework loggingFramework) : base(fixture)
        {
            _fixture = fixture;
            _fixture.SetTimeout(System.TimeSpan.FromMinutes(2));
            _fixture.TestLogger = output;

            _fixture.AddCommand($"LoggingTester SetFramework {loggingFramework}");
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
                    .EnableDistributedTrace()
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
            // TODO: Better assertions, this should work for now though
            var logData = _fixture.AgentLog.GetLogEventDataLogLines().ToArray();
            Assert.NotEmpty(logData);
            Assert.Equal(2, logData.Length);
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
    public class Log4netLogLevelNetCore50Tests : LogLevelTestsBase<ConsoleDynamicMethodFixtureCore50>
    {
        public Log4netLogLevelNetCore50Tests(ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Log4net)
        {
        }
    }

    [NetCoreTest]
    public class Log4netLogLevelTestsNetCore31Tests : LogLevelTestsBase<ConsoleDynamicMethodFixtureCore31>
    {
        public Log4netLogLevelTestsNetCore31Tests(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
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
    public class MicrosoftLoggingLogLevelTestsNetCore50Tests : LogLevelTestsBase<ConsoleDynamicMethodFixtureCore50>
    {
        public MicrosoftLoggingLogLevelTestsNetCore50Tests(ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.MicrosoftLogging)
        {
        }
    }

    [NetCoreTest]
    public class MicrosoftLoggingLogLevelTestsNetCore31Tests : LogLevelTestsBase<ConsoleDynamicMethodFixtureCore31>
    {
        public MicrosoftLoggingLogLevelTestsNetCore31Tests(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
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
    public class SerilogLogLevelTestsNetCore50Tests : LogLevelTestsBase<ConsoleDynamicMethodFixtureCore50>
    {
        public SerilogLogLevelTestsNetCore50Tests(ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Serilog)
        {
        }
    }

    [NetCoreTest]
    public class SerilogLogLevelTestsNetCore31Tests : LogLevelTestsBase<ConsoleDynamicMethodFixtureCore31>
    {
        public SerilogLogLevelTestsNetCore31Tests(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Serilog)
        {
        }
    }

    [NetCoreTest]
    public class SerilogWebLogLevelTestsNetCore60Tests : LogLevelTestsBase<ConsoleDynamicMethodFixtureCore60>
    {
        public SerilogWebLogLevelTestsNetCore60Tests(ConsoleDynamicMethodFixtureCore60 fixture,
            ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.SerilogWeb)
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
    public class NLogLogLevelTestsNetCore50Tests : LogLevelTestsBase<ConsoleDynamicMethodFixtureCore50>
    {
        public NLogLogLevelTestsNetCore50Tests(ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.NLog)
        {
        }
    }

    [NetCoreTest]
    public class NLogLogLevelTestsNetCore31Tests : LogLevelTestsBase<ConsoleDynamicMethodFixtureCore31>
    {
        public NLogLogLevelTestsNetCore31Tests(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.NLog)
        {
        }
    }

    #endregion

}

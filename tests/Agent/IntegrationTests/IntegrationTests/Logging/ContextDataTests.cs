// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using MultiFunctionApplicationHelpers;
using NewRelic.Agent.IntegrationTestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.Logging.ContextData
{
    public abstract class ContextDataTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
        where TFixture : ConsoleDynamicMethodFixture
    {
        private readonly TFixture _fixture;
        private LoggingFramework _loggingFramework;

        private const string InfoMessage = "HelloWorld";

        // There are several entries in this dictionary to allow for different methods of adding the values in the test adapter
        // If you need more entries for your framework, add them
        private Dictionary<string, string> _expectedAttributes = new Dictionary<string, string>()
        {
            { "mycontext1", "foo" },
            { "mycontext2", "bar" },
            { "mycontext3", "test" },
            { "mycontext4", "value" },
        };


        public ContextDataTestsBase(TFixture fixture, ITestOutputHelper output, LoggingFramework loggingFramework) : base(fixture)
        {
            _fixture = fixture;
            _loggingFramework = loggingFramework;
            _fixture.SetTimeout(TimeSpan.FromMinutes(2));
            _fixture.TestLogger = output;

            _fixture.AddCommand($"LoggingTester SetFramework {_loggingFramework}");
            _fixture.AddCommand($"LoggingTester Configure");

            string context = string.Join(",", _expectedAttributes.Select(x => x.Key + "=" + x.Value).ToArray());

            _fixture.AddCommand($"LoggingTester CreateSingleLogMessage {InfoMessage} INFO {context}");

            _fixture.AddActions
            (
                setupConfiguration: () =>
                {
                    var configModifier = new NewRelicConfigModifier(fixture.DestinationNewRelicConfigFilePath);

                    configModifier
                    .EnableContextData(true)
                    .SetLogLevel("debug");
                },
                exerciseApplication: () =>
                {
                    _fixture.AgentLog.WaitForLogLine(AgentLogBase.LogDataLogLineRegex, TimeSpan.FromSeconds(30));
                }
            );

            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var expectedLogLines = new Assertions.ExpectedLogLine[]
            {
                new Assertions.ExpectedLogLine
                {
                    Level = LogUtils.GetLevelName(_loggingFramework, "INFO"),
                    LogMessage = InfoMessage,
                    Attributes = _expectedAttributes
                }
            };

            var logLines = _fixture.AgentLog.GetLogEventDataLogLines().ToArray();

            // different verions of log4net add varying amounts of default attributes, we ignore the counts becuase of this
            Assertions.LogLinesExist(expectedLogLines, logLines, ignoreAttributeCount: true);
        }
    }

    #region log4net

    [NetFrameworkTest]
    public class Log4NetContextDataFWLatestTests : ContextDataTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public Log4NetContextDataFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Log4net)
        {
        }
    }

    [NetFrameworkTest]
    public class Log4NetContextDataFW471Tests : ContextDataTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public Log4NetContextDataFW471Tests(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Log4net)
        {
        }
    }

    [NetFrameworkTest]
    public class Log4NetContextDataFW462Tests : ContextDataTestsBase<ConsoleDynamicMethodFixtureFW462>
    {
        public Log4NetContextDataFW462Tests(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Log4net)
        {
        }
    }

    [NetCoreTest]
    public class Log4NetContextDataNetCoreLatestTests : ContextDataTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public Log4NetContextDataNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Log4net)
        {
        }
    }

    [NetCoreTest]
    public class Log4NetContextDataNetCore60Tests : ContextDataTestsBase<ConsoleDynamicMethodFixtureCore60>
    {
        public Log4NetContextDataNetCore60Tests(ConsoleDynamicMethodFixtureCore60 fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Log4net)
        {
        }
    }

    [NetCoreTest]
    public class Log4NetContextDataNetCore50Tests : ContextDataTestsBase<ConsoleDynamicMethodFixtureCore50>
    {
        public Log4NetContextDataNetCore50Tests(ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Log4net)
        {
        }
    }

    [NetCoreTest]
    public class Log4NetContextDataNetCore31Tests : ContextDataTestsBase<ConsoleDynamicMethodFixtureCore31>
    {
        public Log4NetContextDataNetCore31Tests(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Log4net)
        {
        }
    }

    #endregion

    #region NLog

    [NetFrameworkTest]
    public class NLogContextDataFWLatestTests : ContextDataTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public NLogContextDataFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.NLog)
        {
        }
    }

    [NetFrameworkTest]
    public class NLogContextDataFW471Tests : ContextDataTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public NLogContextDataFW471Tests(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.NLog)
        {
        }
    }

    [NetFrameworkTest]
    public class NLogContextDataFW462Tests : ContextDataTestsBase<ConsoleDynamicMethodFixtureFW462>
    {
        public NLogContextDataFW462Tests(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.NLog)
        {
        }
    }

    [NetCoreTest]
    public class NLogContextDataNetCoreLatestTests : ContextDataTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public NLogContextDataNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.NLog)
        {
        }
    }

    [NetCoreTest]
    public class NLogContextDataNetCore60Tests : ContextDataTestsBase<ConsoleDynamicMethodFixtureCore60>
    {
        public NLogContextDataNetCore60Tests(ConsoleDynamicMethodFixtureCore60 fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.NLog)
        {
        }
    }

    [NetCoreTest]
    public class NLogContextDataNetCore50Tests : ContextDataTestsBase<ConsoleDynamicMethodFixtureCore50>
    {
        public NLogContextDataNetCore50Tests(ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.NLog)
        {
        }
    }

    [NetCoreTest]
    public class NLogContextDataNetCore31Tests : ContextDataTestsBase<ConsoleDynamicMethodFixtureCore31>
    {
        public NLogContextDataNetCore31Tests(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.NLog)
        {
        }
    }

    #endregion

    #region Serilog

    [NetFrameworkTest]
    public class SerilogContextDataFWLatestTests : ContextDataTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public SerilogContextDataFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Serilog)
        {
        }
    }

    [NetFrameworkTest]
    public class SerilogContextDataFW471Tests : ContextDataTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public SerilogContextDataFW471Tests(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Serilog)
        {
        }
    }

    [NetFrameworkTest]
    public class SerilogContextDataFW462Tests : ContextDataTestsBase<ConsoleDynamicMethodFixtureFW462>
    {
        public SerilogContextDataFW462Tests(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Serilog)
        {
        }
    }

    [NetCoreTest]
    public class SerilogContextDataNetCoreLatestTests : ContextDataTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public SerilogContextDataNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Serilog)
        {
        }
    }

    [NetCoreTest]
    public class SerilogContextDataNetCore60Tests : ContextDataTestsBase<ConsoleDynamicMethodFixtureCore60>
    {
        public SerilogContextDataNetCore60Tests(ConsoleDynamicMethodFixtureCore60 fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Serilog)
        {
        }
    }

    [NetCoreTest]
    public class SerilogContextDataNetCore50Tests : ContextDataTestsBase<ConsoleDynamicMethodFixtureCore50>
    {
        public SerilogContextDataNetCore50Tests(ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Serilog)
        {
        }
    }

    [NetCoreTest]
    public class SerilogContextDataNetCore31Tests : ContextDataTestsBase<ConsoleDynamicMethodFixtureCore31>
    {
        public SerilogContextDataNetCore31Tests(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Serilog)
        {
        }
    }

    [NetCoreTest]
    public class SerilogWebContextDataNetCore60Tests : ContextDataTestsBase<ConsoleDynamicMethodFixtureCore60>
    {
        public SerilogWebContextDataNetCore60Tests(ConsoleDynamicMethodFixtureCore60 fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.SerilogWeb)
        {
        }
    }

    #endregion

    #region MEL

    [NetFrameworkTest]
    public class MELContextDataFWLatestTests : ContextDataTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public MELContextDataFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.MicrosoftLogging)
        {
        }
    }

    [NetCoreTest]
    public class MELContextDataNetCoreLatestTests : ContextDataTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public MELContextDataNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.MicrosoftLogging)
        {
        }
    }

    [NetCoreTest]
    public class MELContextDataNetCore60Tests : ContextDataTestsBase<ConsoleDynamicMethodFixtureCore60>
    {
        public MELContextDataNetCore60Tests(ConsoleDynamicMethodFixtureCore60 fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.MicrosoftLogging)
        {
        }
    }

    [NetCoreTest]
    public class MELContextDataNetCore50Tests : ContextDataTestsBase<ConsoleDynamicMethodFixtureCore50>
    {
        public MELContextDataNetCore50Tests(ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.MicrosoftLogging)
        {
        }
    }

    [NetCoreTest]
    public class MELContextDataNetCore31Tests : ContextDataTestsBase<ConsoleDynamicMethodFixtureCore31>
    {
        public MELContextDataNetCore31Tests(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.MicrosoftLogging)
        {
        }
    }

    #endregion
}

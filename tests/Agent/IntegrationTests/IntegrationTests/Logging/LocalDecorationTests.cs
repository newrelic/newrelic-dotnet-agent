// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;
using MultiFunctionApplicationHelpers;
using NewRelic.Agent.IntegrationTestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.Logging.LocalDecoration
{
    public enum LayoutType
    {
        Pattern,
        Json
    }

    public abstract class LocalDecorationTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
        where TFixture : ConsoleDynamicMethodFixture
    {
        private readonly TFixture _fixture;
        private readonly bool _isWebLogTest;
        private const string _primaryApplicationName = "Local Decoration Test App Name";
        private const string _secondaryApplicationName = "Some other testing application name";
        private const string _compositeApplicationName = _primaryApplicationName + ", " + _secondaryApplicationName;
        private const string _testMessage = "DecorateMe";

        public LocalDecorationTestsBase(TFixture fixture, ITestOutputHelper output, LayoutType layoutType,
            LoggingFramework loggingFramework, bool isWebLogTest = false,  bool logWithParam = false) : base(fixture)
        {
            _isWebLogTest = isWebLogTest;
            _fixture = fixture;
            _fixture.SetTimeout(TimeSpan.FromMinutes(2));
            _fixture.TestLogger = output;

            _fixture.AddCommand($"LoggingTester SetFramework {loggingFramework}");
            _fixture.AddCommand($"LoggingTester Configure{layoutType}LayoutAppenderForDecoration");
            if (logWithParam)
            {
                _fixture.AddCommand($"LoggingTester CreateSingleLogMessageInTransactionWithParam {_testMessage}{"{@param}"}");
            }
            else
            {
                _fixture.AddCommand($"LoggingTester CreateSingleLogMessageInTransaction {_testMessage} DEBUG");
            }

            _fixture.RemoteApplication.AppName = _compositeApplicationName;

            _fixture.AddActions
            (
                setupConfiguration: () =>
                {
                    var configModifier = new NewRelicConfigModifier(fixture.DestinationNewRelicConfigFilePath);

                    configModifier
                    .EnableLogDecoration(true)
                    .SetLogLevel("finest");
                },
                exerciseApplication: () =>
                {
                    _fixture.AgentLog.WaitForLogLine(AgentLogBase.TransactionTransformCompletedLogLineRegex, TimeSpan.FromSeconds(30));
                    _fixture.AgentLog.WaitForLogLine(AgentLogBase.LogDataLogLineRegex, TimeSpan.FromSeconds(30));
                }
            );

            _fixture.Initialize();
        }

        [Fact]
        public void LogIsDecorated()
        {
            var testOutput = _fixture.RemoteApplication.CapturedOutput.StandardOutput;
            // Make sure the original message is there
            var commandResults = Regex.Split(testOutput, System.Environment.NewLine).Where(l => !l.Contains("EXECUTING"));
            Assert.Contains(_testMessage, string.Join(System.Environment.NewLine, commandResults));

            // Sample decorated data we are looking for:
            // "NR-LINKING|MjczMDcwfEFQTXxBUFBMSUNBVElPTnwxODQyMg|blah.hsd1.ca.comcast.net|45f120972d61834b96fb890d2a8f97e7|840d9a82e8bc18a8|myApplicationName|"
            // For web logging tests there will be multiple unexpected log lines. To support this, we prefix all log lines in web tests with a known string.
            var regex = new Regex((_isWebLogTest ? "^ThisIsAWebLog.*" : "") + @"NR-LINKING\|([a-zA-Z0-9]*)\|([a-zA-Z0-9._-]*)\|([a-zA-Z0-9]*)\|([a-zA-Z0-9]*)\|(.+?)\|", RegexOptions.Multiline);

            // Make sure the added metadata is there
            MatchCollection matches = regex.Matches(testOutput);
            Assert.True(matches.Count > 0);

            foreach (Match match in matches)
            {
                Assert.True(match.Success);
                Assert.NotEmpty(match.Groups);
                var entityGuid = match.Groups[1].Value;
                var hostname = match.Groups[2].Value;
                var traceId = match.Groups[3].Value;
                var spanId = match.Groups[4].Value;
                var entityName = HttpUtility.UrlDecode(match.Groups[5].Value);

                Assert.NotNull(entityGuid);
                Assert.NotNull(hostname);
                Assert.NotNull(traceId);
                Assert.NotNull(spanId);
                Assert.Equal(_primaryApplicationName, entityName);
            }
        }
    }

    #region log4net

    #region Json layout

    [NetFrameworkTest]
    public class Log4netJsonLayoutDecorationEnabledTestsFWLatestTests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public Log4netJsonLayoutDecorationEnabledTestsFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, LayoutType.Json, LoggingFramework.Log4net)
        {
        }
    }

    [NetFrameworkTest]
    public class Log4netJsonLayoutDecorationEnabledTestsFW471Tests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public Log4netJsonLayoutDecorationEnabledTestsFW471Tests(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output, LayoutType.Json, LoggingFramework.Log4net)
        {
        }
    }

    [NetCoreTest]
    public class Log4netJsonLayoutDecorationEnabledTestsNetCoreLatestTests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public Log4netJsonLayoutDecorationEnabledTestsNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, LayoutType.Json, LoggingFramework.Log4net)
        {
        }
    }

    [NetCoreTest]
    public class Log4netJsonLayoutDecorationEnabledTestsNetCore60Tests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCore60>
    {
        public Log4netJsonLayoutDecorationEnabledTestsNetCore60Tests(ConsoleDynamicMethodFixtureCore60 fixture, ITestOutputHelper output)
            : base(fixture, output, LayoutType.Json, LoggingFramework.Log4net)
        {
        }
    }

    [NetCoreTest]
    public class Log4netJsonLayoutDecorationEnabledTestsNetCore50Tests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCore50>
    {
        public Log4netJsonLayoutDecorationEnabledTestsNetCore50Tests(ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
            : base(fixture, output, LayoutType.Json, LoggingFramework.Log4net)
        {
        }
    }

    [NetCoreTest]
    public class Log4netJsonLayoutDecorationEnabledTestsNetCore31Tests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCore31>
    {
        public Log4netJsonLayoutDecorationEnabledTestsNetCore31Tests(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
            : base(fixture, output, LayoutType.Json, LoggingFramework.Log4net)
        {
        }
    }

    #endregion

    #region Pattern layout

    [NetFrameworkTest]
    public class Log4netPatternLayoutDecorationEnabledTestsFWLatestTests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public Log4netPatternLayoutDecorationEnabledTestsFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, LayoutType.Pattern, LoggingFramework.Log4net)
        {
        }
    }

    [NetFrameworkTest]
    public class Log4netPatternLayoutDecorationEnabledTestsFW471Tests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public Log4netPatternLayoutDecorationEnabledTestsFW471Tests(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output, LayoutType.Pattern, LoggingFramework.Log4net)
        {
        }
    }

    [NetCoreTest]
    public class Log4netPatternLayoutDecorationEnabledTestsNetCoreLatestTests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public Log4netPatternLayoutDecorationEnabledTestsNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, LayoutType.Pattern, LoggingFramework.Log4net)
        {
        }
    }

    [NetCoreTest]
    public class Log4netPatternLayoutDecorationEnabledTestsNetCore60Tests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCore60>
    {
        public Log4netPatternLayoutDecorationEnabledTestsNetCore60Tests(ConsoleDynamicMethodFixtureCore60 fixture, ITestOutputHelper output)
            : base(fixture, output, LayoutType.Pattern, LoggingFramework.Log4net)
        {
        }
    }

    [NetCoreTest]
    public class Log4netPatternLayoutDecorationEnabledTestsNetCore50Tests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCore50>
    {
        public Log4netPatternLayoutDecorationEnabledTestsNetCore50Tests(ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
            : base(fixture, output, LayoutType.Pattern, LoggingFramework.Log4net)
        {
        }
    }

    [NetCoreTest]
    public class Log4netPatternLayoutDecorationEnabledTestsNetCore31Tests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCore31>
    {
        public Log4netPatternLayoutDecorationEnabledTestsNetCore31Tests(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
            : base(fixture, output, LayoutType.Pattern, LoggingFramework.Log4net)
        {
        }
    }

    #endregion

    #endregion

    #region Serilog

    #region Json layout
    [NetFrameworkTest]
    public class SerilogJsonLayoutDecorationEnabledTestsFWLatestTests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public SerilogJsonLayoutDecorationEnabledTestsFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, LayoutType.Json, LoggingFramework.Serilog)
        {
        }
    }

    [NetFrameworkTest]
    public class SerilogJsonLayoutDecorationEnabledTestsFW471Tests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public SerilogJsonLayoutDecorationEnabledTestsFW471Tests(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output, LayoutType.Json, LoggingFramework.Serilog)
        {
        }
    }

    [NetCoreTest]
    public class SerilogJsonLayoutDecorationEnabledTestsNetCoreLatestTests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public SerilogJsonLayoutDecorationEnabledTestsNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, LayoutType.Json, LoggingFramework.Serilog)
        {
        }
    }

    [NetCoreTest]
    public class SerilogJsonLayoutDecorationEnabledTestsNetCore60Tests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCore60>
    {
        public SerilogJsonLayoutDecorationEnabledTestsNetCore60Tests(ConsoleDynamicMethodFixtureCore60 fixture, ITestOutputHelper output)
            : base(fixture, output, LayoutType.Json, LoggingFramework.Serilog)
        {
        }
    }

    [NetCoreTest]
    public class SerilogJsonLayoutDecorationEnabledTestsNetCore50Tests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCore50>
    {
        public SerilogJsonLayoutDecorationEnabledTestsNetCore50Tests(ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
            : base(fixture, output, LayoutType.Json, LoggingFramework.Serilog)
        {
        }
    }

    [NetCoreTest]
    public class SerilogJsonLayoutDecorationEnabledTestsNetCore31Tests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCore31>
    {
        public SerilogJsonLayoutDecorationEnabledTestsNetCore31Tests(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
            : base(fixture, output, LayoutType.Json, LoggingFramework.Serilog)
        {
        }
    }

    #endregion

    #region Pattern layout

    [NetFrameworkTest]
    public class SerilogPatternLayoutDecorationEnabledTestsFWLatestTests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public SerilogPatternLayoutDecorationEnabledTestsFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, LayoutType.Pattern, LoggingFramework.Serilog)
        {
        }
    }

    [NetFrameworkTest]
    public class SerilogPatternLayoutDecorationEnabledTestsFW471Tests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public SerilogPatternLayoutDecorationEnabledTestsFW471Tests(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output, LayoutType.Pattern, LoggingFramework.Serilog)
        {
        }
    }

    [NetCoreTest]
    public class SerilogPatternLayoutDecorationEnabledTestsNetCoreLatestTests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public SerilogPatternLayoutDecorationEnabledTestsNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, LayoutType.Pattern, LoggingFramework.Serilog)
        {
        }
    }

    [NetCoreTest]
    public class SerilogWebPatternLayoutDecorationEnabledTestsNetCore60Tests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCore60>
    {
        public SerilogWebPatternLayoutDecorationEnabledTestsNetCore60Tests(ConsoleDynamicMethodFixtureCore60 fixture, ITestOutputHelper output)
            : base(fixture, output, LayoutType.Pattern, LoggingFramework.SerilogWeb, true)
        {
        }
    }

    [NetCoreTest]
    public class SerilogPatternLayoutDecorationEnabledTestsNetCore60Tests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCore60>
    {
        public SerilogPatternLayoutDecorationEnabledTestsNetCore60Tests(ConsoleDynamicMethodFixtureCore60 fixture, ITestOutputHelper output)
            : base(fixture, output, LayoutType.Pattern, LoggingFramework.Serilog)
        {
        }
    }

    [NetCoreTest]
    public class SerilogPatternLayoutDecorationEnabledTestsNetCore50Tests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCore50>
    {
        public SerilogPatternLayoutDecorationEnabledTestsNetCore50Tests(ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
            : base(fixture, output, LayoutType.Pattern, LoggingFramework.Serilog)
        {
        }
    }

    [NetCoreTest]
    public class SerilogPatternLayoutDecorationEnabledTestsNetCore31Tests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCore31>
    {
        public SerilogPatternLayoutDecorationEnabledTestsNetCore31Tests(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
            : base(fixture, output, LayoutType.Pattern, LoggingFramework.Serilog)
        {
        }
    }

    #endregion

    #endregion

    #region MicrosoftLogging

    #region Json layout

    [NetCoreTest]
    public class MicrosoftLoggingJsonLayoutDecorationEnabledTestsNetCoreLatestTests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public MicrosoftLoggingJsonLayoutDecorationEnabledTestsNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, LayoutType.Json, LoggingFramework.MicrosoftLogging)
        {
        }
    }

    [NetCoreTest]
    public class MicrosoftLoggingJsonLayoutDecorationEnabledTestsNetCore60Tests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCore60>
    {
        public MicrosoftLoggingJsonLayoutDecorationEnabledTestsNetCore60Tests(ConsoleDynamicMethodFixtureCore60 fixture, ITestOutputHelper output)
            : base(fixture, output, LayoutType.Json, LoggingFramework.MicrosoftLogging)
        {
        }
    }

    [NetCoreTest]
    public class MicrosoftLoggingJsonLayoutDecorationEnabledTestsNetCore50Tests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCore50>
    {
        public MicrosoftLoggingJsonLayoutDecorationEnabledTestsNetCore50Tests(ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
            : base(fixture, output, LayoutType.Json, LoggingFramework.MicrosoftLogging)
        {
        }
    }

    [NetCoreTest]
    public class MicrosoftLoggingJsonLayoutDecorationEnabledTestsNetCore31Tests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCore31>
    {
        public MicrosoftLoggingJsonLayoutDecorationEnabledTestsNetCore31Tests(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
            : base(fixture, output, LayoutType.Json, LoggingFramework.MicrosoftLogging)
        {
        }
    }

    [NetFrameworkTest]
    public class MicrosoftLoggingJsonLayoutDecorationEnabledTestsFWLatestTests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public MicrosoftLoggingJsonLayoutDecorationEnabledTestsFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, LayoutType.Json, LoggingFramework.MicrosoftLogging)
        {
        }
    }

    #endregion

    #region Pattern layout

    [NetCoreTest]
    public class MicrosoftLoggingPatternLayoutDecorationEnabledTestsNetCoreLatestTests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public MicrosoftLoggingPatternLayoutDecorationEnabledTestsNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, LayoutType.Pattern, LoggingFramework.MicrosoftLogging)
        {
        }
    }

    [NetCoreTest]
    public class MicrosoftLoggingPatternLayoutDecorationEnabledTestsNetCore60Tests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCore60>
    {
        public MicrosoftLoggingPatternLayoutDecorationEnabledTestsNetCore60Tests(ConsoleDynamicMethodFixtureCore60 fixture, ITestOutputHelper output)
            : base(fixture, output, LayoutType.Pattern, LoggingFramework.MicrosoftLogging)
        {
        }
    }

    [NetCoreTest]
    public class MicrosoftLoggingPatternLayoutDecorationEnabledTestsNetCore50Tests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCore50>
    {
        public MicrosoftLoggingPatternLayoutDecorationEnabledTestsNetCore50Tests(ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
            : base(fixture, output, LayoutType.Pattern, LoggingFramework.MicrosoftLogging)
        {
        }
    }

    [NetCoreTest]
    public class MicrosoftLoggingPatternLayoutDecorationEnabledTestsNetCore31Tests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCore31>
    {
        public MicrosoftLoggingPatternLayoutDecorationEnabledTestsNetCore31Tests(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
            : base(fixture, output, LayoutType.Pattern, LoggingFramework.MicrosoftLogging)
        {
        }
    }

    [NetFrameworkTest]
    public class MicrosoftLoggingPatternLayoutDecorationEnabledTestsFWLatestTests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public MicrosoftLoggingPatternLayoutDecorationEnabledTestsFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, LayoutType.Pattern, LoggingFramework.MicrosoftLogging)
        {
        }
    }

    #endregion

    #endregion

    #region NLog

    #region Json layout

    [NetFrameworkTest]
    public class NLogJsonLayoutDecorationEnabledTestsFWLatestTests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public NLogJsonLayoutDecorationEnabledTestsFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, LayoutType.Json, LoggingFramework.NLog)
        {
        }
    }

    [NetFrameworkTest]
    public class NLogJsonLayoutDecorationEnabledTestsFW471Tests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public NLogJsonLayoutDecorationEnabledTestsFW471Tests(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output, LayoutType.Json, LoggingFramework.NLog)
        {
        }
    }

    [NetFrameworkTest]
    public class NLogJsonLayoutWithParamDecorationEnabledTestsFWLatestTests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public NLogJsonLayoutWithParamDecorationEnabledTestsFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, LayoutType.Json, LoggingFramework.NLog, logWithParam: true)
        {
        }
    }

    [NetFrameworkTest]
    public class NLogJsonLayoutWithParamDecorationEnabledTestsFW471Tests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public NLogJsonLayoutWithParamDecorationEnabledTestsFW471Tests(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output, LayoutType.Json, LoggingFramework.NLog, logWithParam: true)
        {
        }
    }
    [NetCoreTest]
    public class NLogJsonLayoutDecorationEnabledTestsNetCoreLatestTests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public NLogJsonLayoutDecorationEnabledTestsNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, LayoutType.Json, LoggingFramework.NLog)
        {
        }
    }

    [NetCoreTest]
    public class NLogJsonLayoutDecorationEnabledTestsNetCore60Tests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCore60>
    {
        public NLogJsonLayoutDecorationEnabledTestsNetCore60Tests(ConsoleDynamicMethodFixtureCore60 fixture, ITestOutputHelper output)
            : base(fixture, output, LayoutType.Json, LoggingFramework.NLog)
        {
        }
    }

    [NetCoreTest]
    public class NLogJsonLayoutDecorationEnabledTestsNetCore50Tests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCore50>
    {
        public NLogJsonLayoutDecorationEnabledTestsNetCore50Tests(ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
            : base(fixture, output, LayoutType.Json, LoggingFramework.NLog)
        {
        }
    }

    [NetCoreTest]
    public class NLogJsonLayoutDecorationEnabledTestsNetCore31Tests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCore31>
    {
        public NLogJsonLayoutDecorationEnabledTestsNetCore31Tests(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
            : base(fixture, output, LayoutType.Json, LoggingFramework.NLog)
        {
        }
    }

    [NetCoreTest]
    public class NLogJsonLayoutWithParamDecorationEnabledTestsNetCoreLatestTests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public NLogJsonLayoutWithParamDecorationEnabledTestsNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, LayoutType.Json, LoggingFramework.NLog, logWithParam: true)
        {
        }
    }

    [NetCoreTest]
    public class NLogJsonLayoutWithParamDecorationEnabledTestsNetCore31Tests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCore31>
    {
        public NLogJsonLayoutWithParamDecorationEnabledTestsNetCore31Tests(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
            : base(fixture, output, LayoutType.Json, LoggingFramework.NLog, logWithParam: true)
        {
        }
    }

    #endregion

    #region Pattern layout

    [NetFrameworkTest]
    public class NLogPatternLayoutDecorationEnabledTestsFWLatestTests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public NLogPatternLayoutDecorationEnabledTestsFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, LayoutType.Pattern, LoggingFramework.NLog)
        {
        }
    }

    [NetFrameworkTest]
    public class NLogPatternLayoutDecorationEnabledTestsFW471Tests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public NLogPatternLayoutDecorationEnabledTestsFW471Tests(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output, LayoutType.Pattern, LoggingFramework.NLog)
        {
        }
    }

    [NetFrameworkTest]
    public class NLogPatternLayoutWithParamDecorationEnabledTestsFWLatestTests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public NLogPatternLayoutWithParamDecorationEnabledTestsFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, LayoutType.Pattern, LoggingFramework.NLog, logWithParam: true)
        {
        }
    }

    [NetFrameworkTest]
    public class NLogPatternLayoutWithParamDecorationEnabledTestsFW471Tests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public NLogPatternLayoutWithParamDecorationEnabledTestsFW471Tests(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output, LayoutType.Pattern, LoggingFramework.NLog, logWithParam: true)
        {
        }
    }

    [NetCoreTest]
    public class NLogPatternLayoutDecorationEnabledTestsNetCoreLatestTests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public NLogPatternLayoutDecorationEnabledTestsNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, LayoutType.Pattern, LoggingFramework.NLog)
        {
        }
    }

    [NetCoreTest]
    public class NLogPatternLayoutDecorationEnabledTestsNetCore60Tests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCore60>
    {
        public NLogPatternLayoutDecorationEnabledTestsNetCore60Tests(ConsoleDynamicMethodFixtureCore60 fixture, ITestOutputHelper output)
            : base(fixture, output, LayoutType.Pattern, LoggingFramework.NLog)
        {
        }
    }

    [NetCoreTest]
    public class NLogPatternLayoutDecorationEnabledTestsNetCore50Tests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCore50>
    {
        public NLogPatternLayoutDecorationEnabledTestsNetCore50Tests(ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
            : base(fixture, output, LayoutType.Pattern, LoggingFramework.NLog)
        {
        }
    }

    [NetCoreTest]
    public class NLogPatternLayoutDecorationEnabledTestsNetCore31Tests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCore31>
    {
        public NLogPatternLayoutDecorationEnabledTestsNetCore31Tests(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
            : base(fixture, output, LayoutType.Pattern, LoggingFramework.NLog)
        {
        }
    }

    // structured logging...only test latest and oldest
    [NetCoreTest]
    public class NLogPatternLayoutWithParamDecorationEnabledTestsNetCoreLatestTests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public NLogPatternLayoutWithParamDecorationEnabledTestsNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, LayoutType.Pattern, LoggingFramework.NLog, logWithParam: true)
        {
        }
    }

    [NetCoreTest]
    public class NLogPatternLayoutWithParamDecorationEnabledTestsNetCore31Tests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCore31>
    {
        public NLogPatternLayoutWithParamDecorationEnabledTestsNetCore31Tests(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
            : base(fixture, output, LayoutType.Pattern, LoggingFramework.NLog, logWithParam: true)
        {
        }
    }

    #endregion

    #endregion
}

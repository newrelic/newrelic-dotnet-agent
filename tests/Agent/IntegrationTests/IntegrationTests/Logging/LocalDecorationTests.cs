// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
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
        private readonly bool _decorationEnabled;
        private readonly bool _isWebLogTest;
        private const string _primaryApplicationName = "Local Decoration Test App Name";
        private const string _secondaryApplicationName = "Some other testing application name";
        private const string _compositeApplicationName = _primaryApplicationName + ", " + _secondaryApplicationName;
        private const string _testMessage = "DecorateMe";

        public LocalDecorationTestsBase(TFixture fixture, ITestOutputHelper output, bool decorationEnabled, LayoutType layoutType,
            LoggingFramework loggingFramework, bool isWebLogTest = false,  bool logWithParam = false) : base(fixture)
        {
            _decorationEnabled = decorationEnabled;
            _isWebLogTest = isWebLogTest;
            _fixture = fixture;
            _fixture.SetTimeout(TimeSpan.FromMinutes(2));
            _fixture.TestLogger = output;

            _fixture.AddCommand($"LoggingTester SetFramework {loggingFramework} {RandomPortGenerator.NextPort()}");
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
                    .EnableLogDecoration(_decorationEnabled)
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
            var commandResults = Regex.Split(testOutput, Environment.NewLine).Where(l => !l.Contains("EXECUTING"));
            Assert.Contains(_testMessage, string.Join(Environment.NewLine, commandResults));

            // Sample decorated data we are looking for:
            // "NR-LINKING|MjczMDcwfEFQTXxBUFBMSUNBVElPTnwxODQyMg|blah.hsd1.ca.comcast.net|45f120972d61834b96fb890d2a8f97e7|840d9a82e8bc18a8|myApplicationName|"
            // For web logging tests there will be multiple unexpected log lines. To support this, we prefix all log lines in web tests with a known string.
            var regex = new Regex((_isWebLogTest ? "^ThisIsAWebLog.*" : "") + @"NR-LINKING\|([a-zA-Z0-9]*)\|([a-zA-Z0-9._-]*)\|([a-zA-Z0-9]*)\|([a-zA-Z0-9]*)\|(.+?)\|", RegexOptions.Multiline);

            if (_decorationEnabled)
            {
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
            else
            {
                Assert.DoesNotMatch(regex, _fixture.RemoteApplication.CapturedOutput.StandardOutput);
            }
        }
    }

    #region log4net

    #region Json layout, decoration enabled

    [NetFrameworkTest]
    public class Log4netJsonLayoutDecorationEnabledTestsFWLatestTests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public Log4netJsonLayoutDecorationEnabledTestsFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, true, LayoutType.Json, LoggingFramework.Log4net)
        {
        }
    }

    [NetFrameworkTest]
    public class Log4netJsonLayoutDecorationEnabledTestsFW471Tests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public Log4netJsonLayoutDecorationEnabledTestsFW471Tests(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output, true, LayoutType.Json, LoggingFramework.Log4net)
        {
        }
    }

    [NetCoreTest]
    public class Log4netJsonLayoutDecorationEnabledTestsNetCoreLatestTests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public Log4netJsonLayoutDecorationEnabledTestsNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, true, LayoutType.Json, LoggingFramework.Log4net)
        {
        }
    }

    [NetCoreTest]
    public class Log4netJsonLayoutDecorationEnabledTestsNetCoreOldestTests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
    {
        public Log4netJsonLayoutDecorationEnabledTestsNetCoreOldestTests(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
            : base(fixture, output, true, LayoutType.Json, LoggingFramework.Log4net)
        {
        }
    }

    #endregion

    #region Json layout, decoration disabled

    [NetFrameworkTest]
    public class Log4netJsonLayoutDecorationDisabledTestsFWLatestTests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public Log4netJsonLayoutDecorationDisabledTestsFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, false, LayoutType.Json, LoggingFramework.Log4net)
        {
        }
    }

    [NetFrameworkTest]
    public class Log4netJsonLayoutDecorationDisabledTestsFW471Tests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public Log4netJsonLayoutDecorationDisabledTestsFW471Tests(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output, false, LayoutType.Json, LoggingFramework.Log4net)
        {
        }
    }

    [NetCoreTest]
    public class Log4netJsonLayoutDecorationDisabledTestsNetCoreLatestTests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public Log4netJsonLayoutDecorationDisabledTestsNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, false, LayoutType.Json, LoggingFramework.Log4net)
        {
        }
    }

    [NetCoreTest]
    public class Log4netJsonLayoutDecorationDisabledTestsNetCoreOldestTests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
    {
        public Log4netJsonLayoutDecorationDisabledTestsNetCoreOldestTests(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
            : base(fixture, output, false, LayoutType.Json, LoggingFramework.Log4net)
        {
        }
    }

    #endregion

    #region Pattern layout, decoration enabled

    [NetFrameworkTest]
    public class Log4netPatternLayoutDecorationEnabledTestsFWLatestTests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public Log4netPatternLayoutDecorationEnabledTestsFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, true, LayoutType.Pattern, LoggingFramework.Log4net)
        {
        }
    }

    [NetFrameworkTest]
    public class Log4netPatternLayoutDecorationEnabledTestsFW471Tests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public Log4netPatternLayoutDecorationEnabledTestsFW471Tests(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output, true, LayoutType.Pattern, LoggingFramework.Log4net)
        {
        }
    }

    [NetCoreTest]
    public class Log4netPatternLayoutDecorationEnabledTestsNetCoreLatestTests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public Log4netPatternLayoutDecorationEnabledTestsNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, true, LayoutType.Pattern, LoggingFramework.Log4net)
        {
        }
    }

    [NetCoreTest]
    public class Log4netPatternLayoutDecorationEnabledTestsNetCoreOldestTests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
    {
        public Log4netPatternLayoutDecorationEnabledTestsNetCoreOldestTests(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
            : base(fixture, output, true, LayoutType.Pattern, LoggingFramework.Log4net)
        {
        }
    }

    #endregion

    #region Pattern layout, decoration disabled

    [NetFrameworkTest]
    public class Log4netPatternLayoutDecorationDisabledTestsFWLatestTests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public Log4netPatternLayoutDecorationDisabledTestsFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, false, LayoutType.Pattern, LoggingFramework.Log4net)
        {
        }
    }

    [NetFrameworkTest]
    public class Log4netPatternLayoutDecorationDisabledTestsFW471Tests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public Log4netPatternLayoutDecorationDisabledTestsFW471Tests(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output, false, LayoutType.Pattern, LoggingFramework.Log4net)
        {
        }
    }

    [NetCoreTest]
    public class Log4netPatternLayoutDecorationDisabledTestsNetCoreLatestTests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public Log4netPatternLayoutDecorationDisabledTestsNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, false, LayoutType.Pattern, LoggingFramework.Log4net)
        {
        }
    }

    [NetCoreTest]
    public class Log4netPatternLayoutDecorationDisabledTestsNetCoreOldestTests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
    {
        public Log4netPatternLayoutDecorationDisabledTestsNetCoreOldestTests(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
            : base(fixture, output, false, LayoutType.Pattern, LoggingFramework.Log4net)
        {
        }
    }

    #endregion

    #endregion

    #region Serilog

    #region Json layout, decoration enabled
    [NetFrameworkTest]
    public class SerilogJsonLayoutDecorationEnabledTestsFWLatestTests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public SerilogJsonLayoutDecorationEnabledTestsFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, true, LayoutType.Json, LoggingFramework.Serilog)
        {
        }
    }

    [NetFrameworkTest]
    public class SerilogJsonLayoutDecorationEnabledTestsFW471Tests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public SerilogJsonLayoutDecorationEnabledTestsFW471Tests(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output, true, LayoutType.Json, LoggingFramework.Serilog)
        {
        }
    }

    [NetCoreTest]
    public class SerilogJsonLayoutDecorationEnabledTestsNetCoreLatestTests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public SerilogJsonLayoutDecorationEnabledTestsNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, true, LayoutType.Json, LoggingFramework.Serilog)
        {
        }
    }

    [NetCoreTest]
    public class SerilogJsonLayoutDecorationEnabledTestsNetCoreOldestTests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
    {
        public SerilogJsonLayoutDecorationEnabledTestsNetCoreOldestTests(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
            : base(fixture, output, true, LayoutType.Json, LoggingFramework.Serilog)
        {
        }
    }

    #endregion

    #region Json layout, decoration disabled
    [NetFrameworkTest]
    public class SerilogJsonLayoutDecorationDisabledTestsFWLatestTests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public SerilogJsonLayoutDecorationDisabledTestsFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, false, LayoutType.Json, LoggingFramework.Serilog)
        {
        }
    }

    [NetFrameworkTest]
    public class SerilogJsonLayoutDecorationDisabledTestsFW471Tests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public SerilogJsonLayoutDecorationDisabledTestsFW471Tests(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output, false, LayoutType.Json, LoggingFramework.Serilog)
        {
        }
    }

    [NetCoreTest]
    public class SerilogJsonLayoutDecorationDisabledTestsNetCoreLatestTests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public SerilogJsonLayoutDecorationDisabledTestsNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, false, LayoutType.Json, LoggingFramework.Serilog)
        {
        }
    }

    [NetCoreTest]
    public class SerilogJsonLayoutDecorationDisabledTestsNetCoreOldestTests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
    {
        public SerilogJsonLayoutDecorationDisabledTestsNetCoreOldestTests(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
            : base(fixture, output, false, LayoutType.Json, LoggingFramework.Serilog)
        {
        }
    }

    #endregion

    #region Pattern layout, decoration enabled

    [NetFrameworkTest]
    public class SerilogPatternLayoutDecorationEnabledTestsFWLatestTests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public SerilogPatternLayoutDecorationEnabledTestsFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, true, LayoutType.Pattern, LoggingFramework.Serilog)
        {
        }
    }

    [NetFrameworkTest]
    public class SerilogPatternLayoutDecorationEnabledTestsFW471Tests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public SerilogPatternLayoutDecorationEnabledTestsFW471Tests(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output, true, LayoutType.Pattern, LoggingFramework.Serilog)
        {
        }
    }

    [NetCoreTest]
    public class SerilogPatternLayoutDecorationEnabledTestsNetCoreLatestTests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public SerilogPatternLayoutDecorationEnabledTestsNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, true, LayoutType.Pattern, LoggingFramework.Serilog)
        {
        }
    }

    [NetCoreTest]
    public class SerilogWebPatternLayoutDecorationEnabledTestsNetCoreLatestTests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public SerilogWebPatternLayoutDecorationEnabledTestsNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, true, LayoutType.Pattern, LoggingFramework.SerilogWeb, true)
        {
        }
    }

    [NetCoreTest]
    public class SerilogPatternLayoutDecorationEnabledTestsNetCoreOldestTests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
    {
        public SerilogPatternLayoutDecorationEnabledTestsNetCoreOldestTests(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
            : base(fixture, output, true, LayoutType.Pattern, LoggingFramework.Serilog)
        {
        }
    }


    #endregion

    #region Pattern Layout, decoration disabled

    [NetFrameworkTest]
    public class SerilogPatternLayoutDecorationDisabledTestsFWLatestTests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public SerilogPatternLayoutDecorationDisabledTestsFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, false, LayoutType.Pattern, LoggingFramework.Serilog)
        {
        }
    }

    [NetFrameworkTest]
    public class SerilogPatternLayoutDecorationDisabledTestsFW471Tests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public SerilogPatternLayoutDecorationDisabledTestsFW471Tests(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output, false, LayoutType.Pattern, LoggingFramework.Serilog)
        {
        }
    }

    [NetCoreTest]
    public class SerilogPatternLayoutDecorationDisabledTestsNetCoreLatestTests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public SerilogPatternLayoutDecorationDisabledTestsNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, false, LayoutType.Pattern, LoggingFramework.Serilog)
        {
        }
    }

    [NetCoreTest]
    public class SerilogPatternLayoutDecorationDisabledTestsNetCoreOldestTests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
    {
        public SerilogPatternLayoutDecorationDisabledTestsNetCoreOldestTests(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
            : base(fixture, output, false, LayoutType.Pattern, LoggingFramework.Serilog)
        {
        }
    }

    #endregion


    #endregion

    #region MicrosoftLogging

    #region Json layout, decoration enabled

    [NetCoreTest]
    public class MicrosoftLoggingJsonLayoutDecorationEnabledTestsNetCoreLatestTests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public MicrosoftLoggingJsonLayoutDecorationEnabledTestsNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, true, LayoutType.Json, LoggingFramework.MicrosoftLogging)
        {
        }
    }

    [NetCoreTest]
    public class MicrosoftLoggingJsonLayoutDecorationEnabledTestsNetCoreOldestTests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
    {
        public MicrosoftLoggingJsonLayoutDecorationEnabledTestsNetCoreOldestTests(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
            : base(fixture, output, true, LayoutType.Json, LoggingFramework.MicrosoftLogging)
        {
        }
    }

    [NetFrameworkTest]
    public class MicrosoftLoggingJsonLayoutDecorationEnabledTestsFWLatestTests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public MicrosoftLoggingJsonLayoutDecorationEnabledTestsFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, true, LayoutType.Json, LoggingFramework.MicrosoftLogging)
        {
        }
    }

    #endregion

    #region Json layout, decoration disabled

    [NetCoreTest]
    public class MicrosoftLoggingJsonLayoutDecorationDisabledTestsNetCoreLatestTests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public MicrosoftLoggingJsonLayoutDecorationDisabledTestsNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, false, LayoutType.Json, LoggingFramework.MicrosoftLogging)
        {
        }
    }

    [NetCoreTest]
    public class MicrosoftLoggingJsonLayoutDecorationDisabledTestsNetCoreOldestTests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
    {
        public MicrosoftLoggingJsonLayoutDecorationDisabledTestsNetCoreOldestTests(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
            : base(fixture, output, false, LayoutType.Json, LoggingFramework.MicrosoftLogging)
        {
        }
    }

    [NetFrameworkTest]
    public class MicrosoftLoggingJsonLayoutDecorationDisabledTestsFWLatestTests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public MicrosoftLoggingJsonLayoutDecorationDisabledTestsFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, false, LayoutType.Json, LoggingFramework.MicrosoftLogging)
        {
        }
    }

    #endregion

    #region Pattern layout, decoration enabled

    [NetCoreTest]
    public class MicrosoftLoggingPatternLayoutDecorationEnabledTestsNetCoreLatestTests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public MicrosoftLoggingPatternLayoutDecorationEnabledTestsNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, true, LayoutType.Pattern, LoggingFramework.MicrosoftLogging)
        {
        }
    }

    [NetCoreTest]
    public class MicrosoftLoggingPatternLayoutDecorationEnabledTestsNetCoreOldestTests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
    {
        public MicrosoftLoggingPatternLayoutDecorationEnabledTestsNetCoreOldestTests(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
            : base(fixture, output, true, LayoutType.Pattern, LoggingFramework.MicrosoftLogging)
        {
        }
    }

    [NetFrameworkTest]
    public class MicrosoftLoggingPatternLayoutDecorationEnabledTestsFWLatestTests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public MicrosoftLoggingPatternLayoutDecorationEnabledTestsFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, true, LayoutType.Pattern, LoggingFramework.MicrosoftLogging)
        {
        }
    }

    #endregion

    #region Pattern layout, decoration disabled

    [NetCoreTest]
    public class MicrosoftLoggingPatternLayoutDecorationDisabledTestsNetCoreLatestTests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public MicrosoftLoggingPatternLayoutDecorationDisabledTestsNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, false, LayoutType.Pattern, LoggingFramework.MicrosoftLogging)
        {
        }
    }

    [NetCoreTest]
    public class MicrosoftLoggingPatternLayoutDecorationDisabledTestsNetCoreOldestTests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
    {
        public MicrosoftLoggingPatternLayoutDecorationDisabledTestsNetCoreOldestTests(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
            : base(fixture, output, false, LayoutType.Pattern, LoggingFramework.MicrosoftLogging)
        {
        }
    }

    [NetFrameworkTest]
    public class MicrosoftLoggingPatternLayoutDecorationDisabledTestsFWLatestTests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public MicrosoftLoggingPatternLayoutDecorationDisabledTestsFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, false, LayoutType.Pattern, LoggingFramework.MicrosoftLogging)
        {
        }
    }

    #endregion

    #endregion

    #region NLog

    #region Json layout, decoration enabled

    [NetFrameworkTest]
    public class NLogJsonLayoutDecorationEnabledTestsFWLatestTests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public NLogJsonLayoutDecorationEnabledTestsFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, true, LayoutType.Json, LoggingFramework.NLog)
        {
        }
    }

    [NetFrameworkTest]
    public class NLogJsonLayoutDecorationEnabledTestsFW471Tests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public NLogJsonLayoutDecorationEnabledTestsFW471Tests(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output, true, LayoutType.Json, LoggingFramework.NLog)
        {
        }
    }

    [NetFrameworkTest]
    public class NLogJsonLayoutWithParamDecorationEnabledTestsFWLatestTests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public NLogJsonLayoutWithParamDecorationEnabledTestsFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, true, LayoutType.Json, LoggingFramework.NLog, logWithParam: true)
        {
        }
    }

    [NetFrameworkTest]
    public class NLogJsonLayoutWithParamDecorationEnabledTestsFW471Tests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public NLogJsonLayoutWithParamDecorationEnabledTestsFW471Tests(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output, true, LayoutType.Json, LoggingFramework.NLog, logWithParam: true)
        {
        }
    }
    [NetCoreTest]
    public class NLogJsonLayoutDecorationEnabledTestsNetCoreLatestTests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public NLogJsonLayoutDecorationEnabledTestsNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, true, LayoutType.Json, LoggingFramework.NLog)
        {
        }
    }

    [NetCoreTest]
    public class NLogJsonLayoutDecorationEnabledTestsNetCoreOldestTests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
    {
        public NLogJsonLayoutDecorationEnabledTestsNetCoreOldestTests(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
            : base(fixture, output, true, LayoutType.Json, LoggingFramework.NLog)
        {
        }
    }

    [NetCoreTest]
    public class NLogJsonLayoutWithParamDecorationEnabledTestsNetCoreLatestTests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public NLogJsonLayoutWithParamDecorationEnabledTestsNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, true, LayoutType.Json, LoggingFramework.NLog, logWithParam: true)
        {
        }
    }

    [NetCoreTest]
    public class NLogJsonLayoutWithParamDecorationEnabledTestsNetCoreOldestTests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
    {
        public NLogJsonLayoutWithParamDecorationEnabledTestsNetCoreOldestTests(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
            : base(fixture, output, true, LayoutType.Json, LoggingFramework.NLog, logWithParam: true)
        {
        }
    }

    #endregion

    #region Json layout, decoration disabled

    [NetFrameworkTest]
    public class NLogJsonLayoutDecorationDisabledTestsFWLatestTests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public NLogJsonLayoutDecorationDisabledTestsFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, false, LayoutType.Json, LoggingFramework.NLog)
        {
        }
    }

    [NetFrameworkTest]
    public class NLogJsonLayoutDecorationDisabledTestsFW471Tests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public NLogJsonLayoutDecorationDisabledTestsFW471Tests(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output, false, LayoutType.Json, LoggingFramework.NLog)
        {
        }
    }

    [NetCoreTest]
    public class NLogJsonLayoutDecorationDisabledTestsNetCoreLatestTests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public NLogJsonLayoutDecorationDisabledTestsNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, false, LayoutType.Json, LoggingFramework.NLog)
        {
        }
    }

    [NetCoreTest]
    public class NLogJsonLayoutDecorationDisabledTestsNetCoreOldestTests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
    {
        public NLogJsonLayoutDecorationDisabledTestsNetCoreOldestTests(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
            : base(fixture, output, false, LayoutType.Json, LoggingFramework.NLog)
        {
        }
    }

    #endregion

    #region Pattern layout, decoration enabled

    [NetFrameworkTest]
    public class NLogPatternLayoutDecorationEnabledTestsFWLatestTests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public NLogPatternLayoutDecorationEnabledTestsFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, true, LayoutType.Pattern, LoggingFramework.NLog)
        {
        }
    }

    [NetFrameworkTest]
    public class NLogPatternLayoutDecorationEnabledTestsFW471Tests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public NLogPatternLayoutDecorationEnabledTestsFW471Tests(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output, true, LayoutType.Pattern, LoggingFramework.NLog)
        {
        }
    }

    [NetFrameworkTest]
    public class NLogPatternLayoutWithParamDecorationEnabledTestsFWLatestTests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public NLogPatternLayoutWithParamDecorationEnabledTestsFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, true, LayoutType.Pattern, LoggingFramework.NLog, logWithParam: true)
        {
        }
    }

    [NetFrameworkTest]
    public class NLogPatternLayoutWithParamDecorationEnabledTestsFW471Tests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public NLogPatternLayoutWithParamDecorationEnabledTestsFW471Tests(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output, true, LayoutType.Pattern, LoggingFramework.NLog, logWithParam: true)
        {
        }
    }

    [NetCoreTest]
    public class NLogPatternLayoutDecorationEnabledTestsNetCoreLatestTests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public NLogPatternLayoutDecorationEnabledTestsNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, true, LayoutType.Pattern, LoggingFramework.NLog)
        {
        }
    }

    [NetCoreTest]
    public class NLogPatternLayoutDecorationEnabledTestsNetCoreOldestTests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
    {
        public NLogPatternLayoutDecorationEnabledTestsNetCoreOldestTests(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
            : base(fixture, output, true, LayoutType.Pattern, LoggingFramework.NLog)
        {
        }
    }

    // structured logging...only test latest and oldest
    [NetCoreTest]
    public class NLogPatternLayoutWithParamDecorationEnabledTestsNetCoreLatestTests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public NLogPatternLayoutWithParamDecorationEnabledTestsNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, true, LayoutType.Pattern, LoggingFramework.NLog, logWithParam: true)
        {
        }
    }

    [NetCoreTest]
    public class NLogPatternLayoutWithParamDecorationEnabledTestsNetCoreOldestTests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
    {
        public NLogPatternLayoutWithParamDecorationEnabledTestsNetCoreOldestTests(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
            : base(fixture, output, true, LayoutType.Pattern, LoggingFramework.NLog, logWithParam: true)
        {
        }
    }

    #endregion

    #region Pattern layout, decoration disabled

    [NetFrameworkTest]
    public class NLogPatternLayoutDecorationDisabledTestsFWLatestTests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public NLogPatternLayoutDecorationDisabledTestsFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, false, LayoutType.Pattern, LoggingFramework.NLog)
        {
        }
    }

    [NetFrameworkTest]
    public class NLogPatternLayoutDecorationDisabledTestsFW471Tests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public NLogPatternLayoutDecorationDisabledTestsFW471Tests(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output, false, LayoutType.Pattern, LoggingFramework.NLog)
        {
        }
    }

    [NetCoreTest]
    public class NLogPatternLayoutDecorationDisabledTestsNetCoreLatestTests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public NLogPatternLayoutDecorationDisabledTestsNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, false, LayoutType.Pattern, LoggingFramework.NLog)
        {
        }
    }

    [NetCoreTest]
    public class NLogPatternLayoutDecorationDisabledTestsNetCoreOldestTests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
    {
        public NLogPatternLayoutDecorationDisabledTestsNetCoreOldestTests(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
            : base(fixture, output, false, LayoutType.Pattern, LoggingFramework.NLog)
        {
        }
    }

    #endregion

    #endregion
}

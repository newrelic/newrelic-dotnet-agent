// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

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
        private readonly bool _decorationEnabled;
        private const string _primaryApplicationName = "Local Decoration Test App Name";
        private const string _secondaryApplicationName = "Some other testing application name";
        private const string _compositeApplicationName = _primaryApplicationName + ", " + _secondaryApplicationName;
        private const string _testMessage = "DecorateMe";

        public LocalDecorationTestsBase(TFixture fixture, ITestOutputHelper output, bool decorationEnabled, LayoutType layoutType,
            LoggingFramework loggingFramework, bool useStructuredLogging = false) : base(fixture)
        {
            _decorationEnabled = decorationEnabled;
            _fixture = fixture;
            _fixture.SetTimeout(System.TimeSpan.FromMinutes(2));
            _fixture.TestLogger = output;

            _fixture.AddCommand($"LoggingTester SetFramework {loggingFramework}");
            _fixture.AddCommand($"LoggingTester Configure{layoutType}LayoutAppenderForDecoration");
            if (useStructuredLogging)
            {
                _fixture.AddCommand($"LoggingTester CreateSingleLogMessageInTransactionWithParam {_testMessage}{"{@param}"}");
            }
            else
            {
                _fixture.AddCommand($"LoggingTester CreateSingleLogMessageInTransaction {_testMessage} DEBUG");
            }

            _fixture.RemoteApplication.AppName = _compositeApplicationName;

            _fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var configModifier = new NewRelicConfigModifier(fixture.DestinationNewRelicConfigFilePath);

                    configModifier
                    .EnableLogDecoration(_decorationEnabled)
                    .EnableDistributedTrace()
                    .SetLogLevel("debug");
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
            var regex = new Regex(@"NR-LINKING\|([a-zA-Z0-9]*)\|([a-zA-Z0-9._-]*)\|([a-zA-Z0-9]*)\|([a-zA-Z0-9]*)\|(.+?)\|");
            if (_decorationEnabled)
            {
                // Make sure the added metadata is there
                var match = regex.Match(testOutput);
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
    public class Log4netJsonLayoutDecorationEnabledTestsNetCore60Tests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCore60>
    {
        public Log4netJsonLayoutDecorationEnabledTestsNetCore60Tests(ConsoleDynamicMethodFixtureCore60 fixture, ITestOutputHelper output)
            : base(fixture, output, true, LayoutType.Json, LoggingFramework.Log4net)
        {
        }
    }

    [NetCoreTest]
    public class Log4netJsonLayoutDecorationEnabledTestsNetCore50Tests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCore50>
    {
        public Log4netJsonLayoutDecorationEnabledTestsNetCore50Tests(ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
            : base(fixture, output, true, LayoutType.Json, LoggingFramework.Log4net)
        {
        }
    }

    [NetCoreTest]
    public class Log4netJsonLayoutDecorationEnabledTestsNetCore31Tests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCore31>
    {
        public Log4netJsonLayoutDecorationEnabledTestsNetCore31Tests(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
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
    public class Log4netJsonLayoutDecorationDisabledTestsNetCore60Tests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCore60>
    {
        public Log4netJsonLayoutDecorationDisabledTestsNetCore60Tests(ConsoleDynamicMethodFixtureCore60 fixture, ITestOutputHelper output)
            : base(fixture, output, false, LayoutType.Json, LoggingFramework.Log4net)
        {
        }
    }

    [NetCoreTest]
    public class Log4netJsonLayoutDecorationDisabledTestsNetCore50Tests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCore50>
    {
        public Log4netJsonLayoutDecorationDisabledTestsNetCore50Tests(ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
            : base(fixture, output, false, LayoutType.Json, LoggingFramework.Log4net)
        {
        }
    }

    [NetCoreTest]
    public class Log4netJsonLayoutDecorationDisabledTestsNetCore31Tests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCore31>
    {
        public Log4netJsonLayoutDecorationDisabledTestsNetCore31Tests(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
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
    public class Log4netPatternLayoutDecorationEnabledTestsNetCore60Tests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCore60>
    {
        public Log4netPatternLayoutDecorationEnabledTestsNetCore60Tests(ConsoleDynamicMethodFixtureCore60 fixture, ITestOutputHelper output)
            : base(fixture, output, true, LayoutType.Pattern, LoggingFramework.Log4net)
        {
        }
    }

    [NetCoreTest]
    public class Log4netPatternLayoutDecorationEnabledTestsNetCore50Tests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCore50>
    {
        public Log4netPatternLayoutDecorationEnabledTestsNetCore50Tests(ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
            : base(fixture, output, true, LayoutType.Pattern, LoggingFramework.Log4net)
        {
        }
    }

    [NetCoreTest]
    public class Log4netPatternLayoutDecorationEnabledTestsNetCore31Tests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCore31>
    {
        public Log4netPatternLayoutDecorationEnabledTestsNetCore31Tests(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
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
    public class Log4netPatternLayoutDecorationDisabledTestsNetCore60Tests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCore60>
    {
        public Log4netPatternLayoutDecorationDisabledTestsNetCore60Tests(ConsoleDynamicMethodFixtureCore60 fixture, ITestOutputHelper output)
            : base(fixture, output, false, LayoutType.Pattern, LoggingFramework.Log4net)
        {
        }
    }

    [NetCoreTest]
    public class Log4netPatternLayoutDecorationDisabledTestsNetCore50Tests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCore50>
    {
        public Log4netPatternLayoutDecorationDisabledTestsNetCore50Tests(ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
            : base(fixture, output, false, LayoutType.Pattern, LoggingFramework.Log4net)
        {
        }
    }

    [NetCoreTest]
    public class Log4netPatternLayoutDecorationDisabledTestsNetCore31Tests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCore31>
    {
        public Log4netPatternLayoutDecorationDisabledTestsNetCore31Tests(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
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
    public class SerilogJsonLayoutDecorationEnabledTestsNetCore60Tests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCore60>
    {
        public SerilogJsonLayoutDecorationEnabledTestsNetCore60Tests(ConsoleDynamicMethodFixtureCore60 fixture, ITestOutputHelper output)
            : base(fixture, output, true, LayoutType.Json, LoggingFramework.Serilog)
        {
        }
    }

    [NetCoreTest]
    public class SerilogJsonLayoutDecorationEnabledTestsNetCore50Tests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCore50>
    {
        public SerilogJsonLayoutDecorationEnabledTestsNetCore50Tests(ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
            : base(fixture, output, true, LayoutType.Json, LoggingFramework.Serilog)
        {
        }
    }

    [NetCoreTest]
    public class SerilogJsonLayoutDecorationEnabledTestsNetCore31Tests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCore31>
    {
        public SerilogJsonLayoutDecorationEnabledTestsNetCore31Tests(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
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
    public class SerilogJsonLayoutDecorationDisabledTestsNetCore60Tests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCore60>
    {
        public SerilogJsonLayoutDecorationDisabledTestsNetCore60Tests(ConsoleDynamicMethodFixtureCore60 fixture, ITestOutputHelper output)
            : base(fixture, output, false, LayoutType.Json, LoggingFramework.Serilog)
        {
        }
    }

    [NetCoreTest]
    public class SerilogJsonLayoutDecorationDisabledTestsNetCore50Tests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCore50>
    {
        public SerilogJsonLayoutDecorationDisabledTestsNetCore50Tests(ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
            : base(fixture, output, false, LayoutType.Json, LoggingFramework.Serilog)
        {
        }
    }

    [NetCoreTest]
    public class SerilogJsonLayoutDecorationDisabledTestsNetCore31Tests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCore31>
    {
        public SerilogJsonLayoutDecorationDisabledTestsNetCore31Tests(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
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
    public class SerilogPatternLayoutDecorationEnabledTestsNetCore60Tests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCore60>
    {
        public SerilogPatternLayoutDecorationEnabledTestsNetCore60Tests(ConsoleDynamicMethodFixtureCore60 fixture, ITestOutputHelper output)
            : base(fixture, output, true, LayoutType.Pattern, LoggingFramework.Serilog)
        {
        }
    }

    [NetCoreTest]
    public class SerilogPatternLayoutDecorationEnabledTestsNetCore50Tests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCore50>
    {
        public SerilogPatternLayoutDecorationEnabledTestsNetCore50Tests(ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
            : base(fixture, output, true, LayoutType.Pattern, LoggingFramework.Serilog)
        {
        }
    }

    [NetCoreTest]
    public class SerilogPatternLayoutDecorationEnabledTestsNetCore31Tests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCore31>
    {
        public SerilogPatternLayoutDecorationEnabledTestsNetCore31Tests(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
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
    public class SerilogPatternLayoutDecorationDisabledTestsNetCore60Tests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCore60>
    {
        public SerilogPatternLayoutDecorationDisabledTestsNetCore60Tests(ConsoleDynamicMethodFixtureCore60 fixture, ITestOutputHelper output)
            : base(fixture, output, false, LayoutType.Pattern, LoggingFramework.Serilog)
        {
        }
    }

    [NetCoreTest]
    public class SerilogPatternLayoutDecorationDisabledTestsNetCore50Tests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCore50>
    {
        public SerilogPatternLayoutDecorationDisabledTestsNetCore50Tests(ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
            : base(fixture, output, false, LayoutType.Pattern, LoggingFramework.Serilog)
        {
        }
    }

    [NetCoreTest]
    public class SerilogPatternLayoutDecorationDisabledTestsNetCore31Tests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCore31>
    {
        public SerilogPatternLayoutDecorationDisabledTestsNetCore31Tests(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
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
    public class MicrosoftLoggingJsonLayoutDecorationEnabledTestsNetCore60Tests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCore60>
    {
        public MicrosoftLoggingJsonLayoutDecorationEnabledTestsNetCore60Tests(ConsoleDynamicMethodFixtureCore60 fixture, ITestOutputHelper output)
            : base(fixture, output, true, LayoutType.Json, LoggingFramework.MicrosoftLogging)
        {
        }
    }

    [NetCoreTest]
    public class MicrosoftLoggingJsonLayoutDecorationEnabledTestsNetCore50Tests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCore50>
    {
        public MicrosoftLoggingJsonLayoutDecorationEnabledTestsNetCore50Tests(ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
            : base(fixture, output, true, LayoutType.Json, LoggingFramework.MicrosoftLogging)
        {
        }
    }

    [NetCoreTest]
    public class MicrosoftLoggingJsonLayoutDecorationEnabledTestsNetCore31Tests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCore31>
    {
        public MicrosoftLoggingJsonLayoutDecorationEnabledTestsNetCore31Tests(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
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
    public class MicrosoftLoggingJsonLayoutDecorationDisabledTestsNetCore60Tests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCore60>
    {
        public MicrosoftLoggingJsonLayoutDecorationDisabledTestsNetCore60Tests(ConsoleDynamicMethodFixtureCore60 fixture, ITestOutputHelper output)
            : base(fixture, output, false, LayoutType.Json, LoggingFramework.MicrosoftLogging)
        {
        }
    }

    [NetCoreTest]
    public class MicrosoftLoggingJsonLayoutDecorationDisabledTestsNetCore50Tests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCore50>
    {
        public MicrosoftLoggingJsonLayoutDecorationDisabledTestsNetCore50Tests(ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
            : base(fixture, output, false, LayoutType.Json, LoggingFramework.MicrosoftLogging)
        {
        }
    }

    [NetCoreTest]
    public class MicrosoftLoggingJsonLayoutDecorationDisabledTestsNetCore31Tests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCore31>
    {
        public MicrosoftLoggingJsonLayoutDecorationDisabledTestsNetCore31Tests(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
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
    public class MicrosoftLoggingPatternLayoutDecorationEnabledTestsNetCore60Tests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCore60>
    {
        public MicrosoftLoggingPatternLayoutDecorationEnabledTestsNetCore60Tests(ConsoleDynamicMethodFixtureCore60 fixture, ITestOutputHelper output)
            : base(fixture, output, true, LayoutType.Pattern, LoggingFramework.MicrosoftLogging)
        {
        }
    }

    [NetCoreTest]
    public class MicrosoftLoggingPatternLayoutDecorationEnabledTestsNetCore50Tests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCore50>
    {
        public MicrosoftLoggingPatternLayoutDecorationEnabledTestsNetCore50Tests(ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
            : base(fixture, output, true, LayoutType.Pattern, LoggingFramework.MicrosoftLogging)
        {
        }
    }

    [NetCoreTest]
    public class MicrosoftLoggingPatternLayoutDecorationEnabledTestsNetCore31Tests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCore31>
    {
        public MicrosoftLoggingPatternLayoutDecorationEnabledTestsNetCore31Tests(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
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
    public class MicrosoftLoggingPatternLayoutDecorationDisabledTestsNetCore60Tests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCore60>
    {
        public MicrosoftLoggingPatternLayoutDecorationDisabledTestsNetCore60Tests(ConsoleDynamicMethodFixtureCore60 fixture, ITestOutputHelper output)
            : base(fixture, output, false, LayoutType.Pattern, LoggingFramework.MicrosoftLogging)
        {
        }
    }

    [NetCoreTest]
    public class MicrosoftLoggingPatternLayoutDecorationDisabledTestsNetCore50Tests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCore50>
    {
        public MicrosoftLoggingPatternLayoutDecorationDisabledTestsNetCore50Tests(ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
            : base(fixture, output, false, LayoutType.Pattern, LoggingFramework.MicrosoftLogging)
        {
        }
    }

    [NetCoreTest]
    public class MicrosoftLoggingPatternLayoutDecorationDisabledTestsNetCore31Tests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCore31>
    {
        public MicrosoftLoggingPatternLayoutDecorationDisabledTestsNetCore31Tests(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
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

    [NetCoreTest]
    public class NLogJsonLayoutDecorationEnabledTestsNetCoreLatestTests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public NLogJsonLayoutDecorationEnabledTestsNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, true, LayoutType.Json, LoggingFramework.NLog)
        {
        }
    }

    [NetCoreTest]
    public class NLogJsonLayoutDecorationEnabledTestsNetCore60Tests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCore60>
    {
        public NLogJsonLayoutDecorationEnabledTestsNetCore60Tests(ConsoleDynamicMethodFixtureCore60 fixture, ITestOutputHelper output)
            : base(fixture, output, true, LayoutType.Json, LoggingFramework.NLog)
        {
        }
    }

    [NetCoreTest]
    public class NLogJsonLayoutDecorationEnabledTestsNetCore50Tests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCore50>
    {
        public NLogJsonLayoutDecorationEnabledTestsNetCore50Tests(ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
            : base(fixture, output, true, LayoutType.Json, LoggingFramework.NLog)
        {
        }
    }

    [NetCoreTest]
    public class NLogJsonLayoutDecorationEnabledTestsNetCore31Tests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCore31>
    {
        public NLogJsonLayoutDecorationEnabledTestsNetCore31Tests(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
            : base(fixture, output, true, LayoutType.Json, LoggingFramework.NLog)
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
    public class NLogJsonLayoutDecorationDisabledTestsNetCore60Tests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCore60>
    {
        public NLogJsonLayoutDecorationDisabledTestsNetCore60Tests(ConsoleDynamicMethodFixtureCore60 fixture, ITestOutputHelper output)
            : base(fixture, output, false, LayoutType.Json, LoggingFramework.NLog)
        {
        }
    }

    [NetCoreTest]
    public class NLogJsonLayoutDecorationDisabledTestsNetCore50Tests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCore50>
    {
        public NLogJsonLayoutDecorationDisabledTestsNetCore50Tests(ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
            : base(fixture, output, false, LayoutType.Json, LoggingFramework.NLog)
        {
        }
    }

    [NetCoreTest]
    public class NLogJsonLayoutDecorationDisabledTestsNetCore31Tests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCore31>
    {
        public NLogJsonLayoutDecorationDisabledTestsNetCore31Tests(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
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

    [NetCoreTest]
    public class NLogPatternLayoutDecorationEnabledTestsNetCoreLatestTests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public NLogPatternLayoutDecorationEnabledTestsNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, true, LayoutType.Pattern, LoggingFramework.NLog)
        {
        }
    }

    [NetCoreTest]
    public class NLogPatternLayoutStructuredLoggingDecorationEnabledTestsNetCoreLatestTests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public NLogPatternLayoutStructuredLoggingDecorationEnabledTestsNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, true, LayoutType.Pattern, LoggingFramework.NLog, useStructuredLogging: true)
        {
        }
    }

    [NetCoreTest]
    public class NLogPatternLayoutDecorationEnabledTestsNetCore60Tests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCore60>
    {
        public NLogPatternLayoutDecorationEnabledTestsNetCore60Tests(ConsoleDynamicMethodFixtureCore60 fixture, ITestOutputHelper output)
            : base(fixture, output, true, LayoutType.Pattern, LoggingFramework.NLog)
        {
        }
    }

    [NetCoreTest]
    public class NLogPatternLayoutDecorationEnabledTestsNetCore50Tests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCore50>
    {
        public NLogPatternLayoutDecorationEnabledTestsNetCore50Tests(ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
            : base(fixture, output, true, LayoutType.Pattern, LoggingFramework.NLog)
        {
        }
    }

    [NetCoreTest]
    public class NLogPatternLayoutDecorationEnabledTestsNetCore31Tests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCore31>
    {
        public NLogPatternLayoutDecorationEnabledTestsNetCore31Tests(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
            : base(fixture, output, true, LayoutType.Pattern, LoggingFramework.NLog)
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
    public class NLogPatternLayoutDecorationDisabledTestsNetCore60Tests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCore60>
    {
        public NLogPatternLayoutDecorationDisabledTestsNetCore60Tests(ConsoleDynamicMethodFixtureCore60 fixture, ITestOutputHelper output)
            : base(fixture, output, false, LayoutType.Pattern, LoggingFramework.NLog)
        {
        }
    }

    [NetCoreTest]
    public class NLogPatternLayoutDecorationDisabledTestsNetCore50Tests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCore50>
    {
        public NLogPatternLayoutDecorationDisabledTestsNetCore50Tests(ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
            : base(fixture, output, false, LayoutType.Pattern, LoggingFramework.NLog)
        {
        }
    }

    [NetCoreTest]
    public class NLogPatternLayoutDecorationDisabledTestsNetCore31Tests : LocalDecorationTestsBase<ConsoleDynamicMethodFixtureCore31>
    {
        public NLogPatternLayoutDecorationDisabledTestsNetCore31Tests(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
            : base(fixture, output, false, LayoutType.Pattern, LoggingFramework.NLog)
        {
        }
    }

    #endregion

    #endregion
}

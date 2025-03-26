// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using Xunit;


namespace NewRelic.Agent.IntegrationTests.Logging.ContextData
{
    public abstract class ContextDataTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
        where TFixture : ConsoleDynamicMethodFixture
    {
        private readonly TFixture _fixture;
        private List<LoggingFramework> _loggingFrameworks;
        private readonly bool _testNestedContexts;

        private const string InfoMessage = "HelloWorld";

        // There are several entries in this dictionary to allow for different methods of adding the values in the test adapter

        private Dictionary<string, string> GetExpectedAttributes(LoggingFramework framework) => new Dictionary<string, string>()
        {
            { "framework", framework.ToString() },
            { "mycontext1", "foo" },
            { "mycontext2", "bar" },
            { "mycontext3", "test" },
            { "mycontext4", "value" },
        };

        private string FlattenExpectedAttributes(Dictionary<string, string> attributes) => string.Join(",", attributes.Select(x => x.Key + "=" + x.Value).ToArray());

        public ContextDataTestsBase(TFixture fixture, ITestOutputHelper output, bool testNestedContexts, params LoggingFramework[] loggingFrameworks) : base(fixture)
        {
            _fixture = fixture;
            _loggingFrameworks = loggingFrameworks.ToList();
            _testNestedContexts = testNestedContexts;
            _fixture.SetTimeout(TimeSpan.FromMinutes(2));
            _fixture.TestLogger = output;

            _loggingFrameworks.ForEach(x => _fixture.AddCommand($"LoggingTester SetFramework {x} {RandomPortGenerator.NextPort()}"));
            _fixture.AddCommand($"LoggingTester Configure");

            _loggingFrameworks.ForEach(x => _fixture.AddCommand($"LoggingTester CreateSingleLogMessage {x} {InfoMessage} INFO {FlattenExpectedAttributes(GetExpectedAttributes(x))}"));

            if (_testNestedContexts) // on supported frameworks, ensure that we don't blow up when accumulating the context key/value pairs
                _fixture.AddCommand($"LoggingTester LogMessageInNestedScopes");

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
            List<Assertions.ExpectedLogLine> expectedLogLines = new List<Assertions.ExpectedLogLine>();
            _loggingFrameworks.ForEach(x => expectedLogLines.Add(
                new Assertions.ExpectedLogLine
                {
                    Level = LogUtils.GetLevelName(x, "INFO"),
                    LogMessage = InfoMessage,
                    Attributes = GetExpectedAttributes(x)
                }
                ));

            var logLines = _fixture.AgentLog.GetLogEventDataLogLines().ToArray();

            // different versions of log4net add varying amounts of default attributes, we ignore the counts because of this
            Assertions.LogLinesExist(expectedLogLines, logLines, ignoreAttributeCount: true);

            if (_testNestedContexts)
            {
                List<Assertions.ExpectedLogLine> contextExpectedLogLines = new List<Assertions.ExpectedLogLine>();
                _loggingFrameworks.ForEach(x =>
                {
                    var outerContextExpectedAttributes = new Dictionary<string, string>();
                    foreach (var kvp in GetExpectedAttributes(x))
                        outerContextExpectedAttributes.Add(kvp.Key, kvp.Value);
                    outerContextExpectedAttributes.Add("ScopeKey1", "scopeValue1");

                    var innerContextExpectedAttributes = new Dictionary<string, string>();
                    foreach (var kvp in GetExpectedAttributes(x))
                        innerContextExpectedAttributes.Add(kvp.Key, kvp.Value);
                    innerContextExpectedAttributes.Add("ScopeKey1", "scopeValue2");


                    contextExpectedLogLines.Add(new Assertions.ExpectedLogLine
                    {
                        Level = LogUtils.GetLevelName(x, "INFO"),
                        LogMessage = "Outer Scope",
                        Attributes = outerContextExpectedAttributes
                    });
                    contextExpectedLogLines.Add(new Assertions.ExpectedLogLine
                    {
                        Level = LogUtils.GetLevelName(x, "INFO"),
                        LogMessage = "Inner Scope",
                        Attributes = innerContextExpectedAttributes
                    });
                });

                Assertions.LogLinesExist(contextExpectedLogLines, logLines, ignoreAttributeCount: true);
            }
        }
    }

    #region log4net

    [NetFrameworkTest]
    public class Log4NetContextDataFWLatestTests : ContextDataTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public Log4NetContextDataFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, false, LoggingFramework.Log4net)
        {
        }
    }

    [NetFrameworkTest]
    public class Log4NetContextDataFW471Tests : ContextDataTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public Log4NetContextDataFW471Tests(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output, false, LoggingFramework.Log4net)
        {
        }
    }

    [NetFrameworkTest]
    public class Log4NetContextDataFW462Tests : ContextDataTestsBase<ConsoleDynamicMethodFixtureFW462>
    {
        public Log4NetContextDataFW462Tests(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
            : base(fixture, output, false, LoggingFramework.Log4net)
        {
        }
    }

    [NetCoreTest]
    public class Log4NetContextDataNetCoreLatestTests : ContextDataTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public Log4NetContextDataNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, false, LoggingFramework.Log4net)
        {
        }
    }

    [NetCoreTest]
    public class Log4NetContextDataNetCoreOldestTests : ContextDataTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
    {
        public Log4NetContextDataNetCoreOldestTests(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
            : base(fixture, output, false, LoggingFramework.Log4net)
        {
        }
    }

    #endregion

    #region NLog

    [NetFrameworkTest]
    public class NLogContextDataFWLatestTests : ContextDataTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public NLogContextDataFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, false, LoggingFramework.NLog)
        {
        }
    }

    [NetFrameworkTest]
    public class NLogContextDataFW471Tests : ContextDataTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public NLogContextDataFW471Tests(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output, false, LoggingFramework.NLog)
        {
        }
    }

    [NetFrameworkTest]
    public class NLogContextDataFW462Tests : ContextDataTestsBase<ConsoleDynamicMethodFixtureFW462>
    {
        public NLogContextDataFW462Tests(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
            : base(fixture, output, false, LoggingFramework.NLog)
        {
        }
    }

    [NetCoreTest]
    public class NLogContextDataNetCoreLatestTests : ContextDataTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public NLogContextDataNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, false, LoggingFramework.NLog)
        {
        }
    }

    [NetCoreTest]
    public class NLogContextDataNetCoreOldestTests : ContextDataTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
    {
        public NLogContextDataNetCoreOldestTests(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
            : base(fixture, output, false, LoggingFramework.NLog)
        {
        }
    }

    #endregion

    #region Serilog

    [NetFrameworkTest]
    public class SerilogContextDataFWLatestTests : ContextDataTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public SerilogContextDataFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, false, LoggingFramework.Serilog)
        {
        }
    }

    [NetFrameworkTest]
    public class SerilogContextDataFW471Tests : ContextDataTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public SerilogContextDataFW471Tests(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output, false, LoggingFramework.Serilog)
        {
        }
    }

    [NetFrameworkTest]
    public class SerilogContextDataFW462Tests : ContextDataTestsBase<ConsoleDynamicMethodFixtureFW462>
    {
        public SerilogContextDataFW462Tests(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
            : base(fixture, output, false, LoggingFramework.Serilog)
        {
        }
    }

    [NetCoreTest]
    public class SerilogContextDataNetCoreLatestTests : ContextDataTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public SerilogContextDataNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, false, LoggingFramework.Serilog)
        {
        }
    }

    [NetCoreTest]
    public class SerilogContextDataNetCoreOldestTests : ContextDataTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
    {
        public SerilogContextDataNetCoreOldestTests(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
            : base(fixture, output, false, LoggingFramework.Serilog)
        {
        }
    }

    #endregion

    #region MEL

    [NetFrameworkTest]
    public class MELContextDataFWLatestTests : ContextDataTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public MELContextDataFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, true, LoggingFramework.MicrosoftLogging)
        {
        }
    }

    [NetCoreTest]
    public class MELContextDataNetCoreLatestTests : ContextDataTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public MELContextDataNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, true, LoggingFramework.MicrosoftLogging)
        {
        }
    }

    [NetCoreTest]
    public class MELContextDataNetCoreOldestTests : ContextDataTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
    {
        public MELContextDataNetCoreOldestTests(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
            : base(fixture, output, true, LoggingFramework.MicrosoftLogging)
        {
        }
    }

    #endregion

    #region Sitecore
    public class SitecoreContextDataFWLatestTests : ContextDataTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public SitecoreContextDataFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, false, LoggingFramework.Sitecore)
        {
        }
    }

    public class SitecoreContextDataFW48Tests : ContextDataTestsBase<ConsoleDynamicMethodFixtureFW48>
    {
        public SitecoreContextDataFW48Tests(ConsoleDynamicMethodFixtureFW48 fixture, ITestOutputHelper output)
            : base(fixture, output, false, LoggingFramework.Sitecore)
        {
        }
    }

    public class SitecorePlusLog4NetContextDataFWLatestTests : ContextDataTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public SitecorePlusLog4NetContextDataFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, false, LoggingFramework.Sitecore, LoggingFramework.Log4net)
        {
        }
    }

    public class SitecorePlusLog4NetContextDataFW48Tests : ContextDataTestsBase<ConsoleDynamicMethodFixtureFW48>
    {
        public SitecorePlusLog4NetContextDataFW48Tests(ConsoleDynamicMethodFixtureFW48 fixture, ITestOutputHelper output)
            : base(fixture, output, false, LoggingFramework.Sitecore, LoggingFramework.Log4net)
        {
        }
    }


    #endregion // Sitecore

    #region SEL
    public class SELContextDataFWLatestTests : ContextDataTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public SELContextDataFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, true, LoggingFramework.SerilogEL)
        {
        }
    }

    public class SELContextDataFW48Tests : ContextDataTestsBase<ConsoleDynamicMethodFixtureFW48>
    {
        public SELContextDataFW48Tests(ConsoleDynamicMethodFixtureFW48 fixture, ITestOutputHelper output)
            : base(fixture, output, true, LoggingFramework.SerilogEL)
        {
        }
    }

    public class SELContextDataCoreLatestTests : ContextDataTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public SELContextDataCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, true, LoggingFramework.SerilogEL)
        {
        }
    }

    public class SELContextDataCoreOldestTests : ContextDataTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
    {
        public SELContextDataCoreOldestTests(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
            : base(fixture, output, true, LoggingFramework.SerilogEL)
        {
        }
    }

    #endregion // SEL

    #region NEL
    public class NELContextDataFWLatestTests : ContextDataTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public NELContextDataFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, true, LoggingFramework.NLogEL)
        {
        }
    }

    public class NELContextDataCoreLatestTests : ContextDataTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public NELContextDataCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, true, LoggingFramework.NLogEL)
        {
        }
    }

    #endregion // NEL
}

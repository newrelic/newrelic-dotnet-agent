// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.Logging.ContextData
{
    public abstract class ContextDataTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
        where TFixture : ConsoleDynamicMethodFixture
    {
        private readonly TFixture _fixture;
        private LoggingFramework _loggingFramework;
        private readonly bool _testNestedContexts;

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


        public ContextDataTestsBase(TFixture fixture, ITestOutputHelper output, LoggingFramework loggingFramework, bool testNestedContexts) : base(fixture)
        {
            _fixture = fixture;
            _loggingFramework = loggingFramework;
            _testNestedContexts = testNestedContexts;
            _fixture.SetTimeout(TimeSpan.FromMinutes(2));
            _fixture.TestLogger = output;

            _fixture.AddCommand($"LoggingTester SetFramework {_loggingFramework} {RandomPortGenerator.NextPort()}");
            _fixture.AddCommand($"LoggingTester Configure");

            string context = string.Join(",", _expectedAttributes.Select(x => x.Key + "=" + x.Value).ToArray());

            _fixture.AddCommand($"LoggingTester CreateSingleLogMessage {InfoMessage} INFO {context}");

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

            // different versions of log4net add varying amounts of default attributes, we ignore the counts because of this
            Assertions.LogLinesExist(expectedLogLines, logLines, ignoreAttributeCount: true);

            if (_testNestedContexts)
            {
                var outerContextExpectedAttributes = new Dictionary<string, string>();
                foreach(var kvp in _expectedAttributes)
                    outerContextExpectedAttributes.Add(kvp.Key, kvp.Value);
                outerContextExpectedAttributes.Add("ScopeKey1", "scopeValue1");

                var innerContextExpectedAttributes = new Dictionary<string, string>();
                foreach(var kvp in _expectedAttributes)
                    innerContextExpectedAttributes.Add(kvp.Key, kvp.Value);
                innerContextExpectedAttributes.Add("ScopeKey1", "scopeValue2");

                var contextExpectedLogLines = new []
                {
                    new Assertions.ExpectedLogLine
                    {
                        Level = LogUtils.GetLevelName(_loggingFramework, "INFO"),
                        LogMessage = "Outer Scope",
                        Attributes = outerContextExpectedAttributes
                    },
                    new Assertions.ExpectedLogLine
                    {
                        Level = LogUtils.GetLevelName(_loggingFramework, "INFO"),
                        LogMessage = "Inner Scope",
                        Attributes = innerContextExpectedAttributes
                    }
                };

                Assertions.LogLinesExist(contextExpectedLogLines, logLines, ignoreAttributeCount: true);
            }
        }
    }

    #region log4net

    [NetFrameworkTest]
    public class Log4NetContextDataFWLatestTests : ContextDataTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public Log4NetContextDataFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Log4net, false)
        {
        }
    }

    [NetFrameworkTest]
    public class Log4NetContextDataFW471Tests : ContextDataTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public Log4NetContextDataFW471Tests(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Log4net, false)
        {
        }
    }

    [NetFrameworkTest]
    public class Log4NetContextDataFW462Tests : ContextDataTestsBase<ConsoleDynamicMethodFixtureFW462>
    {
        public Log4NetContextDataFW462Tests(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Log4net, false)
        {
        }
    }

    [NetCoreTest]
    public class Log4NetContextDataNetCoreLatestTests : ContextDataTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public Log4NetContextDataNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Log4net, false)
        {
        }
    }

    [NetCoreTest]
    public class Log4NetContextDataNetCoreOldestTests : ContextDataTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
    {
        public Log4NetContextDataNetCoreOldestTests(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Log4net, false)
        {
        }
    }

    #endregion

    #region NLog

    [NetFrameworkTest]
    public class NLogContextDataFWLatestTests : ContextDataTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public NLogContextDataFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.NLog, false)
        {
        }
    }

    [NetFrameworkTest]
    public class NLogContextDataFW471Tests : ContextDataTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public NLogContextDataFW471Tests(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.NLog, false)
        {
        }
    }

    [NetFrameworkTest]
    public class NLogContextDataFW462Tests : ContextDataTestsBase<ConsoleDynamicMethodFixtureFW462>
    {
        public NLogContextDataFW462Tests(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.NLog, false)
        {
        }
    }

    [NetCoreTest]
    public class NLogContextDataNetCoreLatestTests : ContextDataTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public NLogContextDataNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.NLog, false)
        {
        }
    }

    [NetCoreTest]
    public class NLogContextDataNetCoreOldestTests : ContextDataTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
    {
        public NLogContextDataNetCoreOldestTests(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.NLog, false)
        {
        }
    }

    #endregion

    #region Serilog

    [NetFrameworkTest]
    public class SerilogContextDataFWLatestTests : ContextDataTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public SerilogContextDataFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Serilog, false)
        {
        }
    }

    [NetFrameworkTest]
    public class SerilogContextDataFW471Tests : ContextDataTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public SerilogContextDataFW471Tests(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Serilog, false)
        {
        }
    }

    [NetFrameworkTest]
    public class SerilogContextDataFW462Tests : ContextDataTestsBase<ConsoleDynamicMethodFixtureFW462>
    {
        public SerilogContextDataFW462Tests(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Serilog, false)
        {
        }
    }

    [NetCoreTest]
    public class SerilogContextDataNetCoreLatestTests : ContextDataTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public SerilogContextDataNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Serilog, false)
        {
        }
    }

    [NetCoreTest]
    public class SerilogContextDataNetCoreOldestTests : ContextDataTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
    {
        public SerilogContextDataNetCoreOldestTests(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Serilog, false)
        {
        }
    }

    #endregion

    #region MEL

    [NetFrameworkTest]
    public class MELContextDataFWLatestTests : ContextDataTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public MELContextDataFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.MicrosoftLogging, true)
        {
        }
    }

    [NetCoreTest]
    public class MELContextDataNetCoreLatestTests : ContextDataTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public MELContextDataNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.MicrosoftLogging, true)
        {
        }
    }

    [NetCoreTest]
    public class MELContextDataNetCoreOldestTests : ContextDataTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
    {
        public MELContextDataNetCoreOldestTests(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.MicrosoftLogging, true)
        {
        }
    }

    #endregion

    #region Sitecore
    public class SitecoreContextDataFWLatestTests : ContextDataTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public SitecoreContextDataFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Sitecore, false)
        {
        }
    }

    public class SitecoreContextDataFW48Tests : ContextDataTestsBase<ConsoleDynamicMethodFixtureFW48>
    {
        public SitecoreContextDataFW48Tests(ConsoleDynamicMethodFixtureFW48 fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Sitecore, false)
        {
        }
    }

    #endregion // Sitecore
     
    #region SEL
    public class SELContextDataFWLatestTests : ContextDataTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public SELContextDataFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.SerilogEL, true)
        {
        }
    }

    public class SELContextDataFW48Tests : ContextDataTestsBase<ConsoleDynamicMethodFixtureFW48>
    {
        public SELContextDataFW48Tests(ConsoleDynamicMethodFixtureFW48 fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.SerilogEL, true)
        {
        }
    }

    public class SELContextDataCoreLatestTests : ContextDataTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public SELContextDataCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.SerilogEL, true)
        {
        }
    }

    public class SELContextDataCoreOldestTests : ContextDataTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
    {
        public SELContextDataCoreOldestTests(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.SerilogEL, true)
        {
        }
    }

    #endregion // SEL

    #region NEL
    public class NELContextDataFWLatestTests : ContextDataTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public NELContextDataFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.NLogEL, true)
        {
        }
    }

    public class NELContextDataCoreLatestTests : ContextDataTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public NELContextDataCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.NLogEL, true)
        {
        }
    }

    #endregion // NEL
}

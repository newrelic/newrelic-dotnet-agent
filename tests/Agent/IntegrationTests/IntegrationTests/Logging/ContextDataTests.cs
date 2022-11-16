﻿// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using MongoDB.Bson;
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
        private bool _contextDataEnabled;

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


        public ContextDataTestsBase(TFixture fixture, ITestOutputHelper output, bool contextDataEnabled, LoggingFramework loggingFramework) : base(fixture)
        {
            _fixture = fixture;
            _loggingFramework = loggingFramework;
            _contextDataEnabled = contextDataEnabled;
            _fixture.SetTimeout(TimeSpan.FromMinutes(2));
            _fixture.TestLogger = output;

            _fixture.AddCommand($"LoggingTester SetFramework {_loggingFramework}");
            _fixture.AddCommand($"LoggingTester Configure");

            string context = string.Join(",", _expectedAttributes.Select(x => x.Key + "=" + x.Value).ToArray());

            _fixture.AddCommand($"LoggingTester CreateSingleLogMessage {InfoMessage} INFO {context}");

            // Give the unawaited async logs some time to catch up
            _fixture.AddCommand($"RootCommands DelaySeconds 10");

            _fixture.AddActions
            (
                setupConfiguration: () =>
                {
                    var configModifier = new NewRelicConfigModifier(fixture.DestinationNewRelicConfigFilePath);

                    configModifier
                    .EnableLogMetrics(true)
                    .EnableLogForwarding(true)
                    .EnableContextData(_contextDataEnabled)
                    .EnableDistributedTrace()
                    .SetLogLevel("debug");
                },
                exerciseApplication: () =>
                {
                    Thread.Sleep(TimeSpan.FromSeconds(10));
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

            if (_contextDataEnabled)
            {
                // different verions of log4net add varying amounts of default attributes, we ignore the counts becuase of this
                Assertions.LogLinesExist(expectedLogLines, logLines, ignoreAttributeCount:true);
            }
            else
            {
                Assertions.LogLinesDontExist(expectedLogLines, logLines);
            }
            
        }
    }

    #region log4net

    #region ContextData Enabled

    [NetFrameworkTest]
    public class Log4NetContextDataEnabledFWLatestTests : ContextDataTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public Log4NetContextDataEnabledFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, true, LoggingFramework.Log4net)
        {
        }
    }

    [NetFrameworkTest]
    public class Log4NetContextDataEnabledFW471Tests : ContextDataTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public Log4NetContextDataEnabledFW471Tests(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output, true, LoggingFramework.Log4net)
        {
        }
    }

    [NetFrameworkTest]
    public class Log4NetContextDataEnabledFW462Tests : ContextDataTestsBase<ConsoleDynamicMethodFixtureFW462>
    {
        public Log4NetContextDataEnabledFW462Tests(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
            : base(fixture, output, true, LoggingFramework.Log4net)
        {
        }
    }

    [NetCoreTest]
    public class Log4NetContextDataEnabledNetCoreLatestTests : ContextDataTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public Log4NetContextDataEnabledNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, true, LoggingFramework.Log4net)
        {
        }
    }

    [NetCoreTest]
    public class Log4NetContextDataEnabledNetCore60Tests : ContextDataTestsBase<ConsoleDynamicMethodFixtureCore60>
    {
        public Log4NetContextDataEnabledNetCore60Tests(ConsoleDynamicMethodFixtureCore60 fixture, ITestOutputHelper output)
            : base(fixture, output, true, LoggingFramework.Log4net)
        {
        }
    }

    [NetCoreTest]
    public class Log4NetContextDataEnabledNetCore50Tests : ContextDataTestsBase<ConsoleDynamicMethodFixtureCore50>
    {
        public Log4NetContextDataEnabledNetCore50Tests(ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
            : base(fixture, output, true, LoggingFramework.Log4net)
        {
        }
    }

    [NetCoreTest]
    public class Log4NetContextDataEnabledNetCore31Tests : ContextDataTestsBase<ConsoleDynamicMethodFixtureCore31>
    {
        public Log4NetContextDataEnabledNetCore31Tests(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
            : base(fixture, output, true, LoggingFramework.Log4net)
        {
        }
    }

    #endregion

    #region ContextData Disabled

    [NetFrameworkTest]
    public class Log4NetContextDataDisabledFWLatestTests : ContextDataTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public Log4NetContextDataDisabledFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, false, LoggingFramework.Log4net)
        {
        }
    }

    [NetFrameworkTest]
    public class Log4NetContextDataDisabledFW471Tests : ContextDataTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public Log4NetContextDataDisabledFW471Tests(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output, false, LoggingFramework.Log4net)
        {
        }
    }

    [NetFrameworkTest]
    public class Log4NetContextDataDisabledFW462Tests : ContextDataTestsBase<ConsoleDynamicMethodFixtureFW462>
    {
        public Log4NetContextDataDisabledFW462Tests(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
            : base(fixture, output, false, LoggingFramework.Log4net)
        {
        }
    }

    [NetCoreTest]
    public class Log4NetContextDataDisabledNetCoreLatestTests : ContextDataTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public Log4NetContextDataDisabledNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, false, LoggingFramework.Log4net)
        {
        }
    }

    [NetCoreTest]
    public class Log4NetContextDataDisabledNetCore60Tests : ContextDataTestsBase<ConsoleDynamicMethodFixtureCore60>
    {
        public Log4NetContextDataDisabledNetCore60Tests(ConsoleDynamicMethodFixtureCore60 fixture, ITestOutputHelper output)
            : base(fixture, output, false, LoggingFramework.Log4net)
        {
        }
    }

    [NetCoreTest]
    public class Log4NetContextDataDisabledNetCore50Tests : ContextDataTestsBase<ConsoleDynamicMethodFixtureCore50>
    {
        public Log4NetContextDataDisabledNetCore50Tests(ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
            : base(fixture, output, false, LoggingFramework.Log4net)
        {
        }
    }

    [NetCoreTest]
    public class Log4NetContextDataDisabledNetCore31Tests : ContextDataTestsBase<ConsoleDynamicMethodFixtureCore31>
    {
        public Log4NetContextDataDisabledNetCore31Tests(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
            : base(fixture, output, false, LoggingFramework.Log4net)
        {
        }
    }

    #endregion

    #endregion

}

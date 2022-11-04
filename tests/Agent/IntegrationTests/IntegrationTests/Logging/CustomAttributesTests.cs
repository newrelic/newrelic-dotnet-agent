// Copyright 2020 New Relic, Inc. All rights reserved.
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

namespace NewRelic.Agent.IntegrationTests.Logging.MetricsAndForwarding
{
    public abstract class CustomAttributesTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
        where TFixture : ConsoleDynamicMethodFixture
    {
        private readonly TFixture _fixture;
        private LoggingFramework _loggingFramework;

        private const string InfoMessage = "HelloWorld";
        private Dictionary<string, string> _expectedAttributes = new Dictionary<string, string>()
        {
            { "mycontext1", "foo" },
            { "mycontext2", "bar" }
        };


        public CustomAttributesTestsBase(TFixture fixture, ITestOutputHelper output, bool metricsEnabled, bool forwardingEnabled, bool canHaveLogsOutsideTransaction, LoggingFramework loggingFramework) : base(fixture)
        {
            _fixture = fixture;
            _loggingFramework = loggingFramework;
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
                    .EnableLogMetrics(metricsEnabled)
                    .EnableLogForwarding(forwardingEnabled)
                    .EnableContextData(true)
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

            Assertions.LogLinesExist(expectedLogLines, logLines);
        }
    }

    #region log4net

    namespace log4net
    {
        #region Metrics and Forwarding Enabled

        [NetCoreTest]
        public class Log4NetCustomAttributesNetCore60Tests : CustomAttributesTestsBase<ConsoleDynamicMethodFixtureCore60>
        {
            public Log4NetCustomAttributesNetCore60Tests(ConsoleDynamicMethodFixtureCore60 fixture, ITestOutputHelper output)
                : base(fixture, output, true, true, true, LoggingFramework.Log4net)
            {
            }
        }

        //[NetCoreTest]
        //public class Log4NetCustomAttributesNetCore50Tests : CustomAttributesTestsBase<ConsoleDynamicMethodFixtureCore50>
        //{
        //    public Log4NetCustomAttributesNetCore50Tests(ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
        //        : base(fixture, output, true, true, true, LoggingFramework.Log4net)
        //    {
        //    }
        //}

        //[NetCoreTest]
        //public class Log4NetCustomAttributesNetCore31Tests : CustomAttributesTestsBase<ConsoleDynamicMethodFixtureCore31>
        //{
        //    public Log4NetCustomAttributesNetCore31Tests(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
        //        : base(fixture, output, true, true, true, LoggingFramework.Log4net)
        //    {
        //    }
        //}

        #endregion

    }

    #endregion

}

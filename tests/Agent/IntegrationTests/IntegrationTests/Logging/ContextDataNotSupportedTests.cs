﻿// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using MultiFunctionApplicationHelpers;
using NewRelic.Agent.IntegrationTestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.Logging.ContextData
{
    public abstract class ContextDataNotSupportedTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
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


        public ContextDataNotSupportedTestsBase(TFixture fixture, ITestOutputHelper output, LoggingFramework loggingFramework) : base(fixture)
        {
            _fixture = fixture;
            _loggingFramework = loggingFramework;
            _fixture.SetTimeout(TimeSpan.FromMinutes(2));
            _fixture.TestLogger = output;

            _fixture.AddCommand($"LoggingTester SetFramework {_loggingFramework}");
            _fixture.AddCommand($"LoggingTester Configure");

            string context = string.Join(",", _expectedAttributes.Select(x => x.Key + "=" + x.Value).ToArray());

            // should generate an exception message in the log since the dummy ILogger doesn't have the right properties
            _fixture.AddCommand($"LoggingTester CreateSingleLogMessage {InfoMessage} INFO {context}");
            // do it again - this time, context data should be marked as unsupported and not generate an exception in the log
            _fixture.AddCommand($"LoggingTester CreateSingleLogMessage {InfoMessage} INFO {context}");

            _fixture.AddActions
            (
                setupConfiguration: () =>
                {
                    var configModifier = new NewRelicConfigModifier(fixture.DestinationNewRelicConfigFilePath);

                    configModifier
                    .EnableContextData()
                    .SetLogLevel("finest");
                },
                exerciseApplication: () =>
                {
                    Thread.Sleep(TimeSpan.FromSeconds(2));
                }
            );

            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            // verify the "not supported" warning was logged
            var match = _fixture.AgentLog.TryGetLogLines(AgentLogBase.ContextDataNotSupportedLogLineRegex);
            Assert.Single(match);

            // verify the log data was forwarded, but *without* the attributes
            var expectedLogLines = new[]
            {
                new Assertions.ExpectedLogLine
                {
                    Level = LogUtils.GetLevelName(_loggingFramework, "INFO"),
                    LogMessage = InfoMessage
                }
            };

            var logLines = _fixture.AgentLog.GetLogEventDataLogLines().ToArray();
            Assertions.LogLinesExist(expectedLogLines, logLines, ignoreAttributeCount: true);
        }
    }

    [NetFrameworkTest]
    public class ContextDataNotSupportedFWLatestTests : ContextDataNotSupportedTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public ContextDataNotSupportedFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.DummyMEL)
        {
        }
    }

    [NetCoreTest]
    public class ContextDataNotSupportedNetCoreLatestTests : ContextDataNotSupportedTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public ContextDataNotSupportedNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.DummyMEL)
        {
        }
    }

    [NetCoreTest]
    public class ContextDataNotSupportedNetCore60Tests : ContextDataNotSupportedTestsBase<ConsoleDynamicMethodFixtureCore60>
    {
        public ContextDataNotSupportedNetCore60Tests(ConsoleDynamicMethodFixtureCore60 fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.DummyMEL)
        {
        }
    }

    [NetCoreTest]
    public class ContextDataNotSupportedNetCore50Tests : ContextDataNotSupportedTestsBase<ConsoleDynamicMethodFixtureCore50>
    {
        public ContextDataNotSupportedNetCore50Tests(ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.DummyMEL)
        {
        }
    }

    [NetCoreTest]
    public class ContextDataNotSupportedNetCore31Tests : ContextDataNotSupportedTestsBase<ConsoleDynamicMethodFixtureCore31>
    {
        public ContextDataNotSupportedNetCore31Tests(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.DummyMEL)
        {
        }
    }
}

// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using MultiFunctionApplicationHelpers;
using NewRelic.Agent.IntegrationTestHelpers;
using Newtonsoft.Json;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.Logging
{
    public abstract class Log4netInstrumentationEnabledTests<TFixture> : NewRelicIntegrationTest<TFixture>
        where TFixture : ConsoleDynamicMethodFixture
    {
        private const string OutsideTransactionInfoMessage = "OutsideTransactionInfoLogMessage";
        private const string OutsideTransactionDebugMessage = "OutsideTransactionDebugLogMessage";
        private const string OutsideTransactionErrorMessage = "OutsideTransactionErrorLogMessage";
        private const string OutsideTransactionFatalMessage = "OutsideTransactionFatalLogMessage";

        private const string InTransactionInfoMessage = "InTransactionInfoLogMessage";
        private const string InTransactionDebugMessage = "InTransactionDebugLogMessage";
        private const string InTransactionErrorMessage = "InTransactionErrorLogMessage";
        private const string InTransactionFatalMessage = "InTransactionFatalLogMessage";

        private readonly TFixture _fixture;

        public Log4netInstrumentationEnabledTests(TFixture fixture, ITestOutputHelper output) : base(fixture)
        {
            _fixture = fixture;
            _fixture.SetTimeout(System.TimeSpan.FromMinutes(2));
            _fixture.TestLogger = output;

            _fixture.AddCommand($"Log4netTester Configure");
            _fixture.AddCommand($"Log4netTester CreateSingleLogMessage {OutsideTransactionInfoMessage} info");
            _fixture.AddCommand($"Log4netTester CreateSingleLogMessage {OutsideTransactionDebugMessage} debug");
            _fixture.AddCommand($"Log4netTester CreateSingleLogMessage {OutsideTransactionErrorMessage} error");
            _fixture.AddCommand($"Log4netTester CreateSingleLogMessage {OutsideTransactionFatalMessage} fatal");

            _fixture.AddCommand($"Log4netTester CreateSingleLogMessageInTransaction {InTransactionInfoMessage} info");
            _fixture.AddCommand($"Log4netTester CreateSingleLogMessageInTransaction {InTransactionDebugMessage} debug");
            _fixture.AddCommand($"Log4netTester CreateSingleLogMessageInTransaction {InTransactionErrorMessage} error");
            _fixture.AddCommand($"Log4netTester CreateSingleLogMessageInTransaction {InTransactionFatalMessage} fatal");

            _fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var configModifier = new NewRelicConfigModifier(fixture.DestinationNewRelicConfigFilePath);

                    configModifier.EnableLogMetrics(true)
                    .EnableDistributedTrace()
                    .SetLogLevel("debug");
                }
            );

            _fixture.Initialize();
        }

        [Fact]
        public void CountsAndValuesAreAsExpected()
        {
            var logs = _fixture.AgentLog.GetLogEventData().ToArray();
            Assert.NotEmpty(logs);

            foreach (var log in logs)
            {
                Assert.NotNull(log.Common);
                Assert.NotNull(log.Common.Attributes);
                Assert.False(string.IsNullOrWhiteSpace(log.Common.Attributes.EntityGuid));
                Assert.False(string.IsNullOrWhiteSpace(log.Common.Attributes.EntityName));
                Assert.False(string.IsNullOrWhiteSpace(log.Common.Attributes.EntityType));
                Assert.False(string.IsNullOrWhiteSpace(log.Common.Attributes.Hostname));
                Assert.Equal("nr-dotnet-agent", log.Common.Attributes.PluginType);
            }

            var logLines = _fixture.AgentLog.GetLogEventDataLogLines().ToArray();
            Assert.Equal(8, logLines.Length);

            foreach(var logLine in logLines)
            {
                Assert.False(string.IsNullOrWhiteSpace(logLine.Message));
                Assert.False(string.IsNullOrWhiteSpace(logLine.Level));
                Assert.NotEqual(0, logLine.Timestamp);
            }
        }

        [Fact]
        public void LoggingWorksInsideTransaction()
        {
            var expectedLogLines = new Assertions.ExpectedLogLine[]
            {
                new Assertions.ExpectedLogLine { LogLevel = "INFO", LogMessage = InTransactionInfoMessage, HasTraceId = true, HasSpanId = true },
                new Assertions.ExpectedLogLine { LogLevel = "DEBUG", LogMessage = InTransactionDebugMessage, HasTraceId = true, HasSpanId = true },
                new Assertions.ExpectedLogLine { LogLevel = "ERROR", LogMessage = InTransactionErrorMessage, HasTraceId = true, HasSpanId = true },
                new Assertions.ExpectedLogLine { LogLevel = "FATAL", LogMessage = InTransactionFatalMessage, HasTraceId = true, HasSpanId = true }
            };

            var logLines = _fixture.AgentLog.GetLogEventDataLogLines();

            Assertions.LogLinesExist(expectedLogLines, logLines);

            Assert.Equal(4, logLines.Where(x => x.Message.StartsWith("InTransaction")).Count());
        }

        [Fact]
        public void LoggingWorksOutsideTransaction()
        {
            var expectedLogLines = new Assertions.ExpectedLogLine[]
            {
                new Assertions.ExpectedLogLine { LogLevel = "INFO", LogMessage = OutsideTransactionInfoMessage},
                new Assertions.ExpectedLogLine { LogLevel = "DEBUG", LogMessage = OutsideTransactionDebugMessage},
                new Assertions.ExpectedLogLine { LogLevel = "ERROR", LogMessage = OutsideTransactionErrorMessage},
                new Assertions.ExpectedLogLine { LogLevel = "FATAL", LogMessage = OutsideTransactionFatalMessage},
            };

            var logLines = _fixture.AgentLog.GetLogEventDataLogLines();

            Assertions.LogLinesExist(expectedLogLines, logLines);

            Assert.Equal(4, logLines.Where(x => x.Message.StartsWith("OutsideTransaction")).Count());
        }
    }

    [NetFrameworkTest]
    public class Log4netInstrumentationTestsFWLatestTests : Log4netInstrumentationEnabledTests<ConsoleDynamicMethodFixtureFWLatest>
    {
        public Log4netInstrumentationTestsFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetFrameworkTest]
    public class Log4netInstrumentationTestsFW471Tests : Log4netInstrumentationEnabledTests<ConsoleDynamicMethodFixtureFW471>
    {
        public Log4netInstrumentationTestsFW471Tests(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetFrameworkTest]
    public class Log4netInstrumentationTestsFW462Tests : Log4netInstrumentationEnabledTests<ConsoleDynamicMethodFixtureFW462>
    {
        public Log4netInstrumentationTestsFW462Tests(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class Log4netInstrumentationTestsNetCoreLatestTests : Log4netInstrumentationEnabledTests<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public Log4netInstrumentationTestsNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class Log4netInstrumentationTestsNetCore50Tests : Log4netInstrumentationEnabledTests<ConsoleDynamicMethodFixtureCore50>
    {
        public Log4netInstrumentationTestsNetCore50Tests(ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class Log4netInstrumentationTestsNetCore31Tests : Log4netInstrumentationEnabledTests<ConsoleDynamicMethodFixtureCore31>
    {
        public Log4netInstrumentationTestsNetCore31Tests(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class Log4netInstrumentationTestsNetCore21Tests : Log4netInstrumentationEnabledTests<ConsoleDynamicMethodFixtureCore21>
    {
        public Log4netInstrumentationTestsNetCore21Tests(ConsoleDynamicMethodFixtureCore21 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }
}


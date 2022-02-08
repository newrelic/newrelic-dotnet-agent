// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using System.Dynamic;
using MultiFunctionApplicationHelpers;
using NewRelic.Agent.IntegrationTestHelpers;
using Newtonsoft.Json;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.Logging
{
    public abstract class Log4netInstrumentationTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
        where TFixture : ConsoleDynamicMethodFixture
    {
        private const string InfoMessage = "InfoLogMessage";
        private const string DebugMessage = "DebugLogMessage";
        private const string ErrorMessage = "ErrorLogMessage";
        private const string FatalMessage = "FatalLogMessage";

        private readonly TFixture _fixture;

        public Log4netInstrumentationTestsBase(TFixture fixture, ITestOutputHelper output) : base(fixture)
        {
            _fixture = fixture;
            _fixture.SetTimeout(System.TimeSpan.FromMinutes(2));
            _fixture.TestLogger = output;

            _fixture.AddCommand($"Log4netTester Configure");
            _fixture.AddCommand($"Log4netTester CreateSingleLogMessage {InfoMessage} info");
            _fixture.AddCommand($"Log4netTester CreateSingleLogMessage {DebugMessage} debug");
            _fixture.AddCommand($"Log4netTester CreateSingleLogMessage {ErrorMessage} error");
            _fixture.AddCommand($"Log4netTester CreateSingleLogMessage {FatalMessage} fatal");

            _fixture.AddCommand($"Log4netTester CreateSingleLogMessageInTransaction {InfoMessage} info");
            _fixture.AddCommand($"Log4netTester CreateSingleLogMessageInTransaction {DebugMessage} debug");
            _fixture.AddCommand($"Log4netTester CreateSingleLogMessageInTransaction {ErrorMessage} error");
            _fixture.AddCommand($"Log4netTester CreateSingleLogMessageInTransaction {FatalMessage} fatal");

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
        public void LogsSentRegardlessOfTransaction()
        {
            // Sending 1 info and 1 debug message, total 2 messages
            var expectedInfoMessages = 1;
            var expectedDebugMessages = 1;
            var expectedTotalMessages = 2;

            var actualMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = @"Logging/lines/INFO", callCount = expectedInfoMessages },
                new Assertions.ExpectedMetric { metricName = @"Logging/lines/DEBUG", callCount = expectedDebugMessages },
                new Assertions.ExpectedMetric { metricName = @"Logging/lines", callCount = expectedTotalMessages },
            };

            var metrics = _fixture.AgentLog.GetMetrics();
            var logs = _fixture.AgentLog.GetLogData();
            foreach (var log in logs)
            {
                Console.WriteLine(log);
                var whatisIt = log.logs;
                foreach(varWhatItIs in whatisIt)
                {

                }
                Console.WriteLine(whatisIt);
                //dynamic deserialized = JsonConvert.DeserializeObject(log);
               // Console.WriteLine(deserialized);
            }

            Assertions.MetricsExist(actualMetrics, metrics);
        }
    }

    [NetFrameworkTest]
    public class Log4netInstrumentationTestsFWLatestTests : Log4netInstrumentationTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public Log4netInstrumentationTestsFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }


    [NetFrameworkTest]
    public class Log4netInstrumentationTestsFW471Tests : Log4netInstrumentationTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public Log4netInstrumentationTestsFW471Tests(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetFrameworkTest]
    public class Log4netInstrumentationTestsFW462Tests : Log4netInstrumentationTestsBase<ConsoleDynamicMethodFixtureFW462>
    {
        public Log4netInstrumentationTestsFW462Tests(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class Log4netInstrumentationTestsNetCoreLatestTests : Log4netInstrumentationTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public Log4netInstrumentationTestsNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class Log4netInstrumentationTestsNetCore50Tests : Log4netInstrumentationTestsBase<ConsoleDynamicMethodFixtureCore50>
    {
        public Log4netInstrumentationTestsNetCore50Tests(ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class Log4netInstrumentationTestsNetCore31Tests : Log4netInstrumentationTestsBase<ConsoleDynamicMethodFixtureCore31>
    {
        public Log4netInstrumentationTestsNetCore31Tests(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class Log4netInstrumentationTestsNetCore21Tests : Log4netInstrumentationTestsBase<ConsoleDynamicMethodFixtureCore21>
    {
        public Log4netInstrumentationTestsNetCore21Tests(ConsoleDynamicMethodFixtureCore21 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }
}


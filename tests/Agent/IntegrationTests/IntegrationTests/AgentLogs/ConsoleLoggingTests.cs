// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.AgentLogs
{
    public abstract class ConsoleLoggingTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
        where TFixture : ConsoleDynamicMethodFixture
    {
        private readonly TFixture _fixture;

        public ConsoleLoggingTestsBase(TFixture fixture, ITestOutputHelper output) : base(fixture)
        {
            _fixture = fixture;
            _fixture.SetTimeout(TimeSpan.FromMinutes(1));
            _fixture.TestLogger = output;

            _fixture.AddCommand($"RootCommands InstrumentedMethodToStartAgent");
            _fixture.AddCommand($"RootCommands DelaySeconds 10");

            _fixture.AddActions
            (
                setupConfiguration: () =>
                {
                    var configModifier = new NewRelicConfigModifier(fixture.DestinationNewRelicConfigFilePath);
                    configModifier
                    .SetLogLevel("debug");

                    _fixture.RemoteApplication.SetAdditionalEnvironmentVariable("NEW_RELIC_LOG_CONSOLE", "true");

                },
                exerciseApplication: () =>
                {
                }
            );

            _fixture.Initialize();
        }

        [Fact]
        public void ConsoleLogsExist()
        {
            var stdOut = _fixture.RemoteApplication.CapturedOutput.StandardOutput;

            Assert.Contains("Console logging enabled", stdOut); // A profiler log message
            Assert.Contains("Log level set to DEBUG", stdOut);  // An agent log message
        }
    }

    [NetFrameworkTest]
    public class ConsoleLoggingFWLatestTests : ConsoleLoggingTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public ConsoleLoggingFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetFrameworkTest]
    public class ConsoleLoggingFW462Tests : ConsoleLoggingTestsBase<ConsoleDynamicMethodFixtureFW462>
    {
        public ConsoleLoggingFW462Tests(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class ConsoleLoggingCoreLatestTests : ConsoleLoggingTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public ConsoleLoggingCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class ConsoleLoggingCoreOldestTests : ConsoleLoggingTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
    {
        public ConsoleLoggingCoreOldestTests(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }
}

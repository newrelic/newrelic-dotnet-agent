// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.IO;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.AgentLogs
{
    public abstract class LoggingDisabledTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
        where TFixture : ConsoleDynamicMethodFixture
    {
        private readonly TFixture _fixture;

        public LoggingDisabledTestsBase(TFixture fixture, ITestOutputHelper output) : base(fixture)
        {
            _fixture = fixture;
            _fixture.SetTimeout(TimeSpan.FromMinutes(1));
            _fixture.TestLogger = output;
            _fixture.AgentLogExpected = false;

            _fixture.AddCommand($"RootCommands InstrumentedMethodToStartAgent");
            _fixture.AddCommand($"RootCommands DelaySeconds 10");

            _fixture.AddActions
            (
                setupConfiguration: () =>
                {
                    var configModifier = new NewRelicConfigModifier(fixture.DestinationNewRelicConfigFilePath);
                    configModifier
                    .SetLogLevel("debug");

                    _fixture.RemoteApplication.SetAdditionalEnvironmentVariable("NEW_RELIC_LOG_ENABLED", "false");

                },
                exerciseApplication: () =>
                {
                }
            );

            _fixture.Initialize();
        }

        [Fact]
        public void LogsDirShouldNotExist()
        {
            Assert.False(Directory.Exists(_fixture.RemoteApplication.DefaultLogFileDirectoryPath), "Logs are disabled so logs dir should not exist.");
        }
    }

    [NetFrameworkTest]
    public class LoggingDisabledFWLatestTests : LoggingDisabledTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public LoggingDisabledFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetFrameworkTest]
    public class LoggingDisabledFW462Tests : LoggingDisabledTestsBase<ConsoleDynamicMethodFixtureFW462>
    {
        public LoggingDisabledFW462Tests(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class LoggingDisabledCoreLatestTests : LoggingDisabledTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public LoggingDisabledCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class LoggingDisabledCoreOldestTests : LoggingDisabledTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
    {
        public LoggingDisabledCoreOldestTests(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }
}

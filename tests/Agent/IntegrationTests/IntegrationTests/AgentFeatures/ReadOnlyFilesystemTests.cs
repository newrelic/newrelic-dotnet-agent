// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.AgentFeatures
{
    public abstract class ReadOnlyFilesytemTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
        where TFixture : ConsoleDynamicMethodFixture
    {
        private readonly TFixture _fixture;

        public ReadOnlyFilesytemTestsBase(TFixture fixture, ITestOutputHelper output) : base(fixture)
        {
            _fixture = fixture;
            _fixture.SetTimeout(TimeSpan.FromMinutes(1));
            _fixture.TestLogger = output;
            _fixture.AgentLogExpected = false;

            _fixture.AddCommand($"RootCommands InstrumentedMethodToStartAgent");
            _fixture.AddCommand($"RootCommands DelaySeconds 60");

            _fixture.AddActions
            (
                setupConfiguration: () =>
                {
                    var configModifier = new NewRelicConfigModifier(fixture.DestinationNewRelicConfigFilePath);
                    configModifier
                    .SetLogLevel("debug");

                    _fixture.RemoteApplication.SetAdditionalEnvironmentVariable("NEW_RELIC_LOG_ENABLED", "false");
                    _fixture.RemoteApplication.SetAdditionalEnvironmentVariable("NEW_RELIC_LOG_CONSOLE", "true");


                },
                exerciseApplication: () =>
                {
                    // Can't wait on log lines with no log!

                    //_fixture.AgentLog.WaitForLogLine(AgentLogBase.SetApplicationnameAPICalledDuringConnectMethodLogLineRegex, TimeSpan.FromMinutes(1));
                    //_fixture.AgentLog.WaitForLogLine(AgentLogBase.AttemptReconnectLogLineRegex, TimeSpan.FromMinutes(1));
                    //// There should be two connected log lines, one for the initial connect and the other after the reconnect
                    //_fixture.AgentLog.WaitForLogLines(AgentLogBase.AgentConnectedLogLineRegex, TimeSpan.FromMinutes(1), 2);
                }
            );

            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {

            _ = NrAssert.Equals(true, true);
            //TBD

            // Assert no agent log file created
            // Assert no profiler log file created
            // Assert console output from profiler exists
            // Assert console output from agent exists
        }
    }

    [NetFrameworkTest]
    public class ReadOnlyFilesystemFWLatestTests : ReadOnlyFilesytemTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public ReadOnlyFilesystemFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetFrameworkTest]
    public class ReadOnlyFilesystemFW462Tests : ReadOnlyFilesytemTestsBase<ConsoleDynamicMethodFixtureFW462>
    {
        public ReadOnlyFilesystemFW462Tests(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class ReadOnlyFilesystemCoreLatestTests : ReadOnlyFilesytemTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public ReadOnlyFilesystemCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class ReadOnlyFilesystemCoreOldestTests : ReadOnlyFilesytemTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
    {
        public ReadOnlyFilesystemCoreOldestTests(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }
}

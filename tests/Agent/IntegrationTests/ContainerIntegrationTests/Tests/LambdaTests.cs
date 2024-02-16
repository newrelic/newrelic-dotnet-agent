// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.ContainerIntegrationTests.Fixtures;
using NewRelic.Agent.IntegrationTestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.ContainerIntegrationTests.Tests;

public abstract class LambdaTest<T> : NewRelicIntegrationTest<T> where T : LambdaTestFixtureBase
{
    private readonly T _fixture;

    protected LambdaTest(T fixture, ITestOutputHelper output) : base(fixture)
    {
        _fixture = fixture;
        _fixture.TestLogger = output;

        _fixture.Actions(setupConfiguration: () =>
            {
                var configModifier = new NewRelicConfigModifier(_fixture.DestinationNewRelicConfigFilePath);
                configModifier.SetLogLevel("finest");
                configModifier.SetSyncStartup(true);
                configModifier.SetCompleteTransactionsOnThread(true);
                //configModifier.SetDebugStartupDelaySeconds(15);
                configModifier.LogToConsole();
            },
            exerciseApplication: () =>
            {
                _fixture.Delay(15); 
                _fixture.ExerciseApplication();

                _fixture.AgentLog.WaitForLogLine(AgentLogBase.HarvestFinishedLogLineRegex, TimeSpan.FromSeconds(11));

                // shut down the container and wait for the agent log to see it
                _fixture.ShutdownRemoteApplication();
                _fixture.AgentLog.WaitForLogLine(AgentLogBase.ShutdownLogLineRegex, TimeSpan.FromSeconds(10));
            });

        _fixture.Initialize();
    }

    [Fact]
    public void Test()
    {

    }

}

public class LambdaDotNet7Test : LambdaTest<LambdaDotNet7TestFixture>
{
    public LambdaDotNet7Test(LambdaDotNet7TestFixture fixture, ITestOutputHelper output) : base(fixture, output)
    {
    }
}

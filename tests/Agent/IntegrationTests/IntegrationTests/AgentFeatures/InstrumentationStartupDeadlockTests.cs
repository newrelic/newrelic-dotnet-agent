// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.IntegrationTestHelpers;
using Xunit;

namespace NewRelic.Agent.IntegrationTests.AgentFeatures;

/// <summary>
/// Tests that the agent doesn't deadlock at startup
/// </summary>
public abstract class InstrumentationStartupDeadlockTestsBase<TFixture> : NewRelicIntegrationTest<TFixture> where TFixture : RemoteServiceFixtures.ConsoleInstrumentationStartupFixtureCore
{
    private readonly TFixture _fixture;

    protected InstrumentationStartupDeadlockTestsBase(TFixture fixture, ITestOutputHelper output) : base(fixture)
    {
        _fixture = fixture;
        _fixture.TestLogger = output;

        _fixture.Actions
        (
            setupConfiguration: () =>
            {
                var configPath = fixture.DestinationNewRelicConfigFilePath;
                var configModifier = new NewRelicConfigModifier(configPath);

                configModifier.SetHostPort(9999); // use a bogus port to generate an exception during HttpClient.SendAsync() on connect
                configModifier.SetSendDataOnExit();

                configModifier.ForceTransactionTraces();
                configModifier.SetLogLevel("finest");
                configModifier.DisableEventListenerSamplers(); // Required for .NET 10+ to pass.
            },
            exerciseApplication: () =>
            {
            }
        );

        _fixture.Initialize();
    }

    [Fact]
    public void Test()
    {
        var expectedLogLineRegexes = new[] { AgentLogBase.ShutdownLogLineRegex };

        Assertions.LogLinesExist(expectedLogLineRegexes, _fixture.AgentLog.GetFileLines());
    }
}

public class InstrumentationStartupDeadlockTestsCoreOldest : InstrumentationStartupDeadlockTestsBase<RemoteServiceFixtures.ConsoleInstrumentationStartupFixtureCoreOldest>
{
    public InstrumentationStartupDeadlockTestsCoreOldest(RemoteServiceFixtures.ConsoleInstrumentationStartupFixtureCoreOldest fixture, ITestOutputHelper output)
        : base(fixture, output)
    {
    }
}

public class InstrumentationStartupDeadlockTestsCoreLatest : InstrumentationStartupDeadlockTestsBase<RemoteServiceFixtures.ConsoleInstrumentationStartupFixtureCoreLatest>
{
    public InstrumentationStartupDeadlockTestsCoreLatest(RemoteServiceFixtures.ConsoleInstrumentationStartupFixtureCoreLatest fixture, ITestOutputHelper output)
        : base(fixture, output)
    {
    }
}

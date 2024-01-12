// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.AgentFeatures
{
    /// <summary>
    /// Tests that the agent doesn't deadlock at startup
    /// </summary>
    [NetCoreTest]
    public class InstrumentationStartupDeadlockTests : NewRelicIntegrationTest<RemoteServiceFixtures.ConsoleInstrumentationStartupFixtureCore>
    {
        private readonly RemoteServiceFixtures.ConsoleInstrumentationStartupFixtureCore _fixture;

        public InstrumentationStartupDeadlockTests(RemoteServiceFixtures.ConsoleInstrumentationStartupFixtureCore fixture, ITestOutputHelper output) : base(fixture)
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
                    configModifier.DisableEventListenerSamplers(); // Required for .NET 8 to pass.
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
}

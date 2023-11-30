// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using Xunit;
using Xunit.Abstractions;
using NewRelic.Agent.IntegrationTestHelpers;
using System.Linq;
using NewRelic.Testing.Assertions;

namespace NewRelic.Agent.IntegrationTests.AgentFeatures
{
    [NetFrameworkTest]
    public class InstrumentationLoaderTests : NewRelicIntegrationTest<RemoteServiceFixtures.ConsoleInstrumentationLoaderFixture>
    {
        private readonly RemoteServiceFixtures.ConsoleInstrumentationLoaderFixture _fixture;
        private readonly ITestOutputHelper _output;

        public InstrumentationLoaderTests(RemoteServiceFixtures.ConsoleInstrumentationLoaderFixture fixture, ITestOutputHelper output) : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;

            _output = output;

            _fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var configPath = fixture.DestinationNewRelicConfigFilePath;
                    var configModifier = new NewRelicConfigModifier(configPath);

                    configModifier.ForceTransactionTraces();
                    configModifier.SetLogLevel("finest");
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
            var trxTransformLines = _fixture.AgentLog
                .TryGetLogLines(AgentLogBase.TransactionTransformCompletedLogLineRegex)
                .ToList();

            var stdError = _fixture.RemoteApplication.CapturedOutput.StandardError;

            NrAssert.Multiple(
                () => { Assert.True(string.IsNullOrWhiteSpace(stdError), "There were exceptions generated in the console app"); },
                () => { Assert.Single(trxTransformLines); },
                () => { Assert.Contains("InstrumentedMethod", trxTransformLines.FirstOrDefault().Value); }
            );
        }
    }

    [NetCoreTest]
    public class InstrumentationLoaderTestsCore : NewRelicIntegrationTest<RemoteServiceFixtures.ConsoleInstrumentationLoaderFixtureCore>
    {
        private readonly RemoteServiceFixtures.ConsoleInstrumentationLoaderFixtureCore _fixture;

        public InstrumentationLoaderTestsCore(RemoteServiceFixtures.ConsoleInstrumentationLoaderFixtureCore fixture, ITestOutputHelper output) : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;

            _fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var configPath = fixture.DestinationNewRelicConfigFilePath;
                    var configModifier = new NewRelicConfigModifier(configPath);

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
            var trxTransformLines = _fixture.AgentLog
                .TryGetLogLines(AgentLogBase.TransactionTransformCompletedLogLineRegex)
                .ToList();

            var stdError = _fixture.RemoteApplication.CapturedOutput.StandardError;

            NrAssert.Multiple(
                () => { Assert.True(string.IsNullOrWhiteSpace(stdError), "There were exceptions generated in the console app"); },
                () => { Assert.Single(trxTransformLines); },
                () => { Assert.Contains("InstrumentedMethod", trxTransformLines.FirstOrDefault().Value); }
            );
        }
    }
}

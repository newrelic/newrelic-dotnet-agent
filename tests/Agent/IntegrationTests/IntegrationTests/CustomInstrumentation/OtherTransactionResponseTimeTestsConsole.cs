// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Testing.Assertions;
using Xunit;


namespace NewRelic.Agent.IntegrationTests.CustomInstrumentation
{
    [NetFrameworkTest]
    public class OtherTransactionResponseTimeTestsConsole : NewRelicIntegrationTest<RemoteServiceFixtures.ConsoleOtherTransactionWrapperFixture>
    {
        private readonly RemoteServiceFixtures.ConsoleOtherTransactionWrapperFixture _fixture;
        private const int _delayDuration = 2;

        public OtherTransactionResponseTimeTestsConsole(RemoteServiceFixtures.ConsoleOtherTransactionWrapperFixture fixture, ITestOutputHelper output) : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;

            _fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var configModifier = new NewRelicConfigModifier(_fixture.DestinationNewRelicConfigFilePath);
                    configModifier.SetLogLevel("Finest");
                    configModifier.ForceTransactionTraces();
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
            const int durationShouldBeGreaterThan = _delayDuration * 2;

            var trxEvents = _fixture.AgentLog.GetTransactionEvents().ToList();

            //There should only be one transaction
            Assert.Single(trxEvents);

            var trx = trxEvents.First();

            //It should have the following intrinsic values
            NrAssert.Multiple(
                () => { Assert.True(trx.IntrinsicAttributes.ContainsKey("totalTime")); },
                () => { Assert.True(trx.IntrinsicAttributes.ContainsKey("duration")); }
            );

            var trxDuration = (double)trx.IntrinsicAttributes["duration"];
            var trxTotalTime = (double)trx.IntrinsicAttributes["totalTime"];

            //The times should all be greater than 2x the delay since the InnerMethod, which uses
            //OtherTransactionWrapper should NOT record responsetime
            NrAssert.Multiple(
                () => { Assert.True(trxDuration > durationShouldBeGreaterThan); },
                () => { Assert.True(trxTotalTime > durationShouldBeGreaterThan); });
        }
    }
}

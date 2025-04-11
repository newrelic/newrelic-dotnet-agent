// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.CustomInstrumentation
{
    public class OtherTransactionResponseTimeTestsWebApi : NewRelicIntegrationTest<RemoteServiceFixtures.WebApiAsyncFixture>
    {
        private readonly RemoteServiceFixtures.WebApiAsyncFixture _fixture;
        private const int _delayDuration = 2;

        public OtherTransactionResponseTimeTestsWebApi(RemoteServiceFixtures.WebApiAsyncFixture fixture, ITestOutputHelper output) : base(fixture)
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
                    _fixture.ExecuteResponseTimeTestOperation(_delayDuration);
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
                () => { Assert.True(trx.IntrinsicAttributes.ContainsKey("duration")); },
                () => { Assert.True(trx.IntrinsicAttributes.ContainsKey("webDuration")); });

            var trxDuration = (double)trx.IntrinsicAttributes["duration"];
            var trxWebDuration = (double)trx.IntrinsicAttributes["webDuration"];
            var trxTotalTime = (double)trx.IntrinsicAttributes["totalTime"];

            //The times should all be greater than 2x the delay since the InnerMethod, which uses
            //OtherTransactionWrapper should NOT record responsetime
            NrAssert.Multiple(
                () => { Assert.True(trxDuration > durationShouldBeGreaterThan); },
                () => { Assert.True(trxWebDuration > durationShouldBeGreaterThan); },
                () => { Assert.True(trxTotalTime > durationShouldBeGreaterThan); });
        }
    }
}

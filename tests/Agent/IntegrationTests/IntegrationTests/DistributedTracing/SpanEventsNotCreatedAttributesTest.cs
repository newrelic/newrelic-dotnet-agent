// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Testing.Assertions;
using Xunit;

namespace NewRelic.Agent.IntegrationTests.DistributedTracing
{
    public class SpanEventsNotCreatedAttributesTest : NewRelicIntegrationTest<RemoteServiceFixtures.DTBasicMVCApplicationFixture>
    {
        private readonly RemoteServiceFixtures.DTBasicMVCApplicationFixture _fixture;

        public SpanEventsNotCreatedAttributesTest(RemoteServiceFixtures.DTBasicMVCApplicationFixture fixture, ITestOutputHelper output) : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;

            _fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var configPath = fixture.DestinationNewRelicConfigFilePath;
                    var configModifier = new NewRelicConfigModifier(configPath);
                    configModifier.SetOrDeleteSpanEventsEnabled(false);
                    configModifier.ForceTransactionTraces();
                },
                exerciseApplication: () =>
                {
                    _fixture.Initiate();
                }
            );
            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var spanEvents = _fixture.AgentLog.GetSpanEvents();

            NrAssert.Multiple(
                () => Assert.False(spanEvents.Any())
            );
        }
    }
}

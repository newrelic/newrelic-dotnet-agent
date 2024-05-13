// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Testing.Assertions;
using NewRelic.Agent.Tests.TestSerializationHelpers.Models;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.DistributedTracing
{
    [NetFrameworkTest]
    public class SpanEventsAreCreatedAttributesTest : NewRelicIntegrationTest<RemoteServiceFixtures.DTBasicMVCApplicationFixture>
    {
        private readonly RemoteServiceFixtures.DTBasicMVCApplicationFixture _fixture;

        public SpanEventsAreCreatedAttributesTest(RemoteServiceFixtures.DTBasicMVCApplicationFixture fixture, ITestOutputHelper output) : base(fixture)
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
            var expectedAttributes = new List<string>()
            {
                "type",
                "traceId",
                "guid",
                "transactionId",
                "sampled",
                "priority",
                "timestamp",
                "duration",
                "name",
                "category"
            };

            var expectedAgentAttributes = new List<string>()
            {
                "error.class",
                "error.message"
            };

            var unexpectedAttributes = new List<string>()
            {
                "parentId"
            };

            var spanEvents = _fixture.AgentLog.GetSpanEvents().ToList();
            var rootSpanEvent = spanEvents.FirstOrDefault(se => se.IntrinsicAttributes.ContainsKey("nr.entryPoint"));
            var nonRootSpanEvent = spanEvents.FirstOrDefault(se => se.IntrinsicAttributes.ContainsKey("parentId"));

            NrAssert.Multiple(
                () => Assertions.SpanEventHasAttributes(expectedAttributes, SpanEventAttributeType.Intrinsic, rootSpanEvent),
                () => Assertions.SpanEventDoesNotHaveAttributes(unexpectedAttributes, SpanEventAttributeType.Intrinsic, rootSpanEvent),
                () => Assert.Empty(rootSpanEvent.GetByType(SpanEventAttributeType.User)),
                () => Assertions.SpanEventHasAttributes(expectedAgentAttributes, SpanEventAttributeType.Agent, rootSpanEvent),
                () => Assert.Equal(rootSpanEvent.IntrinsicAttributes["guid"], nonRootSpanEvent.IntrinsicAttributes["parentId"])
            );
        }
    }
}

// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.Models;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;


namespace NewRelic.Agent.IntegrationTests.DistributedTracing
{
    [NetFrameworkTest]
    public class ReceiveDTAttributesTest : IClassFixture<RemoteServiceFixtures.DTBasicMVCApplicationFixture>
    {
        private readonly RemoteServiceFixtures.DTBasicMVCApplicationFixture _fixture;

        public ReceiveDTAttributesTest(RemoteServiceFixtures.DTBasicMVCApplicationFixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;

            _fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var configPath = fixture.DestinationNewRelicConfigFilePath;
                    var configModifier = new NewRelicConfigModifier(configPath);
                    configModifier.SetOrDeleteDistributedTraceEnabled(true);
                    configModifier.ForceTransactionTraces();
                    configModifier.SetOrDeleteSpanEventsEnabled(true);
                },
                exerciseApplication: () =>
                {
                    _fixture.ReceiveDTPayload();
                }
            );
            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var expectedAttributes = new List<string>()
            {
                "parent.type",
                "parent.app",
                "parent.account",
                "parent.transportType",
                "parent.transportDuration",
                "guid",
                "traceId",
                "priority",
                "sampled"
            };

            var transactionEventExpectedAttributes = new List<string>(expectedAttributes) { "parentId" };

            var transactionEvent = _fixture.AgentLog.GetTransactionEvents().FirstOrDefault();
            var errorEvent = _fixture.AgentLog.GetErrorEvents().First().Events.First();
            var errorTrace = _fixture.AgentLog.GetErrorTraces().FirstOrDefault();
            var transactionSample = _fixture.AgentLog.GetTransactionSamples()
                .FirstOrDefault(sample => sample.Path == @"WebTransaction/MVC/DistributedTracingController/ReceivePayload");

            NrAssert.Multiple(
                () => Assertions.TransactionEventHasAttributes(transactionEventExpectedAttributes, TransactionEventAttributeType.Intrinsic, transactionEvent),
                () => Assertions.ErrorEventHasAttributes(expectedAttributes, EventAttributeType.Intrinsic, errorEvent),
                () => Assertions.ErrorTraceHasAttributes(expectedAttributes, ErrorTraceAttributeType.Intrinsic, errorTrace),
                () => Assertions.TransactionTraceHasAttributes(expectedAttributes, TransactionTraceAttributeType.Intrinsic, transactionSample)
            );

            var parentIdKey = "parentId";
            var expectedParentId = "27856f70d3d314b7";

            var transactionEventParentId = transactionEvent.IntrinsicAttributes[parentIdKey];

            Assert.Equal(expectedParentId, transactionEventParentId);
        }
    }
}

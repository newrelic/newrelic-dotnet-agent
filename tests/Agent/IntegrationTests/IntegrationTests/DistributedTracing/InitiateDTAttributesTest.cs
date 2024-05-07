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
    public class InitiateDTAttributesTest : NewRelicIntegrationTest<RemoteServiceFixtures.DTBasicMVCApplicationFixture>
    {

        private readonly RemoteServiceFixtures.DTBasicMVCApplicationFixture _fixture;

        public InitiateDTAttributesTest(RemoteServiceFixtures.DTBasicMVCApplicationFixture fixture, ITestOutputHelper output) : base(fixture)
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
                "guid",
                "traceId",
                "priority",
                "sampled"
            };

            var transactionEvent = _fixture.AgentLog.GetTransactionEvents().FirstOrDefault();
            var errorEvent = _fixture.AgentLog.GetErrorEvents().FirstOrDefault();
            var errorTrace = _fixture.AgentLog.GetErrorTraces().FirstOrDefault();

            var transactionSample = _fixture.AgentLog.GetTransactionSamples()
                .FirstOrDefault(sample => sample.Path == @"WebTransaction/MVC/DistributedTracingController/Initiate");

            NrAssert.Multiple(
                () => Assertions.TransactionEventHasAttributes(expectedAttributes, TransactionEventAttributeType.Intrinsic, transactionEvent),
                () => Assertions.ErrorEventHasAttributes(expectedAttributes, EventAttributeType.Intrinsic, errorEvent),
                () => Assertions.ErrorTraceHasAttributes(expectedAttributes, ErrorTraceAttributeType.Intrinsic, errorTrace),
                () => Assertions.TransactionTraceHasAttributes(expectedAttributes, TransactionTraceAttributeType.Intrinsic, transactionSample)
            );
        }
    }
}

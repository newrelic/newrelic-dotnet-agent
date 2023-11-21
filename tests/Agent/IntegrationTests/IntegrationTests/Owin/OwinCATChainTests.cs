// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Linq;
using Xunit;
using Xunit.Abstractions;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.Models;
using System.Collections.Generic;
using NewRelic.Testing.Assertions;

namespace NewRelic.Agent.IntegrationTests.Owin
{
    [NetFrameworkTest]
    public class OwinCATChainTests : NewRelicIntegrationTest<OwinTracingChainFixture>
    {
        private readonly OwinTracingChainFixture _fixture;

        public OwinCATChainTests(OwinTracingChainFixture fixture, ITestOutputHelper output) : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var configModifier = new NewRelicConfigModifier(_fixture.DestinationNewRelicConfigFilePath);
                    configModifier.SetOrDeleteDistributedTraceEnabled(false);
                    configModifier.SetOrDeleteSpanEventsEnabled(false);
                    configModifier.SetLogLevel("all");

                    var environmentVariables = new Dictionary<string, string>();

                    _fixture.ReceiverApplication = _fixture.SetupReceiverApplication(isDistributedTracing: false, isWebApplication: false);
                    _fixture.ReceiverApplication.Start(string.Empty, environmentVariables, captureStandardOutput: true);
                },
                exerciseApplication: () =>
                {
                    _fixture.ExecuteTraceRequestChainHttpClient();

                    _fixture.AgentLog.WaitForLogLine(AgentLogFile.AnalyticsEventDataLogLineRegex, TimeSpan.FromMinutes(2));
                }
            );

            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var senderAppTxEvent = _fixture.AgentLog.GetTransactionEvents().FirstOrDefault();
            Assert.NotNull(senderAppTxEvent);

            var receiverAppTxEvent = _fixture.ReceiverAppAgentLog.GetTransactionEvents().FirstOrDefault();
            Assert.NotNull(receiverAppTxEvent);

            var expectedSenderAttributes = new List<string>
            {
                "nr.tripId",
                "nr.guid",
                "nr.pathHash"
            };

            var expectedReceiverAttributes = new List<string>
            {
                "nr.tripId",
                "nr.guid",
                "nr.pathHash",
                "nr.referringPathHash",
                "nr.referringTransactionGuid"
            };

            var unexpectedAttributes = new List<string>()
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

            NrAssert.Multiple(
                () => Assertions.TransactionEventHasAttributes(expectedSenderAttributes, TransactionEventAttributeType.Intrinsic, senderAppTxEvent),
                () => Assertions.TransactionEventHasAttributes(expectedReceiverAttributes, TransactionEventAttributeType.Intrinsic, receiverAppTxEvent),
                () => Assertions.TransactionEventDoesNotHaveAttributes(unexpectedAttributes, TransactionEventAttributeType.Intrinsic, receiverAppTxEvent),
                () => Assertions.TransactionEventDoesNotHaveAttributes(unexpectedAttributes, TransactionEventAttributeType.Intrinsic, senderAppTxEvent),
                () => Assert.Equal(senderAppTxEvent.IntrinsicAttributes["nr.tripId"], receiverAppTxEvent.IntrinsicAttributes["nr.tripId"]),
                () => Assert.Equal(senderAppTxEvent.IntrinsicAttributes["nr.guid"], receiverAppTxEvent.IntrinsicAttributes["nr.referringTransactionGuid"])
                );
        }
    }
}

// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTests.RemoteServiceFixtures.AwsLambda;
using NewRelic.Agent.Tests.TestSerializationHelpers.Models;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.AwsLambda.General
{
    [NetCoreTest]
    public abstract class AwsLambdaMultipleInvocationsTest<T> : NewRelicIntegrationTest<T> where T : LambdaCustomEventsTriggerFixtureBase
    {
        private readonly LambdaCustomEventsTriggerFixtureBase _fixture;

        protected AwsLambdaMultipleInvocationsTest(T fixture, ITestOutputHelper output)
            : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.Actions(
                exerciseApplication: () =>
                {
                    _fixture.EnqueueTrigger();
                    _fixture.EnqueueTrigger();
                    _fixture.EnqueueTrigger();
                    _fixture.AgentLog.WaitForLogLines(AgentLogBase.ServerlessPayloadLogLineRegex, TimeSpan.FromMinutes(1), 3);
                }
                );
            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var serverlessPayloads = _fixture.AgentLog.GetServerlessPayloads();

            Assert.Multiple(
                () => Assert.Equal(3, serverlessPayloads.Count()),
                () => Assert.All(serverlessPayloads, ValidateSingleServerlessPayload),
                // Only one of the invocations should be a coldstart
                () => Assert.Single(serverlessPayloads, IsColdStartPayload)
                );
        }

        private static void ValidateSingleServerlessPayload(ServerlessPayload serverlessPayload)
        {
            var customEventPayload = serverlessPayload.Telemetry.CustomEventsPayload;

            Assert.Multiple(
                () => Assert.Equal("OtherTransaction/Lambda/CustomEvent", serverlessPayload.Telemetry.TransactionEventsPayload.TransactionEvents.Single().IntrinsicAttributes["name"]),
                () => Assert.Single(customEventPayload.CustomEvents),
                () => Assert.Equal("TestLambdaCustomEvent", customEventPayload.CustomEvents[0].Header.Type),
                () => Assert.Single(customEventPayload.CustomEvents[0].Attributes),
                () => Assert.Equal(new KeyValuePair<string, object>("lambdaHandler", "CustomEventHandler"), customEventPayload.CustomEvents[0].Attributes.Single())
                );
        }

        private static bool IsColdStartPayload(ServerlessPayload serverlessPayload)
        {
            var transactionEvent = serverlessPayload.Telemetry.TransactionEventsPayload.TransactionEvents.Single();
            return transactionEvent.AgentAttributes.TryGetValue("aws.lambda.coldStart", out var coldStartAttributeValue)
                && (string)coldStartAttributeValue == "true";
        }
    }

    public class AwsLambdaMultipleInvocationsTestNet6 : AwsLambdaMultipleInvocationsTest<LambdaCustomEventsTriggerFixtureNet6>
    {
        public AwsLambdaMultipleInvocationsTestNet6(LambdaCustomEventsTriggerFixtureNet6 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    public class AwsLambdaMultipleInvocationsTestNet8 : AwsLambdaMultipleInvocationsTest<LambdaCustomEventsTriggerFixtureNet8>
    {
        public AwsLambdaMultipleInvocationsTestNet8(LambdaCustomEventsTriggerFixtureNet8 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }
}

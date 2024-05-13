// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTests.RemoteServiceFixtures.AwsLambda;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.AwsLambda.Custom
{
    [NetCoreTest]
    public abstract class AwsLambdaCustomEventsTest<T> : NewRelicIntegrationTest<T> where T : LambdaCustomEventsTriggerFixtureBase
    {
        private readonly LambdaCustomEventsTriggerFixtureBase _fixture;

        protected AwsLambdaCustomEventsTest(T fixture, ITestOutputHelper output)
            : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.Actions(
                exerciseApplication: () =>
                {
                    _fixture.EnqueueTrigger();
                    _fixture.AgentLog.WaitForLogLine(AgentLogBase.ServerlessPayloadLogLineRegex, TimeSpan.FromMinutes(1));
                }
                );
            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var serverlessPayload = _fixture.AgentLog.GetServerlessPayloads().Single();
            var customEventPayload = serverlessPayload.Telemetry.CustomEventsPayload;

            Assert.Multiple(
                () => Assert.Equal("OtherTransaction/Lambda/CustomEvent", serverlessPayload.Telemetry.TransactionEventsPayload.TransactionEvents.Single().IntrinsicAttributes["name"]),
                () => Assert.Single(customEventPayload.CustomEvents),
                () => Assert.Equal("TestLambdaCustomEvent", customEventPayload.CustomEvents[0].Header.Type),
                () => Assert.Single(customEventPayload.CustomEvents[0].Attributes),
                () => Assert.Equal(new KeyValuePair<string, object>("lambdaHandler", "CustomEventHandler"), customEventPayload.CustomEvents[0].Attributes.Single())
                );
        }
    }

    public class AwsLambdaCustomEventsTestNet6 : AwsLambdaCustomEventsTest<LambdaCustomEventsTriggerFixtureNet6>
    {
        public AwsLambdaCustomEventsTestNet6(LambdaCustomEventsTriggerFixtureNet6 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    public class AwsLambdaCustomEventsTestNet8 : AwsLambdaCustomEventsTest<LambdaCustomEventsTriggerFixtureNet8>
    {
        public AwsLambdaCustomEventsTestNet8(LambdaCustomEventsTriggerFixtureNet8 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }
}

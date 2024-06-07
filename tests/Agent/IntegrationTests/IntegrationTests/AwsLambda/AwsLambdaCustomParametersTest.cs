// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTests.RemoteServiceFixtures.AwsLambda;
using NewRelic.Agent.Tests.TestSerializationHelpers.Models;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.AwsLambda.Custom
{
    [NetCoreTest]
    public abstract class AwsLambdaCustomParametersTest<T> : NewRelicIntegrationTest<T> where T : LambdaCustomParametersFixtureBase
    {
        private readonly string _expectedTransactionName;
        private readonly LambdaCustomParametersFixtureBase _fixture;

        protected AwsLambdaCustomParametersTest(string expectedTransactionName, T fixture, ITestOutputHelper output)
            : base(fixture)
        {
            _expectedTransactionName = expectedTransactionName;

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
                () => Assert.Equal("$LATEST", serverlessPayload.Metadata.FunctionVersion),
                () => ValidateServerlessPayload(serverlessPayload),
                () => ValidateTraceHasNoParent(serverlessPayload)
                );
        }

        private void ValidateServerlessPayload(ServerlessPayload serverlessPayload)
        {
            var transactionEvent = serverlessPayload.Telemetry.TransactionEventsPayload.TransactionEvents.Single();

            var expectedMissingAgentAttributes = new[]
            {
                // These attributes are expected to come from an ILambdaContext parameter which this lambda does not have
                "aws.lambda.arn",
                "aws.requestId",
                // Unknown event types should omit the eventType attribute
                "aws.lambda.eventSource.eventType"
            };

            Assert.Equal(_expectedTransactionName, transactionEvent.IntrinsicAttributes["name"]);

            Assertions.TransactionEventDoesNotHaveAttributes(expectedMissingAgentAttributes, TransactionEventAttributeType.Agent, transactionEvent);
        }

        private void ValidateTraceHasNoParent(ServerlessPayload serverlessPayload)
        {
            var entrySpan = serverlessPayload.Telemetry.SpanEventsPayload.SpanEvents.Single(s => (string)s.IntrinsicAttributes["name"] == _expectedTransactionName);

            Assertions.SpanEventDoesNotHaveAttributes(["parentId"], SpanEventAttributeType.Intrinsic, entrySpan);
        }
    }

    public class AwsLambdaCustomParametersTestNet6 : AwsLambdaCustomParametersTest<LambdaCustomParametersFixtureNet6>
    {
        public AwsLambdaCustomParametersTestNet6(LambdaCustomParametersFixtureNet6 fixture, ITestOutputHelper output)
            : base("OtherTransaction/Lambda/StringInputAndOutput", fixture, output)
        {
        }
    }

    public class AwsLambdaCustomParametersAsyncTestNet6 : AwsLambdaCustomParametersTest<LambdaCustomParametersAsyncFixtureNet6>
    {
        public AwsLambdaCustomParametersAsyncTestNet6(LambdaCustomParametersAsyncFixtureNet6 fixture, ITestOutputHelper output)
            : base("OtherTransaction/Lambda/StringInputAndOutputAsync", fixture, output)
        {
        }
    }

    public class AwsLambdaCustomParametersTestNet8 : AwsLambdaCustomParametersTest<LambdaCustomParametersFixtureNet8>
    {
        public AwsLambdaCustomParametersTestNet8(LambdaCustomParametersFixtureNet8 fixture, ITestOutputHelper output)
            : base("OtherTransaction/Lambda/StringInputAndOutput", fixture, output)
        {
        }
    }

    public class AwsLambdaCustomParametersAsyncTestNet8 : AwsLambdaCustomParametersTest<LambdaCustomParametersAsyncFixtureNet8>
    {
        public AwsLambdaCustomParametersAsyncTestNet8(LambdaCustomParametersAsyncFixtureNet8 fixture, ITestOutputHelper output)
            : base("OtherTransaction/Lambda/StringInputAndOutputAsync", fixture, output)
        {
        }
    }
}

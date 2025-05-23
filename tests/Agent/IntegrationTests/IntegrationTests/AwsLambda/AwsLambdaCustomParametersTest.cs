// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTests.RemoteServiceFixtures.AwsLambda;
using NewRelic.Agent.Tests.TestSerializationHelpers.Models;
using Xunit;

namespace NewRelic.Agent.IntegrationTests.AwsLambda.Custom
{
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

    public class AwsLambdaCustomParametersTestCoreOldest : AwsLambdaCustomParametersTest<LambdaCustomParametersFixtureCoreOldest>
    {
        public AwsLambdaCustomParametersTestCoreOldest(LambdaCustomParametersFixtureCoreOldest fixture, ITestOutputHelper output)
            : base("OtherTransaction/Lambda/StringInputAndOutput", fixture, output)
        {
        }
    }

    public class AwsLambdaCustomParametersTestCoreLatest : AwsLambdaCustomParametersTest<LambdaCustomParametersFixtureCoreLatest>
    {
        public AwsLambdaCustomParametersTestCoreLatest(LambdaCustomParametersFixtureCoreLatest fixture, ITestOutputHelper output)
            : base("OtherTransaction/Lambda/StringInputAndOutput", fixture, output)
        {
        }
    }

    public class AwsLambdaCustomParametersAsyncTestCoreOldest : AwsLambdaCustomParametersTest<LambdaCustomParametersAsyncFixtureCoreOldest>
    {
        public AwsLambdaCustomParametersAsyncTestCoreOldest(LambdaCustomParametersAsyncFixtureCoreOldest fixture, ITestOutputHelper output)
            : base("OtherTransaction/Lambda/StringInputAndOutputAsync", fixture, output)
        {
        }
    }

    public class AwsLambdaCustomParametersAsyncTestCoreLatest : AwsLambdaCustomParametersTest<LambdaCustomParametersAsyncFixtureCoreLatest>
    {
        public AwsLambdaCustomParametersAsyncTestCoreLatest(LambdaCustomParametersAsyncFixtureCoreLatest fixture, ITestOutputHelper output)
            : base("OtherTransaction/Lambda/StringInputAndOutputAsync", fixture, output)
        {
        }
    }
}

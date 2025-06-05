// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTests.RemoteServiceFixtures.AwsLambda;
using NewRelic.Agent.Tests.TestSerializationHelpers.Models;
using Xunit;

namespace NewRelic.Agent.IntegrationTests.AwsLambda.General
{
    public abstract class AwsLambdaStreamParameterTest<T> : NewRelicIntegrationTest<T> where T : LambdaStreamParameterFixtureBase
    {
        private const string ExpectedTransactionName = "OtherTransaction/Function/StreamParameter";

        private readonly LambdaStreamParameterFixtureBase _fixture;

        protected AwsLambdaStreamParameterTest(T fixture, ITestOutputHelper output)
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
                () => Assert.Equal("$LATEST", serverlessPayload.Metadata.FunctionVersion),
                () => ValidateServerlessPayload(serverlessPayload),
                () => ValidateTraceHasNoParent(serverlessPayload)
                );
        }

        private void ValidateServerlessPayload(ServerlessPayload serverlessPayload)
        {
            var transactionEvent = serverlessPayload.Telemetry.TransactionEventsPayload.TransactionEvents.Single();

            var expectedAgentAttributes = new[]
            {
                "aws.lambda.arn",
                "aws.requestId"
            };

            var expectedMissingAgentAttributeValues = new[]
            {
                // Unknown event types should omit the eventType attribute
                "aws.lambda.eventSource.eventType"
            };

            Assert.Equal(ExpectedTransactionName, transactionEvent.IntrinsicAttributes["name"]);

            Assertions.TransactionEventHasAttributes(expectedAgentAttributes, TransactionEventAttributeType.Agent, transactionEvent);
            Assertions.TransactionEventDoesNotHaveAttributes(expectedMissingAgentAttributeValues, TransactionEventAttributeType.Agent, transactionEvent);
        }

        private void ValidateTraceHasNoParent(ServerlessPayload serverlessPayload)
        {
            var entrySpan = serverlessPayload.Telemetry.SpanEventsPayload.SpanEvents.Single(s => (string)s.IntrinsicAttributes["name"] == ExpectedTransactionName);

            Assertions.SpanEventDoesNotHaveAttributes(["parentId"], SpanEventAttributeType.Intrinsic, entrySpan);
        }
    }

    public class AwsLambdaStreamParameterTestCoreOldest : AwsLambdaStreamParameterTest<LambdaStreamParameterFixtureCoreOldest>
    {
        public AwsLambdaStreamParameterTestCoreOldest(LambdaStreamParameterFixtureCoreOldest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    public class AwsLambdaStreamParameterTestCoreLatest : AwsLambdaStreamParameterTest<LambdaStreamParameterFixtureCoreLatest>
    {
        public AwsLambdaStreamParameterTestCoreLatest(LambdaStreamParameterFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }
}

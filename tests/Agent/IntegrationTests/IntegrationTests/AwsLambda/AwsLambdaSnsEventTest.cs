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

namespace NewRelic.Agent.IntegrationTests.AwsLambda.Sns
{
    [NetCoreTest]
    public abstract class AwsLambdaSnsEventTest<T> : NewRelicIntegrationTest<T> where T : LambdaSnsEventTriggerFixtureBase
    {
        private readonly LambdaSnsEventTriggerFixtureBase _fixture;
        private const string TestTraceId = "74be672b84ddc4e4b28be285632bbc0a";
        private const string TestParentSpanId = "27ddd2d8890283b4";
        private const string ExpectedTransactionName = "OtherTransaction/Lambda/SnsHandler";

        protected AwsLambdaSnsEventTest(T fixture, ITestOutputHelper output)
            : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.Actions(
                exerciseApplication: () =>
                {
                    _fixture.EnqueueSnsEvent();
                    _fixture.EnqueueSnsEventWithDTHeaders(TestTraceId, TestParentSpanId);
                    _fixture.AgentLog.WaitForLogLines(AgentLogBase.ServerlessPayloadLogLineRegex, TimeSpan.FromMinutes(1), 2);
                }
                );
            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var serverlessPayloads = _fixture.AgentLog.GetServerlessPayloads().ToList();

            Assert.Multiple(
                () => Assert.Equal(2, serverlessPayloads.Count),
                () => Assert.All(serverlessPayloads, ValidateServerlessPayload),
                () => ValidateTraceHasNoParent(serverlessPayloads[0]),
                () => ValidateTraceHasParent(serverlessPayloads[1])
                );
        }

        private static void ValidateServerlessPayload(ServerlessPayload serverlessPayload)
        {
            var transactionEvent = serverlessPayload.Telemetry.TransactionEventsPayload.TransactionEvents.Single();

            var expectedAgentAttributes = new[]
            {
                "aws.lambda.arn",
                "aws.requestId"
            };

            var expectedAgentAttributeValues = new Dictionary<string, object>
            {
                { "aws.lambda.eventSource.arn", "arn:{partition}:sns:EXAMPLE1" },
                { "aws.lambda.eventSource.eventType", "sns" },
                { "aws.lambda.eventSource.length", 1 },
                { "aws.lambda.eventSource.messageId", "95df01b4-ee98-5cb9-9903-4c221d41eb5e" },
                { "aws.lambda.eventSource.timestamp", "1/1/1970 12:00:00 AM" },
                { "aws.lambda.eventSource.topicArn", "arn:{partition}:sns:EXAMPLE2" },
                { "aws.lambda.eventSource.type", "Notification" }
            };

            Assert.Equal(ExpectedTransactionName, transactionEvent.IntrinsicAttributes["name"]);

            Assertions.TransactionEventHasAttributes(expectedAgentAttributes, TransactionEventAttributeType.Agent, transactionEvent);
            Assertions.TransactionEventHasAttributes(expectedAgentAttributeValues, TransactionEventAttributeType.Agent, transactionEvent);
        }

        private static void ValidateTraceHasNoParent(ServerlessPayload serverlessPayload)
        {
            var entrySpan = serverlessPayload.Telemetry.SpanEventsPayload.SpanEvents.Single(s => (string)s.IntrinsicAttributes["name"] == ExpectedTransactionName);

            Assertions.SpanEventDoesNotHaveAttributes(["parentId"], SpanEventAttributeType.Intrinsic, entrySpan);
        }

        private static void ValidateTraceHasParent(ServerlessPayload serverlessPayload)
        {
            var entrySpan = serverlessPayload.Telemetry.SpanEventsPayload.SpanEvents.Single(s => (string)s.IntrinsicAttributes["name"] == ExpectedTransactionName);

            var expectedAttributeValues = new Dictionary<string, object>
            {
                { "traceId", TestTraceId },
                { "parentId", TestParentSpanId }
            };

            Assertions.SpanEventHasAttributes(expectedAttributeValues, SpanEventAttributeType.Intrinsic, entrySpan);
        }
    }

    public class AwsLambdaSnsEventTestNet6 : AwsLambdaSnsEventTest<LambdaSnsEventTriggerFixtureNet6>
    {
        public AwsLambdaSnsEventTestNet6(LambdaSnsEventTriggerFixtureNet6 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    public class AwsLambdaSnsEventTestNet8 : AwsLambdaSnsEventTest<LambdaSnsEventTriggerFixtureNet8>
    {
        public AwsLambdaSnsEventTestNet8(LambdaSnsEventTriggerFixtureNet8 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }
}

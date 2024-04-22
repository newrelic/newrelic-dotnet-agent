// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.Models;
using NewRelic.Agent.IntegrationTests.RemoteServiceFixtures.AwsLambda;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.AwsLambda
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
                () => Assert.Single(serverlessPayloads, ValidateTraceHasNoParent),
                () => Assert.Single(serverlessPayloads, ValidateTraceHasParent)
                );
        }

        private static void ValidateServerlessPayload(ServerlessPayload serverlessPayload)
        {
            var transactionEvent = serverlessPayload.Telemetry.TransactionEventsPayload.TransactionEvents.Single();

            Assert.Multiple(
                () => Assert.Equal(ExpectedTransactionName, transactionEvent.IntrinsicAttributes["name"]),
                () => Assert.False(string.IsNullOrWhiteSpace((string)transactionEvent.AgentAttributes["aws.lambda.arn"])),
                () => Assert.Equal("arn:{partition}:sns:EXAMPLE1", transactionEvent.AgentAttributes["aws.lambda.eventSource.arn"]),
                () => Assert.Equal("sns", transactionEvent.AgentAttributes["aws.lambda.eventSource.eventType"]),
                () => Assert.False(string.IsNullOrWhiteSpace((string)transactionEvent.AgentAttributes["aws.requestId"])),
                () => Assert.Equal((long)1, transactionEvent.AgentAttributes["aws.lambda.eventSource.length"]), // Json.NET deserializes to long by default
                () => Assert.Equal("95df01b4-ee98-5cb9-9903-4c221d41eb5e", transactionEvent.AgentAttributes["aws.lambda.eventSource.messageId"]),
                () => Assert.Equal("1/1/1970 12:00:00 AM", transactionEvent.AgentAttributes["aws.lambda.eventSource.timestamp"]),
                () => Assert.Equal("arn:{partition}:sns:EXAMPLE2", transactionEvent.AgentAttributes["aws.lambda.eventSource.topicArn"]),
                () => Assert.Equal("Notification", transactionEvent.AgentAttributes["aws.lambda.eventSource.type"])
                );
        }

        private static bool ValidateTraceHasNoParent(ServerlessPayload serverlessPayload)
        {
            return !ValidateTraceHasParent(serverlessPayload);
        }

        private static bool ValidateTraceHasParent(ServerlessPayload serverlessPayload)
        {
            var entrySpan = serverlessPayload.Telemetry.SpanEventsPayload.SpanEvents.Single(s => (string)s.IntrinsicAttributes["name"] == ExpectedTransactionName);

            return entrySpan.IntrinsicAttributes.TryGetValue("traceId", out var traceId) && (string)traceId == TestTraceId
                && entrySpan.IntrinsicAttributes.TryGetValue("parentId", out var parentId) && (string)parentId == TestParentSpanId;
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

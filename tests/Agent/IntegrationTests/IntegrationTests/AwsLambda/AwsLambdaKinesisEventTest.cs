// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTests.RemoteServiceFixtures.AwsLambda;
using NewRelic.Agent.Tests.TestSerializationHelpers.Models;
using Xunit;

namespace NewRelic.Agent.IntegrationTests.AwsLambda.Kinesis
{
    public abstract class AwsLambdaKinesisEventTest<T> : NewRelicIntegrationTest<T> where T : LambdaKinesisEventTriggerFixtureBase
    {
        private readonly LambdaKinesisEventTriggerFixtureBase _fixture;
        private readonly string _expectedTransactionName;

        protected AwsLambdaKinesisEventTest(T fixture, ITestOutputHelper output, string expectedTransactionName)
            : base(fixture)
        {
            _expectedTransactionName = expectedTransactionName;

            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.Actions(
                exerciseApplication: () =>
                {
                    _fixture.EnqueueEvent();
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
                () => Assert.Single(serverlessPayloads),
                () => ValidateServerlessPayload(serverlessPayloads[0]),
                () => Assert.All(serverlessPayloads, ValidateTraceHasNoParent)
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

            var expectedAgentAttributeValues = new Dictionary<string, object>
            {
                { "aws.lambda.eventSource.arn", "arn:aws:kinesis:us-east-2:111122223333:stream/lambda-stream" },
                { "aws.lambda.eventSource.eventType", "kinesis" },
                { "aws.lambda.eventSource.length", 1 },
                { "aws.lambda.eventSource.region", "us-east-2" }
            };

            Assert.Equal(_expectedTransactionName, transactionEvent.IntrinsicAttributes["name"]);

            Assertions.TransactionEventHasAttributes(expectedAgentAttributes, TransactionEventAttributeType.Agent, transactionEvent);
            Assertions.TransactionEventHasAttributes(expectedAgentAttributeValues, TransactionEventAttributeType.Agent, transactionEvent);
        }

        private void ValidateTraceHasNoParent(ServerlessPayload serverlessPayload)
        {
            var entrySpan = serverlessPayload.Telemetry.SpanEventsPayload.SpanEvents.Single(s => (string)s.IntrinsicAttributes["name"] == _expectedTransactionName);

            Assertions.SpanEventDoesNotHaveAttributes(["parentId"], SpanEventAttributeType.Intrinsic, entrySpan);
        }
    }

    public class AwsLambdaKinesisEventTestCoreOldest : AwsLambdaKinesisEventTest<LambdaKinesisEventTriggerFixtureCoreOldest>
    {
        public AwsLambdaKinesisEventTestCoreOldest(LambdaKinesisEventTriggerFixtureCoreOldest fixture, ITestOutputHelper output)
            : base(fixture, output, "OtherTransaction/Lambda/KinesisEvent")
        {
        }
    }

    public class AwsLambdaAsyncKinesisEventTestCoreOldest : AwsLambdaKinesisEventTest<AsyncLambdaKinesisEventTriggerFixtureCoreOldest>
    {
        public AwsLambdaAsyncKinesisEventTestCoreOldest(AsyncLambdaKinesisEventTriggerFixtureCoreOldest fixture, ITestOutputHelper output)
            : base(fixture, output, "OtherTransaction/Lambda/KinesisEventAsync")
        {
        }
    }

    public class AwsLambdaKinesisEventTestCoreLatest : AwsLambdaKinesisEventTest<LambdaKinesisEventTriggerFixtureCoreLatest>
    {
        public AwsLambdaKinesisEventTestCoreLatest(LambdaKinesisEventTriggerFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, "OtherTransaction/Lambda/KinesisEvent")
        {
        }
    }

    public class AwsLambdaAsyncKinesisEventTestCoreLatest : AwsLambdaKinesisEventTest<AsyncLambdaKinesisEventTriggerFixtureCoreLatest>
    {
        public AwsLambdaAsyncKinesisEventTestCoreLatest(AsyncLambdaKinesisEventTriggerFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, "OtherTransaction/Lambda/KinesisEventAsync")
        {
        }
    }

    public class AwsLambdaKinesisTimeWindowEventTestCoreOldest : AwsLambdaKinesisEventTest<LambdaKinesisTimeWindowEventTriggerFixtureCoreOldest>
    {
        public AwsLambdaKinesisTimeWindowEventTestCoreOldest(LambdaKinesisTimeWindowEventTriggerFixtureCoreOldest fixture, ITestOutputHelper output)
            : base(fixture, output, "OtherTransaction/Lambda/KinesisTimeWindowEvent")
        {
        }
    }

    public class AwsLambdaAsyncKinesisTimeWindowEventTestCoreOldest : AwsLambdaKinesisEventTest<AsyncLambdaKinesisTimeWindowEventTriggerFixtureCoreOldest>
    {
        public AwsLambdaAsyncKinesisTimeWindowEventTestCoreOldest(AsyncLambdaKinesisTimeWindowEventTriggerFixtureCoreOldest fixture, ITestOutputHelper output)
            : base(fixture, output, "OtherTransaction/Lambda/KinesisTimeWindowEventAsync")
        {
        }
    }

    public class AwsLambdaKinesisTimeWindowEventTestCoreLatest : AwsLambdaKinesisEventTest<LambdaKinesisTimeWindowEventTriggerFixtureCoreLatest>
    {
        public AwsLambdaKinesisTimeWindowEventTestCoreLatest(LambdaKinesisTimeWindowEventTriggerFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, "OtherTransaction/Lambda/KinesisTimeWindowEvent")
        {
        }
    }

    public class AwsLambdaAsyncKinesisTimeWindowEventTestCoreLatest : AwsLambdaKinesisEventTest<AsyncLambdaKinesisTimeWindowEventTriggerFixtureCoreLatest>
    {
        public AwsLambdaAsyncKinesisTimeWindowEventTestCoreLatest(AsyncLambdaKinesisTimeWindowEventTriggerFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, "OtherTransaction/Lambda/KinesisTimeWindowEventAsync")
        {
        }
    }
}

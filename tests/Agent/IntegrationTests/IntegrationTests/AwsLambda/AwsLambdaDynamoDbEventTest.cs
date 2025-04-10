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

namespace NewRelic.Agent.IntegrationTests.AwsLambda.DynamoDb
{
    public abstract class AwsLambdaDynamoDbEventTest<T> : NewRelicIntegrationTest<T> where T : LambdaDynamoDbEventTriggerFixtureBase
    {
        private readonly LambdaDynamoDbEventTriggerFixtureBase _fixture;
        private readonly string _expectedTransactionName;

        protected AwsLambdaDynamoDbEventTest(T fixture, ITestOutputHelper output, string expectedTransactionName)
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
                { "aws.lambda.eventSource.arn", "arn:{partition}:dynamodb:{region}:account-id:table/ExampleTableWithStream/stream/2015-06-27T00:48:05.899" },
                { "aws.lambda.eventSource.eventType", "dynamo_streams" }
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

    public class AwsLambdaDynamoDbEventTestCoreOldest : AwsLambdaDynamoDbEventTest<LambdaDynamoDbEventTriggerFixtureCoreOldest>
    {
        public AwsLambdaDynamoDbEventTestCoreOldest(LambdaDynamoDbEventTriggerFixtureCoreOldest fixture, ITestOutputHelper output)
            : base(fixture, output, "OtherTransaction/Lambda/DynamoDbEvent")
        {
        }
    }

    public class AwsLambdaDynamoDbEventTestCoreLatest : AwsLambdaDynamoDbEventTest<LambdaDynamoDbEventTriggerFixtureCoreLatest>
    {
        public AwsLambdaDynamoDbEventTestCoreLatest(LambdaDynamoDbEventTriggerFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, "OtherTransaction/Lambda/DynamoDbEvent")
        {
        }
    }

    public class AwsLambdaAsyncDynamoDbEventTestCoreOldest : AwsLambdaDynamoDbEventTest<AsyncLambdaDynamoDbEventTriggerFixtureCoreOldest>
    {
        public AwsLambdaAsyncDynamoDbEventTestCoreOldest(AsyncLambdaDynamoDbEventTriggerFixtureCoreOldest fixture, ITestOutputHelper output)
            : base(fixture, output, "OtherTransaction/Lambda/DynamoDbEventAsync")
        {
        }
    }

    public class AwsLambdaAsyncDynamoDbEventTestCoreLatest : AwsLambdaDynamoDbEventTest<AsyncLambdaDynamoDbEventTriggerFixtureCoreLatest>
    {
        public AwsLambdaAsyncDynamoDbEventTestCoreLatest(AsyncLambdaDynamoDbEventTriggerFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, "OtherTransaction/Lambda/DynamoDbEventAsync")
        {
        }
    }

    public class AwsLambdaDynamoDbTimeWindowEventTestCoreOldest : AwsLambdaDynamoDbEventTest<LambdaDynamoDbTimeWindowEventTriggerFixtureCoreOldest>
    {
        public AwsLambdaDynamoDbTimeWindowEventTestCoreOldest(LambdaDynamoDbTimeWindowEventTriggerFixtureCoreOldest fixture, ITestOutputHelper output)
            : base(fixture, output, "OtherTransaction/Lambda/DynamoDbTimeWindowEvent")
        {
        }
    }

    public class AwsLambdaDynamoDbTimeWindowEventTestCoreLatest : AwsLambdaDynamoDbEventTest<LambdaDynamoDbTimeWindowEventTriggerFixtureCoreLatest>
    {
        public AwsLambdaDynamoDbTimeWindowEventTestCoreLatest(LambdaDynamoDbTimeWindowEventTriggerFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, "OtherTransaction/Lambda/DynamoDbTimeWindowEvent")
        {
        }
    }

    public class AwsLambdaAsyncDynamoDbTimeWindowEventTestCoreOldest : AwsLambdaDynamoDbEventTest<AsyncLambdaDynamoDbTimeWindowEventTriggerFixtureCoreOldest>
    {
        public AwsLambdaAsyncDynamoDbTimeWindowEventTestCoreOldest(AsyncLambdaDynamoDbTimeWindowEventTriggerFixtureCoreOldest fixture, ITestOutputHelper output)
            : base(fixture, output, "OtherTransaction/Lambda/DynamoDbTimeWindowEventAsync")
        {
        }
    }

    public class AwsLambdaAsyncDynamoDbTimeWindowEventTestCoreLatest : AwsLambdaDynamoDbEventTest<AsyncLambdaDynamoDbTimeWindowEventTriggerFixtureCoreLatest>
    {
        public AwsLambdaAsyncDynamoDbTimeWindowEventTestCoreLatest(AsyncLambdaDynamoDbTimeWindowEventTriggerFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, "OtherTransaction/Lambda/DynamoDbTimeWindowEventAsync")
        {
        }
    }
}

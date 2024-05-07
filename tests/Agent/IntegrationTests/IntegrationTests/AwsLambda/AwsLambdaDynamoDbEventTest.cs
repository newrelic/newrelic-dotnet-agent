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
    [NetCoreTest]
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

    public class AwsLambdaDynamoDbEventTestNet6 : AwsLambdaDynamoDbEventTest<LambdaDynamoDbEventTriggerFixtureNet6>
    {
        public AwsLambdaDynamoDbEventTestNet6(LambdaDynamoDbEventTriggerFixtureNet6 fixture, ITestOutputHelper output)
            : base(fixture, output, "OtherTransaction/Lambda/DynamoDbEvent")
        {
        }
    }

    public class AwsLambdaAsyncDynamoDbEventTestNet6 : AwsLambdaDynamoDbEventTest<AsyncLambdaDynamoDbEventTriggerFixtureNet6>
    {
        public AwsLambdaAsyncDynamoDbEventTestNet6(AsyncLambdaDynamoDbEventTriggerFixtureNet6 fixture, ITestOutputHelper output)
            : base(fixture, output, "OtherTransaction/Lambda/DynamoDbEventAsync")
        {
        }
    }

    public class AwsLambdaDynamoDbEventTestNet8 : AwsLambdaDynamoDbEventTest<LambdaDynamoDbEventTriggerFixtureNet8>
    {
        public AwsLambdaDynamoDbEventTestNet8(LambdaDynamoDbEventTriggerFixtureNet8 fixture, ITestOutputHelper output)
            : base(fixture, output, "OtherTransaction/Lambda/DynamoDbEvent")
        {
        }
    }

    public class AwsLambdaAsyncDynamoDbEventTestNet8 : AwsLambdaDynamoDbEventTest<AsyncLambdaDynamoDbEventTriggerFixtureNet8>
    {
        public AwsLambdaAsyncDynamoDbEventTestNet8(AsyncLambdaDynamoDbEventTriggerFixtureNet8 fixture, ITestOutputHelper output)
            : base(fixture, output, "OtherTransaction/Lambda/DynamoDbEventAsync")
        {
        }
    }

    public class AwsLambdaDynamoDbTimeWindowEventTestNet6 : AwsLambdaDynamoDbEventTest<LambdaDynamoDbTimeWindowEventTriggerFixtureNet6>
    {
        public AwsLambdaDynamoDbTimeWindowEventTestNet6(LambdaDynamoDbTimeWindowEventTriggerFixtureNet6 fixture, ITestOutputHelper output)
            : base(fixture, output, "OtherTransaction/Lambda/DynamoDbTimeWindowEvent")
        {
        }
    }

    public class AwsLambdaAsyncDynamoDbTimeWindowEventTestNet6 : AwsLambdaDynamoDbEventTest<AsyncLambdaDynamoDbTimeWindowEventTriggerFixtureNet6>
    {
        public AwsLambdaAsyncDynamoDbTimeWindowEventTestNet6(AsyncLambdaDynamoDbTimeWindowEventTriggerFixtureNet6 fixture, ITestOutputHelper output)
            : base(fixture, output, "OtherTransaction/Lambda/DynamoDbTimeWindowEventAsync")
        {
        }
    }

    public class AwsLambdaDynamoDbTimeWindowEventTestNet8 : AwsLambdaDynamoDbEventTest<LambdaDynamoDbTimeWindowEventTriggerFixtureNet8>
    {
        public AwsLambdaDynamoDbTimeWindowEventTestNet8(LambdaDynamoDbTimeWindowEventTriggerFixtureNet8 fixture, ITestOutputHelper output)
            : base(fixture, output, "OtherTransaction/Lambda/DynamoDbTimeWindowEvent")
        {
        }
    }

    public class AwsLambdaAsyncDynamoDbTimeWindowEventTestNet8 : AwsLambdaDynamoDbEventTest<AsyncLambdaDynamoDbTimeWindowEventTriggerFixtureNet8>
    {
        public AwsLambdaAsyncDynamoDbTimeWindowEventTestNet8(AsyncLambdaDynamoDbTimeWindowEventTriggerFixtureNet8 fixture, ITestOutputHelper output)
            : base(fixture, output, "OtherTransaction/Lambda/DynamoDbTimeWindowEventAsync")
        {
        }
    }
}

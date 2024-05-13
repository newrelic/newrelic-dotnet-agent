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

namespace NewRelic.Agent.IntegrationTests.AwsLambda.Kinesis
{
    [NetCoreTest]
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

    public class AwsLambdaKinesisEventTestNet6 : AwsLambdaKinesisEventTest<LambdaKinesisEventTriggerFixtureNet6>
    {
        public AwsLambdaKinesisEventTestNet6(LambdaKinesisEventTriggerFixtureNet6 fixture, ITestOutputHelper output)
            : base(fixture, output, "OtherTransaction/Lambda/KinesisEvent")
        {
        }
    }

    public class AwsLambdaAsyncKinesisEventTestNet6 : AwsLambdaKinesisEventTest<AsyncLambdaKinesisEventTriggerFixtureNet6>
    {
        public AwsLambdaAsyncKinesisEventTestNet6(AsyncLambdaKinesisEventTriggerFixtureNet6 fixture, ITestOutputHelper output)
            : base(fixture, output, "OtherTransaction/Lambda/KinesisEventAsync")
        {
        }
    }

    public class AwsLambdaKinesisEventTestNet8 : AwsLambdaKinesisEventTest<LambdaKinesisEventTriggerFixtureNet8>
    {
        public AwsLambdaKinesisEventTestNet8(LambdaKinesisEventTriggerFixtureNet8 fixture, ITestOutputHelper output)
            : base(fixture, output, "OtherTransaction/Lambda/KinesisEvent")
        {
        }
    }

    public class AwsLambdaAsyncKinesisEventTestNet8 : AwsLambdaKinesisEventTest<AsyncLambdaKinesisEventTriggerFixtureNet8>
    {
        public AwsLambdaAsyncKinesisEventTestNet8(AsyncLambdaKinesisEventTriggerFixtureNet8 fixture, ITestOutputHelper output)
            : base(fixture, output, "OtherTransaction/Lambda/KinesisEventAsync")
        {
        }
    }

    public class AwsLambdaKinesisTimeWindowEventTestNet6 : AwsLambdaKinesisEventTest<LambdaKinesisTimeWindowEventTriggerFixtureNet6>
    {
        public AwsLambdaKinesisTimeWindowEventTestNet6(LambdaKinesisTimeWindowEventTriggerFixtureNet6 fixture, ITestOutputHelper output)
            : base(fixture, output, "OtherTransaction/Lambda/KinesisTimeWindowEvent")
        {
        }
    }

    public class AwsLambdaAsyncKinesisTimeWindowEventTestNet6 : AwsLambdaKinesisEventTest<AsyncLambdaKinesisTimeWindowEventTriggerFixtureNet6>
    {
        public AwsLambdaAsyncKinesisTimeWindowEventTestNet6(AsyncLambdaKinesisTimeWindowEventTriggerFixtureNet6 fixture, ITestOutputHelper output)
            : base(fixture, output, "OtherTransaction/Lambda/KinesisTimeWindowEventAsync")
        {
        }
    }

    public class AwsLambdaKinesisTimeWindowEventTestNet8 : AwsLambdaKinesisEventTest<LambdaKinesisTimeWindowEventTriggerFixtureNet8>
    {
        public AwsLambdaKinesisTimeWindowEventTestNet8(LambdaKinesisTimeWindowEventTriggerFixtureNet8 fixture, ITestOutputHelper output)
            : base(fixture, output, "OtherTransaction/Lambda/KinesisTimeWindowEvent")
        {
        }
    }

    public class AwsLambdaAsyncKinesisTimeWindowEventTestNet8 : AwsLambdaKinesisEventTest<AsyncLambdaKinesisTimeWindowEventTriggerFixtureNet8>
    {
        public AwsLambdaAsyncKinesisTimeWindowEventTestNet8(AsyncLambdaKinesisTimeWindowEventTriggerFixtureNet8 fixture, ITestOutputHelper output)
            : base(fixture, output, "OtherTransaction/Lambda/KinesisTimeWindowEventAsync")
        {
        }
    }
}

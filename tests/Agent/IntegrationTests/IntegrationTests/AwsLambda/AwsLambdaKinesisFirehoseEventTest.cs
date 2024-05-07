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
    public abstract class AwsLambdaKinesisFirehoseEventTest<T> : NewRelicIntegrationTest<T> where T : LambdaKinesisFirehoseEventTriggerFixtureBase
    {
        private readonly LambdaKinesisFirehoseEventTriggerFixtureBase _fixture;
        private readonly string _expectedTransactionName;

        protected AwsLambdaKinesisFirehoseEventTest(T fixture, ITestOutputHelper output, string expectedTransactionName)
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
                { "aws.lambda.eventSource.arn", "aws:lambda:events" },
                { "aws.lambda.eventSource.eventType", "firehose" },
                { "aws.lambda.eventSource.length", 2 },
                { "aws.lambda.eventSource.region", "us-west-2" }
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

    public class AwsLambdaKinesisFirehoseEventTestNet6 : AwsLambdaKinesisFirehoseEventTest<LambdaKinesisFirehoseEventTriggerFixtureNet6>
    {
        public AwsLambdaKinesisFirehoseEventTestNet6(LambdaKinesisFirehoseEventTriggerFixtureNet6 fixture, ITestOutputHelper output)
            : base(fixture, output, "OtherTransaction/Lambda/KinesisFirehoseEvent")
        {
        }
    }

    public class AwsLambdaAsyncKinesisFirehoseEventTestNet6 : AwsLambdaKinesisFirehoseEventTest<AsyncLambdaKinesisFirehoseEventTriggerFixtureNet6>
    {
        public AwsLambdaAsyncKinesisFirehoseEventTestNet6(AsyncLambdaKinesisFirehoseEventTriggerFixtureNet6 fixture, ITestOutputHelper output)
            : base(fixture, output, "OtherTransaction/Lambda/KinesisFirehoseEventAsync")
        {
        }
    }

    public class AwsLambdaKinesisFirehoseEventTestNet8 : AwsLambdaKinesisFirehoseEventTest<LambdaKinesisFirehoseEventTriggerFixtureNet8>
    {
        public AwsLambdaKinesisFirehoseEventTestNet8(LambdaKinesisFirehoseEventTriggerFixtureNet8 fixture, ITestOutputHelper output)
            : base(fixture, output, "OtherTransaction/Lambda/KinesisFirehoseEvent")
        {
        }
    }

    public class AwsLambdaAsyncKinesisFirehoseEventTestNet8 : AwsLambdaKinesisFirehoseEventTest<AsyncLambdaKinesisFirehoseEventTriggerFixtureNet8>
    {
        public AwsLambdaAsyncKinesisFirehoseEventTestNet8(AsyncLambdaKinesisFirehoseEventTriggerFixtureNet8 fixture, ITestOutputHelper output)
            : base(fixture, output, "OtherTransaction/Lambda/KinesisFirehoseEventAsync")
        {
        }
    }
}

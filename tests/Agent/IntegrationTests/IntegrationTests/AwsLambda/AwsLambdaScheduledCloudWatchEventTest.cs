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

namespace NewRelic.Agent.IntegrationTests.AwsLambda.CloudWatch
{
    [NetCoreTest]
    public abstract class AwsLambdaScheduledCloudWatchEventTest<T> : NewRelicIntegrationTest<T> where T : LambdaScheduledCloudWatchEventTriggerFixtureBase
    {
        private readonly LambdaScheduledCloudWatchEventTriggerFixtureBase _fixture;
        private readonly string _expectedTransactionName;

        protected AwsLambdaScheduledCloudWatchEventTest(T fixture, ITestOutputHelper output, string expectedTransactionName)
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
                { "aws.lambda.eventSource.arn", "arn:aws:events:us-east-2:123456789012:rule/my-schedule" },
                { "aws.lambda.eventSource.eventType", "cloudWatch_scheduled" },
                { "aws.lambda.eventSource.account", "123456789012" },
                { "aws.lambda.eventSource.id", "cdc73f9d-aea9-11e3-9d5a-835b769c0d9c" },
                { "aws.lambda.eventSource.region", "us-east-2" },
                { "aws.lambda.eventSource.resource", "arn:aws:events:us-east-2:123456789012:rule/my-schedule" },
                { "aws.lambda.eventSource.time", "3/1/2019 1:23:45 AM" }
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

    public class AwsLambdaScheduledCloudWatchEventTestNet6 : AwsLambdaScheduledCloudWatchEventTest<LambdaScheduledCloudWatchEventTriggerFixtureNet6>
    {
        public AwsLambdaScheduledCloudWatchEventTestNet6(LambdaScheduledCloudWatchEventTriggerFixtureNet6 fixture, ITestOutputHelper output)
            : base(fixture, output, "OtherTransaction/Lambda/ScheduledCloudWatchEvent")
        {
        }
    }

    public class AwsLambdaAsyncScheduledCloudWatchEventTestNet6 : AwsLambdaScheduledCloudWatchEventTest<AsyncLambdaScheduledCloudWatchEventTriggerFixtureNet6>
    {
        public AwsLambdaAsyncScheduledCloudWatchEventTestNet6(AsyncLambdaScheduledCloudWatchEventTriggerFixtureNet6 fixture, ITestOutputHelper output)
            : base(fixture, output, "OtherTransaction/Lambda/ScheduledCloudWatchEventAsync")
        {
        }
    }

    public class AwsLambdaScheduledCloudWatchEventTestNet8 : AwsLambdaScheduledCloudWatchEventTest<LambdaScheduledCloudWatchEventTriggerFixtureNet8>
    {
        public AwsLambdaScheduledCloudWatchEventTestNet8(LambdaScheduledCloudWatchEventTriggerFixtureNet8 fixture, ITestOutputHelper output)
            : base(fixture, output, "OtherTransaction/Lambda/ScheduledCloudWatchEvent")
        {
        }
    }

    public class AwsLambdaAsyncScheduledCloudWatchEventTestNet8 : AwsLambdaScheduledCloudWatchEventTest<AsyncLambdaScheduledCloudWatchEventTriggerFixtureNet8>
    {
        public AwsLambdaAsyncScheduledCloudWatchEventTestNet8(AsyncLambdaScheduledCloudWatchEventTriggerFixtureNet8 fixture, ITestOutputHelper output)
            : base(fixture, output, "OtherTransaction/Lambda/ScheduledCloudWatchEventAsync")
        {
        }
    }
}

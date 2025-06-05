// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTests.RemoteServiceFixtures.AwsLambda;
using NewRelic.Agent.Tests.TestSerializationHelpers.Models;
using Xunit;

namespace NewRelic.Agent.IntegrationTests.AwsLambda.S3
{
    public abstract class AwsLambdaS3EventTest<T> : NewRelicIntegrationTest<T> where T : LambdaS3EventTriggerFixtureBase
    {
        private readonly LambdaS3EventTriggerFixtureBase _fixture;
        private readonly string _expectedTransactionName;

        protected AwsLambdaS3EventTest(T fixture, ITestOutputHelper output, string expectedTransactionName)
            : base(fixture)
        {
            _expectedTransactionName = expectedTransactionName;

            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.Actions(
                exerciseApplication: () =>
                {
                    _fixture.EnqueueS3PutEvent();
                    _fixture.EnqueueS3DeleteEvent();
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
                () => ValidateServerlessPayload(serverlessPayloads[0], true),
                () => ValidateServerlessPayload(serverlessPayloads[1], false),
                () => Assert.All(serverlessPayloads, ValidateTraceHasNoParent)
                );
        }

        private void ValidateServerlessPayload(ServerlessPayload serverlessPayload, bool expectPutEvent)
        {
            var transactionEvent = serverlessPayload.Telemetry.TransactionEventsPayload.TransactionEvents.Single();

            var expectedAgentAttributes = new[]
            {
                "aws.lambda.arn",
                "aws.requestId"
            };

            var expectedAgentAttributeValues = new Dictionary<string, object>
            {
                { "aws.lambda.eventSource.arn", "arn:{partition}:s3:::mybucket" },
                { "aws.lambda.eventSource.eventType", "s3" },
                { "aws.lambda.eventSource.eventName", expectPutEvent ? "ObjectCreated:Put" : "ObjectRemoved:Delete"},
                { "aws.lambda.eventSource.length", 1 },
                { "aws.lambda.eventSource.region", "{region}" },
                { "aws.lambda.eventSource.eventTime", "1/1/1970 12:00:00 AM" },
                { "aws.lambda.eventSource.xAmzId2", "EXAMPLE123/5678abcdefghijklambdaisawesome/mnopqrstuvwxyzABCDEFGH" },
                { "aws.lambda.eventSource.bucketName", "sourcebucket" },
                { "aws.lambda.eventSource.objectKey", "HappyFace.jpg" },
                { "aws.lambda.eventSource.objectSequencer", "0A1B2C3D4E5F678901" },
                { "aws.lambda.eventSource.objectSize", expectPutEvent ? 1024 : 0 }
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

    public class AwsLambdaS3EventTestCoreOldest : AwsLambdaS3EventTest<LambdaS3EventTriggerFixtureCoreOldest>
    {
        public AwsLambdaS3EventTestCoreOldest(LambdaS3EventTriggerFixtureCoreOldest fixture, ITestOutputHelper output)
            : base(fixture, output, "OtherTransaction/Function/S3Event")
        {
        }
    }

    public class AwsLambdaAsyncS3EventTestCoreOldest : AwsLambdaS3EventTest<AsyncLambdaS3EventTriggerFixtureCoreOldest>
    {
        public AwsLambdaAsyncS3EventTestCoreOldest(AsyncLambdaS3EventTriggerFixtureCoreOldest fixture, ITestOutputHelper output)
            : base(fixture, output, "OtherTransaction/Function/S3EventAsync")
        {
        }
    }

    public class AwsLambdaS3EventTestCoreLatest : AwsLambdaS3EventTest<LambdaS3EventTriggerFixtureCoreLatest>
    {
        public AwsLambdaS3EventTestCoreLatest(LambdaS3EventTriggerFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, "OtherTransaction/Function/S3Event")
        {
        }
    }

    public class AwsLambdaAsyncS3EventTestCoreLatest : AwsLambdaS3EventTest<AsyncLambdaS3EventTriggerFixtureCoreLatest>
    {
        public AwsLambdaAsyncS3EventTestCoreLatest(AsyncLambdaS3EventTriggerFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, "OtherTransaction/Function/S3EventAsync")
        {
        }
    }
}

// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Extensions.Lambda;
using NUnit.Framework;

namespace Agent.Extensions.Tests.Lambda
{
    internal class AwsLambdaEventTypeExtensionsTests
    {
        [Test]
        [TestCase("Amazon.Lambda.APIGatewayEvents.APIGatewayProxyRequest", AwsLambdaEventType.APIGatewayProxyRequest)]
        [TestCase("Amazon.Lambda.APIGatewayEvents.APIGatewayHttpApiV2ProxyRequest", AwsLambdaEventType.APIGatewayHttpApiV2ProxyRequest)]
        [TestCase("Amazon.Lambda.ApplicationLoadBalancerEvents.ApplicationLoadBalancerRequest", AwsLambdaEventType.ApplicationLoadBalancerRequest)]
        [TestCase("Amazon.Lambda.CloudWatchEvents.ScheduledEvents.ScheduledEvent", AwsLambdaEventType.CloudWatchScheduledEvent)]
        [TestCase("Amazon.Lambda.KinesisEvents.KinesisEvent", AwsLambdaEventType.KinesisStreamingEvent)]
        [TestCase("Amazon.Lambda.KinesisEvents.KinesisTimeWindowEvent", AwsLambdaEventType.KinesisStreamingEvent)]
        [TestCase("Amazon.Lambda.KinesisFirehoseEvents.KinesisFirehoseEvent", AwsLambdaEventType.KinesisFirehoseEvent)]
        [TestCase("Amazon.Lambda.SNSEvents.SNSEvent", AwsLambdaEventType.SNSEvent)]
        [TestCase("Amazon.Lambda.S3Events.S3Event", AwsLambdaEventType.S3Event)]
        [TestCase("Amazon.Lambda.SimpleEmailEvents.SimpleEmailEvent", AwsLambdaEventType.SimpleEmailEvent)]
        [TestCase("Amazon.Lambda.SimpleEmailEvents.SimpleEmailEvent<LambdaReceiptAction>", AwsLambdaEventType.SimpleEmailEvent)]
        [TestCase("Amazon.Lambda.SQSEvents.SQSEvent", AwsLambdaEventType.SQSEvent)]
        [TestCase("Amazon.Lambda.DynamoDBEvents.DynamoDBEvent", AwsLambdaEventType.DynamoStream)]
        [TestCase("Amazon.Lambda.DynamoDBEvents.DynamoDBTimeWindowEvent", AwsLambdaEventType.DynamoStream)]
        [TestCase("Gibberish", AwsLambdaEventType.Unknown)]
        public void ToEventType_ReturnsCorrectEventType(string inputType, AwsLambdaEventType expectedEventType)
        {
            var actualEventType = inputType.ToEventType();
            Assert.That(actualEventType, Is.EqualTo(expectedEventType));
        }
        [Test]
        [TestCase(AwsLambdaEventType.APIGatewayProxyRequest, "apiGateway")]
        [TestCase(AwsLambdaEventType.APIGatewayHttpApiV2ProxyRequest, "apiGateway")]
        [TestCase(AwsLambdaEventType.ApplicationLoadBalancerRequest, "alb")]
        [TestCase(AwsLambdaEventType.CloudWatchScheduledEvent, "cloudWatch_scheduled")]
        [TestCase(AwsLambdaEventType.KinesisStreamingEvent, "kinesis")]
        [TestCase(AwsLambdaEventType.KinesisFirehoseEvent, "firehose")]
        [TestCase(AwsLambdaEventType.SNSEvent, "sns")]
        [TestCase(AwsLambdaEventType.S3Event, "s3")]
        [TestCase(AwsLambdaEventType.SimpleEmailEvent, "ses")]
        [TestCase(AwsLambdaEventType.SQSEvent, "sqs")]
        [TestCase(AwsLambdaEventType.DynamoStream, "dynamo_streams")]
        [TestCase(AwsLambdaEventType.Unknown, "Unknown")]
        public void ToEventTypeString_ReturnsCorrectEventTypeString(AwsLambdaEventType inputEventType, string expectedEventTypeString)
        {
            var actualEventTypeString = inputEventType.ToEventTypeString();
            Assert.That(actualEventTypeString, Is.EqualTo(expectedEventTypeString));
        }
    }
}

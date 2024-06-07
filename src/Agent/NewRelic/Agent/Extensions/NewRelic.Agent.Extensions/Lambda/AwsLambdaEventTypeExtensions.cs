// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;

namespace NewRelic.Agent.Extensions.Lambda;

public static class AwsLambdaEventTypeExtensions
{
    public static AwsLambdaEventType ToEventType(this string typeFullName)
    {
        return typeFullName switch
        {
            "Amazon.Lambda.APIGatewayEvents.APIGatewayProxyRequest" => AwsLambdaEventType.APIGatewayProxyRequest,
            "Amazon.Lambda.APIGatewayEvents.APIGatewayHttpApiV2ProxyRequest" => AwsLambdaEventType.APIGatewayHttpApiV2ProxyRequest,
            "Amazon.Lambda.ApplicationLoadBalancerEvents.ApplicationLoadBalancerRequest" => AwsLambdaEventType.ApplicationLoadBalancerRequest,
            "Amazon.Lambda.CloudWatchEvents.ScheduledEvents.ScheduledEvent" => AwsLambdaEventType.CloudWatchScheduledEvent,
            "Amazon.Lambda.KinesisEvents.KinesisEvent" => AwsLambdaEventType.KinesisStreamingEvent,
            "Amazon.Lambda.KinesisEvents.KinesisTimeWindowEvent" => AwsLambdaEventType.KinesisStreamingEvent,
            "Amazon.Lambda.KinesisFirehoseEvents.KinesisFirehoseEvent" => AwsLambdaEventType.KinesisFirehoseEvent,
            "Amazon.Lambda.SNSEvents.SNSEvent" => AwsLambdaEventType.SNSEvent,
            "Amazon.Lambda.S3Events.S3Event" => AwsLambdaEventType.S3Event,
            // SimpleEmailEvents are a generic type in newer library versions
            string typeName when typeName.StartsWith("Amazon.Lambda.SimpleEmailEvents.SimpleEmailEvent") => AwsLambdaEventType.SimpleEmailEvent,
            "Amazon.Lambda.SQSEvents.SQSEvent" => AwsLambdaEventType.SQSEvent,
            "Amazon.Lambda.DynamoDBEvents.DynamoDBEvent" => AwsLambdaEventType.DynamoStream,
            "Amazon.Lambda.DynamoDBEvents.DynamoDBTimeWindowEvent" => AwsLambdaEventType.DynamoStream,
            _ => AwsLambdaEventType.Unknown
        };
    }
    public static string ToEventTypeString(this AwsLambdaEventType eventType)
    {
        return eventType switch
        {
            AwsLambdaEventType.APIGatewayProxyRequest => "apiGateway",
            AwsLambdaEventType.APIGatewayHttpApiV2ProxyRequest => "apiGateway",
            AwsLambdaEventType.ApplicationLoadBalancerRequest => "alb",
            AwsLambdaEventType.CloudWatchScheduledEvent => "cloudWatch_scheduled",
            AwsLambdaEventType.KinesisStreamingEvent => "kinesis",
            AwsLambdaEventType.KinesisFirehoseEvent => "firehose",
            AwsLambdaEventType.SNSEvent => "sns",
            AwsLambdaEventType.S3Event => "s3",
            AwsLambdaEventType.SimpleEmailEvent => "ses",
            AwsLambdaEventType.SQSEvent => "sqs",
            AwsLambdaEventType.DynamoStream => "dynamo_streams",
            AwsLambdaEventType.Unknown => "Unknown",
            _ => throw new ArgumentOutOfRangeException(nameof(eventType), eventType, "Unexpected eventType"),
        };
    }

    public static bool IsWebEvent(this AwsLambdaEventType eventType)
    {
        return eventType == AwsLambdaEventType.APIGatewayProxyRequest || eventType == AwsLambdaEventType.ApplicationLoadBalancerRequest || eventType == AwsLambdaEventType.APIGatewayHttpApiV2ProxyRequest;
    }
}

// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.Extensions.Lambda;

public enum AwsLambdaEventType
{
    Unknown,
    APIGatewayProxyRequest,
    APIGatewayHttpApiV2ProxyRequest,
    ApplicationLoadBalancerRequest,
    CloudWatchScheduledEvent,
    KinesisStreamingEvent,
    KinesisFirehoseEvent,
    SNSEvent,
    S3Event,
    SimpleEmailEvent,
    SQSEvent,
    DynamoStream,
}

# New Relic .NET Agent AwsSdk Instrumentation

## Overview

The AwsSdk instrumentation wrapper provides automatic monitoring for AWS SDK for .NET operations. It instruments the AWS SDK's internal pipeline to capture service calls for supported AWS services including SQS, Lambda, DynamoDB, Kinesis Firehose, and Kinesis. The wrapper creates segments with AWS-specific attributes and handles distributed tracing for messaging services.

## Instrumented Methods

### AwsSdkPipelineWrapper
- **Wrapper**: [AwsSdkPipelineWrapper.cs](AwsSdkPipelineWrapper.cs)
- **Assembly**: `AWSSDK.Core`
- **Type**: `Amazon.Runtime.Internal.RuntimePipeline`

| Method | Creates Transaction | Requires Existing Transaction |
|--------|-------------------|------------------------------|
| [InvokeSync](https://github.com/aws/aws-sdk-net/blob/main/sdk/src/Core/Amazon.Runtime/Pipeline/RuntimePipeline.cs) | No | Yes |
| [InvokeAsync](https://github.com/aws/aws-sdk-net/blob/main/sdk/src/Core/Amazon.Runtime/Pipeline/RuntimePipeline.cs) | No | Yes |

### AmazonServiceClientWrapper
- **Wrapper**: [AmazonServiceClientWrapper.cs](AmazonServiceClientWrapper.cs)
- **Assembly**: `AWSSDK.Core`
- **Type**: `Amazon.Runtime.AmazonServiceClient`

| Method | Creates Transaction | Requires Existing Transaction |
|--------|-------------------|------------------------------|
| [.ctor](https://github.com/aws/aws-sdk-net/blob/main/sdk/src/Core/Amazon.Runtime/AmazonServiceClient.cs) | No | No |

## Configuration

[Instrumentation.xml](https://github.com/newrelic/newrelic-dotnet-agent/blob/main/src/Agent/NewRelic/Agent/Extensions/Providers/Wrapper/AwsSdk/Instrumentation.xml)

## Transaction Lifecycle

### Transaction Enrichment
- **AwsSdkPipelineWrapper** requires existing transactions and adds service-specific segments to provide detailed timing and metadata for AWS service calls
- **AmazonServiceClientWrapper** enriches client configuration by extracting and caching AWS account IDs from credentials for ARN construction

## Attributes Added

The wrapper adds the following attributes to AWS service segments:

### Common Attributes
- **aws.operation**: AWS service operation name (e.g., "InvokeRequest", "SendMessage", "PutRecord")
- **aws.region**: AWS region where the service call is made
- **aws.requestId**: Request ID from AWS service response (when available)
- **cloud.resource_id**: Amazon Resource Name (ARN) of the target resource

### Service-Specific Attributes
- **cloud.platform**: Set to "aws_lambda" for Lambda invocations
- **messaging.system**: Set to "SQS" for SQS operations
- **messaging.destination.name**: Queue name for SQS operations
- **messaging.operation**: Set to "produce", "consume", or "purge" for SQS operations

## Trigger/Type Resolution

The wrapper uses request type resolution to determine handling:

- **SQS**: Request types starting with "Amazon.SQS" → Message broker segments
- **Lambda**: Request type "Amazon.Lambda.Model.InvokeRequest" → Transaction segments
- **DynamoDB**: Request types starting with "Amazon.DynamoDBv2" → Datastore segments
- **Kinesis Firehose**: Request types starting with "Amazon.KinesisFirehose" → Transaction segments
- **Kinesis**: Request types starting with "Amazon.Kinesis." → Transaction segments

Unsupported request types are logged once per type and return NoOp delegates.

## Distributed Tracing

### Header Insertion (SQS Publishing)
- **SQS SendMessage operations** insert distributed tracing headers into message attributes for outbound messages
- Supports both W3C Trace Context and New Relic proprietary headers
- Transport type set to `AMQP` for message broker operations

### Header Extraction (SQS Consuming)
- **SQS ReceiveMessage operations** extract distributed tracing headers from message attributes
- Continues trace context across SQS message boundaries

## License
Copyright 2020 New Relic, Inc. All rights reserved.
SPDX-License-Identifier: Apache-2.0

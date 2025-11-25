# New Relic .NET Agent AwsLambda Instrumentation

## Overview
Instrumentation for AWS Lambda .NET functions (ASP.NET Core server-style handlers and generic Lambda handlers) providing transaction creation, cold start detection, request/response metadata capture for supported event sources (API Gateway REST & HTTP API v2, Application Load Balancer), and Lambda-specific attributes (ARN, request id). Includes early agent initialization and OpenTracing compatibility detection.

## Instrumented Methods

### NoOpWrapper (early init)
- Wrapper: [`NewRelic.Agent.Core.Wrapper.NoOpWrapper`](https://github.com/newrelic/newrelic-dotnet-agent/blob/main/src/Agent/NewRelic/Agent/Core/Wrapper/NoOpWrapper.cs)
- Assembly: `Amazon.Lambda.RuntimeSupport`
- Type: `Amazon.Lambda.RuntimeSupport.LambdaBootstrap`

| Method | Creates Transaction | Requires Existing Transaction |
|--------|---------------------|-------------------------------|
| [`RunAsync`](https://github.com/aws/aws-lambda-dotnet/blob/master/Libraries/src/Amazon.Lambda.RuntimeSupport/Bootstrap/LambdaBootstrap.cs) | No | No |

### OpenTracingWrapper
- Wrapper: [`OpenTracingWrapper`](https://github.com/newrelic/newrelic-dotnet-agent/blob/main/src/Agent/NewRelic/Agent/Extensions/Providers/Wrapper/AwsLambda/OpenTracingWrapper.cs)
- Assembly: `NewRelic.OpenTracing.AmazonLambda.Tracer`
- Type: `NewRelic.OpenTracing.AmazonLambda.LambdaTracer`

| Method | Creates Transaction | Requires Existing Transaction |
|--------|---------------------|-------------------------------|
| `.ctor` | No | No |

### HandlerMethodWrapper
- Wrapper: [`HandlerMethodWrapper`](https://github.com/newrelic/newrelic-dotnet-agent/blob/main/src/Agent/NewRelic/Agent/Extensions/Providers/Wrapper/AwsLambda/HandlerMethodWrapper.cs)
- Assembly: `Amazon.Lambda.AspNetCoreServer`
- Type: `Amazon.Lambda.AspNetCoreServer.AbstractAspNetCoreFunction`\`2

| Method | Creates Transaction | Requires Existing Transaction |
|--------|---------------------|-------------------------------|
| [`FunctionHandlerAsync`](https://github.com/aws/aws-lambda-dotnet/blob/master/Libraries/src/Amazon.Lambda.AspNetCoreServer/AbstractAspNetCoreFunction.cs) | Yes | No |

## Instrumentation XML
[`Instrumentation.xml`](https://github.com/newrelic/newrelic-dotnet-agent/blob/main/src/Agent/NewRelic/Agent/Extensions/Providers/Wrapper/AwsLambda/Instrumentation.xml)

## Transaction Lifecycle
Creation: `FunctionHandlerAsync` for each invocation (web vs non-web determined by event type). Early initialization via `LambdaBootstrap.RunAsync` no-op wrapper. Enrichment includes cold start flag, event source attributes, request id, ARN, HTTP status/headers for web events.

## Attributes Added
Lambda transactions include:
- `aws.lambda.coldStart` (first invocation only)
- `aws.lambda.arn`
- `aws.requestId`
- Event source key `eventType` (normalized AWS event type)
- For web events: HTTP status code, subset of response headers (`content-type`, `content-length`), request method/path (derived), plus event-type specific attributes via `LambdaEventHelpers`

## Trigger / Event Type Resolution
Event type inferred from input parameter type name (e.g., `APIGatewayProxyRequest`, `APIGatewayHttpApiV2ProxyRequest`, `ApplicationLoadBalancerRequest`). Mapped to web vs non-web categorization and normalized string used in transaction naming and attributes. Unknown types logged once and skipped for enrichment.

## Distributed Tracing
Inbound distributed trace headers for API Gateway / ALB events are extracted from the event payload (handled in `LambdaEventHelpers`). Outbound propagation relies on other wrappers (e.g., HttpClient). Request/response spans nest under the Lambda transaction. 

## Early Load Strategy
`LambdaBootstrap.RunAsync` is wrapped with a no-op tracer to start agent initialization during the Lambda init phase, reducing cold start overhead.

## License
Copyright 2020 New Relic, Inc. All rights reserved.  
SPDX-License-Identifier: Apache-2.0

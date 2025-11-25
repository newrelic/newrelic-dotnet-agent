# New Relic .NET Agent Azure Function Instrumentation

## Overview
Automatic tracing and metadata capture for Azure Functions (in-process `FUNCTIONS_WORKER_RUNTIME=dotnet` and isolated `FUNCTIONS_WORKER_RUNTIME=dotnet-isolated`). The wrappers create / enrich transactions with FAAS attributes (name, trigger, invocation id, cold start), resource identifiers, HTTP request/response data, and distributed tracing headers. Isolated HTTP enrichment is added when the ASP.NET Core Functions middleware is present.

## Instrumented Methods

### AzureFunctionIsolatedInvokeAsyncWrapper
- Wrapper: [`AzureFunctionIsolatedInvokeAsyncWrapper`](https://github.com/newrelic/newrelic-dotnet-agent/blob/main/src/Agent/NewRelic/Agent/Extensions/Providers/Wrapper/AzureFunction/AzureFunctionIsolatedInvokeFunctionAsyncWrapper.cs)
- Assembly: `Microsoft.Azure.Functions.Worker.Core`
- Type: `Microsoft.Azure.Functions.Worker.FunctionsApplication`

| Method | Creates Transaction | Requires Existing Transaction |
|--------|---------------------|-------------------------------|
| [`InvokeFunctionAsync`](https://github.com/Azure/azure-functions-dotnet-worker/blob/main/src/DotNetWorker.Core/FunctionsApplication.cs) | Yes | No |

### AzureFunctionInProcessExecuteWithWatchersAsyncWrapper
- Wrapper: [`AzureFunctionInProcessExecuteWithWatchersAsyncWrapper`](https://github.com/newrelic/newrelic-dotnet-agent/blob/main/src/Agent/NewRelic/Agent/Extensions/Providers/Wrapper/AzureFunction/AzureFunctionInProcessExecuteWithWatchersAsyncWrapper.cs)
- Assembly: `Microsoft.Azure.WebJobs.Host`
- Type: `Microsoft.Azure.WebJobs.Host.Executors.FunctionExecutor`

| Method | Creates Transaction | Requires Existing Transaction |
|--------|---------------------|-------------------------------|
| [`ExecuteWithWatchersAsync`](https://github.com/Azure/azure-webjobs-sdk/blob/65bc8a29e09cbeb3a7023c28be7a363a21d8199a/src/Microsoft.Azure.WebJobs.Host/Executors/FunctionExecutor.cs) | Yes | No |

### AzureFunctionInProcessInvokeAsyncWrapper
- Wrapper: [`AzureFunctionInProcessInvokeAsyncWrapper`](https://github.com/newrelic/newrelic-dotnet-agent/blob/main/src/Agent/NewRelic/Agent/Extensions/Providers/Wrapper/AzureFunction/AzureFunctionInProcessInvokeAsyncWrapper.cs)
- Assembly: `Microsoft.Azure.WebJobs.Host`
- Type: `Microsoft.Azure.WebJobs.Host.Executors.FunctionInvoker`\`2

| Method | Creates Transaction | Requires Existing Transaction |
|--------|---------------------|-------------------------------|
| [`InvokeAsync`](https://github.com/Azure/azure-webjobs-sdk/blob/dev/src/Microsoft.Azure.WebJobs.Host/Executors/FunctionInvoker.cs) | No | Yes |

### FunctionsHttpProxyingMiddlewareWrapper
- Wrapper: [`FunctionsHttpProxyingMiddlewareWrapper`](https://github.com/newrelic/newrelic-dotnet-agent/blob/main/src/Agent/NewRelic/Agent/Extensions/Providers/Wrapper/AzureFunction/FunctionsHttpProxyingMiddlewareWrapper.cs)
- Assembly: `Microsoft.Azure.Functions.Worker.Extensions.Http.AspNetCore`
- Type: `Microsoft.Azure.Functions.Worker.Extensions.Http.AspNetCore.FunctionsHttpProxyingMiddleware`

| Method | Creates Transaction | Requires Existing Transaction |
|--------|---------------------|-------------------------------|
| [`AddHttpContextToFunctionContext`](https://github.com/Azure/azure-functions-dotnet-worker/blob/main/extensions/Worker.Extensions.Http.AspNetCore/src/FunctionsMiddleware/FunctionsHttpProxyingMiddleware.cs) | No | Yes |
| [`TryHandleHttpResult`](https://github.com/Azure/azure-functions-dotnet-worker/blob/main/extensions/Worker.Extensions.Http.AspNetCore/src/FunctionsMiddleware/FunctionsHttpProxyingMiddleware.cs) | No | Yes |
| [`TryHandleOutputBindingsHttpResult`](https://github.com/Azure/azure-functions-dotnet-worker/blob/main/extensions/Worker.Extensions.Http.AspNetCore/src/FunctionsMiddleware/FunctionsHttpProxyingMiddleware.cs) | No | Yes |

### NoOpWrapper (early load)
- Wrapper: [`NewRelic.Agent.Core.Wrapper.NoOpWrapper`](https://github.com/newrelic/newrelic-dotnet-agent/blob/main/src/Agent/NewRelic/Agent/Core/Wrapper/NoOpWrapper.cs)
- Assembly: `Microsoft.Extensions.Hosting`
- Type: `Microsoft.Extensions.Hosting.HostBuilder`

| Method | Creates Transaction | Requires Existing Transaction |
|--------|---------------------|-------------------------------|
| [`Build`](https://github.com/dotnet/runtime/blob/main/src/libraries/Microsoft.Extensions.Hosting/src/HostBuilder.cs) | No | No |

## Instrumentation XML
[`Instrumentation.xml`](https://github.com/newrelic/newrelic-dotnet-agent/blob/main/src/Agent/NewRelic/Agent/Extensions/Providers/Wrapper/AzureFunction/Instrumentation.xml)

## Transaction Lifecycle
Creation: `InvokeFunctionAsync` (isolated), `ExecuteWithWatchersAsync` (in-process). Enrichment: `InvokeAsync` (HTTP metadata, DT headers), middleware methods (isolated HTTP request/response). Cold start flagged once via `faas.coldStart=true`.

## Attributes Added
- `faas.coldStart` (first invocation only)
- `faas.invocation_id`
- `faas.name` (`<FunctionAppName>/<FunctionName>`)
- `faas.trigger`
- `cloud.resource_id`
Plus HTTP method, request path, response status code when available.

## Trigger Type Resolution
Trigger attribute short type names ending with `TriggerAttribute` are mapped to categories:
- `http`
- `pubsub` (Kafka, EventHub, ServiceBus, Redis*, RabbitMQ, SignalR, WebPubSub, EventGrid, DaprTopic)
- `datasource` (Sql, Blob, CosmosDB, Queue, DaprBinding)
- `timer` (Timer)
- `other` (Activity, Entity, Orchestration, DaprServiceInvocation, unknown)

## Distributed Tracing
Incoming HTTP triggers (in-process & isolated) accept W3C/New Relic headers once per transaction; response codes captured when present. Outbound propagation handled by other wrappers (e.g., HttpClient). Non-HTTP triggers may accept headers in future (e.g., ServiceBus) but are currently limited to HTTP.

## Early Load Strategy
`HostBuilder.Build` is wrapped with a no-op tracer to ensure early agent initialization in isolated worker scenarios.

## License
Copyright 2020 New Relic, Inc. All rights reserved.  
SPDX-License-Identifier: Apache-2.0

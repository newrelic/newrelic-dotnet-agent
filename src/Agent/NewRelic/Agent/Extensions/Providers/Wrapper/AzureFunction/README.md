# New Relic .NET Agent Azure Function Instrumentation

## Overview
This project provides automatic instrumentation for Azure Functions running on .NET (in-process and isolated worker models). It captures transactions, distributed tracing headers, FAAS (Functions-as-a-Service) attributes, and HTTP metadata for supported trigger types without requiring user code changes.

Supported models:
- In-process (FUNCTIONS_WORKER_RUNTIME = `dotnet`) using `Microsoft.Azure.WebJobs.Host`.
- Isolated worker (FUNCTIONS_WORKER_RUNTIME = `dotnet-isolated`) using `Microsoft.Azure.Functions.Worker.Core`, optionally enriched when the ASP.NET Core HTTP middleware package is present.

## Activation & Detection
Instrumentation activates only when both:
1. Azure Functions environment is detected (presence of `FUNCTIONS_WORKER_RUNTIME`).
2. Azure Function mode is enabled (environment variable `NEW_RELIC_AZURE_FUNCTION_MODE_ENABLED` absent or set to `true` / `1`).

If disabled, wrappers still load but do not create or enrich transactions; only a supportability metric indicating disabled state is recorded.

## Environment & Metadata Sources
FAAS and cloud attributes rely on Azure App Service / Functions environment variables:
- `WEBSITE_OWNER_NAME` (contains subscription id + other data)
- `WEBSITE_RESOURCE_GROUP`
- `WEBSITE_SITE_NAME`

These compose the Azure Functions resource id (`cloud.resource_id`) and function-level resource ids via `AzureFunctionResourceIdWithFunctionName(functionName)`.

## Instrumented Methods
| Method | Creates Transaction | Requires Existing Transaction | Min Version | Max Version | Notes | Config Flags |
|--------|---------------------|-------------------------------|-------------|-------------|-------|--------------|
| [Microsoft.Azure.Functions.Worker.FunctionsApplication.InvokeFunctionAsync()](https://github.com/Azure/azure-functions-dotnet-worker/blob/main/src/DotNetWorker/FunctionsApplication.cs) | Yes | No | n/a | n/a | Isolated entry point. Starts transaction, attaches async, sets FAAS attributes. | `FUNCTIONS_WORKER_RUNTIME`, `NEW_RELIC_AZURE_FUNCTION_MODE_ENABLED` |
| [Microsoft.Azure.WebJobs.Host.Executors.FunctionExecutor.ExecuteWithWatchersAsync(IFunctionInstanceEx, ParameterHelper, ILogger, CancellationTokenSource)](https://github.com/Azure/azure-functions-host/blob/main/src/WebJobs.Host/Executors/FunctionExecutor.cs) | Yes | No | n/a | n/a | In-process creation point. Resolves trigger, sets FAAS attributes (cold start only once). | `FUNCTIONS_WORKER_RUNTIME`, `NEW_RELIC_AZURE_FUNCTION_MODE_ENABLED` |
| [Microsoft.Azure.WebJobs.Host.Executors.FunctionInvoker\`2.InvokeAsync(object, object[])](https://github.com/Azure/azure-functions-host/blob/main/src/WebJobs.Host/Executors/FunctionInvoker.cs) | No | Yes | n/a | n/a | Enriches existing in-process transaction (HTTP method, status, DT headers). | `FUNCTIONS_WORKER_RUNTIME`, `NEW_RELIC_AZURE_FUNCTION_MODE_ENABLED` |
| [Microsoft.Azure.Functions.Worker.Extensions.Http.AspNetCore.FunctionsHttpProxyingMiddleware.AddHttpContextToFunctionContext()](https://github.com/Azure/azure-functions-dotnet-worker/blob/main/src/Extensions/Http/aspnetcore/FunctionsHttpProxyingMiddleware.cs)<br>[...TryHandleHttpResult()](https://github.com/Azure/azure-functions-dotnet-worker/blob/main/src/Extensions/Http/aspnetcore/FunctionsHttpProxyingMiddleware.cs)<br>[...TryHandleOutputBindingsHttpResult()](https://github.com/Azure/azure-functions-dotnet-worker/blob/main/src/Extensions/Http/aspnetcore/FunctionsHttpProxyingMiddleware.cs) | No | Yes | n/a | n/a | Isolated + ASP.NET Core pipeline enrichment (request method/path, status code, DT headers). | `FUNCTIONS_WORKER_RUNTIME`, `NEW_RELIC_AZURE_FUNCTION_MODE_ENABLED` |
| [Microsoft.Extensions.Hosting.HostBuilder.Build()](https://github.com/dotnet/runtime/blob/main/src/libraries/Microsoft.Extensions.Hosting/src/HostBuilder.cs) | No | No | n/a | n/a | Early load (isolated). No tracing; ensures agent initialization. | `FUNCTIONS_WORKER_RUNTIME` |

Instrumentation XML: `src/Agent/NewRelic/Agent/Extensions/Providers/Wrapper/AzureFunction/Instrumentation.xml`

## Transaction Lifecycle
- Created by: `ExecuteWithWatchersAsync` (in-process), `InvokeFunctionAsync` (isolated).
- Enriched by: `InvokeAsync` (in-process HTTP context / headers), middleware methods (isolated ASP.NET Core pipeline).
- Cold start detection: First successful transaction per process sets `faas.coldStart = true`.

## FAAS & Cloud Attributes Added
When creating transactions (and mode enabled):
- `faas.coldStart` (only on first invocation)
- `faas.invocation_id`
- `faas.name` (format: `<FunctionAppName>/<FunctionName>`)
- `faas.trigger` (resolved trigger category)
- `cloud.resource_id` (function-level resource identifier)

## Trigger Type Resolution
Trigger type is inferred from the parameter attribute type name ending in `TriggerAttribute` and mapped to a normalized category:
- `Http` -> `http`
- Messaging / pub-sub (e.g., `Kafka`, `EventHub`, `ServiceBus`, `Redis*`, `RabbitMQ`, `SignalR`, `WebPubSub`, `EventGrid`, `DaprTopic`) -> `pubsub`
- Data / storage (`Sql`, `Blob`, `CosmosDB`, `Queue`, `DaprBinding`) -> `datasource`
- `Timer` -> `timer`
- Other / durable (`Activity`, `Entity`, `Orchestration`, `DaprServiceInvocation`, unknown) -> `other`

Resolution logic lives in `TriggerTypeExtensions.ResolveTriggerType`.

## Distributed Tracing
Incoming HTTP requests (isolated pipeline `AddHttpContextToFunctionContext`, in-process HTTP trigger handling) accept standard W3C headers (`traceparent`, `tracestate`). DT headers are processed only once per transaction.

## Version Considerations
Current instrumentation does not specify `minVersion` / `maxVersion` constraints. If upstream APIs change, constraints may need adding in `Instrumentation.xml`.

## Disabling / Opt-Out
Ways to disable Azure Function instrumentation:
- Set `NEW_RELIC_AZURE_FUNCTION_MODE_ENABLED=false` or `0`.
- Remove or modify entries in `Instrumentation.xml`.
- Use ignore rules in `newrelic.config` `<instrumentation><rules><ignore ... /></rules></instrumentation>` for advanced exclusion.

## Early Load Strategy
`HostBuilder.Build()` is wrapped with a no-op tracer to force early agent initialization for isolated worker scenarios (ensures configuration and logging ready before function invocations).

## License
Copyright 2020 New Relic, Inc. All rights reserved.  
SPDX-License-Identifier: Apache-2.0

# New Relic .NET Agent AspNetCore Instrumentation

## Overview

The AspNetCore instrumentation wrapper provides automatic monitoring for ASP.NET Core applications (versions 1.0 through 5.x). It instruments application startup and MVC controller actions to create transactions and segments with comprehensive request tracing. The wrapper injects middleware at the start of the request pipeline to manage the complete transaction lifecycle.

## Instrumented Methods

### BuildCommonServicesWrapper
- **Wrapper**: [BuildCommonServicesWrapper.cs](BuildCommonServicesWrapper.cs)
- **Assembly**: `Microsoft.AspNetCore.Hosting`
- **Type**: `Microsoft.AspNetCore.Hosting.WebHostBuilder`

| Method | Creates Transaction | Requires Existing Transaction | Min Version | Max Version |
|--------|-------------------|------------------------------|-------------|-------------|
| [BuildCommonServices](https://github.com/dotnet/aspnetcore/blob/main/src/Hosting/Hosting/src/WebHostBuilder.cs) | No | No | 1.0.0.0 | 6.0.0.0 |

### GenericHostWebHostBuilderExtensionsWrapper
- **Wrapper**: [GenericHostWebHostBuilderExtensionsWrapper.cs](GenericHostWebHostBuilderExtensionsWrapper.cs)
- **Assembly**: `Microsoft.AspNetCore.Hosting`
- **Type**: `Microsoft.Extensions.Hosting.GenericHostWebHostBuilderExtensions`

| Method | Creates Transaction | Requires Existing Transaction | Min Version | Max Version |
|--------|-------------------|------------------------------|-------------|-------------|
| ConfigureWebHost | No | No | 1.0.0.0 | 6.0.0.0 |

### InvokeActionMethodAsyncWrapper
- **Wrapper**: [InvokeActionMethodAsyncWrapper.cs](InvokeActionMethodAsyncWrapper.cs)
- **Assembly**: `Microsoft.AspNetCore.Mvc.Core`
- **Type**: `Microsoft.AspNetCore.Mvc.Internal.ControllerActionInvoker`, `Microsoft.AspNetCore.Mvc.Infrastructure.ControllerActionInvoker`

| Method | Creates Transaction | Requires Existing Transaction | Min Version | Max Version |
|--------|-------------------|------------------------------|-------------|-------------|
| [InvokeActionMethodAsync](https://github.com/dotnet/aspnetcore/blob/main/src/Mvc/Mvc.Core/src/Infrastructure/ControllerActionInvoker.cs) | No | Yes | 1.0.0.0 | 6.0.0.0 |

[Instrumentation.xml](https://github.com/newrelic/newrelic-dotnet-agent/blob/main/src/Agent/NewRelic/Agent/Extensions/Providers/Wrapper/AspNetCore/Instrumentation.xml)

## Transaction Lifecycle

### Middleware Injection
- **BuildCommonServicesWrapper** and **GenericHostWebHostBuilderExtensionsWrapper** inject the New Relic startup filter (`AddNewRelicStartupFilter`) into the ASP.NET Core pipeline during application initialization. The startup filter injects the `WrapPipelineMiddleware` at the beginning of the request pipeline.
- **GenericHostWebHostBuilderExtensionsWrapper** uses `AspNetCore21Types` nested class to lazy-load ASP.NET Core 2.1+ types, ensuring compatibility with ASP.NET Core 2.0 applications.

### Transaction Creation
- **WrapPipelineMiddleware** creates web transactions at the start of the ASP.NET Core middleware pipeline for all HTTP requests (except OPTIONS requests). Transactions are created with:
  - Transaction display name based on the request path (e.g., `ROOT` for `/`, or the path without leading slash)
  - Request method, URI, and query string parameters captured
  - Distributed tracing headers accepted from incoming requests
  - Request headers captured based on configuration (`AllowAllRequestHeaders` or default capture list)

### Transaction Lifecycle Management
- **WrapPipelineMiddleware** manages the complete transaction lifecycle:
  - Attaches transaction to async context and detaches from thread-local storage
  - Creates a "Middleware Pipeline" segment that encompasses the entire request processing
  - Registers a callback to write distributed tracing response headers when the response starts
  - Ends transaction after the pipeline completes, capturing:
    - HTTP response status code
    - Exceptions thrown during request processing
    - Errors captured by ASP.NET Core's exception handler middleware
    - Low-priority transaction naming for error responses (4xx/5xx status codes)

### Transaction Naming
- **InvokeActionMethodAsyncWrapper** sets web transaction names for MVC controller actions using the pattern `Controller/Action/{parameters}` with `FrameworkHigh` priority.

## Distributed Tracing

### Header Acceptance (Consuming)
- **WrapPipelineMiddleware** accepts distributed tracing headers from incoming HTTP requests using `AcceptDistributedTraceHeaders()` with transport type `HTTP`
- Headers are extracted from the request at the start of the middleware pipeline

### Header Insertion (Producing)
- **WrapPipelineMiddleware** inserts distributed tracing response headers using the `Response.OnStarting()` callback
- Response headers are written via `transaction.GetResponseMetadata()` before the response stream begins

## Version Considerations

All instrumented methods specify both minimum and maximum versions:
- **Minimum version**: 1.0.0.0 (ASP.NET Core 1.0)
- **Maximum version**: 6.0.0.0 (exclusive upper bound)

This wrapper targets ASP.NET Core versions 1.0 through 5.x. For ASP.NET Core 6+, use the AspNetCore6Plus instrumentation wrapper instead.

## License
Copyright 2020 New Relic, Inc. All rights reserved.
SPDX-License-Identifier: Apache-2.0
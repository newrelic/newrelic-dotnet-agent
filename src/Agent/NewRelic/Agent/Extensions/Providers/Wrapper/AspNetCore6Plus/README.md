# New Relic .NET Agent AspNetCore6Plus Instrumentation

## Overview

The AspNetCore6Plus instrumentation wrapper provides automatic monitoring for ASP.NET Core 6+ applications. It instruments application startup, MVC controller actions, Razor Pages handlers, and response compression to create transactions and segments with comprehensive request tracing. The wrapper also enables browser monitoring injection for ASP.NET Core 6+ applications.

## Instrumented Methods

### BuildCommonServicesWrapper6Plus
- **Wrapper**: [BuildCommonServicesWrapper6Plus.cs](BuildCommonServicesWrapper6Plus.cs)
- **Assembly**: `Microsoft.AspNetCore.Hosting`
- **Type**: `Microsoft.AspNetCore.Hosting.WebHostBuilder`

| Method | Creates Transaction | Requires Existing Transaction | Min Version |
|--------|-------------------|------------------------------|-------------|
| [BuildCommonServices](https://github.com/dotnet/aspnetcore/blob/main/src/Hosting/Hosting/src/WebHostBuilder.cs) | No | No | 6.0.0.0 |

### GenericHostWebHostBuilderExtensionsWrapper6Plus
- **Wrapper**: [GenericHostWebHostBuilderExtensionsWrapper6Plus.cs](GenericHostWebHostBuilderExtensionsWrapper6Plus.cs)
- **Assembly**: `Microsoft.AspNetCore.Hosting`
- **Type**: `Microsoft.Extensions.Hosting.GenericHostWebHostBuilderExtensions`

| Method | Creates Transaction | Requires Existing Transaction | Min Version |
|--------|-------------------|------------------------------|-------------|
| ConfigureWebHost | No | No | 6.0.0.0 |

### InvokeActionMethodAsyncWrapper6Plus
- **Wrapper**: [InvokeActionMethodAsyncWrapper6Plus.cs](InvokeActionMethodAsyncWrapper6Plus.cs)
- **Assembly**: `Microsoft.AspNetCore.Mvc.Core`
- **Type**: `Microsoft.AspNetCore.Mvc.Infrastructure.ControllerActionInvoker`

| Method | Creates Transaction | Requires Existing Transaction | Min Version |
|--------|-------------------|------------------------------|-------------|
| [InvokeActionMethodAsync](https://github.com/dotnet/aspnetcore/blob/main/src/Mvc/Mvc.Core/src/Infrastructure/ControllerActionInvoker.cs) | No | Yes | 6.0.0.0 |

### ResponseCompressionBodyOnWriteWrapper
- **Wrapper**: [ResponseCompressionBodyOnWriteWrapper.cs](ResponseCompressionBodyOnWriteWrapper.cs)
- **Assembly**: `Microsoft.AspNetCore.ResponseCompression`
- **Type**: `Microsoft.AspNetCore.ResponseCompression.ResponseCompressionBody`

| Method | Creates Transaction | Requires Existing Transaction | Min Version |
|--------|-------------------|------------------------------|-------------|
| [OnWrite](https://github.com/dotnet/aspnetcore/blob/main/src/Middleware/ResponseCompression/src/ResponseCompressionBody.cs) | No | No | 6.0.0.0 |

### PageActionInvokeHandlerAsyncWrapper6Plus
- **Wrapper**: [PageActionInvokeHandlerAsyncWrapper6Plus.cs](PageActionInvokeHandlerAsyncWrapper6Plus.cs)
- **Assembly**: `Microsoft.AspNetCore.Mvc.RazorPages`
- **Type**: `Microsoft.AspNetCore.Mvc.RazorPages.Infrastructure.PageActionInvoker`

| Method | Creates Transaction | Requires Existing Transaction | Min Version |
|--------|-------------------|------------------------------|-------------|
| [InvokeHandlerMethodAsync](https://github.com/dotnet/aspnetcore/blob/main/src/Mvc/Mvc.RazorPages/src/Infrastructure/PageActionInvoker.cs) | No | Yes | 6.0.0.0 |

[Instrumentation.xml](https://github.com/newrelic/newrelic-dotnet-agent/blob/main/src/Agent/NewRelic/Agent/Extensions/Providers/Wrapper/AspNetCore6Plus/Instrumentation.xml)

## Transaction Lifecycle

### Middleware Injection
- **BuildCommonServicesWrapper6Plus** and **GenericHostWebHostBuilderExtensionsWrapper6Plus** inject the New Relic startup filter (`AddNewRelicStartupFilter`) into the ASP.NET Core pipeline during application initialization. The startup filter injects two key middleware components at the beginning of the request pipeline:
  - **WrapPipelineMiddleware**: The primary middleware that creates and manages web transactions for all incoming requests
  - **BrowserInjectionMiddleware**: Conditionally injected to enable browser monitoring script insertion

### Transaction Creation
- **WrapPipelineMiddleware** creates web transactions at the start of the ASP.NET Core middleware pipeline for all HTTP requests (except OPTIONS requests and when Azure Function mode is enabled). Transactions are created with:
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
- **InvokeActionMethodAsyncWrapper6Plus** sets web transaction names for MVC controller actions using the pattern `Controller/Action/{parameters}` with `FrameworkHigh` priority.
- **PageActionInvokeHandlerAsyncWrapper6Plus** sets web transaction names for Razor Pages using the pattern `Pages{DisplayName}` with `FrameworkHigh` priority.

## Browser Monitoring

### Response Stream Injection
- **ResponseCompressionBodyOnWriteWrapper** wraps the response compression stream to enable browser monitoring script injection when:
  - Browser monitoring auto-instrumentation is enabled
  - ASP.NET Core 6+ browser injection is enabled
  - The request is not a gRPC request (content type is not `application/grpc`)
- Browser script is injected into HTML responses through the `BrowserInjectingStreamWrapper`

## Version Considerations

All instrumented methods require a minimum version of 6.0.0.0 for their respective assemblies. This ensures compatibility with ASP.NET Core 6 and later versions.

## License
Copyright 2020 New Relic, Inc. All rights reserved.
SPDX-License-Identifier: Apache-2.0

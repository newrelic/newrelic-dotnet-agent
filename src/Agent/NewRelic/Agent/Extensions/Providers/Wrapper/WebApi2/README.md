# New Relic .NET Agent WebApi2 Instrumentation

## Overview

The WebApi2 instrumentation wrapper provides automatic monitoring for ASP.NET Web API 2.x controller actions within existing web transactions. It creates method segments for Web API controller action execution, sets transaction names based on controller and action routing information, handles async context attachment, and captures exception logging.

## Instrumented Methods

### InvokeActionAsyncWrapper
- **Wrapper**: [InvokeActionAsyncWrapper.cs](InvokeActionAsyncWrapper.cs)
- **Assembly**: `System.Web.Http`
- **Type**: `System.Web.Http.Controllers.ApiControllerActionInvoker`

| Method | Creates Transaction | Requires Existing Transaction |
|--------|-------------------|------------------------------|
| InvokeActionAsync | No | Yes |

### AttachToAsyncWrapper
- **Wrapper**: [AttachToAsyncWrapper.cs](AttachToAsyncWrapper.cs)
- **Assembly**: `System.Web.Http`
- **Type**: `System.Web.Http.Controllers.ApiControllerActionInvoker`

| Method | Creates Transaction | Requires Existing Transaction |
|--------|-------------------|------------------------------|
| InvokeActionAsyncCore | No | Yes |

### CompositeExceptionLoggerWrapper
- **Wrapper**: [CompositeExceptionLoggerWrapper.cs](CompositeExceptionLoggerWrapper.cs)
- **Assembly**: `System.Web.Http`
- **Type**: `System.Web.Http.ExceptionHandling.CompositeExceptionLogger`

| Method | Creates Transaction | Requires Existing Transaction |
|--------|-------------------|------------------------------|
| LogAsync | No | Yes |

[Instrumentation.xml](https://github.com/newrelic/newrelic-dotnet-agent/blob/main/src/Agent/NewRelic/Agent/Extensions/Providers/Wrapper/WebApi2/Instrumentation.xml)

## Transaction Naming

The wrapper extracts controller and action names from the Web API routing context to name web transactions:

- **Format**: Typically `{ControllerName}/{ActionName}`
- **Priority**: Framework-level priority (allows higher-priority naming to override)
- **Source**: Retrieved from `ApiControllerActionContext`

## Async Context Management

Web API 2.x introduced improved async support. The `AttachToAsyncWrapper` instruments `InvokeActionAsyncCore` to ensure proper transaction context attachment for async controller actions. This ensures that async operations within controller actions maintain proper transaction context.

## Exception Handling

The `CompositeExceptionLoggerWrapper` instruments Web API 2.x exception logging to capture unhandled exceptions that occur during action execution. This enables automatic error capture and reporting for Web API applications.

## License
Copyright 2020 New Relic, Inc. All rights reserved.
SPDX-License-Identifier: Apache-2.0

# New Relic .NET Agent ScriptHandlerFactory Instrumentation

## Overview

The ScriptHandlerFactory instrumentation wrapper provides automatic transaction naming for ASP.NET AJAX ScriptService web services (asmx web services with `[ScriptService]` attribute). It instruments the internal handler wrappers used by `System.Web.Extensions` to process script service requests.

## Instrumented Methods

### HandlerWrapperWrapper
- **Wrapper**: [HandlerWrapperWrapper.cs](HandlerWrapperWrapper.cs)
- **Assembly**: `System.Web.Extensions`
- **Type**: `System.Web.Script.Services.ScriptHandlerFactory+HandlerWrapper`

| Method | Creates Transaction | Requires Existing Transaction |
|--------|-------------------|------------------------------|
| ProcessRequest | No | Yes |

### AsyncHandlerWrapperWrapper
- **Wrapper**: [AsyncHandlerWrapperWrapper.cs](AsyncHandlerWrapperWrapper.cs)
- **Assembly**: `System.Web.Extensions`
- **Type**: `System.Web.Script.Services.ScriptHandlerFactory+AsyncHandlerWrapper`

| Method | Creates Transaction | Requires Existing Transaction |
|--------|-------------------|------------------------------|
| BeginProcessRequest | No | Yes |

[Instrumentation.xml](https://github.com/newrelic/newrelic-dotnet-agent/blob/main/src/Agent/NewRelic/Agent/Extensions/Providers/Wrapper/ScriptHandlerFactory/Instrumentation.xml)

## Transaction Naming

The wrapper sets web transaction names for AJAX ScriptService requests:

- **Transaction naming priority**: 3 (defined in instrumentation.xml via `transactionNamingPriority` attribute)
- **Purpose**: Provides meaningful names for web service methods called via AJAX
- **Format**: Based on the web service type and method being invoked

## ScriptService Context

This instrumentation targets ASP.NET AJAX ScriptServices, which are:
- Standard ASMX web services decorated with `[ScriptService]` attribute
- Exposed for client-side JavaScript consumption
- Processed through `ScriptHandlerFactory` which wraps the actual handler

The instrumentation captures both synchronous (`ProcessRequest`) and asynchronous (`BeginProcessRequest`) handler invocations.

## License
Copyright 2020 New Relic, Inc. All rights reserved.
SPDX-License-Identifier: Apache-2.0

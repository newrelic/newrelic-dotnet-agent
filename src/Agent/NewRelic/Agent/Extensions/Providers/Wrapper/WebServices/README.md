# New Relic .NET Agent WebServices Instrumentation

## Overview

The WebServices instrumentation wrapper provides automatic monitoring for legacy ASP.NET Web Services (ASMX) and AJAX ScriptServices. It creates method segments for web service method invocations, sets transaction names based on service and method names, and handles both synchronous and asynchronous web service operations.

## Instrumented Methods

### LogicalMethodInfoWrapper
- **Wrapper**: [LogicalMethodInfoWrapper.cs](LogicalMethodInfoWrapper.cs)
- **Assembly**: `System.Web.Services`
- **Type**: `System.Web.Services.Protocols.LogicalMethodInfo`

| Method | Creates Transaction | Requires Existing Transaction |
|--------|-------------------|------------------------------|
| BeginInvoke | No | Yes |
| Invoke | No | Yes |

### WebServiceMethodDataWrapper
- **Wrapper**: [WebServiceMethodDataWrapper.cs](WebServiceMethodDataWrapper.cs)
- **Assembly**: `System.Web.Extensions`
- **Type**: `System.Web.Script.Services.WebServiceMethodData`

| Method | Creates Transaction | Requires Existing Transaction |
|--------|-------------------|------------------------------|
| CallMethod | No | Yes |

[Instrumentation.xml](https://github.com/newrelic/newrelic-dotnet-agent/blob/main/src/Agent/NewRelic/Agent/Extensions/Providers/Wrapper/WebServices/Instrumentation.xml)

## Web Service Types

### Traditional ASMX Web Services
Instrumented via `LogicalMethodInfo.Invoke` and `LogicalMethodInfo.BeginInvoke`:
- Standard SOAP web services (.asmx files)
- Supports both synchronous (`Invoke`) and asynchronous Begin/End pattern (`BeginInvoke`)
- Transaction naming based on web service class and method name

### AJAX ScriptServices
Instrumented via `WebServiceMethodData.CallMethod`:
- Web services exposed for JavaScript consumption (marked with `[ScriptService]` attribute)
- JSON-serialized requests and responses
- Used with ASP.NET AJAX client-side framework

## Transaction Naming

The wrapper sets web transaction names based on:
- **Service type**: Web service class name
- **Method name**: Web service method being invoked
- **Format**: Typically follows pattern like `{ServiceName}/{MethodName}`

## License
Copyright 2020 New Relic, Inc. All rights reserved.
SPDX-License-Identifier: Apache-2.0

# New Relic .NET Agent WebApi1 Instrumentation

## Overview

The WebApi1 instrumentation wrapper provides automatic monitoring for ASP.NET Web API 1.x controller actions within existing web transactions. It creates method segments for Web API controller action execution and sets transaction names based on controller and action routing information.

## Instrumented Methods

### InvokeActionAsyncWrapper
- **Wrapper**: [InvokeActionAsyncWrapper.cs](InvokeActionAsyncWrapper.cs)
- **Assembly**: `System.Web.Http`
- **Type**: `System.Web.Http.Controllers.ApiControllerActionInvoker`

| Method | Creates Transaction | Requires Existing Transaction |
|--------|-------------------|------------------------------|
| InvokeActionAsync | No | Yes |

[Instrumentation.xml](https://github.com/newrelic/newrelic-dotnet-agent/blob/main/src/Agent/NewRelic/Agent/Extensions/Providers/Wrapper/WebApi1/Instrumentation.xml)

## Transaction Naming

The wrapper extracts controller and action names from the Web API routing context to name web transactions:

- **Format**: Typically `{ControllerName}/{ActionName}`
- **Priority**: Framework-level priority (allows higher-priority naming to override)
- **Source**: Retrieved from `ApiControllerActionContext`

## License
Copyright 2020 New Relic, Inc. All rights reserved.
SPDX-License-Identifier: Apache-2.0

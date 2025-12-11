# New Relic .NET Agent Mvc3 Instrumentation

## Overview

The Mvc3 instrumentation wrapper provides automatic monitoring for ASP.NET MVC 3/4/5 controller actions within existing web transactions. It creates method segments for controller action execution, names transactions based on controller and action names, handles both synchronous and asynchronous action invocations, captures exception filter execution, and handles unknown actions and controller instantiation.

## Instrumented Methods

### SyncInvokeActionWrapper
- **Wrapper**: [SyncInvokeActionWrapper.cs](SyncInvokeActionWrapper.cs)
- **Assembly**: `System.Web.Mvc`
- **Type**: `System.Web.Mvc.ControllerActionInvoker`

| Method | Creates Transaction | Requires Existing Transaction |
|--------|-------------------|------------------------------|
| InvokeAction | No | Yes |

### AsyncBeginInvokeActionWrapper
- **Wrapper**: [AsyncBeginInvokeActionWrapper.cs](AsyncBeginInvokeActionWrapper.cs)
- **Assembly**: `System.Web.Mvc`
- **Type**: `System.Web.Mvc.Async.AsyncControllerActionInvoker`

| Method | Creates Transaction | Requires Existing Transaction |
|--------|-------------------|------------------------------|
| BeginInvokeAction | No | Yes |

### AsyncEndInvokeActionWrapper
- **Wrapper**: [AsyncEndInvokeActionWrapper.cs](AsyncEndInvokeActionWrapper.cs)
- **Assembly**: `System.Web.Mvc`
- **Type**: `System.Web.Mvc.Async.AsyncControllerActionInvoker`

| Method | Creates Transaction | Requires Existing Transaction |
|--------|-------------------|------------------------------|
| EndInvokeAction | No | Yes |

### InvokeExceptionFiltersWrapper
- **Wrapper**: [InvokeExceptionFiltersWrapper.cs](InvokeExceptionFiltersWrapper.cs)
- **Assembly**: `System.Web.Mvc`
- **Type**: `System.Web.Mvc.ControllerActionInvoker`

| Method | Creates Transaction | Requires Existing Transaction |
|--------|-------------------|------------------------------|
| InvokeExceptionFilters | No | Yes |

### HandleUnknownActionWrapper
- **Wrapper**: [HandleUnknownActionWrapper.cs](HandleUnknownActionWrapper.cs)
- **Assembly**: `System.Web.Mvc`
- **Type**: `System.Web.Mvc.Controller`

| Method | Creates Transaction | Requires Existing Transaction |
|--------|-------------------|------------------------------|
| HandleUnknownAction | No | Yes |

### GetControllerInstanceWrapper
- **Wrapper**: [GetControllerInstanceWrapper.cs](GetControllerInstanceWrapper.cs)
- **Assembly**: `System.Web.Mvc`
- **Type**: `System.Web.Mvc.DefaultControllerFactory`

| Method | Creates Transaction | Requires Existing Transaction |
|--------|-------------------|------------------------------|
| GetControllerInstance | No | Yes |

[Instrumentation.xml](https://github.com/newrelic/newrelic-dotnet-agent/blob/main/src/Agent/NewRelic/Agent/Extensions/Providers/Wrapper/Mvc3/Instrumentation.xml)

## Transaction Naming

The wrapper extracts controller and action names from route data and sets the web transaction name:

- **Format**: `{ControllerName}/{ActionName}`
- **Priority**: `TransactionNamePriority.FrameworkLow` (allows higher-priority naming from attributes or custom code)
- **Source**: Retrieved from `ControllerContext.RouteData`

Example transaction names:
- `Home/Index`
- `Products/Details`
- `Account/Login`

## Method Segments

For each controller action invocation, the wrapper creates a method segment:

- **Type name**: Controller short name (e.g., "HomeController" → "Home")
- **Method name**: Action name (e.g., "Index", "Details")
- **Metadata**: Full controller type name and action name are set as `UserCodeNamespace` and `UserCodeFunction` (for internal use, not exposed as attributes)

## Async Action Handling

ASP.NET MVC supports asynchronous controller actions through the Async Action Invoker pattern:

1. **BeginInvokeAction**: Starts async action execution
   - Attaches to async context
   - Sets transaction name
   - Creates and stores segment in `HttpContext.Items`

2. **EndInvokeAction**: Completes async action execution
   - Retrieves segment from `HttpContext.Items`
   - Ends the segment

## Exception Filter Handling

The `InvokeExceptionFiltersWrapper` instruments exception filter execution:

- **Purpose**: Captures timing of exception handling logic
- **Coverage**: All exception filters registered for the action
- **Note**: Does not capture or report the exceptions themselves (handled by ASP.NET instrumentation)

## Unknown Action Handling

The `HandleUnknownActionWrapper` instruments the fallback method called when a requested action doesn't exist:

- **Transaction naming**: Sets transaction name to `{ControllerName}/HandleUnknownAction`
- **Use case**: Captures 404 scenarios within MVC routing

## Controller Instantiation

The `GetControllerInstanceWrapper` instruments controller factory instantiation:

- **Purpose**: Tracks time spent creating controller instances (including dependency injection)
- **Coverage**: All controllers created through `DefaultControllerFactory`

## License
Copyright 2020 New Relic, Inc. All rights reserved.
SPDX-License-Identifier: Apache-2.0

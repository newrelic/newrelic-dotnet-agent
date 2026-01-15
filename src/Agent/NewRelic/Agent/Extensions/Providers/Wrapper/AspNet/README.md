# New Relic .NET Agent AspNet Instrumentation

## Overview

The AspNet instrumentation wrapper provides automatic monitoring for ASP.NET Framework web applications. It supports both Classic and Integrated pipeline modes, creating and managing web transactions throughout the request lifecycle, capturing timing for pipeline events, naming transactions based on routes/handlers/pages, handling errors, and injecting browser monitoring scripts.

## Instrumented Methods

### CreateEventExecutionStepsWrapper (Classic Pipeline)
- **Wrapper**: [CreateEventExecutionStepsWrapper.cs](ClassicPipeline/CreateEventExecutionStepsWrapper.cs)
- **Assembly**: `System.Web`
- **Type**: `System.Web.HttpApplication`

| Method | Creates Transaction | Requires Existing Transaction |
|--------|-------------------|------------------------------|
| CreateEventExecutionSteps | Yes | No |

### ExecuteStepWrapper (Integrated Pipeline)
- **Wrapper**: [ExecuteStepWrapper.cs](IntegratedPipeline/ExecuteStepWrapper.cs)
- **Assembly**: `System.Web`
- **Type**: `System.Web.HttpApplication`

| Method | Creates Transaction | Requires Existing Transaction |
|--------|-------------------|------------------------------|
| ExecuteStep | Yes | No |

### FinishPipelineRequestWrapper (Integrated Pipeline)
- **Wrapper**: [FinishPipelineRequestWrapper.cs](IntegratedPipeline/FinishPipelineRequestWrapper.cs)
- **Assembly**: `System.Web`
- **Type**: `System.Web.HttpRuntime`

| Method | Creates Transaction | Requires Existing Transaction |
|--------|-------------------|------------------------------|
| FinishPipelineRequest | No | No |

### OnErrorWrapper
- **Wrapper**: [OnErrorWrapper.cs](Shared/OnErrorWrapper.cs)
- **Assembly**: `System.Web`
- **Type**: `System.Web.HttpApplication`

| Method | Creates Transaction | Requires Existing Transaction |
|--------|-------------------|------------------------------|
| RecordError | No | Yes |

### RouteNamingWrapper
- **Wrapper**: [RouteNamingWrapper.cs](Shared/RouteNamingWrapper.cs)
- **Assembly**: `System.Web`
- **Type**: `System.Web.Routing.RouteCollection`

| Method | Creates Transaction | Requires Existing Transaction |
|--------|-------------------|------------------------------|
| GetRouteData | No | Yes |

### CallHandlerWrapper
- **Wrapper**: [CallHandlerWrapper.cs](Shared/CallHandlerWrapper.cs)
- **Assembly**: `System.Web`
- **Type**: `System.Web.HttpApplication+CallHandlerExecutionStep`

| Method | Creates Transaction | Requires Existing Transaction |
|--------|-------------------|------------------------------|
| System.Web.HttpApplication.IExecutionStep.Execute | No | Yes |

### FilterWrapper
- **Wrapper**: [FilterWrapper.cs](Shared/FilterWrapper.cs)
- **Assembly**: `System.Web`
- **Type**: `System.Web.HttpWriter`

| Method | Creates Transaction | Requires Existing Transaction |
|--------|-------------------|------------------------------|
| Filter | No | No |
| FilterIntegrated | No | No |

### AspPagesTransactionNameWrapper
- **Wrapper**: [AspPagesTransactionNameWrapper.cs](Shared/AspPagesTransactionNameWrapper.cs)
- **Assembly**: `System.Web`
- **Type**: `System.Web.UI.Page`

| Method | Creates Transaction | Requires Existing Transaction |
|--------|-------------------|------------------------------|
| AsyncPageBeginProcessRequest | No | Yes |
| ProcessRequest | No | Yes |

### Additional Instrumentation (No Wrappers)
- **Assembly**: `System.Web`
- **Type**: `System.Web.UI.Page`

| Method | Creates Transaction | Requires Existing Transaction |
|--------|-------------------|------------------------------|
| PerformPreInit | No | No |

- **Assembly**: `System.Web`
- **Type**: `System.Web.Compilation.AssemblyBuilder`

| Method | Creates Transaction | Requires Existing Transaction |
|--------|-------------------|------------------------------|
| Compile | No | No |

- **Assembly**: `System.Web`
- **Type**: `System.Web.Compilation.BuildManager`

| Method | Creates Transaction | Requires Existing Transaction |
|--------|-------------------|------------------------------|
| CompileWebFile | No | No |

- **Assembly**: `System.Web.Extensions`
- **Type**: `System.Web.Handlers.ScriptResourceHandler`

| Method | Creates Transaction | Requires Existing Transaction |
|--------|-------------------|------------------------------|
| Throw404 | No | No |

[Instrumentation.xml](https://github.com/newrelic/newrelic-dotnet-agent/blob/main/src/Agent/NewRelic/Agent/Extensions/Providers/Wrapper/AspNet/Instrumentation.xml)

## Transaction Lifecycle

### Classic Pipeline Mode

Transactions are created and managed using the `CreateEventExecutionSteps` wrapper, which injects event handlers before and after each pipeline event:

1. **BeginRequest event**: Creates transaction with category "ASP" and initial name "Classic Pipeline"
2. **Pipeline events**: Creates segments for each event (AuthenticateRequest, AuthorizeRequest, ResolveRequestCache, MapRequestHandler, AcquireRequestState, PreRequestHandlerExecute, PostRequestHandlerExecute, etc.)
3. **EndRequest event**: Ends transaction and performs cleanup

### Integrated Pipeline Mode

Transactions are created and managed using the `ExecuteStep` and `FinishPipelineRequest` wrappers:

1. **Early pipeline notifications** (BeginRequest through PreExecuteRequestHandler): Creates transaction with category "ASP" and initial name "Integrated Pipeline" if no transaction exists
2. **Late pipeline detection**: If `ExecuteStep` is reached after AcquireRequestState without a transaction, no transaction is created (likely occurred during agent startup)
3. **Each notification**: Creates segment with notification name (e.g., "AuthenticateRequest", "ExecuteRequestHandler")
4. **FinishPipelineRequest**: Ends transaction and performs cleanup

### OPTIONS Request Handling

Both pipeline modes skip instrumenting HTTP OPTIONS pre-flight requests to avoid creating unnecessary transactions.

## Transaction Naming

Transaction names are determined by priority using `TransactionNamePriority`:

1. **FrameworkHigh**: ASP.NET Pages (`Page.ProcessRequest`) - uses `Page.AppRelativeVirtualPath` (e.g., "mypage.aspx")
2. **Route**: MVC/WebAPI Routes (`RouteCollection.GetRouteData`) - uses `Route.Url` pattern (e.g., "api/{controller}/{action}")
3. **Handler**: HTTP Handler type name (`CallHandlerWrapper`) - uses handler class name (e.g., "MyCustomHandler")
4. **FrameworkLow**: Default fallback - uses request path from `HttpRequest.Path`

## Attributes Added

The wrapper captures and stores the following attributes during transaction startup and shutdown:

- **Request metadata**:
  - `request.method`: HTTP method (GET, POST, etc.)
  - `request.uri`: Request path
  - `request.referer`: Referrer URI if present
  - `request.headers.*`: Request headers (all headers if `allow_all_headers` is enabled, otherwise default set)
  - `request.parameters.*`: Query string parameters

- **Response metadata**:
  - `http.statusCode`: HTTP status code
  - `response.status`: Sub-status code (Integrated pipeline only)
  - Response headers for distributed tracing

- **Timing**:
  - Queue time: Calculated from `HttpWorkerRequest.GetStartTime()` to transaction start

## Distributed Tracing

The wrapper handles distributed tracing during transaction startup by accepting trace context headers from incoming requests:

1. **Header acceptance**: `AcceptDistributedTraceHeaders` is called in `HttpContextActions.ProcessHeaders`
2. **Headers processed**: Standard W3C Trace Context and New Relic proprietary headers
3. **Transport type**: Set to `TransportType.HTTP`
4. **Response headers**: Distributed tracing response headers are written during transaction shutdown via `HttpResponse.AddHeader`

## Browser Monitoring Injection

The `FilterWrapper` attaches a response filter to inject browser monitoring scripts:

1. **Injection point**: Triggered by `HttpWriter.Filter` or `HttpWriter.FilterIntegrated` methods
2. **Conditions**: Only injects when status code < 300, content type is HTML, and filter hasn't already been attached
3. **Filter timing**: Must occur late enough to avoid corrupting WebResources but early enough to capture default document redirects
4. **Filter hack**: Sets `HttpResponse.Filter = null` early in pipeline to trigger necessary ASP.NET side effects without actually attaching a filter

## Error Handling

Errors are captured via the `OnErrorWrapper`, which instruments `HttpApplication.RecordError`:

1. **Method**: `RecordError` is called by ASP.NET when unhandled exceptions occur
2. **Capture**: Exception is passed to `Transaction.NoticeError`
3. **Requirement**: Requires an existing transaction

## License
Copyright 2020 New Relic, Inc. All rights reserved.
SPDX-License-Identifier: Apache-2.0
# Changelog
All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]
### New Features
### Fixes
* Fixes issue where applications running on Linux were either reporting no physical memory usage or using VmData to report the physical memory usage of the application. The agent now uses VmRSS through a call to [`Process.WorkingSet64`](https://docs.microsoft.com/en-us/dotnet/api/system.diagnostics.process.workingset64) to report physical memory usage. See the [dotnet runtime discussion](https://github.com/dotnet/runtime/issues/28990) and the [proc man pages](https://man7.org/linux/man-pages/man5/proc.5.html) for more details about this change.

## [8.29] - 2020-06-25
### New Features

* **Additional Transaction Information applied to Span Events** <br/>
When Distributed Tracing and/or Infinite Tracing are enabled, the Agent will now incorporate additional information from the Transaction Event on to the root Span Event of the transaction.

    * The following items are affected:

        * Request Parameters `request.parameter.*`
        * Custom Attribute Values applied to the Transaction via API Calls [`AddCustomParameter`](https://docs.newrelic.com/docs/agents/net-agent/net-agent-api/add-custom-parameter) and [`ITransaction.AddCustomAttribute`](https://docs.newrelic.com/docs/agents/net-agent/net-agent-api/itransaction#addcustomattribute).
        * `request.uri`
        * `response.status`
        * `host.displayName`
    * **Security Recommendation** <br>
    Review your [Transaction Attributes](https://docs.newrelic.com/docs/agents/net-agent/configuration/net-agent-configuration#transaction_events) configuration.  Any attribute include or exclude settings specific to Transaction Events, should be applied to your [Span Attributes](https://docs.newrelic.com/docs/agents/net-agent/configuration/net-agent-configuration#span_events) configuration or your [Global Attributes](https://docs.newrelic.com/docs/agents/net-agent/configuration/net-agent-configuration#agent-attributes) configuration.
    
### Fixes
Fixes issue where updating custom instrumentation while application is running could cause application to crash.

## [8.28] - 2020-06-04
### New Features
### Fixes
* Fixes issue with Infinite Tracing where a communication error can result in consuming too much CPU.
* Fixes issue with Infinite Tracing where a communication error did not clean up its corresponding communication threads.
* Fixes issue in .NET Framework ASP.NET MVC applications where transactions started on one thread would flow to background threads (e.g., started with `Task.Run`) in some scenarios but not others. Transaction state used to only flow to a background thread if the transaction originated from an async controller action. Transaction state now flows to background threads regardless of whether the controller action is async or not.
* Fixes issue in .NET Framework ASP.NET MVC applications where agent instrumentation of an MVC controller action could cause an `InvalidProgramException`.
* Fixes a problem with the reporting of Errors where Error Events may not appear even though Error Traces are being sent.

## [8.27] - 2020-04-30
### New Features
* **Support for W3C Trace Context, with easy upgrade from New Relic trace context**
  * [Distributed Tracing now supports W3C Trace Context headers](https://docs.newrelic.com/docs/understand-dependencies/distributed-tracing/get-started/introduction-distributed-tracing#w3c-support) for HTTP when distributed tracing is enabled.  Our implementation can accept and emit both W3C trace header format and New Relic trace header format. This simplifies agent upgrades, allowing trace context to be propagated between services with older and newer releases of New Relic agents. W3C trace header format will always be accepted and emitted. New Relic trace header format will be accepted, and you can optionally disable emission of the New Relic trace header format.
  * When distributed tracing is enabled with `<distributedTracing enabled="true" />`, the .NET agent will now accept W3C's `traceparent` and `tracestate` headers when calling [Transaction.AcceptDistributedTraceHeaders](https://docs.newrelic.com/docs/agents/net-agent/net-agent-api/itransaction#acceptdistributedtraceheaders).  When calling [Transaction.InsertDistributedTraceHeaders](https://docs.newrelic.com/docs/agents/net-agent/net-agent-api/itransaction#insertdistributedtraceheaders), the .NET agent will include the W3C headers along with the New Relic distributed tracing header, unless the New Relic trace header format is disabled using `<distributedTracing enabled="true" excludeNewrelicHeader="true" />`.
  * The existing `Transaction.AcceptDistributedTracePayload` and `Transaction.CreateDistributedTracePayload` APIs are **deprecated** in favor of [Transaction.AcceptDistributedTraceHeaders](https://docs.newrelic.com/docs/agents/net-agent/net-agent-api/itransaction#acceptdistributedtraceheaders) and [Transaction.InsertDistributedTraceHeaders](https://docs.newrelic.com/docs/agents/net-agent/net-agent-api/itransaction#insertdistributedtraceheaders).

### Fixes
* Fixes issue which prevented Synthetics from working when distributed tracing is enabled.
* Fixes issue where our RPM package for installing the agent on RPM-based Linux distributions included a 32-bit shared library, which created unnecessary dependencies on 
  32-bit system libraries.
* Fixes issue where the TransportDuration metric for distributed traces was always reporting 0.


## [8.26] - 2020-04-20
### New Features
* **Infinite Tracing on New Relic Edge**

  This release adds support for [Infinite Tracing on New Relic Edge](https://docs.newrelic.com/docs/understand-dependencies/distributed-tracing/enable-configure/enable-distributed-tracing). Infinite Tracing observes 100% of your distributed traces and provides visualizations for the most actionable data so you have the examples of errors and long-running traces so you can better diagnose and troubleshoot your systems.

  You configure your agent to send traces to a trace observer in New Relic Edge. You view your distributed traces through the New Relic’s UI. There is no need to install a collector on your network.

  Infinite Tracing is currently available on a sign-up basis. If you would like to participate, please contact your sales representative.
  
* **Error attributes now added to each span that exits with an error or exception**

  Error attributes `error.class` and `error.message` are now included on the span event in which an error or exception was noticed, and, in the case of unhandled exceptions, on any ancestor spans that also exit with an error. The public API method `NoticeError` now attaches these error attributes to the currently executing span.

  [Spans with error details are now highlighted red in the Distributed Tracing UI](https://docs.newrelic.com/docs/apm/distributed-tracing/ui-data/understand-use-distributed-tracing-data#rules-limits), and error details will expose the associated `error.class` and `error.message`. It is also now possible to see when an exception leaves the boundary of the span, and if it is caught in an ancestor span without reaching the entry span. NOTE: This “bubbling up” of exceptions will impact the error count when compared to prior behavior for the same trace. It is possible to have a trace that now has span errors without the trace level showing an error.

  If multiple errors occur on the same span, only the most recent error information is added to the attributes. Prior errors on the same span are overwritten.

  These span event attributes conform to [ignored errors](https://docs.newrelic.com/docs/agents/manage-apm-agents/agent-data/manage-errors-apm-collect-ignore-or-mark-expected#ignore) configuration.

### Fixes
* Fixes issue in the MSI installer which prevented the `InstrumentAllNETFramework` feature selection from working as expected on the command line.
* Fixes issue for Azure App Service environments running on Linux that caused both the application and its Kudu process to be instrumented by the agent. The Kudu process is no longer instrumented.
* Fixes issue when using the [`ignoreErrors` configuration](https://docs.newrelic.com/docs/agents/net-agent/configuration/net-agent-configuration#error-ignoreErrors). Previously, when an exception contained a inner exception(s), the `ignoreErrors` config was only applied to the outer-most exception. Now, both the outer-most and inner-most exception type are considered when evaluating the `ignoreErrors` configuration.
* Fixes an issue that could cause an exception to occur in the instrumentation for StackExchange Redis. This exception caused the instrumentation to shut down leaving StackExchange Redis uninstrumented.

## [8.25] - 2020-03-11

### New Features
* **Thread profiling support for Linux**

  Thread profiling on Linux will be supported on .NET Core 3.0 or later applications when running .NET agent version 8.23 or later. Triggering a thread profile is done from the `Thread profiler` page in APM. This page does not yet have the functionality enabled, but it will be enabled in the next few business days.

* **Accessing Span-Specific information using the .NET Agent API**
  
  New property, `CurrentSpan` has been added to `IAgent` and `ITransaction`.  It returns an object implementing `ISpan` which provides access to span-specific functions within the API.

* **Adding Custom Span Attributes using the .NET Agent API**
  
  New method, `AddCustomAttribute(string, object)` has been added to `ISpan`.

  * This new method accepts and supports all data-types.
  * Further information may be found within [.NET Agent API documentation](https://docs.newrelic.com/docs/agents/net-agent/net-agent-api/iSpan).
  * Adding custom attributes to spans requires distributed tracing and span events to be enabled. See [.NET agent configuration](https://docs.newrelic.com/docs/agents/net-agent/configuration/net-agent-configuration#distributed_tracing)


### Fixes
* Fixes issue where adding multiple custom attributes on a Transaction using [`ITransaction.AddCustomAttribute`](https://docs.newrelic.com/docs/agents/net-agent/net-agent-api/itransaction#addcustomattribute) causes the agent to ignore additional attempts to add custom attributes to any transaction. 
* Fixes issue that prevented Custom Events from being sent to New Relic until the agent shuts down.
* Fixes issue that can cause asynchronous Redis calls in an ASP.NET MVC application to report an inflated duration.


## [8.24] - 2020-02-19

### New Features
* **Adding Custom Transaction Attributes using the .NET Agent API**

  New method, `AddCustomAttribute(string, object)` has been added to `ITransaction`.
  * This new method accepts and supports all data-types.
  * Method `AddCustomParameter(string, IConvertable)` is still available with limited data-type support; however, this method should be considered obsolete and will  be removed in a future release of the Agent API.
  * Further information may be found within [.NET Agent API documentation](https://docs.newrelic.com/docs/agents/net-agent/net-agent-api/itransaction).

* **Enhanced type support for `RecordCustomEvent` and `NoticeError` API Methods.**

  APIs for recording exceptions and custom events now support values of all types.
  * The `NoticeError` API Method has new overloads that accept an `IDictionary<string, object>`.
  * The `RecordCustomEvent` methods have been modified to handle all types of data.  In that past, they only handled `string` and `float` types.
  * Further information may be found within [.NET Agent API documentation](https://docs.newrelic.com/docs/agents/net-agent/net-agent-api).

* **New attributes on span events**

  * Spans created for external HTTP calls now include the `http.statusCode` attribute representing the status code of the call.
  * Spans created for calls to a datastore now include the `db.collection` attribute. For instance, this will be the table name for a call to MS SQL Server.

* **Ability to exclude attributes from span events**

  Attributes on span events (e.g., `http.url`) can now be excluded via configuration. See [.NET agent configuration](https://docs.newrelic.com/docs/agents/net-agent/configuration/net-agent-configuration#span_events) for further information.


### Fixes
* New Relic distributed tracing relies on propagating trace and span identifiers in the headers of external calls (e.g., an HTTP call). These identifiers now only contain lowercase alphanumeric characters. Previous versions of the .NET agent used uppercase alphanumeric characters. The usage of uppercase alphanumeric characters can break traces when calling downstream services also monitored by a New Relic agent that supports W3C trace context (New Relic's .NET agent does not currently support W3C trace context. Support for W3C trace context for .NET will be in an upcoming release). This is only a problem if a .NET application is the originator of the trace.

[Unreleased]: https://github.com/newrelic/newrelic-dotnet-agent/compare/v8.29.0...HEAD

[8.29]: https://github.com/newrelic/newrelic-dotnet-agent/compare/v8.28.0...v8.29.0
[8.28]: https://github.com/newrelic/newrelic-dotnet-agent/compare/v8.27.139...v8.28.0
[8.27]: https://github.com/newrelic/newrelic-dotnet-agent/compare/v8.26.630...v8.27.139
[8.26]: https://github.com/newrelic/newrelic-dotnet-agent/compare/v8.25.214...v8.26.630
[8.25]: https://github.com/newrelic/newrelic-dotnet-agent/compare/v8.24.244...v8.25.214
[8.24]: https://github.com/newrelic/newrelic-dotnet-agent/compare/v8.23.107...v8.24.244

# Changelog
All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

## [Unreleased]

### New Features
* **Accessing Span-Specific information using the .NET Agent API**
  
  New property, `CurrentSpan` has been added to `IAgent` and `ITransaction`.  It returns an object implementing `ISpan` which provides access to span-specific functions within the API.

* **Adding Custom Span Attributes using the .NET Agent API**
  
  New method, `AddCustomAttribute(string, object)` has been added to `ISpan`.

  * This new method accepts and supports all data-types.
  * Further information may be found within [.NET Agent API documentation](https://docs.newrelic.com/docs/agents/net-agent/net-agent-api/iSpan).


### Fixes
* **`AddCustomAttribute` on `ITransaction`**

  Fixes issue where adding multiple custom attributes on a Transaction using the API causes the Agent to ignore additional attempts to add custom attributes to any transaction.
  
* Fixes a bug that prevented Custom Events from being sent to New Relic until the agent shuts down.
* Fixes a bug that can asynchronous Redis calls in an ASP.NET MVC application to report an inflated duration.

### Docs to publish
* https://docs.newrelic.com/docs/agents/net-agent/net-agent-api/itransaction
* https://docs.newrelic.com/docs/agents/net-agent/net-agent-api/iAgent
* https://docs.newrelic.com/docs/agents/net-agent/net-agent-api/iSpan



## [8.24] - 2019-02-19

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

[Unreleased]: https://source.datanerd.us/dotNetAgent/dotnet_agent/compare/1228ae82530e4fec686dd8eafc1885b54ec8dc82...HEAD
[8.24]: https://source.datanerd.us/dotNetAgent/dotnet_agent/compare/v8.24...1228ae82530e4fec686dd8eafc1885b54ec8dc82

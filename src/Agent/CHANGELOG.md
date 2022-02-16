# Changelog
All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased] changes
### New Features
* Adds new supportability metrics to track agent endpoint data usage. New metrics will be reported under the `Supportability/DotNET/Collector` namespace. ([#899](https://github.com/newrelic/newrelic-dotnet-agent/pull/899))
### Fixes

## [9.5.1] - 2022-02-03
### Fixes
* Fixes [application crashes on Alpine Linux](https://github.com/newrelic/newrelic-dotnet-agent/issues/918) introduced in 9.5.0. ([#929]https://github.com/newrelic/newrelic-dotnet-agent/pull/929)

## [9.5.0] - 2022-02-01
### New Features
* Internal improvements to runtime detection logic in the profiler component of the agent. ([#891](https://github.com/newrelic/newrelic-dotnet-agent/pull/891))
### Fixes
* Fixed an [issue with NuGet package metadata](https://github.com/newrelic/newrelic-dotnet-agent/issues/896). ([#901](https://github.com/newrelic/newrelic-dotnet-agent/pull/901))

## [9.4.0] - 2022-01-18
### New Features
* Allows NewRelicQueryName to be specified for SQL, to implement [this suggestion](https://discuss.newrelic.com/t/provide-a-pattern-to-explicitly-name-sql-queries-displayed-in-databases-dashboard/78755). Thanks to community contributor @kevinpohlmeier for the implementation. ([#799](https://github.com/newrelic/newrelic-dotnet-agent/pull/799))
### Fixes
* Resolves an issue where GC metrics were not being properly captured for .NET 6 applications ([#874](https://github.com/newrelic/newrelic-dotnet-agent/pull/874))

## [9.3.0] - 2022-01-04
### New Features
* NServiceBus versions 6 and 7 are now supported in .NET Framework and .NET Core. ([#857](https://github.com/newrelic/newrelic-dotnet-agent/pull/857))
* Add ability to disable agent support for Server-Configuration with `NEW_RELIC_IGNORE_SERVER_SIDE_CONFIG` environment variable. The available value options are `true` and `false`. ([#814](https://github.com/newrelic/newrelic-dotnet-agent/pull/814))
### Fixes
* Fixes issue [#36](https://github.com/newrelic/newrelic-dotnet-agent/issues/36): Total system memory will now be correctly reported on Linux. ([#855](https://github.com/newrelic/newrelic-dotnet-agent/pull/855))
* Fixes an issue in `newrelic.config` file schema validation that could block agent startup. ([#835](https://github.com/newrelic/newrelic-dotnet-agent/pull/835))

## [9.2.0] - 2021-11-18
### .NET 6 Compatibility
As of version 9.2.0, the New Relic .NET Core agent supports .NET 6.

### New Features
* Adds automatic instrumentation for the `Microsoft.Azure.Cosmos` client library. ([#811](https://github.com/newrelic/newrelic-dotnet-agent/pull/811))
* Adds additional logging to the Garbage Collection performance metrics to aid in troubleshooting performance counter issues. ([#792](https://github.com/newrelic/newrelic-dotnet-agent/pull/792))
* Feature [#800](https://github.com/newrelic/newrelic-dotnet-agent/issues/800): On .NET Framework apps instrumented with the .NET Framework agent, the value of the ".NET Version" property in the Environment data page will more accurately reflect the version of .NET Framework in use. ([#801](https://github.com/newrelic/newrelic-dotnet-agent/pull/801))  
### Fixes
* Fixes issue [#803](https://github.com/newrelic/newrelic-dotnet-agent/issues/803): Thread safety issue occurred when accessing HTTP headers collection in HttpClient on .NET 6. ([#804](https://github.com/newrelic/newrelic-dotnet-agent/pull/804))

## [9.1.1] - 2021-11-02
### Fixes
* Fixes issue [#780](https://github.com/newrelic/newrelic-dotnet-agent/issues/780): Improves management of gRPC channels during connection failure scenarios. ([#782](https://github.com/newrelic/newrelic-dotnet-agent/pull/782))
* Fixes issue [#781](https://github.com/newrelic/newrelic-dotnet-agent/issues/781): Windows MSI installer was not deploying gRPC libraries for netcore applications. ([#788](https://github.com/newrelic/newrelic-dotnet-agent/pull/788))

## [9.1.0] - 2021-10-26
### New Features
* Feature [#365](https://github.com/newrelic/newrelic-dotnet-agent/issues/365): This version adds support for the Linux ARM64/AWS Graviton2 platform using .NET 5.0. ([#768](https://github.com/newrelic/newrelic-dotnet-agent/pull/768))
  * Includes a new `Processor Architecture` property reported by the Agent with the Environment.
### Fixes
* Fixes issue [#754](https://github.com/newrelic/newrelic-dotnet-agent/issues/754): Agent could cause applications that use configuration builders from `Microsoft.Configuration.ConfigurationBuilders` to hang on startup. ([#753](https://github.com/newrelic/newrelic-dotnet-agent/pull/753))

## [9.0.0] - 2021-09-16
### New Features
* Feature [#672](https://github.com/newrelic/newrelic-dotnet-agent/issues/672): This release of the .NET agent enables Distributed Tracing by default, and deprecates Cross Application Tracing. ([#700](https://github.com/newrelic/newrelic-dotnet-agent/pull/700))
* Feature [#671](https://github.com/newrelic/newrelic-dotnet-agent/issues/671): The maximum number of samples stored for Span Events can be configured via the `spanEvents.maximumSamplesStored` configuration in the `newrelic.config` or the `NEW_RELIC_SPAN_EVENTS_MAX_SAMPLES_STORED` Environemnt Variable.([#701](https://github.com/newrelic/newrelic-dotnet-agent/pull/701))
* Feature [#703](https://github.com/newrelic/newrelic-dotnet-agent/issues/703): Increases the default maximum number of samples stored for Span Events from 1000 to 2000.([#705](https://github.com/newrelic/newrelic-dotnet-agent/pull/705))
* Feature [#532](https://github.com/newrelic/newrelic-dotnet-agent/issues/532): Adds Environment variables for log level `NEWRELIC_LOG_LEVEL` and directory `NEWRELIC_LOG_DIRECTORY` to allow better control of logs for the Agent and the Profiler. ([#717](https://github.com/newrelic/newrelic-dotnet-agent/pull/717))

### Fixes
* Fixes issue [#707](https://github.com/newrelic/newrelic-dotnet-agent/issues/707): In 8.40.1 SQL explain plans are not being captured for parameterized SQL statements. ([#708](https://github.com/newrelic/newrelic-dotnet-agent/pull/708))
* Fixes issue [#502](https://github.com/newrelic/newrelic-dotnet-agent/issues/502): Agent encountering serialization error ([#715](https://github.com/newrelic/newrelic-dotnet-agent/pull/715))
* Fixes issue [#679](https://github.com/newrelic/newrelic-dotnet-agent/issues/679): Update gRPC libraries from 2.35.0 to 2.40.0 to reduce installation size ([#721](https://github.com/newrelic/newrelic-dotnet-agent/pull/721))

### Deprecations/Removed Features
* Cross Application Tracing is now deprecated, and disabled by default. To continue using it, enable it with `crossApplicationTracer.enabled = true` and `distributedTracing.enabled = false`.
* Issue [#667](https://github.com/newrelic/newrelic-dotnet-agent/issues/611), [668](https://github.com/newrelic/newrelic-dotnet-agent/issues/668), [#669](https://github.com/newrelic/newrelic-dotnet-agent/issues/669): previously deprecated agent configuration options are now disabled.  See the [migration guide](https://docs.newrelic.com/docs/agents/net-agent/getting-started/8x-to-9x-agent-migration-guide/#removal-of-deprecated-agent-configuration-settings) for details.
* Issue [#666](https://github.com/newrelic/newrelic-dotnet-agent/issues/666): previously deprecated agent APIs have been removed, and disabled in the Agent. Disabled APIs will log a warning when invoked by old versions of the Agent API. See the [migration guide](https://docs.newrelic.com/docs/agents/net-agent/getting-started/8x-to-9x-agent-migration-guide/#removal-of-deprecated-public-agent-api-methods) for details. ([#687](https://github.com/newrelic/newrelic-dotnet-agent/pull/687))
* Issue [#702](https://github.com/newrelic/newrelic-dotnet-agent/issues/702) Deprecate instrumentation for Castle.Monorail ([#710](https://github.com/newrelic/newrelic-dotnet-agent/pull/710))

## [8.41.1] - 2021-08-25
### New Features
### Fixes
* Fixes issue [#627](https://github.com/newrelic/newrelic-dotnet-agent/issues/627): Grpc channel shutdown can cause `license_key is required` error message. ([#663](https://github.com/newrelic/newrelic-dotnet-agent/pull/663))
* Fixes issue [#683](https://github.com/newrelic/newrelic-dotnet-agent/issues/683): Requested stack trace depth is not always honored. ([#684](https://github.com/newrelic/newrelic-dotnet-agent/pull/684))

## [8.41.0] - 2021-07-21
### New Features
* Feature [#611](https://github.com/newrelic/newrelic-dotnet-agent/issues/611): Capture HTTP request method on transactions in the AspNetCore, Asp35, Wcf3, and Owin wrappers.
* Feature [#580](https://github.com/newrelic/newrelic-dotnet-agent/issues/580): Send initial app name and source in environment data. ([#653](https://github.com/newrelic/newrelic-dotnet-agent/pull/653))
* Adds support for capturing stack traces for each instrumented method in a Transaction Trace.
  * This feature is disabled by default.
  * You can enable the capture of stack traces by setting either maxStackTrace to any value greater than 1.  This value will only be used to determine if stack traces are captured or not despite the name.
  * The following are the default settings for stack traces. These can be changed using the newrelic.config:
    * A maximum 80 stack frames are reported per stack trace.

### Fixes
* Fixes issue [#639](https://github.com/newrelic/newrelic-dotnet-agent/issues/639): RabbitMQ instrumentation can delete user headers from messages. Thank you @witoldsz for finding and reporting this bug. ([#648](https://github.com/newrelic/newrelic-dotnet-agent/pull/648))

## [8.40.1] - 2021-07-08
### Fixes
* Fixes issue [#485](https://github.com/newrelic/newrelic-dotnet-agent/issues/485): `SendDataOnExit` configuration setting will prevent Infinite Traces data sending interuption on application exit. ([#550](https://github.com/newrelic/newrelic-dotnet-agent/pull/609))
* Fixes issue [#155](https://github.com/newrelic/newrelic-dotnet-agent/issues/155): MVC invalid Action for valid Controller can cause MGI. ([#608](https://github.com/newrelic/newrelic-dotnet-agent/pull/608))
* Fixes issue [#186](https://github.com/newrelic/newrelic-dotnet-agent/issues/186): Attribute based Routing (ex WebAPI) can cause transaction naming issues. ([#612](https://github.com/newrelic/newrelic-dotnet-agent/pull/612))
* Fixes issue [#463](https://github.com/newrelic/newrelic-dotnet-agent/issues/463): Handle OPTIONS requests for asp.net applications. ([#612](https://github.com/newrelic/newrelic-dotnet-agent/pull/612))
* Fixes issue [#551](https://github.com/newrelic/newrelic-dotnet-agent/issues/551): Missing external calls in WCF Service. ([#610](https://github.com/newrelic/newrelic-dotnet-agent/pull/610))
* Fixes issue [#616](https://github.com/newrelic/newrelic-dotnet-agent/issues/616): Linux Kudu not accessible when .NET agent presents. ([#618](https://github.com/newrelic/newrelic-dotnet-agent/pull/618))
* Fixes issue [#266](https://github.com/newrelic/newrelic-dotnet-agent/issues/266): Agent fails to initialize and provides no logs when configured with capitalized booleans. ([#617](https://github.com/newrelic/newrelic-dotnet-agent/pull/617))
* Explain plans will be created if [transactionTracer.explainEnabled](https://docs.newrelic.com/docs/agents/net-agent/configuration/net-agent-configuration/#tracer-explainEnabled) is true and one or both [transactionTracer.enabled](https://docs.newrelic.com/docs/agents/net-agent/configuration/net-agent-configuration/#tracer-enabled) or [slowSql.enabled](https://docs.newrelic.com/docs/agents/net-agent/configuration/net-agent-configuration/#slow_sql) are true.  If transactionTracer.explainEnabled is false or both transactionTracer.enabled and slowSql.enabled are false, no Explain Plans will be created.
* Fixes issue [#600](https://github.com/newrelic/newrelic-dotnet-agent/issues/600): Thread id will now be used in agent logging, even if a thread name has been set. ([#626](https://github.com/newrelic/newrelic-dotnet-agent/pull/626))
* Fixes issue [#476](https://github.com/newrelic/newrelic-dotnet-agent/issues/476): When generating and explain plan MS SQL parsing is matching parts of words instead of whole words
* Fixes issue [#477](https://github.com/newrelic/newrelic-dotnet-agent/issues/476): SQL Explain plans MS SQL parser needs to be able to ToString an object to work with parameterized queries
  * Improves handling serializable types like DateTimeOffset
  * The presence DbTypes Binary and Object will prevent an Explain Plan from being executed.  In order to execute an explain plan, the agent must replace any parameters in a query with the real values.  Binary and Object are too complex to properly serialize without introducing errors.

## [8.40.0] - 2021-06-08
### New Features
* Adds Agent support for capturing HTTP Request Headers.
  * Support included for ASP.NET 4.x, ASP.NET Core, Owin, and WCF instrumentation. ([#558](https://github.com/newrelic/newrelic-dotnet-agent/issues/558), [#559](https://github.com/newrelic/newrelic-dotnet-agent/issues/559), [#560](https://github.com/newrelic/newrelic-dotnet-agent/issues/560), [#561](https://github.com/newrelic/newrelic-dotnet-agent/issues/561))

### Fixes
* Fixes issue [#264](https://github.com/newrelic/newrelic-dotnet-agent/issues/264): Negative GC count metrics will now be clamped to 0, and a log message will be written to note the correction. This should resolve an issue where the GCSampler was encountering negative values and crashing. ([#550](https://github.com/newrelic/newrelic-dotnet-agent/pull/550))
* Fixes issue [#584](https://github.com/newrelic/newrelic-dotnet-agent/issues/584): When the agent is configured to log to the console, the configured logging level from `newrelic.config` will be respected. ([#587](https://github.com/newrelic/newrelic-dotnet-agent/pull/587))

## [8.39.2] - 2021-04-14
### Fixes
* Fixes issue [#500](https://github.com/newrelic/newrelic-dotnet-agent/issues/500): For transactions without errors, Agent should still create the `error` intrinsics attribute with its value set to `false`. ([#501](https://github.com/newrelic/newrelic-dotnet-agent/pull/501))
* Fixes issue [#522](https://github.com/newrelic/newrelic-dotnet-agent/issues/522): When the `maxStackTraceLines` config value is set to 0, the agent should not send any stack trace data in the `error_data` payload. ([#523](https://github.com/newrelic/newrelic-dotnet-agent/pull/523))

## [8.39.1] - 2021-03-17
### Fixes
* Fixes issue [#22](https://github.com/newrelic/newrelic-dotnet-agent/issues/22): Agent causes exception when distributed tracing is enabled in ASP.NET Core applications that use the RequestLocalization middleware in a Linux environment. ([#493](https://github.com/newrelic/newrelic-dotnet-agent/pull/493))
* Fixes issue [#267](https://github.com/newrelic/newrelic-dotnet-agent/issues/267): On Linux, the profiler fails to parse config files that start with a UTF-8 byte-order-mark (BOM). ([#492](https://github.com/newrelic/newrelic-dotnet-agent/pull/492))
* Fixes issue [#464](https://github.com/newrelic/newrelic-dotnet-agent/issues/464): Distributed tracing over RabbitMQ does not work with `RabbitMQ.Client` versions 6.x+ ([#466](https://github.com/newrelic/newrelic-dotnet-agent/pull/466))
* Fixes issue [#169](https://github.com/newrelic/newrelic-dotnet-agent/issues/169): Profiler should be able to match method parameters from XML that contain a space. ([#461](https://github.com/newrelic/newrelic-dotnet-agent/pull/461))

## [8.39] - 2021-02-10
### New Features
* Add `GetBrowserTimingHeader(string nonce)` overload.
  * This allows sites with a `Content-Security-Policy` that disables `'unsafe-inline'` to emit the inline script with a nonce.

### Fixes
* Fixes Issue [#394](https://github.com/newrelic/newrelic-dotnet-agent/issues/394): agent fails to enable infinite tracing in net5.0 docker images

## [8.38] - 2021-01-26
### New Features
* **Improvements to New Relic Edge (Infinite Tracing)** <br/>
  * The agent will now handle having its infinite tracing traffic moved from one backend host to another without losing data or requiring an agent restart.
  * Improved logging of infinite tracing connections.

## [8.37] - 2021-01-04
### New Features
* **Updated support for RabbitMQ** <br/>
  * Adds support for .NET Core applications using RabbitMQ.Client.
  * Adds support for RabbitMQ.Client version 6.2.1.
  * Not supported: Distributed Tracing is not supported with the RabbitMQ AMQP 1.0 plugin.

* **Adds [configuration](https://docs.newrelic.com/docs/agents/net-agent/configuration/net-agent-configuration) Environment Variables** <br/>
  * Adds MAX_TRANSACTION_SAMPLES_STORE - the maximum number of samples stored for Transaction Events.
  * Adds MAX_EVENT_SAMPLES_STORED - the maximum number of samples stored for Custom Events. 
  * Adds NEW_RELIC_LOG - the unqualifed name for the Agent's log file.

## [8.36] - 2020-12-08

### Fixes
* Fixes Issue [#224](https://github.com/newrelic/newrelic-dotnet-agent/issues/224): leading "SET" commands will be ignored when parsing compound SQL statements. ([#370](https://github.com/newrelic/newrelic-dotnet-agent/pull/370))
* Fixes Issue [#226](https://github.com/newrelic/newrelic-dotnet-agent/issues/226): the profiler ignores drive letter in `HOME_EXPANDED` when detecting running in Azure Web Apps. ([#373](https://github.com/newrelic/newrelic-dotnet-agent/pull/373))
* Fixes Issue [#93](https://github.com/newrelic/newrelic-dotnet-agent/issues/93): when the parent methods are blocked by their asynchronous child methods, the agent deducts the child methods' duration from the parent methods' exclusive duration.([#374](https://github.com/newrelic/newrelic-dotnet-agent/pull/374))
* Fixes Issue [#9](https://github.com/newrelic/newrelic-dotnet-agent/issues/9) where the agent failed to read settings from `appsettings.{environment}.json` files. ([#372](https://github.com/newrelic/newrelic-dotnet-agent/pull/372))
* Fixes Issue [#116](https://github.com/newrelic/newrelic-dotnet-agent/issues/116) where the agent failed to read settings from `appsettings.json` in certain hosting scenarios. ([#375](https://github.com/newrelic/newrelic-dotnet-agent/pull/375))
* Fixes Issue [#234](https://github.com/newrelic/newrelic-dotnet-agent/issues/234) by reducing the likelihood of a Fatal CLR Error. ([#376](https://github.com/newrelic/newrelic-dotnet-agent/pull/376))
* Fixes Issue [#377](https://github.com/newrelic/newrelic-dotnet-agent/issues/377) when using the `AddCustomAttribute` API with `Microsoft.Extensions.Primitives.StringValues` type causes unsupported type exception. ([378](https://github.com/newrelic/newrelic-dotnet-agent/pull/378))


## [8.35] - 2020-11-09

### New Features
* **.NET 5 GA Support** <br/>
We have validated that this version of the agent is compatible with .NET 5 GA. See the [compatibility and requirements for .NET Core](https://docs.newrelic.com/docs/agents/net-agent/getting-started/net-agent-compatibility-requirements-net-core) page for more details.

### Fixes
* Fixes Issue [#337](https://github.com/newrelic/newrelic-dotnet-agent/issues/337) by removing obsolete code which was causing memory growth associated with a large number of transaction names.
* PR [#348](https://github.com/newrelic/newrelic-dotnet-agent/pull/348): guards against potential exceptions being thrown from the agent API when the agent is not attached.  

## [8.34] - 2020-10-26

### New Features
* **.NET 5 RC2 Support** <br/>
We have validated that this version of the agent is compatible with .NET 5 Release Candidate 2.

### Fixes
* Fixes issue [#301](https://github.com/newrelic/newrelic-dotnet-agent/issues/301) where the agent incorrectly parses server-side configuration causing agent to shutdown.([#310](https://github.com/newrelic/newrelic-dotnet-agent/pull/310))
* Modifies WCF Instrumentation to address [#314](https://github.com/newrelic/newrelic-dotnet-agent/issues/314) by minimizing the reliance upon handled exceptions during the  attempt to capture CAT and DT payloads.

## [8.33] - 2020-10-12

### Fixes
* Fixes [#223](https://github.com/newrelic/newrelic-dotnet-agent/issues/223) so the agent can be compatible with ASP.NET Core 5 RC1.
* Fixes issue in .NET 5 applications where external calls made with HttpClient may not get instrumented. For example, calls made with `HttpClient.GetStringAsync` would be missed. ([#235](https://github.com/newrelic/newrelic-dotnet-agent/pull/235))
* Fixes issue [#257](https://github.com/newrelic/newrelic-dotnet-agent/issues/223) where .NET Standard Libraries that do not reference `mscorlib` fail to be instrumented in .NET Framework applications.
* Reduces the performance impact of large amounts of instrumentation. See issue [#269](https://github.com/newrelic/newrelic-dotnet-agent/issues/269) for more information.

## [8.32] - 2020-09-17

### New Features
* **Proxy Password Obfuscation Support** <br/>
Agent configuration supports the obfuscation of the proxy password. [The New Relic Command Line Interface (CLI)](https://github.com/newrelic/newrelic-cli/blob/main/README.md) may be used to obscure the proxy password.  The following [documentation](https://docs.newrelic.com/docs/agents/net-agent/configuration/net-agent-configuration#proxy) describes how to use an obscured proxy password in the .NET Agent configuration.
* **MySqlConnector Support** <br/>
The MySqlConnector ADO.NET driver is instrumented by default. Fixes [#85](https://github.com/newrelic/newrelic-dotnet-agent/issues/85) and implements [this suggestion](https://discuss.newrelic.com/t/feature-idea-support-mysqlconnector-driver-for-db-instrumentation/63414).

* **Nullable Reference Type support in the API**<br/>
Enables nullable reference types that are part of the C# 8.0 language specification and updates the signatures of API methods accordingly.  There should be no required changes in API usage.

* **Improved Support for NetTCP Binding in WCF Instrumation**
When the NetTCP Binding type is used in Windows Communication Foundation (WCF), the Agent will now send and receive trace context information in support of Distributed Tracing (DT) or Cross Application Tracing (CAT).  Implements [#209](https://github.com/newrelic/newrelic-dotnet-agent/issues/209).

### Fixes
* Fixes an issue that may cause `InvalidCastException` due to an assembly version mismatch in Mvc3 instrumentation.
* Fixes an async timing issue that can cause the end time of `Task`-returning methods to be determined incorrectly.


## [8.31] - 2020-08-17
### New Features
* **Expected Errors Support** <br/>
Certain errors that are expected within the application may be identified so that they will not be counted towards the application's error rate and Apdex Score.  Only errors that truly affect the health of the application will be alerted on.  Please review the following [documentation](https://docs.newrelic.com/docs/agents/net-agent/configuration/net-agent-configuration#error_collector) for details on how to configure Expected Errors.

* **Ignored Errors Enhancements** <br/>
Certain errors may be identified in configuration so that they will be ignored.  These errors will not be counted towards the application's error rate, Apdex score, and will not be reported by the agent. Please review the following [documentation](https://docs.newrelic.com/docs/agents/net-agent/configuration/net-agent-configuration#error_collector) for details on how to configure Ignored Errors.
    * New configuration element [`<ignoreMessages>`](https://docs.newrelic.com/docs/agents/net-agent/configuration/net-agent-configuration#error-ignoreErrors)supports filtering based on the error message. 
    * Please note that the [`<ignoreErrors>`](https://docs.newrelic.com/docs/agents/net-agent/configuration/net-agent-configuration#error-ignoreErrors) configuration element has been deprecated and replaced by [`<ignoreClasses>`](https://docs.newrelic.com/docs/agents/net-agent/configuration/net-agent-configuration#error-ignoreClasses).  The .NET Agent continues to support this configuration element, but its support may be removed in the future.

### Fixes
* **Garbage Collection Performance Metrics for Windows** <br/>
Fixes an issue where Garbage Collection Performance Metrics may not be reported for Windows Applications.

* **Maintaining newrelic.config on Linux package upgrades** <br/>
Fixes an issue where `newrelic.config` was being overwritten when upgrading the agent via either `rpm`/`yum` (RedHat/Centos) or `dpkg`/`apt` (Debian/Ubuntu).

## [8.30] - 2020-07-15
### New Features
* **The .NET Agent is now open source!** <br/>
The New Relic .NET agent is now open source! Now you can view the source code to help with troubleshooting, observe the project roadmap, and file issues directly in this repository.  We are now using the [Apache 2 license](/LICENSE). See our [Contributing guide](/CONTRIBUTING.md) and [Code of Conduct](https://opensource.newrelic.com/code-of-conduct/) for details on contributing!

### Fixes
* **Memory Usage Reporting for Linux** <br/>
Fixes issue where applications running on Linux were either reporting no physical memory usage or using VmData to report the physical memory usage of the application. The agent now uses VmRSS through a call to [`Process.WorkingSet64`](https://docs.microsoft.com/en-us/dotnet/api/system.diagnostics.process.workingset64) to report physical memory usage. See the [dotnet runtime discussion](https://github.com/dotnet/runtime/issues/28990) and the [proc man pages](https://man7.org/linux/man-pages/man5/proc.5.html) for more details about this change.

* **Infinite Tracing Performance** <br/>
Fixes issue where the Agent may consume too much memory when using Infinite Tracing.

* **.NET 5 support** <br/>
Fixes issue with applications running on .NET 5 that prevented instrumentation changes at runtime (either though editing instrumentation XML files or through the Live Instrumentation editor Beta).

## [8.29] - 2020-06-25
### New Features

* **Additional Transaction Information applied to Span Events** <br/>
When Distributed Tracing and/or Infinite Tracing are enabled, the Agent will now incorporate additional information from the Transaction Event on to the root Span Event of the transaction.

    * The following items are affected:

        * Request Parameters `request.parameter.*`
        * Custom Attribute Values applied to the Transaction via API Calls [`AddCustomParameter`](https://docs.newrelic.com/docs/agents/net-agent/net-agent-api/addcustomparameter-net-agent-api/) and [`ITransaction.AddCustomAttribute`](https://docs.newrelic.com/docs/agents/net-agent/net-agent-api/itransaction#addcustomattribute).
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
* **Infinite Tracing** <br>
    * Fixes issue with Infinite Tracing where a communication error can result in consuming too much CPU.
    * Fixes issue with Infinite Tracing where a communication error did not clean up its corresponding communication threads.
    * <p style="color:red;">Agent version 8.30 introduces significant performance enhancements to Infinite Tracing.  To use Infinite Tracing, please upgrade to version 8.30 or later.</p>

* Fixes issue in .NET Framework ASP.NET MVC applications where transactions started on one thread would flow to background threads (e.g., started with `Task.Run`) in some scenarios but not others. Transaction state used to only flow to a background thread if the transaction originated from an async controller action. Transaction state now flows to background threads regardless of whether the controller action is async or not.
* Fixes issue in .NET Framework ASP.NET MVC applications where agent instrumentation of an MVC controller action could cause an `InvalidProgramException`.
* Fixes a problem with the reporting of Errors where Error Events may not appear even though Error Traces are being sent.

## [8.27] - 2020-04-30
### New Features
* **Support for W3C Trace Context, with easy upgrade from New Relic trace context**
  * [Distributed Tracing now supports W3C Trace Context headers](https://docs.newrelic.com/docs/distributed-tracing/concepts/how-new-relic-distributed-tracing-works/#headers) for HTTP when distributed tracing is enabled.  Our implementation can accept and emit both W3C trace header format and New Relic trace header format. This simplifies agent upgrades, allowing trace context to be propagated between services with older and newer releases of New Relic agents. W3C trace header format will always be accepted and emitted. New Relic trace header format will be accepted, and you can optionally disable emission of the New Relic trace header format.
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

  This release adds support for [Infinite Tracing on New Relic Edge](https://docs.newrelic.com/docs/distributed-tracing/enable-configure/language-agents-enable-distributed-tracing/#infinite-tracing). Infinite Tracing observes 100% of your distributed traces and provides visualizations for the most actionable data so you have the examples of errors and long-running traces so you can better diagnose and troubleshoot your systems.

  You configure your agent to send traces to a trace observer in New Relic Edge. You view your distributed traces through the New Relic’s UI. There is no need to install a collector on your network.

  Infinite Tracing is currently available on a sign-up basis. If you would like to participate, please contact your sales representative.

  <p style="color:red;">Agent version 8.30 introduces significant performance enhancements to Infinite Tracing.  To use Infinite Tracing, please upgrade to version 8.30 or later.</p>
  
* **Error attributes now added to each span that exits with an error or exception**

  Error attributes `error.class` and `error.message` are now included on the span event in which an error or exception was noticed, and, in the case of unhandled exceptions, on any ancestor spans that also exit with an error. The public API method `NoticeError` now attaches these error attributes to the currently executing span.

  [Spans with error details are now highlighted red in the Distributed Tracing UI](https://docs.newrelic.com/docs/distributed-tracing/ui-data/understand-use-distributed-tracing-ui/#error-tips), and error details will expose the associated `error.class` and `error.message`. It is also now possible to see when an exception leaves the boundary of the span, and if it is caught in an ancestor span without reaching the entry span. NOTE: This “bubbling up” of exceptions will impact the error count when compared to prior behavior for the same trace. It is possible to have a trace that now has span errors without the trace level showing an error.

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
  * Further information may be found within [.NET Agent API documentation](https://docs.newrelic.com/docs/agents/net-agent/net-agent-api/ispan).
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

[Unreleased]: https://github.com/newrelic/newrelic-dotnet-agent/compare/v9.5.1...HEAD
[9.5.1]: https://github.com/newrelic/newrelic-dotnet-agent/compare/v9.5.0...v9.5.1
[9.5.0]: https://github.com/newrelic/newrelic-dotnet-agent/compare/v9.4.0...v9.5.0
[9.4.0]: https://github.com/newrelic/newrelic-dotnet-agent/compare/v9.3.0...v9.4.0
[9.3.0]: https://github.com/newrelic/newrelic-dotnet-agent/compare/v9.2.0...v9.3.0
[9.2.0]: https://github.com/newrelic/newrelic-dotnet-agent/compare/v9.1.1...v9.2.0
[9.1.1]: https://github.com/newrelic/newrelic-dotnet-agent/compare/v9.1.0...v9.1.1
[9.1.0]: https://github.com/newrelic/newrelic-dotnet-agent/compare/v9.0.0...v9.1.0
[9.0.0]: https://github.com/newrelic/newrelic-dotnet-agent/compare/v8.41.1...v9.0.0
[8.41.1]: https://github.com/newrelic/newrelic-dotnet-agent/compare/v8.41.0...v8.41.1
[8.41.0]: https://github.com/newrelic/newrelic-dotnet-agent/compare/v8.40.1...v8.41.0
[8.40.1]: https://github.com/newrelic/newrelic-dotnet-agent/compare/v8.40.0...v8.40.1
[8.40.0]: https://github.com/newrelic/newrelic-dotnet-agent/compare/v8.39.2...v8.40.0
[8.39.2]: https://github.com/newrelic/newrelic-dotnet-agent/compare/v8.39.1...v8.39.2
[8.39.1]: https://github.com/newrelic/newrelic-dotnet-agent/compare/v8.39.0...v8.39.1
[8.39]: https://github.com/newrelic/newrelic-dotnet-agent/compare/v8.38.0...v8.39.0
[8.38]: https://github.com/newrelic/newrelic-dotnet-agent/compare/v8.37.0...v8.38.0
[8.37]: https://github.com/newrelic/newrelic-dotnet-agent/compare/v8.36.0...v8.37.0
[8.36]: https://github.com/newrelic/newrelic-dotnet-agent/compare/v8.35.0...v8.36.0
[8.35]: https://github.com/newrelic/newrelic-dotnet-agent/compare/v8.34.0...v8.35.0
[8.34]: https://github.com/newrelic/newrelic-dotnet-agent/compare/v8.33.0...v8.34.0
[8.33]: https://github.com/newrelic/newrelic-dotnet-agent/compare/v8.32.0...v8.33.0
[8.32]: https://github.com/newrelic/newrelic-dotnet-agent/compare/v8.31.0...v8.32.0
[8.31]: https://github.com/newrelic/newrelic-dotnet-agent/compare/v8.30.0...v8.31.0
[8.30]: https://github.com/newrelic/newrelic-dotnet-agent/compare/v8.29.0...v8.30.0
[8.29]: https://github.com/newrelic/newrelic-dotnet-agent/compare/v8.28.0...v8.29.0
[8.28]: https://github.com/newrelic/newrelic-dotnet-agent/compare/v8.27.139...v8.28.0
[8.27]: https://github.com/newrelic/newrelic-dotnet-agent/compare/v8.26.630...v8.27.139
[8.26]: https://github.com/newrelic/newrelic-dotnet-agent/compare/v8.25.214...v8.26.630
[8.25]: https://github.com/newrelic/newrelic-dotnet-agent/compare/v8.24.244...v8.25.214
[8.24]: https://github.com/newrelic/newrelic-dotnet-agent/compare/v8.23.107...v8.24.244

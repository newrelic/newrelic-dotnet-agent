# Changelog
All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [10.16.0](https://github.com/newrelic/newrelic-dotnet-agent/compare/v10.15.0...v10.16.0) (2023-09-11)


### Notice

* The transactionTracer.stackTraceThreshold setting has been deprecated and no longer has any effect. ([#1896](https://github.com/newrelic/newrelic-dotnet-agent/issues/1896)) ([20ab0e7](https://github.com/newrelic/newrelic-dotnet-agent/commit/20ab0e72c86020e712024165ee4d72f832522db2))


### New Features

* Add 32bit profiler path to IIS registry when installing 64bit agent. ([#1890](https://github.com/newrelic/newrelic-dotnet-agent/issues/1890)) ([65dd50b](https://github.com/newrelic/newrelic-dotnet-agent/commit/65dd50be55e27c3b45384f54df40a96cb1e115a4))
* Prevent using different bitness installer when in-place upgrading. ([#1890](https://github.com/newrelic/newrelic-dotnet-agent/issues/1890)) ([65dd50b](https://github.com/newrelic/newrelic-dotnet-agent/commit/65dd50be55e27c3b45384f54df40a96cb1e115a4))


### Fixes

* Fix misleading log message on transaction name change. ([#1857](https://github.com/newrelic/newrelic-dotnet-agent/issues/1857)) ([#1886](https://github.com/newrelic/newrelic-dotnet-agent/issues/1886)) ([737b4f1](https://github.com/newrelic/newrelic-dotnet-agent/commit/737b4f1dda8831225fcf9bbeea61ff3cc0024da5))
* Fix NRHttpClientFactory so that it creates only one client. ([#1873](https://github.com/newrelic/newrelic-dotnet-agent/issues/1873)) ([fc88ff7](https://github.com/newrelic/newrelic-dotnet-agent/commit/fc88ff7690c367043f074cb6df154a58f8eb4f63))
* Prevent broken traces when HttpClient content headers contain tracing headers. ([#1843](https://github.com/newrelic/newrelic-dotnet-agent/issues/1843)) ([#1888](https://github.com/newrelic/newrelic-dotnet-agent/issues/1888)) ([541dd2c](https://github.com/newrelic/newrelic-dotnet-agent/commit/541dd2ccbb01533ac14b903d84394a02aaf84295))
* Remove the retained file count limit for Agent log files. ([#1879](https://github.com/newrelic/newrelic-dotnet-agent/issues/1879)) ([e49250a](https://github.com/newrelic/newrelic-dotnet-agent/commit/e49250aac7e35e06fcea4fd67ef221b2a967a9b6))

## [10.15.0](https://github.com/newrelic/newrelic-dotnet-agent/compare/v10.14.0...v10.15.0) (2023-08-28)


### New Features

* Add support for Serilog.Extensions.Logging and NLog.Extensions.Logging. ([#1860](https://github.com/newrelic/newrelic-dotnet-agent/issues/1860)) ([#1859](https://github.com/newrelic/newrelic-dotnet-agent/issues/1859)) ([ad24201](https://github.com/newrelic/newrelic-dotnet-agent/commit/ad242019989b9105b1ccb0dd5602640a057f3333))
* Log a warning when an unsupported .NET version is detected. ([#1852](https://github.com/newrelic/newrelic-dotnet-agent/issues/1852)) ([7da3e59](https://github.com/newrelic/newrelic-dotnet-agent/commit/7da3e59c9e9dbf865053de5eccd448560f5d78ce))
* Use HttpWebRequest instead of HttpClient on .NET Framework ([#1853](https://github.com/newrelic/newrelic-dotnet-agent/issues/1853)) ([8d6cf0f](https://github.com/newrelic/newrelic-dotnet-agent/commit/8d6cf0faf1b08eb54cc76f8fcbb21d7afc994140))

## [10.14.0](https://github.com/newrelic/newrelic-dotnet-agent/compare/v10.13.0...v10.14.0) (2023-08-08)


### New Features

* Add support for Sitecore.Logging. ([#1790](https://github.com/newrelic/newrelic-dotnet-agent/issues/1790)) ([#1795](https://github.com/newrelic/newrelic-dotnet-agent/issues/1795)) ([6d1934a](https://github.com/newrelic/newrelic-dotnet-agent/commit/6d1934aa3756d20bf45a1b42e5da2286967b2db5))

## [10.13.0](https://github.com/newrelic/newrelic-dotnet-agent/compare/v10.12.1...v10.13.0) (2023-07-14)


### Security

* Update Grpc.Net.Client library to address Dependabot alerts. ([#1768](https://github.com/newrelic/newrelic-dotnet-agent/issues/1768)) ([#1769](https://github.com/newrelic/newrelic-dotnet-agent/issues/1769)) ([eee7564](https://github.com/newrelic/newrelic-dotnet-agent/commit/eee7564cbe79b653ad7909af36f09c9a64cdb731))


### New Features

* Add support for filtering log events based on a list of log levels so that they are not forwarded to New Relic. Also adds new logging metrics to count the total number of filtered log events (Logging/denied). Refer to our [application logging configuration](https://docs.newrelic.com/docs/apm/agents/net-agent/configuration/net-agent-configuration/#application_logging) documentation for more details. ([#1760](https://github.com/newrelic/newrelic-dotnet-agent/issues/1760)) ([#1761](https://github.com/newrelic/newrelic-dotnet-agent/issues/1761)) ([#1762](https://github.com/newrelic/newrelic-dotnet-agent/issues/1762)) ([#1766](https://github.com/newrelic/newrelic-dotnet-agent/issues/1766)) ([aadce3a](https://github.com/newrelic/newrelic-dotnet-agent/commit/aadce3a09f9fe3c77a93f557686f1ddc26fc6169))
* Instrument OpenAsync() for SQL libraries. ([#1725](https://github.com/newrelic/newrelic-dotnet-agent/issues/1725)) ([a695ce6](https://github.com/newrelic/newrelic-dotnet-agent/commit/a695ce6de7e56bc3f803c9b9f6c8c09b30c106fd))


### Fixes

* Refactor StackExchange.Redis v2+ instrumentation to eliminate potential memory leaks. ([902b025](https://github.com/newrelic/newrelic-dotnet-agent/commit/902b025c8c420b8bc288b15d914b47aabc1bd426))
* Remove invalid trailing comma added to W3C tracestate header. ([#1779](https://github.com/newrelic/newrelic-dotnet-agent/issues/1779)) ([790a3b7](https://github.com/newrelic/newrelic-dotnet-agent/commit/790a3b75dd7609d76638ea3625a9289f58b24378))
* Update the MSI UI to clean up formatting and readability issues. ([#1748](https://github.com/newrelic/newrelic-dotnet-agent/issues/1748)) ([3fbc543](https://github.com/newrelic/newrelic-dotnet-agent/commit/3fbc54310ed3989f915e6f39b27ef8867ed573db))

## [10.12.1](https://github.com/newrelic/newrelic-dotnet-agent/compare/v10.12.0...v10.12.1) (2023-06-26)


### Fixes

* Resolved an issue in the `all_solutions.yml` workflow where the MSI installers were built with a self-signed certificate rather than the production code signing certificate. ([386a277](https://github.com/newrelic/newrelic-dotnet-agent/commit/386a27705701a07d591a95f95830bda27898d255))

## [10.12.0](https://github.com/newrelic/newrelic-dotnet-agent/compare/v10.11.0...v10.12.0) (2023-06-23)


### New Features

* add instrumentation for newer MongoDB.Client methods ([#1732](https://github.com/newrelic/newrelic-dotnet-agent/issues/1732)) ([1aa5680](https://github.com/newrelic/newrelic-dotnet-agent/commit/1aa5680a8f7f855895203a45b8dfcc5059d656e0))
* add support for MySql.Data version 8.0.33+ ([#1708](https://github.com/newrelic/newrelic-dotnet-agent/issues/1708)) ([69d15df](https://github.com/newrelic/newrelic-dotnet-agent/commit/69d15dfbed178fb5698695253160ae12a4f7a410))


### Fixes

* Add more validation to msi installer. ([#1716](https://github.com/newrelic/newrelic-dotnet-agent/issues/1716)) ([d7bb7f2](https://github.com/newrelic/newrelic-dotnet-agent/commit/d7bb7f290beae8394599cee1ea9b3213cf2dc473))
* Cache the AgentEnabled setting value. ([#1723](https://github.com/newrelic/newrelic-dotnet-agent/issues/1723)) ([1624938](https://github.com/newrelic/newrelic-dotnet-agent/commit/1624938ab48b63c1fa6e98037d74976dbc8186da))
* Exclude WebResource.axd and ScriptResource.axd from browser instrumentation (via default config). ([#1711](https://github.com/newrelic/newrelic-dotnet-agent/issues/1711)) ([2fcce95](https://github.com/newrelic/newrelic-dotnet-agent/commit/2fcce95093ed4ef6d1efe67489c8d1ae6c9b29e6))
* Format and log audit-level messages only when audit logging is enabled. ([#1734](https://github.com/newrelic/newrelic-dotnet-agent/issues/1734)) ([f71521f](https://github.com/newrelic/newrelic-dotnet-agent/commit/f71521f2540311e97d13646ff6d6524dfcc3965f))
* Handle empty Request.Path values in AspNetCore middleware wrapper. ([#1704](https://github.com/newrelic/newrelic-dotnet-agent/issues/1704)) ([8b734a5](https://github.com/newrelic/newrelic-dotnet-agent/commit/8b734a59a53cfd218322d83acbe9d7eb4e7cc055))
* Include config file path in the "Agent is disabled " message on all platforms. ([#1727](https://github.com/newrelic/newrelic-dotnet-agent/issues/1727)) ([1a56612](https://github.com/newrelic/newrelic-dotnet-agent/commit/1a5661243eaa84683694e022fe9806768b8af9f7))
* Update install script to correctly stop and restart IIS. ([#1740](https://github.com/newrelic/newrelic-dotnet-agent/issues/1740)) ([3b91dff](https://github.com/newrelic/newrelic-dotnet-agent/commit/3b91dff0ad9aa2fc4218cd85d28fb6d0892cc7fb))

## [10.11.0](https://github.com/newrelic/newrelic-dotnet-agent/compare/v10.10.0...v10.11.0) (2023-06-03)


### Notice

* The Dotnet VMs UI page is now available for .NET CLR performance metrics. There is a new New Relic APM UI page available called "Dotnet VMs" that displays the data the .NET agent collects about an application's CLR performance.  See the [performance metrics documentaton](https://docs.newrelic.com/docs/apm/agents/net-agent/other-features/net-performance-metrics/) for more details. ([cc7cede](https://github.com/newrelic/newrelic-dotnet-agent/commit/cc7cedecc113812b5f7274e7a6bf1aa5a2511720))


### Fixes

* Clearing transaction context for held transactions and async WCF client instrumentation timing. ([#1608](https://github.com/newrelic/newrelic-dotnet-agent/issues/1608)) ([db9a48e](https://github.com/newrelic/newrelic-dotnet-agent/commit/db9a48e50b66c345fd53ff64b296025d03da77bb))
* Stop double injecting headers with HttpClient on .NET Framework ([#1679](https://github.com/newrelic/newrelic-dotnet-agent/issues/1679)) ([e8bdc34](https://github.com/newrelic/newrelic-dotnet-agent/commit/e8bdc34072f044e7b056dd2ce773f184aed3bfe5))


### New Features

* Add detailed assembly reporting to enable Vulnerability Management support. ([#1685](https://github.com/newrelic/newrelic-dotnet-agent/issues/1685)) ([f249753](https://github.com/newrelic/newrelic-dotnet-agent/commit/f2497536dadb34caded7aa916b5f404ebf19e52a))
* Adds minimal support for Devart Oracle client. ([181a628](https://github.com/newrelic/newrelic-dotnet-agent/commit/181a628ff1cb7a0f0b7a347378644782f085f3ab))
* Use Serilog instead of log4net for internal logging.  ([#1661](https://github.com/newrelic/newrelic-dotnet-agent/issues/1661)) ([51080df](https://github.com/newrelic/newrelic-dotnet-agent/commit/51080df3848e36e0b6aa29b6cb9a0e94a1638b6f))

## [10.10.0](https://github.com/newrelic/newrelic-dotnet-agent/compare/v10.9.1...v10.10.0) (2023-04-26)


### New Features

* Add additional logging when RUM injection is being skipped. ([#1561](https://github.com/newrelic/newrelic-dotnet-agent/issues/1561)) ([e1b8eca](https://github.com/newrelic/newrelic-dotnet-agent/commit/e1b8eca24fc63671a8ea1bafaebabbb9f3b29cb2))
* Add instrumentation for .NET Elasticsearch clients. ([#1575](https://github.com/newrelic/newrelic-dotnet-agent/issues/1575)) ([8e49d7b](https://github.com/newrelic/newrelic-dotnet-agent/commit/8e49d7bfc22df88abc96a6ebc2518a7be8a1d29b))
* Move TLS config logging closer to connect. ([#1562](https://github.com/newrelic/newrelic-dotnet-agent/issues/1562)) ([0ff3ddd](https://github.com/newrelic/newrelic-dotnet-agent/commit/0ff3ddde1c8c0aed3b0a3c1aaf4c59e7ddc3837c))


### Fixes

* Add missing instrumentation to MSI installer ([#1569](https://github.com/newrelic/newrelic-dotnet-agent/issues/1569)) ([b65b117](https://github.com/newrelic/newrelic-dotnet-agent/commit/b65b1170d7649ab6e82a9796f235925ca147393c))
* Add NServiceBus instrumentation to the MSI installer for .NET Core/5+. ([#1576](https://github.com/newrelic/newrelic-dotnet-agent/issues/1576)) ([3cae03e](https://github.com/newrelic/newrelic-dotnet-agent/commit/3cae03eacbfb4b2c250abb3a35047190571d35a6))
* IsOsPlatform() can fail on older .NET Framework Versions ([#1552](https://github.com/newrelic/newrelic-dotnet-agent/issues/1552)) ([699c205](https://github.com/newrelic/newrelic-dotnet-agent/commit/699c2056883e4548c025e3ee893e215400899e0e))

## [10.9.1](https://github.com/newrelic/newrelic-dotnet-agent/compare/v10.9.0...v10.9.1) (2023-04-10)

### Fixes

* Allow StackExchange.Redis v2+ profiling to start outside of a transaction. ([#1501](https://github.com/newrelic/newrelic-dotnet-agent/issues/1501)) ([#1504](https://github.com/newrelic/newrelic-dotnet-agent/issues/1504)) ([925d016](https://github.com/newrelic/newrelic-dotnet-agent/commit/925d016c145b50b75a3b3401de303f5fa9a64609))
* allow the agent to accept multiple versions of legacy NR distributed tracing headers ([#1489](https://github.com/newrelic/newrelic-dotnet-agent/issues/1489)) ([23ee241](https://github.com/newrelic/newrelic-dotnet-agent/commit/23ee24141ad44afa39e3f35f93aa2ae7570acb72))
* Fix a memory leak when using StackExchange.Redis v2+. ([#1473](https://github.com/newrelic/newrelic-dotnet-agent/issues/1473)) ([#1504](https://github.com/newrelic/newrelic-dotnet-agent/issues/1504)) ([925d016](https://github.com/newrelic/newrelic-dotnet-agent/commit/925d016c145b50b75a3b3401de303f5fa9a64609))
* Retry connection on HttpRequestException error ([#1514](https://github.com/newrelic/newrelic-dotnet-agent/issues/1514)) ([#1484](https://github.com/newrelic/newrelic-dotnet-agent/issues/1484)) ([99b520e](https://github.com/newrelic/newrelic-dotnet-agent/commit/99b520e271df4357f8ea62cad2403884edb4d856))

## [10.9.0] - 2023-03-28

### New Errors inbox features
* **User tracking**: You can now see the number of users impacted by an error group. Identify the end user with the setUserId method.
* **Error fingerprint**: Are your error occurrences grouped poorly? Set your own error fingerprint via a callback function.

### New Features
* Agent API now supports associating a User Id with the current transaction. See our [ITransaction API documentation](https://docs.newrelic.com/docs/apm/agents/net-agent/net-agent-api/itransaction/#setuserid) for more details.  [#1420](https://github.com/newrelic/newrelic-dotnet-agent/pull/1420)
* Agent API now supports providing a callback to determine what error group an exception should be grouped under. See our [SetErrorGroupCallback API documentation](https://docs.newrelic.com/docs/apm/agents/net-agent/net-agent-api/seterrorgroupcallback-net-agent-api/) for more details. [#1434](https://github.com/newrelic/newrelic-dotnet-agent/pull/1434)
* Adds the `Supportability/Logging/Forwarding/Dropped` metric to track the number of log messages that were dropped due to capacity constraints. [#1470](https://github.com/newrelic/newrelic-dotnet-agent/pull/1470)

### Fixes
* Reduce redundant collector request data payload logging in the agent log at DEBUG level. [#1449](https://github.com/newrelic/newrelic-dotnet-agent/pull/1449)
* Fixes [#1459](https://github.com/newrelic/newrelic-dotnet-agent/issues/1459), a regression in NLog local decoration when logging messages with object parameters. [#1480](https://github.com/newrelic/newrelic-dotnet-agent/pull/1480)

### Other
* Renamed `NewRelic.Providers.Wrapper.Asp35` to `NewRelic.Providers.Wrapper.AspNet` since this wrapper instruments multiple versions of ASP.NET. Updated installers to remove old `Asp35` artifacts. [#1448](https://github.com/newrelic/newrelic-dotnet-agent/pull/1448)

## [10.8.0] - 2023-03-14

### New Features
* When running on Linux, distro name and version will be reported as environment settings [#1439](https://github.com/newrelic/newrelic-dotnet-agent/pull/1439)

### Fixes
* Fixes [#1353](https://github.com/newrelic/newrelic-dotnet-agent/issues/1353) so that out-of-process .Net Core web applications are instrumented according to the <applicationPools> setting in newrelic.config. [1392](https://github.com/newrelic/newrelic-dotnet-agent/pull/1392)
* Update NLog to improve local log decoration coverage. [#1393](https://github.com/newrelic/newrelic-dotnet-agent/pull/1393)
* Fixes [#1353](https://github.com/newrelic/newrelic-dotnet-agent/issues/1353) so that out-of-process .Net Core web applications are instrumented according to the <applicationPools> setting in newrelic.config. [1392](https://github.com/newrelic/newrelic-dotnet-agent/pull/1392)

## [10.7.0] - 2023-02-14

### New Features
* Postgres client instrumentation support has been extended to include the following versions: 4.0.x, 4.1.x, 5.0.x, 6.0.x and 7.0.x [#1363](https://github.com/newrelic/newrelic-dotnet-agent/pull/1363)
* Enables gzip compression by default for Infinite Tracing [#1383](https://github.com/newrelic/newrelic-dotnet-agent/pull/1383)

### Fixes
* Fix a race condition when using SetApplicationName [#1361](https://github.com/newrelic/newrelic-dotnet-agent/pull/1361)
* Resolves [#1374](https://github.com/newrelic/newrelic-dotnet-agent/issues/1374) related to enabling Context Data for some loggers [#1381](https://github.com/newrelic/newrelic-dotnet-agent/pull/1381)
* Add missing supportability metrics to gRPC response streams and improve Infinite Tracing integration test reliability [#1379](https://github.com/newrelic/newrelic-dotnet-agent/pull/1379)

### Deprecations
* Infinite Tracing for .NET Framework applications will be deprecated in May 2023. The Infinite Tracing feature depends on the gRPC framework to send streaming data to New Relic. The gRPC library currently in use, [gRPC Core](https://github.com/grpc/grpc/tree/master/src/csharp), has been in the maintenance-only phase since May 2021, and will be deprecated as of May 2023.  The .NET Agent on .NET Core has been migrated to [gRPC for .NET](https://github.com/grpc/grpc-dotnet) per the guidance from grpc.io. However, this library does not have the full functionality that is required for Infinite Tracing on .NET Framework applications.  Those applications will continue to use [gRPC Core](https://github.com/grpc/grpc/tree/master/src/csharp) until May 2023, at which time we will end support for Infinite Tracing for .NET Framework. We may revisit this decision in the future if the situation changes. [#1367](https://github.com/newrelic/newrelic-dotnet-agent/pull/1367)

### Other
* Resolved several static code analysis warnings relating to unused variables and outdated api usage [#1369](https://github.com/newrelic/newrelic-dotnet-agent/pull/1369)
* Update gRPC log message when a response stream is automatically cancelled due to no messages in a time period [#1378](https://github.com/newrelic/newrelic-dotnet-agent/pull/1378)
* Proxy configuration for Infinite Tracing should be specified using only the `https_proxy` environment variable. `grpc_proxy` is no longer supported for all application types.

## [10.6.0] - 2023-01-24

### New Features
* Custom instrumentation now supports targeting specific assembly versions. See [the documentation](https://docs.newrelic.com/docs/apm/agents/net-agent/custom-instrumentation/add-detail-transactions-xml-net/#procedures)  for more details. [#1342](https://github.com/newrelic/newrelic-dotnet-agent/pull/1342)
* RestSharp client instrumentation support has been extended to include the following versions: 106.11.x, 106.12.0, 106.13.0, and 106.15.0. [#1352](https://github.com/newrelic/newrelic-dotnet-agent/pull/1352)
* RestSharp client instrumentation has been verified for versions 107.x and 108.x. For newer versions of RestSharp, external segments/spans are actually generated via our instrumentation of HttpClient. [#1356](https://github.com/newrelic/newrelic-dotnet-agent/pull/1356)
* .NET TLS options are now logged during startup. [#1357](https://github.com/newrelic/newrelic-dotnet-agent/pull/1357)

### Fixes
* StackExchange.Redis versions 2 and above use a new wrapper with improved performance and reduced network overhead. [#1351](https://github.com/newrelic/newrelic-dotnet-agent/pull/1351)

## [10.5.1] - 2023-01-17

### Fixes
* Resolves [#1346](https://github.com/newrelic/newrelic-dotnet-agent/issues/1346) where some NuGet packages were incomplete for the 10.5.0 release. Impacted packages have been delisted from NuGet. ([#1347](https://github.com/newrelic/newrelic-dotnet-agent/pull/1347))

## [10.5.0] - 2023-01-12

### Fixes
* Resolves [#1130](https://github.com/newrelic/newrelic-dotnet-agent/issues/1130). Attribute collections in the agent will now more reliably track the number of attributes contained, and allow updates to attributes that already exist in the collection when collection limits have been reached (255 global attributes, 65 custom attributes). ([#1335](https://github.com/newrelic/newrelic-dotnet-agent/pull/1335))
* The agent has been updated to use System.Net.Http.HTTPClient to send data to New Relic instead of System.Net.WebRequest, in order to fix issue [#897](https://github.com/newrelic/newrelic-dotnet-agent/issues/897), as well as remove use of a deprecated library. ([#1325](https://github.com/newrelic/newrelic-dotnet-agent/pull/1325))

## [10.4.0] - 2022-12-06

### New Features
* Support for .NET 7 has been verified with the GA version of the .NET 7 SDK. Please note that if you use [dynamically-created assemblies](https://learn.microsoft.com/en-us/dotnet/framework/reflection-and-codedom/emitting-dynamic-methods-and-assemblies), there is a [bug in .NET 7](https://github.com/dotnet/runtime/issues/76016) that prevents them from being instrumented at this time.
* Application log fowarding can now be configured to capture and forward context data (also referred to as "custom attributes") to New Relic.  Details (including how to enable and configure this new feature) can be found [here](https://docs.newrelic.com/docs/logs/logs-context/net-configure-logs-context-all/).
* The [NewRelic.Agent NuGet package](https://www.nuget.org/packages/NewRelic.Agent) now includes the Linux Arm64 profiler. This can be found in the `newrelic/linux-arm64` directory. Configure your `CORECLR_PROFILER_PATH` environment variable to use this version of the profiler when deploying to linux ARM64 targets.
* When finest logs are enabled, the transaction guid will be applied to attribute limit log messages, if present.

### Fixes
* Resolves potential crash when using Infinite Tracing. [#1319](https://github.com/newrelic/newrelic-dotnet-agent/issues/1319)

## [10.3.0] - 2022-10-26

### New Features
* Custom Event Limit Increase
  * This version increases the default limit of custom events from 10,000 events per minute to 30,000 events per minute. In the scenario that custom events were being limited, this change will allow more custom events to be sent to New Relic. There is also a new configurable maximum limit of 100,000 events per minute. To change the limits, see the documentation for [max_samples_stored](https://docs.newrelic.com/docs/apm/agents/net-agent/configuration/net-agent-configuration/#custom_events). To learn more about the change and how to determine if custom events are being dropped, see our Explorers Hub [post](https://discuss.newrelic.com/t/send-more-custom-events-with-the-latest-apm-agents/190497). [#1284](https://github.com/newrelic/newrelic-dotnet-agent/pull/1284)

### Fixes

## [10.2.0] - 2022-10-03

### New Features
* Add new environment variables to control SendDataOnExit functionality: `NEW_RELIC_SEND_DATA_ON_EXIT`, `NEW_RELIC_SEND_DATA_ON_EXIT_THRESHOLD_MS`. [#1250](https://github.com/newrelic/newrelic-dotnet-agent/pull/1250)
* Enables integration with CodeStream Code-Level Metrics by default. This allows you to see Golden Signals in your IDE through New Relic CodeStream without altering agent configuration. [Learn more here](https://docs.newrelic.com/docs/apm/agents/net-agent/other-features/net-codestream-integration). For any issues or direct feedback, please reach out to support@codestream.com. [#1255](https://github.com/newrelic/newrelic-dotnet-agent/pull/1255)

### Fixes
* Resolves an issue where the .NET Core agent could crash during application shutdown when SendDataOnExit functionality was triggered. [#1254](https://github.com/newrelic/newrelic-dotnet-agent/pull/1254)
* Resolves an issue where the .NET agent incorrectly injects the browser agent script inside Html pages. [#1247](https://github.com/newrelic/newrelic-dotnet-agent/pull/1247)
* Resolves an issue where some instrumentation was missing for Microsoft.Data.SqlClient in .NET Framework. [#1248](https://github.com/newrelic/newrelic-dotnet-agent/pull/1248)
* Resolves an issue with local log decoration for NLog where the original log message was not included in the output. [#1249](https://github.com/newrelic/newrelic-dotnet-agent/pull/1249)
* Resolves an issue where the .NET agent failed to serialize custom attributes containing some non-primtive types. [#1256](https://github.com/newrelic/newrelic-dotnet-agent/pull/1256)
* Includes missing profiler environment variables in debug logs during application startup. [#1255](https://github.com/newrelic/newrelic-dotnet-agent/pull/1255)
* Resolves an issue where the .NET agent still sends up disabled event types during reconnecting period. [#1251](https://github.com/newrelic/newrelic-dotnet-agent/pull/1251)

## [10.1.0] - 2022-09-12

**Notice:** If using Microsoft.Extensions.Logging as your logging framework of choice, please use .NET agent version 10.1.0 or newer.  We encourage you to adopt the newer version due to bug [#1230](https://github.com/newrelic/newrelic-dotnet-agent/issues/1230), which we fixed in [#1237](https://github.com/newrelic/newrelic-dotnet-agent/pull/1237), that was resolved in .NET agent version 10.1.0.

### New Features
* Support of setting up labels via appsettings.json and app/web.config file. [#1204](https://github.com/newrelic/newrelic-dotnet-agent/pull/1204)
* Additional DEBUG-level logging of all environment variables.
* Forwarded application logs now capture exception details including, error message, error stack, and error class. [#1228](https://github.com/newrelic/newrelic-dotnet-agent/pull/1228)
  * Log events with no message will now be accepted if an exception is present in the log event.
  * The error stack is created using the stack of the inner exception, up to 5 levels deep, just like existing Agent error reporting.
* Adds a new `SetName()` method to the Agent API for spans which allows customization of segment/span/metric names. [#1238](https://github.com/newrelic/newrelic-dotnet-agent/pull/1238)

### Fixes
* Resolves an issue where log forwarding could drop logs in async scenarios. [#1174](https://github.com/newrelic/newrelic-dotnet-agent/pull/1201)
* Resolves an issue where more logs were forwarded than expected from Microsoft.Extensions.Logging. [#1237](https://github.com/newrelic/newrelic-dotnet-agent/pull/1237)
* Resolves an agent configuration bug where values set in the `MAX_EVENT_SAMPLES_STORED` and `MAX_TRANSACTION_SAMPLES_STORED` environment variables, which configure the maximum samples stored per one-minute harvest interval, were not being properly converted to apply to the five-second harvest interval for those data types. [#1239](https://github.com/newrelic/newrelic-dotnet-agent/pull/1239)

## [10.0.0] - 2022-07-19

**Notice:** If using Microsoft.Extensions.Logging as your logging framework of choice, please use .NET agent version 10.1.0 or newer.  We encourage you to adopt the newer version due to bug [#1230](https://github.com/newrelic/newrelic-dotnet-agent/issues/1230), which we fixed in [#1237](https://github.com/newrelic/newrelic-dotnet-agent/pull/1237), that was resolved in .NET agent version 10.1.0.

### New Features
* Adds support for forwarding application logs to New Relic for .NET Framework 4.6.2 and newer applications using Microsoft.Extensions.Logging. [#1172](https://github.com/newrelic/newrelic-dotnet-agent/pull/1172)
* Additional agent configuration options are now visible and easily accessible through the UI on NR1. Agent configuration is also now reported during agent connect. This information can be seen in the `APM->Environment->Agent Initialization` view. [#1174](https://github.com/newrelic/newrelic-dotnet-agent/pull/1174)

### Fixes
* Resolves an issue with transaction trace aggregation where the slowest transaction trace was not always captured due to a race condition. [#1166](https://github.com/newrelic/newrelic-dotnet-agent/pull/1166)
* Adds an ignore rule to prevent profiling `SMSvcHost.exe`. [#1182](https://github.com/newrelic/newrelic-dotnet-agent/pull/1182)
* Updates applicationLogging attribute `log.level` to be `level`. [#1144](https://github.com/newrelic/newrelic-dotnet-agent/pull/1144)

### Deprecations/Removed Features
* This is a major release of the agent, and contains breaking changes. See the [migration guide](https://docs.newrelic.com/docs/apm/agents/net-agent/getting-started/9x-to-10x-agent-migration-guide/) for details.
* This agent release targets .NET Framework 4.6.2 and .NET Standard 2.0. The minimum supported runtime versions for profiled applications are .NET Framework 4.6.2+ and .NET Core 3.1+.
* The scriptable installers have been removed. [#1170](https://github.com/newrelic/newrelic-dotnet-agent/pull/1170)
* Windows installation files have been consolidated and renamed. [#1187](https://github.com/newrelic/newrelic-dotnet-agent/pull/1187)
* The Linux installation packages have been renamed. [#1180](https://github.com/newrelic/newrelic-dotnet-agent/pull/1180)
* Castle.Monorail instrumentation has been removed. [#1177](https://github.com/newrelic/newrelic-dotnet-agent/pull/1177)

## [9.9.0] - 2022-06-08

**Notice:** If using Microsoft.Extensions.Logging as your logging framework of choice, please use .NET agent version 10.1.0 or newer.  We encourage you to adopt the newer version due to bug [#1230](https://github.com/newrelic/newrelic-dotnet-agent/issues/1230), which we fixed in [#1237](https://github.com/newrelic/newrelic-dotnet-agent/pull/1237), that was resolved in .NET agent version 10.1.0.

### New Features
* Adds support for logging metrics, forwarding application logs, and enriching application logs written to disk or standard out for NLog versions v5 and v4. [#1087](https://github.com/newrelic/newrelic-dotnet-agent/pull/1087)
* Adds integration with CodeStream, introducing Code-Level Metrics! Golden Signals visible in your IDE through New Relic CodeStream. [Learn more here](https://docs.newrelic.com/docs/apm/agents/net-agent/other-features/net-codestream-integration). For any issues or direct feedback, please reach out to support@codestream.com
* Updates the following installation methods to check for and remove deprecated files. ([#1104](https://github.com/newrelic/newrelic-dotnet-agent/pull/1104))
  * MSI Installer
  * Azure Site Extension
  * RPM package
  * DEB package

### Fixes
* Upgrades Newtonsoft.Json to version 13.0.1 to address potential security vulnerabilities identified by Snyk ([#1107](https://github.com/newrelic/newrelic-dotnet-agent/pull/1107))
* The agent will now send the values of application logging config options (e.g. `application_logging.forwarding.enabled`) to the agent initialization settings page. ([#1135](https://github.com/newrelic/newrelic-dotnet-agent/pull/1135))

## [9.8.1] - 2022-05-19

**Notice:** If using Microsoft.Extensions.Logging as your logging framework of choice, please use .NET agent version 10.1.0 or newer.  We encourage you to adopt the newer version due to bug [#1230](https://github.com/newrelic/newrelic-dotnet-agent/issues/1230), which we fixed in [#1237](https://github.com/newrelic/newrelic-dotnet-agent/pull/1237), that was resolved in .NET agent version 10.1.0.

### Fixes
* Fixes an [issue with log forwarding](https://github.com/newrelic/newrelic-dotnet-agent/issues/1088) where an agent could momentarily forward logs even if the feature had been disabled at an account level. ([#1097](https://github.com/newrelic/newrelic-dotnet-agent/pull/1097))
* Adds an internal list of deprecated instrumentation xml files which will cause the profiler to ignore deprecated instrumentation. This feature avoids an issue where orphaned deprecated log forwarding instrumentation could conflict with newer instrumentation. ([#1097](https://github.com/newrelic/newrelic-dotnet-agent/pull/1097))
* Serilog instrumentation is now performed by injecting a custom sink in to the logging chain. ([#1084](https://github.com/newrelic/newrelic-dotnet-agent/pull/1084))

## [9.8.0] - 2022-05-05

**Notice:** If using Microsoft.Extensions.Logging as your logging framework of choice, please use .NET agent version 10.1.0 or newer.  We encourage you to adopt the newer version due to bug [#1230](https://github.com/newrelic/newrelic-dotnet-agent/issues/1230), which we fixed in [#1237](https://github.com/newrelic/newrelic-dotnet-agent/pull/1237), that was resolved in .NET agent version 10.1.0.

### APM logs in context
Automatic application log forwarding is now enabled by default. This version of the agent will automatically send enriched application logs to New Relic. To learn more about about this feature see [here](https://docs.newrelic.com/docs/apm/new-relic-apm/getting-started/get-started-logs-context/), and additional configuration options are available [here](https://docs.newrelic.com/docs/logs/logs-context/net-configure-logs-context-all). To learn about how to toggle log ingestion on or off by account see [here](https://docs.newrelic.com/docs/logs/logs-context/disable-automatic-logging).

### New Features
* Error messages in error traces and error events now retain up to 1023 characters instead of 255 characters. [#1058](https://github.com/newrelic/newrelic-dotnet-agent/pull/1058)
* New environment variables have been added for AllowAllHeaders and Attributes configuration settings. See our [documentation](https://docs.newrelic.com/docs/apm/agents/net-agent/configuration/net-agent-configuration/#optional-environment-variables) for more details. [#1059](https://github.com/newrelic/newrelic-dotnet-agent/pull/1059)
* Introduces [environment variables to enabled/disable cloud detection](https://github.com/newrelic/newrelic-dotnet-agent/issues/818) to facilitate customer use cases and reduce errors in logs. ([#1061](https://github.com/newrelic/newrelic-dotnet-agent/pull/1061))
* New environment variables have been added for all Proxy configuration settings.  See our [documentation](https://docs.newrelic.com/docs/apm/agents/net-agent/configuration/net-agent-configuration/#optional-environment-variables) for more details. [#1063](https://github.com/newrelic/newrelic-dotnet-agent/pull/1063)
* Introduces a new configuration option to force custom instrumentation to [create new transactions](https://github.com/newrelic/newrelic-dotnet-agent/issues/347) in async scenarios versus re-using an existing transaction. [#1071](https://github.com/newrelic/newrelic-dotnet-agent/pull/1071)

### Fixes
* Fixes Agent fails to execute explain plan for parameterized stored procedure. ([#1066](https://github.com/newrelic/newrelic-dotnet-agent/pull/1066)) 
* Fixes getting duplicate logs using log forwarding and Serilog. [#1076](https://github.com/newrelic/newrelic-dotnet-agent/pull/1076)

### Deprecations
Microsoft has officially EOL .NET Framework versions 4.5.1, 4.5.2, and 4.6.1 on  Apr 26, 2022.
The informational blog can be found [here](https://devblogs.microsoft.com/dotnet/net-framework-4-5-2-4-6-4-6-1-will-reach-end-of-support-on-april-26-2022).  The official product lifecycle start and end dates can be found [here](https://docs.microsoft.com/en-us/lifecycle/products/microsoft-net-framework).  The dotnet agent support of these framework versions is will continue as is with the released versions.  In a future major release, we will target .NET framework 4.6.2 onwards.

## [9.7.1] - 2022-04-13

**Notice:** If using Microsoft.Extensions.Logging as your logging framework of choice, please use .NET agent version 10.1.0 or newer.  We encourage you to adopt the newer version due to bug [#1230](https://github.com/newrelic/newrelic-dotnet-agent/issues/1230), which we fixed in [#1237](https://github.com/newrelic/newrelic-dotnet-agent/pull/1237), that was resolved in .NET agent version 10.1.0.

### Fixes
* Adds missing instrumentation for application logging feature when using the MSI installer ([#1055](https://github.com/newrelic/newrelic-dotnet-agent/pull/1055))
* Fixes [issue on Linux](https://github.com/newrelic/newrelic-dotnet-agent/issues/763) when specifying a non-default profiler log directory with non-existent intermediate directories. ([#1051](https://github.com/newrelic/newrelic-dotnet-agent/pull/1051))

## [9.7.0] - 2022-04-04

**Notice:** For the new application logging features, if you install using the MSI, please update to version 9.7.1 or later.

**Notice:** If using Microsoft.Extensions.Logging as your logging framework of choice, please use .NET agent version 10.1.0 or newer.  We encourage you to adopt the newer version due to bug [#1230](https://github.com/newrelic/newrelic-dotnet-agent/issues/1230), which we fixed in [#1237](https://github.com/newrelic/newrelic-dotnet-agent/pull/1237), that was resolved in .NET agent version 10.1.0.

### New Features
* Adds support for logging metrics which shows the rate of log message by severity in the Logs chart in the APM Summary view for Log4net, Serilog, and Microsoft.Extensions.Logging. This is enabled by default in this release. ([#1034](https://github.com/newrelic/newrelic-dotnet-agent/pull/1034))
* Adds support for forwarding application logs to New Relic. This automatically sends enriched application logs for Log4net, Serilog, and Microsoft.Extensions.Logging. This is disabled by default in this release. ([#1034](https://github.com/newrelic/newrelic-dotnet-agent/pull/1034))
* Adds support for enriching application logs written to disk or standard out for Log4net, Serilog, Microsoft.Extensions.Logging. This can be used with another log forwarder if in-agent log forwarding is not desired. We recommend enabling either log forwarding or local log decorating, but not both features. This is disabled by default in this release. ([#1034](https://github.com/newrelic/newrelic-dotnet-agent/pull/1034))
* Adds flexibility to what is accepted to enable/disable boolean environment variables per [FR #1008](https://github.com/newrelic/newrelic-dotnet-agent/issues/1008). "0"/"1", and case insensitive "true"/"false" are now accepted. ([#1033](https://github.com/newrelic/newrelic-dotnet-agent/pull/1033))

### Fixes
* Adds a new environment variable `NEW_RELIC_DISABLE_APPDOMAIN_CACHING` for customers to try when experiencing [#533 high lock contention related to AppDomain.GetData()](https://github.com/newrelic/newrelic-dotnet-agent/issues/533) usage by the agent when profiling .NET Framework applications. ([#1033](https://github.com/newrelic/newrelic-dotnet-agent/pull/1033))

### Deprecations
* The scriptable installers are now deprecated and will be removed from the download site in a future major release. (Issue: [#571](https://github.com/newrelic/newrelic-dotnet-agent/issues/571))
* The established release installers are now deprecated and will be removed from the download site in a future major release. (Issue: [#578](https://github.com/newrelic/newrelic-dotnet-agent/issues/578))

## [9.6.1] - 2022-03-15
### Fixes
* Fixes [application pool allow/deny listing bug](https://github.com/newrelic/newrelic-dotnet-agent/issues/1014) introduced in 9.5.0 ([#1015](https://github.com/newrelic/newrelic-dotnet-agent/pull/1015))

## [9.6.0] - 2022-02-24
### Fixes
* Adds new supportability metrics to track agent endpoint data usage. New metrics will be reported under the `Supportability/DotNET/Collector` namespace. ([#899](https://github.com/newrelic/newrelic-dotnet-agent/pull/899))
* Uses IMDSv2 instead of IMDSv1 to gather utilization details for AWS hosted instances. ([#965](https://github.com/newrelic/newrelic-dotnet-agent/pull/965))

## [9.5.1] - 2022-02-03
### Fixes
* Fixes [application crashes on Alpine Linux](https://github.com/newrelic/newrelic-dotnet-agent/issues/918) introduced in 9.5.0. ([#929](https://github.com/newrelic/newrelic-dotnet-agent/pull/929))

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

[Unreleased]: https://github.com/newrelic/newrelic-dotnet-agent/compare/v10.9.0...HEAD
[10.9.0]: https://github.com/newrelic/newrelic-dotnet-agent/compare/v10.8.0...v10.9.0    
[10.8.0]: https://github.com/newrelic/newrelic-dotnet-agent/compare/v10.7.0...v10.8.0
[10.7.0]: https://github.com/newrelic/newrelic-dotnet-agent/compare/v10.6.0...v10.7.0
[10.6.0]: https://github.com/newrelic/newrelic-dotnet-agent/compare/v10.5.1...v10.6.0
[10.5.1]: https://github.com/newrelic/newrelic-dotnet-agent/compare/v10.5.0...v10.5.1
[10.5.0]: https://github.com/newrelic/newrelic-dotnet-agent/compare/v10.4.0...v10.5.0
[10.4.0]: https://github.com/newrelic/newrelic-dotnet-agent/compare/v10.3.0...v10.4.0
[10.3.0]: https://github.com/newrelic/newrelic-dotnet-agent/compare/v10.2.0...v10.3.0
[10.2.0]: https://github.com/newrelic/newrelic-dotnet-agent/compare/v10.1.0...v10.2.0
[10.1.0]: https://github.com/newrelic/newrelic-dotnet-agent/compare/v10.0.0...v10.1.0
[10.0.0]: https://github.com/newrelic/newrelic-dotnet-agent/compare/v9.9.0...v10.0.0
[9.9.0]: https://github.com/newrelic/newrelic-dotnet-agent/compare/v9.8.1...v9.9.0
[9.8.1]: https://github.com/newrelic/newrelic-dotnet-agent/compare/v9.8.0...v9.8.1
[9.8.0]: https://github.com/newrelic/newrelic-dotnet-agent/compare/v9.7.1...v9.8.0
[9.7.1]: https://github.com/newrelic/newrelic-dotnet-agent/compare/v9.7.0...v9.7.1
[9.7.0]: https://github.com/newrelic/newrelic-dotnet-agent/compare/v9.6.1...v9.7.0
[9.6.1]: https://github.com/newrelic/newrelic-dotnet-agent/compare/v9.6.0...v9.6.1
[9.6.0]: https://github.com/newrelic/newrelic-dotnet-agent/compare/v9.5.1...v9.6.0
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

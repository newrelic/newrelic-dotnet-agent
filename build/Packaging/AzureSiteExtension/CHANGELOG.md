# Changelog
All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]
### New Features
### Fixes

## [1.6.0] - 2025-02-03
### New Features
* Azure Site Extension now supports deployment to Windows Azure Functions, with instrumentation enabled by default. [#2976](https://github.com/newrelic/newrelic-dotnet-agent/pull/2976)

## [1.5.5] - 2023-02-07
### New Features
* Adds a new environment variable to capture the app service name at startup. [#1377](https://github.com/newrelic/newrelic-dotnet-agent/pull/1377)

## [1.5.4] - 2022-011-29
### Changed
* Updates the title of this NuGet package from "New Relic" to "New Relic .NET Agent" to align with the newly released Java Agent NuGet package.  The ID of this package, "NewRelic.Azure.WebSites.Extension", is not changing at this time.

## [1.5.3] - 2022-04-13
### Fixes
* Fixes Issue [#1005](https://github.com/newrelic/newrelic-dotnet-agent/issues/1005): site extension did not work when the the web app was set to run from a package, and broke after deployment of customer applications in some cases due to the agent being deployed to the site directory. [#1021](https://github.com/newrelic/newrelic-dotnet-agent/pull/1021)

## [1.5.2] - 2021-03-05
### Fixes
* Fixes Issue [#399](https://github.com/newrelic/newrelic-dotnet-agent/issues/399): site extension did not clean up after installation ([#478](https://github.com/newrelic/newrelic-dotnet-agent/pull/478))

[Unreleased]: https://github.com/newrelic/newrelic-dotnet-agent/compare/AzureSiteExtension_v1.6.0...HEAD
[1.6.0]: https://github.com/newrelic/newrelic-dotnet-agent/compare/AzureSiteExtension_v1.5.5...AzureSiteExtension_v1.6.0
[1.5.5]: https://github.com/newrelic/newrelic-dotnet-agent/compare/AzureSiteExtension_v1.5.4...AzureSiteExtension_v1.5.5
[1.5.4]: https://github.com/newrelic/newrelic-dotnet-agent/compare/AzureSiteExtension_v1.5.3...AzureSiteExtension_v1.5.4
[1.5.3]: https://github.com/newrelic/newrelic-dotnet-agent/compare/AzureSiteExtension_v1.5.2...AzureSiteExtension_v1.5.3
[1.5.2]: https://github.com/newrelic/newrelic-dotnet-agent/compare/AzureSiteExtension_v1.5.1...AzureSiteExtension_v1.5.2

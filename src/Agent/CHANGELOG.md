# Changelog
All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]
### New Features

## [6.26]
### New Features

* **The .NET Agent is now open source!** <br/>
The New Relic .NET agent is now open source! Now you can view the source code to help with troubleshooting, observe the project roadmap, and file issues directly in this repository.  We are now using the [Apache 2 license](/LICENSE). See our [Contributing guide](/CONTRIBUTING.md) and [Code of Conduct](/CODE_OF_CONDUCT.md) for details on contributing!

* **SSL Required** <br/>
In order to increase security, disabling agent HTTPS communication via the service SSL configuration is no longer supported.  All communication with New Relic services will occur via SSL.

* **EU Data Center Support** <br/>
Adds support for communicating with New Relic's EU Data Center.  Please review our [docs site](https://docs.newrelic.com/docs/using-new-relic/welcome-new-relic/get-started/our-eu-us-region-data-centers) for further information.

### Fixes

## [6.25]
### Fixes

* Fixes security vulnerability [NR20-01](https://docs.newrelic.com/docs/security/new-relic-security/security-bulletins/security-bulletin-nr20-01) which may cause SQL parameter values to appear in the agent log file when the logging level is set to Debug or Finest and the calling application supplies SQL parameters without a @ prefix.
* Fixes an issue where Explain Plans are not generated for database commands with parameters that do not have the @ prefix on the name.

## [6.24]
### Fixes

* Resolves security issue where a manually constructed SQL stored procedure invocation may cause sensitive data to be captured in metric names. See [Security Bulletin NR19-05](https://docs.newrelic.com/docs/using-new-relic/new-relic-security/security-bulletins/security-bulletin-nr19-05).

## [6.23]
### Fixes

* Resolved security issue with how SQL Server handles escaping which could lead to a failure to correctly obfuscate SQL statements. See [Security Bulletin NR19-03](https://docs.newrelic.com/docs/using-new-relic/new-relic-security/security-bulletins/security-bulletin-nr19-03).

## [6.22]
### Fixes

* [Security Bulletin NR18-07](https://docs.newrelic.com/docs/using-new-relic/new-relic-security/security-bulletins/security-bulletin-nr18-07): The agent will no longer run explain plans on MySQL queries which have multiple statements.

## [6.21]
### Fixes

* [Security Bulletin NR18-04](https://docs.newrelic.com/docs/accounts-partnerships/welcome-new-relic/security-bulletins/security-bulletin-nr18-04): Fixes issue where error messages were not fully being filtered out of error traces and error events when High Security Mode was enabled.

[Unreleased]: https://github.com/newrelic/newrelic-dotnet-agent/compare/v6.26.0...net35/main

[6.26]: https://github.com/newrelic/newrelic-dotnet-agent/compare/v6.25.0...v6.26.0
[6.25]: https://github.com/newrelic/newrelic-dotnet-agent/compare/v6.24.0...v6.25.0
[6.24]: https://github.com/newrelic/newrelic-dotnet-agent/compare/v6.23.0...v6.24.0
[6.23]: https://github.com/newrelic/newrelic-dotnet-agent/compare/v6.22.0...v6.23.0
[6.22]: https://github.com/newrelic/newrelic-dotnet-agent/compare/v6.21.0...v6.22.0
[6.21]: https://github.com/newrelic/newrelic-dotnet-agent/compare/v6.20.166...v6.21.0

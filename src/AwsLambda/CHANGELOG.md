# Changelog
All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).


** Notice: ** <br/>This New Relic .NET AWS Lambda Open Trace agent is deprecated beginning on Dec 30th, 2022. This agent was built on the [OpenTrace Standard](https://opentracing.io/); however, the OpenTrace standard migrated to OpenTelemetry. Additionally, it was verified with .NET Core 3.1. However, on Dec 13th, 2022, Microsoft ended the support of [.NET Core 3.1](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core). Therefore, with the migration of OpenTrace to Open Telemetry and the EOL of .NET Core 3.1, we've decided to deprecate this agent and encourage our customers to review the New Relic Open Telemetry offering for AWS Lambda support. 

## [Unreleased]
### New Features
### Fixes
* Updates the LambdaTracer.Extract exception type. The previous exception type, ArgumentNullException, was incorrect.  The new type, ArgumentException, is better fit.. [#1287](https://github.com/newrelic/newrelic-dotnet-agent/pull/1287)

## [1.3.1] - 2022-10-03
### Fixes
* Span creation logic has been updated to fall back to using `UNKNOWN` for any missing span name components. The previous behavior was to fail span creation. [#1211](https://github.com/newrelic/newrelic-dotnet-agent/pull/1221)

## [1.3.0] - 2021-07-21

### Fixes
* Fixes `LambdaWrapper` when using Tasks and async methods (`.Result` is no longer called and tasks are awaited correctly). ([#625](https://github.com/newrelic/newrelic-dotnet-agent/pull/625))
	* Thank you [@williamdenton](https://github.com/williamdenton) for submitting this fix!

## [1.2.1] - 2021-03-09

### Fixes
* Fixes for lambdas using extension path that occasionally time out due to IOException: file is in use error. 

## [1.1.0] - 2020-09-15

### New Features
* Allows the sending of agent payloads to New Relic through an externally managed named pipe instead of through CloudWatch. ([#114](https://github.com/newrelic/newrelic-dotnet-agent/pull/114))

## [1.0.0] - 2019-12-10

### New Features
* New SDK providing Open Tracing instrumentation for AWS Lambda. Refer to New Relic's AWS Lambda monitoring documentation to get started https://docs.newrelic.com/docs/serverless-function-monitoring/aws-lambda-monitoring/get-started/monitoring-aws-lambda-serverless-monitoring/.

* **The New Relic AWS Lambda Agent for .NET is now Open Source** <br/>
* The New Relic AWS Lambda Agent for .NET is now open source! Now you can view the source code to help with troubleshooting, observe the project roadmap, and file issues directly in the repository.  We are now using the [Apache 2 license](/LICENSE). See our [Contributing guide](/CONTRIBUTING.md) and [Code of Conduct](https://opensource.newrelic.com/code-of-conduct/) for details on contributing!

[Unreleased]: https://github.com/newrelic/newrelic-dotnet-agent/compare/AwsLambdaOpenTracer_v1.3.1...HEAD
[1.3.1]: https://github.com/newrelic/newrelic-dotnet-agent/compare/AwsLambdaOpenTracer_v1.3.0...AwsLambdaOpenTracer_v1.3.1
[1.3.0]: https://github.com/newrelic/newrelic-dotnet-agent/compare/AwsLambdaOpenTracer_v1.2.1...AwsLambdaOpenTracer_v1.3.0
[1.2.1]: https://github.com/newrelic/newrelic-dotnet-agent/compare/AwsLambdaOpenTracer_v1.2.0...AwsLambdaOpenTracer_v1.2.1
[1.2.0]: https://github.com/newrelic/newrelic-dotnet-agent/compare/AwsLambdaOpenTracer_v1.1.0...AwsLambdaOpenTracer_v1.2.0
[1.1.0]: https://github.com/newrelic/newrelic-dotnet-agent/compare/AwsLambdaOpenTracer_v1.0.0...AwsLambdaOpenTracer_v1.1.0
[1.0.0]: https://github.com/newrelic/newrelic-dotnet-agent/commit/5c27f338a32edb6390a6ebfd4d8c5177bc008b27

# New Relic .NET Agent MicrosoftExtensionsLogging Instrumentation

## Overview

The MicrosoftExtensionsLogging instrumentation wrapper provides automatic capture and forwarding of log messages from Microsoft.Extensions.Logging to New Relic. It enriches log events with distributed tracing context, captures log metadata and scope context data, and optionally decorates log messages with linking metadata for correlation with APM data. The wrapper includes provider detection to avoid double instrumentation when other known logging frameworks (NLog, Serilog) are configured as MEL providers.

## Instrumented Methods

### AddProviderRegistrationWrapper
- **Wrapper**: [AddProviderRegistrationWrapper.cs](AddProviderRegistrationWrapper.cs)
- **Assembly**: `Microsoft.Extensions.Logging`
- **Type**: `Microsoft.Extensions.Logging.LoggerFactory`

| Method | Creates Transaction | Requires Existing Transaction |
|--------|-------------------|------------------------------|
| AddProviderRegistration | No | No |

### MicrosoftLoggingWrapper
- **Wrapper**: [MicrosoftLoggingWrapper.cs](MicrosoftLoggingWrapper.cs)
- **Assembly**: `Microsoft.Extensions.Logging`
- **Type**: `Microsoft.Extensions.Logging.Logger`

| Method | Creates Transaction | Requires Existing Transaction |
|--------|-------------------|------------------------------|
| Log | No | No |

[Instrumentation.xml](https://github.com/newrelic/newrelic-dotnet-agent/blob/main/src/Agent/NewRelic/Agent/Extensions/Providers/Wrapper/MicrosoftExtensionsLogging/Instrumentation.xml)

## Log Message Capture

The `MicrosoftLoggingWrapper` intercepts the `Logger.Log<TState>` method to capture log data. Since MEL doesn't have a dedicated log event object, the wrapper extracts data directly from method parameters:

1. **Log level filtering**: Checks `ILogger.IsEnabled()` before capturing (MEL instrumentation occurs before framework-level filtering)
2. **Timestamp generation**: Uses `DateTime.UtcNow` (MEL doesn't provide a timestamp)
3. **Message extraction**: Retrieves rendered message from method parameters
4. **Exception capture**: Extracts exception parameter if present
5. **Context data**: Extracts scope context from `IExternalScopeProvider`
6. **Trace enrichment**: Associates logs with current span and trace IDs

## Provider Detection

The `AddProviderRegistrationWrapper` monitors when logging providers are registered with `LoggerFactory`:

- **Known providers detected**: NLog, Serilog, and other frameworks with dedicated instrumentation
- **Action on detection**: Disables MEL instrumentation to prevent duplicate log capture
- **Reason**: Known providers have their own New Relic instrumentation that captures logs at a more appropriate layer

When a known provider is detected, `LogProviders.KnownMELProviderEnabled` is set to `true`, causing `MicrosoftLoggingWrapper` to return `NoOp` for subsequent log calls.

## Attributes Added

### Log Event Data Captured

- **timestamp**: Generated at capture time using `DateTime.UtcNow`
- **level**: Log level (Trace, Debug, Information, Warning, Error, Critical)
- **message**: Rendered log message from formatter function
- **exception**: Exception object if included in log call
- **span.id**: Current span ID from distributed trace context
- **trace.id**: Current trace ID from distributed trace context
- **context data**: Scope context properties from `IExternalScopeProvider`

### Scope Context Data Extraction

MEL uses `IExternalScopeProvider` to manage structured logging scopes. The wrapper:

1. Accesses `Logger.ScopeLoggers` array via reflection
2. Retrieves the last `ScopeLogger` (contains the `ExternalScopeProvider`)
3. Calls `ForEachScope` to iterate through all active scopes
4. Extracts `KeyValuePair<string, object>` from each scope
5. Merges all scope data into a context data dictionary (later scopes override earlier ones with the same key)

## Log Decoration

When log decoration is enabled, the wrapper adds a `NR_LINKING` scope to the logger:

- **Implementation**: Uses `ILogger.BeginScope()` to add linking metadata
- **Scope content**: Dictionary containing `NR_LINKING` key with formatted metadata
- **Format**: `NR-LINKING|{entity.guid}|{hostname}|{trace.id}|{span.id}|{entity.name}`
- **Lifecycle**: Scope is disposed when the log method completes
- **Property key**: `NR_LINKING` (uses underscores for Serilog compatibility)

## Configuration Requirements

The wrapper respects the following agent configuration settings:

- **`application_logging.enabled`**: Master switch for all logging features
- **`application_logging.forwarding.enabled`**: Enables forwarding logs to New Relic
- **`application_logging.forwarding.context_data.enabled`**: Enables capturing scope context data
- **`application_logging.local_decorating.enabled`**: Enables adding `NR_LINKING` scope

## License
Copyright 2020 New Relic, Inc. All rights reserved.
SPDX-License-Identifier: Apache-2.0

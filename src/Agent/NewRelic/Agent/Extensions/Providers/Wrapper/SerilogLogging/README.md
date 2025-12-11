# New Relic .NET Agent SerilogLogging Instrumentation

## Overview

The SerilogLogging instrumentation wrapper provides automatic capture and forwarding of log messages from Serilog (versions 1.x and 2.x+) to New Relic. It enriches log events with distributed tracing context, captures log metadata including properties, and optionally decorates log messages with linking metadata for correlation with APM data. The wrapper also detects logger configuration to manage enricher registration.

## Instrumented Methods

### SerilogDispatchWrapper (Serilog 1.x)
- **Wrapper**: [SerilogDispatchWrapper.cs](SerilogDispatchWrapper.cs)
- **Assembly**: `Serilog`
- **Type**: `Serilog.Core.Pipeline.Logger`

| Method | Creates Transaction | Requires Existing Transaction |
|--------|-------------------|------------------------------|
| Dispatch | No | No |

### SerilogDispatchWrapper (Serilog 2.x+)
- **Wrapper**: [SerilogDispatchWrapper.cs](SerilogDispatchWrapper.cs)
- **Assembly**: `Serilog`
- **Type**: `Serilog.Core.Logger`

| Method | Creates Transaction | Requires Existing Transaction |
|--------|-------------------|------------------------------|
| Dispatch | No | No |

### SerilogCreateLoggerWrapper
- **Wrapper**: [SerilogCreateLoggerWrapper.cs](SerilogCreateLoggerWrapper.cs)
- **Assembly**: `Serilog`
- **Type**: `Serilog.LoggerConfiguration`

| Method | Creates Transaction | Requires Existing Transaction |
|--------|-------------------|------------------------------|
| CreateLogger | No | No |

[Instrumentation.xml](https://github.com/newrelic/newrelic-dotnet-agent/blob/main/src/Agent/NewRelic/Agent/Extensions/Providers/Wrapper/SerilogLogging/Instrumentation.xml)

## Log Message Capture

The `SerilogDispatchWrapper` intercepts the `Dispatch` method, which is the internal method called for all log operations:

1. **Capture log data**: Extract timestamp, level, message, exception from `LogEvent`
2. **Enrich with trace context**: Associate logs with current span and trace IDs
3. **Forward to New Relic**: Send log data to New Relic Logs (if enabled)
4. **Decorate log output**: Add linking metadata to log properties (if enabled)

## Logger Configuration Detection

The `SerilogCreateLoggerWrapper` instruments logger creation to detect and manage enricher registration:

- **Purpose**: Detects when a new Serilog logger is created
- **Action**: Checks if New Relic enrichers are already registered in the logger configuration
- **Benefit**: Prevents duplicate enricher registration and ensures proper metadata injection

## Attributes Added

### Log Event Data Captured

- **timestamp**: Log event timestamp (from `Timestamp` property)
- **level**: Log level (Verbose, Debug, Information, Warning, Error, Fatal)
- **message**: Rendered log message template
- **exception**: Exception object if present (from `Exception` property)
- **span.id**: Current span ID from distributed trace context
- **trace.id**: Current trace ID from distributed trace context
- **context data**: Properties from log event

### Context Data Extraction

Serilog context data is extracted from `LogEvent.Properties`:
- All properties attached to the log event
- Includes structured logging properties
- Includes scope properties added via enrichers

## Log Decoration

When log decoration is enabled, the wrapper adds linking metadata to the log event properties:

- **Property name**: `NR-LINKING` (uses hyphens, which Serilog allows)
- **Format**: `NR-LINKING|{entity.guid}|{hostname}|{trace.id}|{span.id}|{entity.name}`
- **Implementation**: Added to `LogEvent.Properties` dictionary
- **Timing**: Added before sinks process the log event

## Version Compatibility

The wrapper supports both major Serilog versions:

### Serilog 1.x
- **Logger type**: `Serilog.Core.Pipeline.Logger`
- **Architecture**: Pipeline-based logging

### Serilog 2.x+
- **Logger type**: `Serilog.Core.Logger`
- **Architecture**: Simplified core logger

Both versions use the same `Dispatch` method, allowing identical instrumentation logic despite the type hierarchy changes.

## Configuration Requirements

The wrapper respects the following agent configuration settings:

- **`application_logging.enabled`**: Master switch for all logging features
- **`application_logging.forwarding.enabled`**: Enables forwarding logs to New Relic
- **`application_logging.forwarding.context_data.enabled`**: Enables capturing context data
- **`application_logging.local_decorating.enabled`**: Enables decorating log output

## License
Copyright 2020 New Relic, Inc. All rights reserved.
SPDX-License-Identifier: Apache-2.0

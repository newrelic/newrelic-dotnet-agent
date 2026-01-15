# New Relic .NET Agent NLogLogging Instrumentation

## Overview

The NLogLogging instrumentation wrapper provides automatic capture and forwarding of log messages from NLog (versions 4 and 5) to New Relic. It enriches log events with distributed tracing context, captures log metadata including properties and scope context, and decorates log messages with linking metadata for correlation with APM data using a dual-decoration approach.

## Instrumented Methods

### NLogWrapper
- **Wrapper**: [NLogWrapper.cs](NLogWrapper.cs)
- **Assembly**: `NLog`
- **Type**: `NLog.LoggerImpl`

| Method | Creates Transaction | Requires Existing Transaction |
|--------|-------------------|------------------------------|
| Write | No | No |

[Instrumentation.xml](https://github.com/newrelic/newrelic-dotnet-agent/blob/main/src/Agent/NewRelic/Agent/Extensions/Providers/Wrapper/NLogLogging/Instrumentation.xml)

## Log Message Capture

The wrapper intercepts the `LoggerImpl.Write` method, which is the internal method called for all log operations after filtering:

1. **Capture log data**: Extract timestamp, level, message, exception from `LogEventInfo`
2. **Enrich with trace context**: Associate logs with current span and trace IDs
3. **Forward to New Relic**: Send log data to New Relic Logs (if enabled)
4. **Decorate log output**: Add linking metadata to log messages (if enabled)

## Attributes Added

### Log Event Data Captured

- **timestamp**: Log event timestamp (from `TimeStamp` property)
- **level**: Log level (Trace, Debug, Info, Warn, Error, Fatal)
- **message**: Formatted log message (from `FormattedMessage` property)
- **exception**: Exception object if present (from `Exception` property)
- **span.id**: Current span ID from distributed trace context
- **trace.id**: Current trace ID from distributed trace context
- **context data**: Properties and scope context

### Context Data Extraction

NLog context data is extracted from two sources:

1. **Properties**: Event-specific properties from `LogEventInfo.Properties` dictionary
2. **Scope Context**: Shared context from `NLog.ScopeContext.GetAllProperties()` (NLog 4.5+)

Both sources are merged into a single context data dictionary.

## Log Decoration

The wrapper uses a "belt-and-suspenders" approach to ensure log decoration works across different NLog configurations:

### Decoration Strategy

1. **First attempt**: Modify `Message` property
   - Gets original `Message` value
   - Sets `Message` to `{original message} {linking metadata}`
   - Triggers message re-formatting

2. **Second attempt**: Check if `FormattedMessage` contains decoration
   - Retrieves `FormattedMessage` property
   - Checks if linking token is already present
   - If not present, directly modifies the formatted message backing field

3. **Backing field modification**: Directly set `_formattedMessage` or `formattedMessage` field
   - NLog 4.5+: Uses `_formattedMessage` backing field
   - NLog pre-4.5: Uses `formattedMessage` backing field
   - Sets field to `{formatted message} {linking metadata}`

### Version Compatibility

Since NLog version strings only report major version numbers (preventing use of min/max version attributes in instrumentation.xml), the wrapper uses reflection to detect the correct backing field name:

- Checks for `_formattedMessage` field first (NLog 4.5+)
- Falls back to `formattedMessage` field if not found (pre-4.5)
- Caches the field accessor for performance

### Linking Metadata Format

- **Format**: `NR-LINKING|{entity.guid}|{hostname}|{trace.id}|{span.id}|{entity.name}`
- **Placement**: Appended to log message with a space separator

## Known Limitations

The dual-decoration approach does not work for all log messages, particularly:
- Messages output by ASP.NET Core when NLog.Web.AspNetCore is used
- Messages processed through certain custom layouts or targets that cache formatted output

## Configuration Requirements

The wrapper respects the following agent configuration settings:

- **`application_logging.enabled`**: Master switch for all logging features
- **`application_logging.forwarding.enabled`**: Enables forwarding logs to New Relic
- **`application_logging.forwarding.context_data.enabled`**: Enables capturing context data
- **`application_logging.local_decorating.enabled`**: Enables decorating log output

## License
Copyright 2020 New Relic, Inc. All rights reserved.
SPDX-License-Identifier: Apache-2.0

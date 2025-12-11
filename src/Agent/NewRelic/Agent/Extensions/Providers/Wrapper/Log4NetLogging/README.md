# New Relic .NET Agent Log4NetLogging Instrumentation

## Overview

The Log4NetLogging instrumentation wrapper provides automatic capture and forwarding of log messages from log4net and Sitecore.Logging (a log4net fork) to New Relic. It enriches log events with distributed tracing context, captures log metadata and context data, and optionally decorates log messages with linking metadata for correlation with APM data.

## Instrumented Methods

### Log4netWrapper
- **Wrapper**: [Log4netWrapper.cs](Log4netWrapper.cs)
- **Assembly**: `log4net`
- **Type**: `log4net.Repository.Hierarchy.Logger`

| Method | Creates Transaction | Requires Existing Transaction |
|--------|-------------------|------------------------------|
| CallAppenders | No | No |

### SitecoreLoggingWrapper
- **Wrapper**: [SitecoreLoggingWrapper.cs](SitecoreLoggingWrapper.cs)
- **Assembly**: `Sitecore.Logging`
- **Type**: `log4net.Repository.Hierarchy.Logger`

| Method | Creates Transaction | Requires Existing Transaction |
|--------|-------------------|------------------------------|
| CallAppenders | No | No |

[Instrumentation.xml](https://github.com/newrelic/newrelic-dotnet-agent/blob/main/src/Agent/NewRelic/Agent/Extensions/Providers/Wrapper/Log4NetLogging/Instrumentation.xml)

## Log Message Capture

Both wrappers intercept the `CallAppenders` method, which is called internally by log4net after a log event is created but before it's written to appenders. This allows the agent to:

1. **Capture log data**: Extract timestamp, level, message, exception, and context properties
2. **Enrich with trace context**: Associate logs with the current distributed trace span and trace IDs
3. **Forward to New Relic**: Send log data to New Relic Logs (if enabled)
4. **Decorate log output**: Add linking metadata to log events (if enabled)

## Attributes Added

### Log Event Data Captured

- **timestamp**: Log event timestamp (from `TimeStamp` property, in local time for older log4net versions)
- **level**: Log level (e.g., DEBUG, INFO, WARN, ERROR, FATAL)
- **message**: Rendered log message (from `RenderedMessage` property)
- **exception**: Exception object if present (from `ExceptionObject` property for log4net, `m_thrownException` field for Sitecore.Logging)
- **span.id**: Current span ID from distributed trace context
- **trace.id**: Current trace ID from distributed trace context
- **context data**: Custom properties from log event context

### Context Data Extraction

#### Log4net Context Data
- Extracted from `LoggingEvent.GetProperties()` method
- Returns all properties set via `log4net.ThreadContext`, `log4net.GlobalContext`, or event properties

#### Sitecore.Logging Context Data
- Extracted from two sources:
  1. **MappedContext**: Modern context properties (from `MappedContext` property)
  2. **Legacy Properties**: Older versions use `Properties.m_ht` internal hashtable
- Combines both sources into a single context data dictionary

## Log Decoration

When log decoration is enabled (`application_logging.forwarding.context_data.enabled=true`), the wrapper adds a `NR_LINKING` property to each log event containing formatted linking metadata:

- **Format**: `NR-LINKING|{entity.guid}|{hostname}|{trace.id}|{span.id}|{entity.name}`
- **Property key**: `NR_LINKING` (uses underscores for compatibility with frameworks that don't allow hyphens)
- **Injection point**: Added to `LoggingEvent.Properties` dictionary before appenders process the event
- **Usage**: Allows correlation between APM transactions and log output in external log aggregators

## Configuration Requirements

### Application Logging Configuration

The wrapper respects the following agent configuration settings:

- **`application_logging.enabled`**: Master switch for all logging features
- **`application_logging.forwarding.enabled`**: Enables forwarding logs to New Relic
- **`application_logging.forwarding.context_data.enabled`**: Enables capturing context data from log events
- **`application_logging.local_decorating.enabled`**: Enables adding `NR_LINKING` property to log events

## Sitecore.Logging Differences

Sitecore.Logging is an older fork of log4net with some internal differences:

1. **Exception field**: Uses `m_thrownException` field instead of `ExceptionObject` property
2. **Legacy context**: Requires accessing internal `PropertiesCollection.m_ht` hashtable for older properties
3. **Context sources**: Retrieves context from both `MappedContext` and legacy `Properties` collection

## License
Copyright 2020 New Relic, Inc. All rights reserved.
SPDX-License-Identifier: Apache-2.0

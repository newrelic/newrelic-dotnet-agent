// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Diagnostics.Tracing;
using NewRelic.Agent.Extensions.Logging;

namespace NewRelic.Agent.Core.Logging
{

    public class OpenTelemetrySDKLogger : EventListener
    {
        // The OpenTelmetry SDK and documentation has a built in diagnostic logger, all code that wants to be compatible with the
        // OpenTelemetry SDK diagnostic logger needs to write events to an EventSource that has a name prefixed with "OpenTelemetry-".
        const string OpenTelemetryEventSourceNamePrefix = "OpenTelemetry-";

        private EventLevel? _eventSourceLevel;

        // We need to configure the EventSource log level outside of the constructor because the OnEventSourceCreated event handler
        // can be triggered before the constructor completes (after the base constructor completes). For performance reasons, we
        // only want to subscribe to events written at a level that matches the logging level enabled for our logging library.
        public EventLevel EventSourceLevel => _eventSourceLevel ??= MapLoggingLevelToEventSourceLevel();

        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            // This method can be called before .ctor is finished so we need to ensure that we only access things
            // on this class that are available before the .ctor completes. For example, the log level is set lazily
            // using a property and then cached.

            if (eventSource.Name.StartsWith(OpenTelemetryEventSourceNamePrefix, StringComparison.OrdinalIgnoreCase))
            {
                EnableEvents(eventSource, EventSourceLevel, EventKeywords.All);
            }
        }

        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            if (!eventData.EventSource.Name.StartsWith(OpenTelemetryEventSourceNamePrefix, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var formattedMessage = string.Empty;
            if (eventData.Message != null)
            {
                formattedMessage = eventData.Message;
                if (eventData.Payload != null)
                {
                    // Convert the payload collection to an array to use the string.Format overload that takes an array of objects.
                    var messageArguments = new object[eventData.Payload.Count];
                    eventData.Payload.CopyTo(messageArguments, 0);

                    formattedMessage = string.Format(eventData.Message, messageArguments);
                }
            }

            // Map EventSource levels to New Relic agent log levels
            // EventSource uses standard .NET diagnostics levels; we map them to our agent's LogLevel enum
            // Reference: LogLevelExtensions.cs maps FINEST→Verbose, DEBUG→Debug, INFO→Information
            // 
            // EventSource Levels:  New Relic Levels:
            // - Verbose        → FINEST (most detailed)
            // - Informational           → INFO   (normal operations)
            // - Warning             → WARN   (potential issues)
            // - Error/Critical     → ERROR  (actual problems)
            var logLevel = eventData.Level switch
            {
                EventLevel.Critical => LogLevel.Error,     // Critical errors → ERROR
                EventLevel.Error => LogLevel.Error,       // Errors → ERROR  
                EventLevel.Warning => LogLevel.Warn,         // Warnings → WARN
                EventLevel.Informational => LogLevel.Info,   // Informational → INFO
                EventLevel.LogAlways => LogLevel.Info,       // Always logged → INFO
                EventLevel.Verbose => LogLevel.Finest,       // Verbose diagnostics → FINEST
                _ => LogLevel.Debug    // Unknown levels → DEBUG (safe default)
            };

            Log.LogMessage(logLevel, "OpenTelemetrySDK: EventSource: '{0}' Message: '{1}'", eventData.EventSource.Name, formattedMessage);
        }

        // Map New Relic agent log levels to EventSource levels for subscribing to OpenTelemetry SDK events
        // 
        // IMPORTANT: EventSource.Verbose maps to New Relic FINEST (not DEBUG!)
        // Reference: LogLevelExtensions.cs shows the official mapping:
        //   - FINEST/VERBOSE/FINE/FINER/TRACE → Serilog.Verbose
        //   - DEBUG       → Serilog.Debug
        //   - INFO/NOTICE             → Serilog.Information
        //   - WARN/ALERT      → Serilog.Warning
        //   - ERROR/CRITICAL/FATAL       → Serilog.Error
        //
        // EventSource API only has: Verbose, Informational, Warning, Error, Critical, LogAlways
        // (no equivalent to Serilog.Debug), so we must choose the closest match for each level.
        private static EventLevel MapLoggingLevelToEventSourceLevel()
        {
            if (Log.IsFinestEnabled)
            {
                // FINEST: Capture ALL OTel diagnostic information including verbose/trace-level details
                // This is the most detailed logging level - maps directly to EventSource.Verbose
                return EventLevel.Verbose;
            }
            else if (Log.IsDebugEnabled)
            {
                // DEBUG: Capture Informational and above from OTel, EXCLUDE Verbose to reduce noise
                // This is correct because:
                //   - EventSource.Verbose = New Relic FINEST (more detailed than DEBUG)
                //   - DEBUG is less verbose than FINEST, so we skip Verbose events
                //   - EventSource has no "Debug" level, so Informational is the closest match
                return EventLevel.Informational;
            }
            else if (Log.IsInfoEnabled)
            {
                // INFO: Capture normal operational messages from OTel
                // Maps directly to EventSource.Informational
                return EventLevel.Informational;
            }
            else if (Log.IsWarnEnabled)
            {
                // WARN: Only capture warnings and errors from OTel, skip informational messages
                // Maps directly to EventSource.Warning
                return EventLevel.Warning;
            }
            else if (Log.IsErrorEnabled)
            {
                // ERROR: Only capture errors and critical issues from OTel
                // Maps directly to EventSource.Error (includes Critical events)
                return EventLevel.Error;
            }
            else
            {
                // OFF/None: Agent logging is disabled, but still capture LogAlways events from OTel
                // LogAlways events are critical diagnostic messages that bypass normal filtering
                return EventLevel.LogAlways;
            }
        }
    }
}

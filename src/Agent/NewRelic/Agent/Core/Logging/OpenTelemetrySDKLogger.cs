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
                    if (eventData.Payload != null)
                    {
                        // Convert the payload collection to an array to to use the string.Format overload that takes an array of objects.
                        var messageArguments = new object[eventData.Payload.Count];
                        eventData.Payload.CopyTo(messageArguments, 0);

                        formattedMessage = string.Format(eventData.Message, messageArguments);
                    }
                }
            }

            // Map EventSource levels to New Relic agent log levels
            // Based on https://github.com/newrelic/newrelic-dotnet-agent/blob/main/src/Agent/NewRelic/Agent/Core/Logging/LogLevelExtensions.cs mapping: Verbose=FINEST, Debug=DEBUG, Information=INFO
            var logLevel = eventData.Level switch
            {
                EventLevel.Critical => LogLevel.Error,       // Critical errors → Error
                EventLevel.Error => LogLevel.Error,          // Errors → Error  
                EventLevel.Warning => LogLevel.Warn,         // Warnings → Warn
                EventLevel.Informational => LogLevel.Info,   // Informational → Info (not Debug!)
                EventLevel.LogAlways => LogLevel.Info,       // Always logged → Info
                EventLevel.Verbose => LogLevel.Finest,       // Verbose diagnostics → Finest
                _ => LogLevel.Debug                          // Unknown → Debug (safe default)
            };

            Log.LogMessage(logLevel, "OpenTelemetrySDK: EventSource: '{0}' Message: '{1}'", eventData.EventSource.Name, formattedMessage);
        }

        // Map New Relic agent log levels to EventSource levels
        private static EventLevel MapLoggingLevelToEventSourceLevel()
        {
            if (Log.IsFinestEnabled)
            {
                // Finest: Capture all OTel diagnostic information including verbose details
                return EventLevel.Verbose;
            }
            else if (Log.IsDebugEnabled)
            {
                // Debug: Capture informational and above, exclude verbose to reduce noise
                return EventLevel.Informational;
            }
            else if (Log.IsInfoEnabled)
            {
                // Info: Match informational level
                return EventLevel.Informational;
            }
            else if (Log.IsWarnEnabled)
            {
                // Warn: Only capture warnings and errors from OTel
                return EventLevel.Warning;
            }
            else if (Log.IsErrorEnabled)
            {
                // Error: Only capture errors and critical issues from OTel
                return EventLevel.Error;
            }
            else
            {
                // Off/None: Only capture critical messages that must always be logged
                return EventLevel.LogAlways;
            }
        }
    }
}

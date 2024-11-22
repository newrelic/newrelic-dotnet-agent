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

            // TODO: Is this the correct mapping
            var logLevel = eventData.Level switch
            {
                EventLevel.Critical => LogLevel.Error,
                EventLevel.Error => LogLevel.Error,
                EventLevel.Warning => LogLevel.Warn,
                EventLevel.Informational => LogLevel.Info,
                EventLevel.LogAlways => LogLevel.Info,
                EventLevel.Verbose => LogLevel.Debug,
                _ => LogLevel.Finest
            };

            Log.LogMessage(logLevel, "OpenTelemetrySDK: EventSource: '{0}' Message: '{1}'", eventData.EventSource.Name, formattedMessage);
        }

        // TODO: Is this the correct mapping
        private static EventLevel MapLoggingLevelToEventSourceLevel()
        {
            if (Log.IsFinestEnabled)
            {
                return EventLevel.Verbose;
            }
            else if (Log.IsDebugEnabled)
            {
                return EventLevel.Verbose;
            }
            else if (Log.IsInfoEnabled)
            {
                return EventLevel.Informational;
            }
            else if (Log.IsWarnEnabled)
            {
                return EventLevel.Warning;
            }
            else if (Log.IsErrorEnabled)
            {
                return EventLevel.Error;
            }
            else
            {
                return EventLevel.LogAlways;
            }
        }
    }
}

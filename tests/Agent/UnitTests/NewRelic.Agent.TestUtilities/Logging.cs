// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NewRelic.Agent.Core.Logging;
using Serilog.Events;
using Serilog;
using Log = NewRelic.Core.Logging.Log;
using Logger = NewRelic.Agent.Core.Logging.Logger;

namespace NewRelic.Agent.TestUtilities
{
    /// <summary>
    /// While this object is in scope, serilog will log to an in-memory sink
    /// </summary>
    public class Logging : IDisposable
    {
        private readonly InMemorySink _inMemorySink = new InMemorySink();

        /// <summary>
        /// Initializes serilog to log to an in-memory sink which can then be queried
        /// </summary>
        public Logging(LogEventLevel logLevel = LogEventLevel.Information)
        {
            var loggerConfig = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .WriteTo.Sink(_inMemorySink);

            Serilog.Log.Logger = loggerConfig.CreateLogger();

            Log.Initialize(new Logger());
        }

        public void Dispose()
        {
        }

        public override string ToString()
        {
            var builder = new StringBuilder();
            var logEvents = _inMemorySink.LogEvents;
            if (logEvents == null)
                return "Nothing was logged.";

            foreach (var logEvent in logEvents)
            {
                builder.AppendLine(logEvent.RenderMessage());
            }

            return builder.ToString();
        }

        /// <summary>
        /// Checks to see if the given message was logged since this object was constructed.
        /// </summary>
        /// <param name="message">The message you want to check for.</param>
        /// <returns>True if the message was logged, false otherwise.</returns>
        public bool HasMessage(string message) => _inMemorySink.LogEvents.Any(e => e.RenderMessage() == message);

        /// <summary>
        /// checks for messages that begins with a segment
        /// </summary>
        /// <param name="segment"></param>
        /// <returns></returns>
        public bool HasMessageBeginningWith(string segment) => _inMemorySink.LogEvents.Any(item => item.RenderMessage().StartsWith(segment));

        /// <summary>
        /// checks for messages that contain a string
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        public bool HasMessageThatContains(string message) => _inMemorySink.LogEvents.Any(item => item.RenderMessage().Contains(message));

        public bool HasErrorMessageThatContains(string message) => _inMemorySink.LogEvents.Any(item => item.Exception?.Message?.Contains(message) == true);

        /// <summary>
        /// Counts the number of messages that were logged since the construction of this object.
        /// </summary>
        public int MessageCount => _inMemorySink.LogEvents.Count();

        /// <summary>
        /// Counts the number of [level] messages that were logged since the construction of this object.
        /// </summary>
        /// <returns>The number of messages logged at [level] level.</returns>
        private int LevelCount(LogEventLevel level) => _inMemorySink.LogEvents.Count(e => e.Level == level);

        public IEnumerable<string> ErrorMessages =>
            _inMemorySink.LogEvents
                .Where(@event => @event.Level == LogEventLevel.Error)
                .Select(@event => @event.RenderMessage());

        /// <summary>
        /// Counts the number of error level messages that were logged since construction of this object.
        /// </summary>
        /// <returns></returns>
        public int ErrorCount => LevelCount(LogEventLevel.Error);

        /// <summary>
        /// Counts the number of warn level messages that were logged since construction of this object.
        /// </summary>
        /// <returns></returns>
        public int WarnCount => LevelCount(LogEventLevel.Warning);

        /// <summary>
        /// Counts the number of info level messages that were logged since construction of this object.
        /// </summary>
        /// <returns></returns>
        public int InfoCount => LevelCount(LogEventLevel.Information);

        /// <summary>
        /// Counts the number of debug level messages that were logged since construction of this object.
        /// </summary>
        /// <returns></returns>
        public int DebugCount => LevelCount(LogEventLevel.Debug);

        /// <summary>
        /// Counts the number of finest level messages that were logged since construction of this object.
        /// </summary>
        /// <returns></returns>
        public int FinestCount => LevelCount(LogEventLevel.Verbose);
    }
}

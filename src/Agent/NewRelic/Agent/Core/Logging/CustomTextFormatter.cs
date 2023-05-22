// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.IO;
using Serilog.Events;
using Serilog.Formatting;

namespace NewRelic.Agent.Core
{
    class CustomTextFormatter : ITextFormatter
    {
        public void Format(LogEvent logEvent, TextWriter output)
        {
            // try to get process and thread id
            logEvent.Properties.TryGetValue("pid", out var pid);
            logEvent.Properties.TryGetValue("tid", out var tid);

            // format matches the log4net format, but adds the Exception property (if present in the message as a separate property) to the end, after the newline
            // if that's not desired, take the Exception property out and refactor calls to `Serilog.Log.Logger.LogXxxx(exception, message)` so the exception is
            // formatted into the message 
            output.Write($"{logEvent.Timestamp.ToUniversalTime():yyyy-MM-dd HH:mm:ss,fff} NewRelic {logEvent.Level.TranslateLogLevel(),6}: [pid: {pid?.ToString()}, tid: {tid?.ToString()}] {logEvent.MessageTemplate}{output.NewLine}{logEvent.Exception}");
        }
    }
}

// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using Serilog.Configuration;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using System.Collections.Concurrent;

namespace NewRelic.Agent.Core.Logging
{
    public class InMemorySink : ILogEventSink, IDisposable
    {
        private static readonly InMemorySink _instance = new InMemorySink();

        public readonly ConcurrentQueue<LogEvent> LogEvents;

        public static InMemorySink Instance
        {
            get
            {
                return _instance;
            }
        }

        protected InMemorySink()
        {
            LogEvents = new ConcurrentQueue<LogEvent>();
        }

        public void Emit(LogEvent logEvent)
        {
            LogEvents.Enqueue(logEvent);
        }

        public void Dispose()
        {
            while (LogEvents.TryDequeue(out _))
            { }
        }
    }
}

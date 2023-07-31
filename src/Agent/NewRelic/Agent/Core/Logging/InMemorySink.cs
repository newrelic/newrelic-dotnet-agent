// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using Serilog.Core;
using Serilog.Events;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace NewRelic.Agent.Core.Logging
{
    public class InMemorySink : ILogEventSink, IDisposable
    {
        private readonly ConcurrentQueue<LogEvent> _logEvents;

        public InMemorySink()
        {
            _logEvents = new ConcurrentQueue<LogEvent>();
        }

        public void Emit(LogEvent logEvent)
        {
            _logEvents.Enqueue(logEvent);
        }

        public IEnumerable<LogEvent> LogEvents
        {
            get
            {
                return _logEvents;
            }
        }

        public void Clear()
        {
            while (_logEvents.TryDequeue(out _))
            { }
        }

        public void Dispose()
        {
            Clear();
        }
    }
}

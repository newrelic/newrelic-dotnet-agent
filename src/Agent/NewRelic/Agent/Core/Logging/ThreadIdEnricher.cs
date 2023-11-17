// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Threading;
using NewRelic.Core.CodeAttributes;
using Serilog.Core;
using Serilog.Events;

namespace NewRelic.Agent.Core
{
    /// <summary>
    /// Adds a tid property to the log event containing the current managed thread id
    /// </summary>
    [NrExcludeFromCodeCoverage]
    internal class ThreadIdEnricher : ILogEventEnricher
    {
        private LogEventProperty _tidProperty;

        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            var threadId = Thread.CurrentThread.ManagedThreadId;

            var prop = _tidProperty;

            if (prop == null || (int?)((ScalarValue)prop.Value).Value != threadId)
                _tidProperty = prop = propertyFactory.CreateProperty("tid", threadId);

            logEvent.AddPropertyIfAbsent(prop);
        }
    }
}

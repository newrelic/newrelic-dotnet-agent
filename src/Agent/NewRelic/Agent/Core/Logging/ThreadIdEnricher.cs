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

        private static readonly ThreadLocal<LogEventProperty> _tidProperty = new ThreadLocal<LogEventProperty>();

        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            if (!_tidProperty.IsValueCreated)
                _tidProperty.Value = propertyFactory.CreateProperty("tid", Thread.CurrentThread.ManagedThreadId);

            logEvent.AddPropertyIfAbsent(_tidProperty.Value);
        }
    }
}

// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Core.CodeAttributes;
using NewRelic.SystemInterfaces;
using Serilog.Core;
using Serilog.Events;

namespace NewRelic.Agent.Core
{
    /// <summary>
    /// Adds a pid property to the log event containing the current process id
    /// </summary>
    [NrExcludeFromCodeCoverage]
    internal class ProcessIdEnricher : ILogEventEnricher
    {
        private static int _pid = new ProcessStatic().GetCurrentProcess().Id;

        private static LogEventProperty _prop;

        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            _prop ??= propertyFactory.CreateProperty("pid", _pid);

            logEvent.AddPropertyIfAbsent(_prop);
        }
    }
}

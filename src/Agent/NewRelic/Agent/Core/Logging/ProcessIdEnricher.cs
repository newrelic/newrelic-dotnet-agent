// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Core.CodeAttributes;
using NewRelic.SystemInterfaces;
using Serilog.Core;
using Serilog.Events;

namespace NewRelic.Agent.Core
{
    [NrExcludeFromCodeCoverage]
    internal class ProcessIdEnricher : ILogEventEnricher
    {
        private static int _pid = new ProcessStatic().GetCurrentProcess().Id;

        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("pid", _pid));
        }
    }
}

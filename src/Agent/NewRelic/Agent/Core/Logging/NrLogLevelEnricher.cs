// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Core.CodeAttributes;
using Serilog.Core;
using Serilog.Events;

namespace NewRelic.Agent.Core
{
    /// <summary>
    /// Maps serilog log level to corresponding "legacy" log4net loglevel and adds the mapped value as a property named NRLogLevel
    /// </summary>
    [NrExcludeFromCodeCoverage]
    internal class NrLogLevelEnricher : ILogEventEnricher
    {
        [NrExcludeFromCodeCoverage]
        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("NRLogLevel", logEvent.Level.TranslateLogLevel()));
        }
    }
}

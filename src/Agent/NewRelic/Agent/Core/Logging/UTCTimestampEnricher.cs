// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Core.CodeAttributes;
using Serilog.Core;
using Serilog.Events;

namespace NewRelic.Agent.Core
{
    /// <summary>
    /// Formats the current UTC time for logging in the agent
    /// </summary>
    [NrExcludeFromCodeCoverage]
    public class UTCTimestampEnricher : ILogEventEnricher
    {
        public const string UTCTimestampPropertyName = "UTCTimestamp";
        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(UTCTimestampPropertyName,
                $"{DateTimeOffset.UtcNow:yyy-MM-dd HH:mm:ss,fff}"));
        }
    }
}

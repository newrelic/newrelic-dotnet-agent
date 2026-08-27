// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;

namespace NewRelic.Agent.Core.Logging;

/// <summary>
/// Translates OTEL_LOG_LEVEL values into agent configuration log levels. The supported values are
/// none, error, warn, info, and debug, as defined by opentelemetry-dotnet-instrumentation.
/// </summary>
public static class OpenTelemetryLogLevelMapper
{
    private static readonly Dictionary<string, string> _agentLogLevelsByOtelLogLevel = new(StringComparer.OrdinalIgnoreCase)
    {
        { "none", "OFF" },
        { "error", "ERROR" },
        { "warn", "WARN" },
        { "info", "INFO" },
        { "debug", "DEBUG" }
    };

    /// <summary>
    /// Maps an OTEL_LOG_LEVEL value to an agent configuration log level.
    /// </summary>
    /// <returns>True when the value is supported. False means the caller must ignore the value and
    /// fall through to the next configuration source.</returns>
    public static bool TryMapToAgentLogLevel(string otelLogLevel, out string agentLogLevel)
    {
        agentLogLevel = null;

        if (string.IsNullOrWhiteSpace(otelLogLevel))
        {
            return false;
        }

        return _agentLogLevelsByOtelLogLevel.TryGetValue(otelLogLevel.Trim(), out agentLogLevel);
    }
}

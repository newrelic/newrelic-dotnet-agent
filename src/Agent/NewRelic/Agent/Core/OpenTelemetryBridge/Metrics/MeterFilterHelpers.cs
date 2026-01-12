// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Linq;
using NewRelic.Agent.Configuration;

namespace NewRelic.Agent.Core.OpenTelemetryBridge.Metrics
{
    public static class MeterFilterHelpers
    {
        public static bool ShouldEnableInstrumentsInMeter(IConfiguration configuration, string meterName)
        {
            if (string.IsNullOrEmpty(meterName))
                return false;

            var includeFilters = configuration.OpenTelemetryMetricsIncludeFilters;
            var excludeFilters = configuration.OpenTelemetryMetricsExcludeFilters;

            // Check customer exclude list (overrides everything)
            if (excludeFilters != null && excludeFilters.Contains(meterName))
                return false;

            // Check customer include list (overrides built-in exclusions)
            if (includeFilters != null && includeFilters.Contains(meterName))
                return true;

            // Check built-in exclusions
            if (!IsNotBuiltInExclusion(meterName))
                return false;

            // Default: permissive (allow all not explicitly excluded)
            return true;
        }

        public static bool IsNotBuiltInExclusion(string meterName)
        {
            if (string.IsNullOrEmpty(meterName))
                return false;

            if (meterName.StartsWith("NewRelic", StringComparison.OrdinalIgnoreCase) ||
                meterName.StartsWith("OpenTelemetry", StringComparison.OrdinalIgnoreCase) ||
                meterName.StartsWith("System.Diagnostics.Metrics", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return true;
        }
    }
}

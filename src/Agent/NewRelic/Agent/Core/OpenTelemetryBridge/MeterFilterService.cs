// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Linq;
using NewRelic.Agent.Configuration;

namespace NewRelic.Agent.Core.OpenTelemetryBridge
{
    public class MeterFilterService
    {
        private readonly IConfiguration _configuration;

        public MeterFilterService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public bool ShouldEnableInstrumentsInMeter(string meterName)
        {
            if (string.IsNullOrEmpty(meterName))
                return false;

            var includeFilters = _configuration.OpenTelemetryMetricsIncludeFilters;
            var excludeFilters = _configuration.OpenTelemetryMetricsExcludeFilters;

            // Customer exclude list (highest precedence)
            if (excludeFilters?.Contains(meterName)==true)
                return false;

            // Customer include list (overrides built-in exclusions)
            if (includeFilters?.Contains(meterName)==true)
                return true;

            // Built-in exclusions
            if (!IsNotBuiltInExclusion(meterName))
                return false;

            // Default: permissive
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

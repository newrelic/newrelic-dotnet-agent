// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace NewRelic.Agent.Core.Configuration
{
    public class EventHarvestConfig
    {
        public const string ErrorEventHarvestLimitKey = "error_event_data";
        public const string CustomEventHarvestLimitKey = "custom_event_data";
        public const string TransactionEventHarvestLimitKey = "analytic_event_data";
        public const string LogEventHarvestLimitKey = "log_event_data";

        [JsonProperty("report_period_ms")]
        public int? ReportPeriodMs { get; set; }

        [JsonProperty("harvest_limits")]
        public Dictionary<string, int> HarvestLimits { get; set; }

        public int? ErrorEventHarvestLimit()
        {
            return GetEventHarvestLimitFor(ErrorEventHarvestLimitKey);
        }

        public TimeSpan? ErrorEventHarvestCycle()
        {
            return GetEventHarvestCycleFor(ErrorEventHarvestLimitKey);
        }

        public int? CustomEventHarvestLimit()
        {
            return GetEventHarvestLimitFor(CustomEventHarvestLimitKey);
        }

        public TimeSpan? CustomEventHarvestCycle()
        {
            return GetEventHarvestCycleFor(CustomEventHarvestLimitKey);
        }

        public int? TransactionEventHarvestLimit()
        {
            return GetEventHarvestLimitFor(TransactionEventHarvestLimitKey);
        }

        public TimeSpan? TransactionEventHarvestCycle()
        {
            return GetEventHarvestCycleFor(TransactionEventHarvestLimitKey);
        }

        public int? LogEventHarvestLimit()
        {
            return GetEventHarvestLimitFor(LogEventHarvestLimitKey);
        }

        public TimeSpan? LogEventHarvestCycle()
        {
            return GetEventHarvestCycleFor(LogEventHarvestLimitKey);
        }

        private int? GetEventHarvestLimitFor(string eventType)
        {
            if (HarvestLimits == null || !ReportPeriodMs.HasValue || !HarvestLimits.ContainsKey(eventType)) return null;

            return HarvestLimits[eventType];
        }

        private TimeSpan? GetEventHarvestCycleFor(string eventType)
        {
            var harvestLimit = GetEventHarvestLimitFor(eventType);
            return harvestLimit.HasValue && harvestLimit > 0 ? TimeSpan.FromMilliseconds(ReportPeriodMs.Value) : null as TimeSpan?;
        }
    }
}

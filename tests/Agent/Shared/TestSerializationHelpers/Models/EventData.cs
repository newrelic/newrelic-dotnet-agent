// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using Newtonsoft.Json;

namespace NewRelic.Agent.Tests.TestSerializationHelpers.Models
{
    public class EventData
    {
        [JsonProperty("report_period_ms")]
        public int ReportPeriodMS { get; set; }

        [JsonProperty("harvest_limits")]
        public HarvertLimits HarvestLimits { get; set; }
    }

    public class HarvertLimits
    {
        [JsonProperty("analytic_event_data")]
        public int AnalyticEventData { get; set; }

        [JsonProperty("custom_event_data")]
        public int CustomEventData { get; set; }

        [JsonProperty("error_event_data")]
        public int ErrorEventData { get; set; }
    }
}

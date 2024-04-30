// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using Newtonsoft.Json;

namespace NewRelic.Agent.Tests.TestSerializationHelpers.Models
{
    public class DataMethods
    {
        [JsonProperty("span_event_data")]
        public DataMethod SpanEventData { get; set; }

        [JsonProperty("error_event_data")]
        public DataMethod ErrorEventData { get; set; }

        [JsonProperty("analytic_event_data")]
        public DataMethod AnalyticEventData { get; set; }

        [JsonProperty("custom_event_data")]
        public DataMethod CustomEventData { get; set; }
    }

    public class DataMethod
    {
        [JsonProperty("report_period_in_seconds")]
        public int ReportPeriodInSeconds { get; set; }

        [JsonProperty("max_samples_stored")]
        public int MaxSamplesStored { get; set; }
    }
}

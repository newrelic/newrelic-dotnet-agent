// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using Newtonsoft.Json;

namespace NewRelic.IntegrationTests.Models
{
    public class HeaderValidationData
    {
        [JsonProperty("metric_data", NullValueHandling = NullValueHandling.Include)]
        public bool? MetricDataHasMap { get; set; }

        [JsonProperty("analytic_event_data", NullValueHandling = NullValueHandling.Include)]
        public bool? AnalyticEventDataHasMap { get; set; }

        [JsonProperty("transaction_sample_data", NullValueHandling = NullValueHandling.Include)]
        public bool? TransactionSampleDataHasMap { get; set; }

        [JsonProperty("span_event_data", NullValueHandling = NullValueHandling.Include)]
        public bool? SpanEventDataHasMap { get; set; }
    }
}

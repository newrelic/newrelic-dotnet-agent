// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Collections.Generic;
using Newtonsoft.Json;

namespace NewRelic.Agent.Tests.TestSerializationHelpers.Models
{
    public class ConnectResponseData
    {
        [JsonProperty("agent_run_id")]
        public string AgentRunId { get; set; }

        [JsonProperty("data_report_period")]
        public int DataReportPeriod { get; set; }

        [JsonProperty("application_id")]
        public string ApplicationId { get; set; }

        [JsonProperty("browser_key")]
        public string BrowserKey { get; set; }

        [JsonProperty("beacon")]
        public string Beacon { get; set; }

        [JsonProperty("apdex_t")]
        public double ApdexT { get; set; }

        [JsonProperty("url_rules")]
        public IEnumerable<UrlRule> UrlRules { get; set; }

        [JsonProperty("data_methods")]
        public DataMethods DataMethods { get; set; }

        [JsonProperty("event_data")]
        public EventData EventData { get; set; }

        [JsonProperty("transaction_naming_scheme")]
        public string TransactionNamingScheme { get; set; }

        [JsonProperty("max_payload_size_in_bytes")]
        public int MaxPayloadSizeInBytes { get; set; }

        [JsonProperty("sampling_rate")]
        public int SamplingRate { get; set; }

        [JsonProperty("collect_error_events")]
        public bool CollectErrorEvents { get; set; }

        [JsonProperty("collect_analytics_events")]
        public bool CollectAnalyticEvents { get; set; }

        [JsonProperty("collect_errors")]
        public bool CollectErrors { get; set; }

        [JsonProperty("collect_traces")]
        public bool CollectTraces { get; set; }

        [JsonProperty("sampling_target_period_in_seconds")]
        public int CollectSamplingTargetPeriodInSeconds { get; set; }

        [JsonProperty("sampling_target")]
        public int SamplingTarget { get; set; }

        [JsonProperty("primary_application_id")]
        public string PrimaryApplicationId { get; set; }

        [JsonProperty("account_id")]
        public string AccountId { get; set; }

        [JsonProperty("messages")]
        public IEnumerable<ConnectResponseMessage> Messages { get; set; }

        [JsonProperty("cross_process_id")]
        public string CrossProcessId { get; set; }

        [JsonProperty("encoding_key")]
        public string EncodingKey { get; set; }

        [JsonProperty("trusted_account_ids")]
        public IEnumerable<int> TrustedAccountIds { get; set; }

        [JsonProperty("trusted_account_key")]
        public string TrustedAccountKey { get; set; }

        [JsonProperty("request_headers_map")]
        public object RequestHeadersMap { get; set; }

        [JsonProperty("js_agent_loader_version")]
        public string JsAgentLoaderVersion { get; set; }
        
        [JsonProperty("js_agent_file")]
        public string JsAgentFile { get; set; }

        [JsonProperty("episodes_url")]
        public string EpisodesUrl { get; set; }

        [JsonProperty("episodes_file")]
        public string EpisodesFile { get; set; }

        [JsonProperty("error_beacon")]
        public string ErrorBeacon { get; set; }

        [JsonProperty("browser_monitoring.loader_version")]
        public string BrowserMonitoringLoaderVersion { get; set; }

        [JsonProperty("browser_monitoring.loader")]
        public string BrowserMonitoringLoader { get; set; }

        [JsonProperty("browser_monitoring.debug")]
        public string BrowserMonitoringDebug { get; set; }

        [JsonProperty("js_agent_loader")]
        public string JsAgentLoader { get; set; }
    }
}

// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.Serialization;
using NewRelic.Agent.Core.JsonConverters;
using NewRelic.Core.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace NewRelic.Agent.Core.Configuration
{
    /// <summary>
    /// from collector connectApplicationAgent(RequestContext) return value (https://github.com/newrelic/collector/blob/master/src/main/java/com/nr/collector/methods/Connect.java#L259)
    /// </summary>
    public class ServerConfiguration
    {
        [JsonProperty(PropertyName = "agent_run_id", Required = Required.Always)]
        public object AgentRunId { get; set; }

        [JsonProperty("apdex_t")]
        public double? ApdexT { get; set; }

        [JsonProperty("collect_error_events")]
        public bool? ErrorEventCollectionEnabled { get; set; }

        [JsonProperty("collect_analytics_events")]
        public bool? AnalyticsEventCollectionEnabled { get; set; }

        [JsonProperty("collect_span_events")]
        public bool? SpanEventCollectionEnabled { get; set; }

        [JsonProperty("collect_custom_events")]
        public bool? CustomEventCollectionEnabled { get; set; }

        [JsonProperty("collect_errors")]
        public bool? ErrorCollectionEnabled { get; set; }

        [JsonProperty("collect_traces")]
        public bool? TraceCollectionEnabled { get; set; }

        [JsonProperty("data_report_period")]
        public long? DataReportPeriod { get; set; }

        [JsonProperty("max_payload_size_in_bytes")]
        // Making it int per the agent spec. 
        public int? MaxPayloadSizeInBytes { get; set; }

        [JsonProperty("encoding_key")]
        public string EncodingKey { get; set; }

        [JsonProperty("entity_guid")]
        public string EntityGuid { get; set; }

        [JsonProperty("high_security")]
        public bool? HighSecurityEnabled { get; set; }

        [JsonProperty("instrumentation")]
        public IEnumerable<InstrumentationConfig> Instrumentation { get; set; }

        [JsonProperty("messages")]
        public IEnumerable<Message> Messages { get; set; }

        [JsonProperty("sampling_rate")]
        public int? SamplingRate { get; set; }

        [JsonProperty("web_transactions_apdex")]
        public IDictionary<string, double> WebTransactionsApdex { get; set; }

        [JsonProperty("trusted_account_ids")]
        public IEnumerable<long> TrustedIds { get; set; }

        [JsonProperty("request_headers_map")]
        public Dictionary<string, string> RequestHeadersMap { get; set; }


        // Server Side Config

        public bool ServerSideConfigurationEnabled { get; private set; }

        [JsonProperty("agent_config")]
        public AgentConfig RpmConfig { get; set; }


        // Faster Event Harvest

        [JsonProperty("event_harvest_config")]
        public EventHarvestConfig EventHarvestConfig { get; set; }

        [JsonProperty("span_event_harvest_config")]
        public SingleEventHarvestConfig SpanEventHarvestConfig { get; set; }


        // CAT

        [JsonProperty("cross_process_id")]
        public string CatId { get; set; }


        // DISTRIBUTED TRACE
        [JsonProperty("primary_application_id")]
        public string PrimaryApplicationId { get; set; }

        [JsonProperty("trusted_account_key")]
        public string TrustedAccountKey { get; set; }

        [JsonProperty("account_id")]
        public string AccountId { get; set; }

        [JsonProperty("sampling_target")]
        public int? SamplingTarget { get; set; }

        [JsonProperty("sampling_target_period_in_seconds")]
        public int? SamplingTargetPeriodInSeconds { get; set; }


        // RUM

        [JsonProperty("application_id")]
        public string RumSettingsApplicationId { get; set; }

        [JsonProperty("beacon")]
        public string RumSettingsBeacon { get; set; }

        [JsonProperty("browser_key")]
        public string RumSettingsBrowserKey { get; set; }

        [JsonProperty("browser_monitoring.loader")]
        public string RumSettingsBrowserMonitoringLoader { get; set; }

        [JsonProperty("browser_monitoring.loader_debug")]
        public string RumSettingsBrowserMonitoringLoaderDebug { get; set; }

        [JsonProperty("browser_monitoring.loader_version")]
        public string RumSettingsBrowserMonitoringLoaderVersion { get; set; }

        [JsonProperty("episodes_url")]
        public string RumSettingsEpisodesUrl { get; set; }

        [JsonProperty("episodes_file")]
        public string RumSettingsEpisodesFile { get; set; }

        [JsonProperty("error_beacon")]
        public string RumSettingsErrorBeacon { get; set; }

        [JsonProperty("js_agent_file")]
        public string RumSettingsJavaScriptAgentFile { get; set; }

        [JsonProperty("js_agent_loader")]
        public string RumSettingsJavaScriptAgentLoader { get; set; }

        [JsonProperty("js_agent_loader_version")]
        public string RumSettingsJavaScriptAgentLoaderVersion { get; set; }

        [JsonProperty("js_errors_beta")]
        public string RumSettingsJavaScriptErrorsBeta { get; set; }


        // rules

        [JsonProperty("metric_name_rules")]
        public IEnumerable<RegexRule> MetricNameRegexRules { get; set; }

        [JsonProperty("transaction_name_rules")]
        public IEnumerable<RegexRule> TransactionNameRegexRules { get; set; }

        [JsonProperty("url_rules")]
        public IEnumerable<RegexRule> UrlRegexRules { get; set; }

        [JsonProperty("transaction_segment_terms")]
        public IEnumerable<WhitelistRule> TransactionNameWhitelistRules { get; set; }

        public ServerConfiguration()
        {
            RpmConfig = new AgentConfig();
        }

        public static ServerConfiguration GetDefault() => new ServerConfiguration
        {
            RpmConfig =
            {
				// CAT should be disabled for empty/default configurations because CAT cannot function without a CrossProcessId.
				CrossApplicationTracerEnabled = false
            }
        };

        /// <summary>
        /// From server side configuration (https://rpm.newrelic.com/admin/agent_configuration_defaults)
        /// </summary>
        public class AgentConfig
        {
            [JsonProperty("cross_application_tracer.enabled")]
            public bool? CrossApplicationTracerEnabled { get; set; }

            [JsonProperty("error_collector.enabled")]
            public bool? ErrorCollectorEnabled { get; set; }

            [JsonProperty("error_collector.ignore_status_codes")]
            public IEnumerable<string> ErrorCollectorStatusCodesToIgnore { get; set; }

            [JsonProperty("error_collector.ignore_errors")]
            public IEnumerable<string> ErrorCollectorErrorsToIgnore { get; set; }

            [JsonProperty("error_collector.ignore_classes")]
            public IEnumerable<string> ErrorCollectorIgnoreClasses { get; set; }

            [JsonProperty("error_collector.ignore_messages")]
            public IEnumerable<KeyValuePair<string, IEnumerable<string>>> ErrorCollectorIgnoreMessages { get; set; }

            [JsonProperty("error_collector.expected_classes")]
            public IEnumerable<string> ErrorCollectorExpectedClasses { get; set; }

            [JsonProperty("error_collector.expected_messages")]
            public IEnumerable<KeyValuePair<string, IEnumerable<string>>> ErrorCollectorExpectedMessages { get; set; }

            [JsonProperty("error_collector.expected_status_codes")]
            public IEnumerable<string> ErrorCollectorExpectedStatusCodes { get; set; }

            [JsonProperty("ignored_params")]
            public IEnumerable<string> ParametersToIgnore { get; set; }

            [JsonProperty("slow_sql.enabled")]
            public bool? SlowSqlEnabled { get; set; }

            [JsonProperty("transaction_tracer.enabled")]
            public bool? TransactionTracerEnabled { get; set; }

            [JsonProperty("transaction_tracer.explain_enabled")]
            public bool? TransactionTracerExplainEnabled { get; set; }

            [JsonProperty("transaction_tracer.explain_threshold")]
            public double? TransactionTracerExplainThreshold { get; set; }

            [JsonProperty("transaction_tracer.record_sql")]
            public string TransactionTracerRecordSql { get; set; }

            [JsonProperty("transaction_tracer.stack_trace_threshold")]
            public double? TransactionTracerStackThreshold { get; set; }

            [JsonProperty("transaction_tracer.transaction_threshold")]
            public object TransactionTracerThreshold { get; set; }

            [JsonProperty("capture_params")]
            public bool? CaptureParametersEnabled { get; set; }
        }

        public class InstrumentationConfig
        {
            [JsonProperty("config")]
            public string Config { get; set; }

            [JsonProperty("name")]
            public string Name { get; set; }

            public InstrumentationConfig()
            {
                Name = "live_instrumentation";
            }
        }

        public class Message
        {
            [JsonProperty("message")]
            public string Text { get; set; }
            [JsonProperty("level")]
            public string Level { get; set; }
        }

        public class RegexRule
        {
            [JsonProperty("match_expression")]
            public string MatchExpression { get; set; }

            [JsonProperty("replacement")]
            public string Replacement { get; set; }

            [JsonProperty("ignore")]
            public bool? Ignore { get; set; }

            [JsonProperty("eval_order")]
            public long? EvaluationOrder { get; set; }

            [JsonProperty("terminate_chain")]
            public bool? TerminateChain { get; set; }

            [JsonProperty("each_segment")]
            public bool? EachSegment { get; set; }

            [JsonProperty("replace_all")]
            public bool? ReplaceAll { get; set; }
        }

        public class WhitelistRule
        {
            [JsonProperty("prefix")]
            public string Prefix { get; set; }
            [JsonProperty("terms")]
            public IEnumerable<string> Terms { get; set; }
        }

        public static ServerConfiguration FromJson(string json, bool ignoreServerConfiguration = false)
        {
            var serverConfiguration = JsonConvert.DeserializeObject<ServerConfiguration>(json);
            Debug.Assert(serverConfiguration != null);

            if (ignoreServerConfiguration)
            {
                serverConfiguration.RpmConfig = new AgentConfig();
            }

            serverConfiguration.ServerSideConfigurationEnabled = JsonContainsNonNullProperty(json, "agent_config");

            return serverConfiguration;
        }

        public static bool JsonContainsNonNullProperty(string json, string propertyName)
        {
            var dictionary = JsonConvert.DeserializeObject<IDictionary<string, object>>(json);
            Debug.Assert(dictionary != null);

            return dictionary.ContainsKey(propertyName)
                && dictionary[propertyName] != null;
        }

        public static ServerConfiguration FromDeserializedReturnValue(object deserializedJson, bool ignoreServerConfiguration = false)
        {
            var json = JsonConvert.SerializeObject(deserializedJson);
            return FromJson(json, ignoreServerConfiguration);
        }

        [OnError]
        public void OnError(StreamingContext context, ErrorContext errorContext)
        {
            Log.Error(errorContext.Error, "Json serializer context path: {0}", errorContext.Path);
        }
    }
}

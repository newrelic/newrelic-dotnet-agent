// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using Newtonsoft.Json;

namespace NewRelic.Agent.Core.Configuration
{
    /// <summary>
    /// The configuration that is reported to the collector using the agent_settings command.
    /// </summary>
    public class ReportedConfiguration
    {
        [JsonProperty("agent")]
        public const string Agent = ".NET Agent";

        [JsonProperty("apdex_t")]
        public double? ApdexT { get; set; }

        [JsonProperty("cross_process_id")]
        public string CatId { get; set; }

        [JsonProperty("encoding_key")]
        public string EncodingKey { get; set; }

        [JsonProperty("trusted_account_ids")]
        public IEnumerable<long> TrustedAccountIds { get; set; }

        [JsonProperty("max_stack_trace_lines")]
        public int MaxStackTraceLines { get; set; }

        [JsonProperty("server_side_configuration_enabled")]
        public bool ServerSideConfigurationEnabled { get; set; }

        [JsonProperty("ignore_server_side_configuration")]
        public bool IgnoreServerSideConfiguration { get; set; }

        [JsonProperty("thread_profiler.enabled")]
        public bool ThreadProfilerEnabled { get; set; }

        [JsonProperty("cross_application_tracer.enabled")]
        public bool CrossApplicationTracerEnabled { get; set; }

        [JsonProperty("distributed_tracing.enabled")]
        public bool DistributedTracingEnabled { get; set; }

        [JsonProperty("error_collector.enabled")]
        public bool ErrorCollectorEnabled { get; set; }

        [JsonProperty("error_collector.ignore_status_codes")]
        public IEnumerable<string> ErrorCollectorIgnoreStatusCodes { get; set; }

        [JsonProperty("error_collector.ignore_classes")]
        public IEnumerable<string> ErrorCollectorIgnoreClasses { get; set; }

        [JsonProperty("error_collector.ignore_messages")]
        public IDictionary<string, IEnumerable<string>> ErrorCollectorIgnoreMessages { get; set; }

        [JsonProperty("error_collector.expected_classes")]
        public IEnumerable<string> ErrorCollectorExpectedClasses { get; set; }

        [JsonProperty("error_collector.expected_messages")]
        public IDictionary<string, IEnumerable<string>> ErrorCollectorExpectedMessages { get; set; }

        [JsonProperty("error_collector.expected_status_codes")]
        public IEnumerable<string> ErrorCollectorExpectedStatusCodes { get; set; }

        [JsonProperty("transaction_tracer.stack_trace_threshold")]
        public double TransactionTracerStackThreshold { get; set; }

        [JsonProperty("transaction_tracer.explain_enabled")]
        public bool TransactionTracerExplainEnabled { get; set; }

        [JsonProperty("transaction_tracer.max_sql_statements")]
        public uint MaxSqlStatements { get; set; }

        [JsonProperty("transaction_tracer.max_explain_plans")]
        public int MaxExplainPlans { get; set; }

        [JsonProperty("transaction_tracer.explain_threshold")]
        public double TransactionTracerExplainThreshold { get; set; }

        [JsonProperty("transaction_tracer.transaction_threshold")]
        public double TransactionTracerThreshold { get; set; }

        [JsonProperty("transaction_tracer.record_sql")]
        public string TransactionTracerRecordSql { get; set; }

        [JsonProperty("slow_sql.enabled")]
        public bool SlowSqlEnabled { get; set; }

        [JsonProperty("browser_monitoring.auto_instrument")]
        public bool BrowserMonitoringAutoInstrument { get; set; }

        [JsonProperty("transaction_event.max_samples_stored")]
        public int TransactionEventMaxSamplesStored { get; set; }

        // Application Logging settings
        [JsonProperty("application_logging.enabled")]
        public bool ApplicationLoggingEnabled { get; set; }

        [JsonProperty("application_logging.forwarding.enabled")]
        public bool ApplicationLoggingForwardingEnabled { get; set; }

        [JsonProperty("application_logging.forwarding.max_samples_stored")]
        public int ApplicationLoggingForwardingMaxSamplesStored { get; set; }

        [JsonProperty("application_logging.metrics.enabled")]
        public bool ApplicationLoggingMetricsEnabled { get; set; }

        [JsonProperty("application_logging.local_decorating.enabled")]
        public bool ApplicationLoggingLocalDecoratingEnabled { get; set; }
    }
}

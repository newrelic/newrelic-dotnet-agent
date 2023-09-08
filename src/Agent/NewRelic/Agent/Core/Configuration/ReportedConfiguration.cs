// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NewRelic.Agent.Configuration;
using Newtonsoft.Json;

namespace NewRelic.Agent.Core.Configuration
{
    /// <summary>
    /// The configuration that is reported to the collector using the agent_settings command, and in connect.settings.
    /// </summary>
    [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore)]
    public class ReportedConfiguration : IConfiguration
    {
        private readonly IConfiguration _configuration;
        public ReportedConfiguration(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [JsonProperty("agent.name")]
        public const string Agent = ".NET Agent";

        #region IConfiguration

        [JsonProperty("agent.run_id")]
        public object AgentRunId => _configuration.AgentRunId?.ToString();

        [JsonProperty("agent.enabled")]
        public bool AgentEnabled => _configuration.AgentEnabled;

        [JsonIgnore()]
        public string AgentLicenseKey => _configuration.AgentLicenseKey;

        [JsonProperty("agent.license_key.configured")]
        public bool AgentLicenseKeyConfigured => !string.IsNullOrWhiteSpace(AgentLicenseKey);

        [JsonProperty("agent.application_names")]
        public IEnumerable<string> ApplicationNames => _configuration.ApplicationNames;

        [JsonProperty("agent.application_names_source")]
        public string ApplicationNamesSource => _configuration.ApplicationNamesSource;

        [JsonProperty("agent.auto_start")]
        public bool AutoStartAgent => _configuration.AutoStartAgent;

        [JsonProperty("browser_monitoring.application_id")]
        public string BrowserMonitoringApplicationId => _configuration.BrowserMonitoringApplicationId;

        [JsonProperty("browser_monitoring.auto_instrument")]
        public bool BrowserMonitoringAutoInstrument => _configuration.BrowserMonitoringAutoInstrument;

        [JsonProperty("browser_monitoring.beacon_address")]
        public string BrowserMonitoringBeaconAddress => _configuration.BrowserMonitoringBeaconAddress;

        [JsonProperty("browser_monitoring.error_beacon_address")]
        public string BrowserMonitoringErrorBeaconAddress => _configuration.BrowserMonitoringErrorBeaconAddress;

        [JsonIgnore()]
        public string BrowserMonitoringJavaScriptAgent => _configuration.BrowserMonitoringJavaScriptAgent;

        [JsonProperty("browser_monitoring.javascript_agent.populated")]
        public bool BrowserMonitoringJavaScriptAgentPopulated => !string.IsNullOrWhiteSpace(BrowserMonitoringJavaScriptAgent);

        [JsonProperty("browser_monitoring.javascript_agent_file")]
        public string BrowserMonitoringJavaScriptAgentFile => _configuration.BrowserMonitoringJavaScriptAgentFile;

        [JsonProperty("browser_monitoring.loader")]
        public string BrowserMonitoringJavaScriptAgentLoaderType => _configuration.BrowserMonitoringJavaScriptAgentLoaderType;

        // Not an IConfiguration member, but this is here to replicate the behavior of the old 'connect' payload
        [JsonProperty("browser_monitoring.loader_debug")]
        public bool LoaderDebug => false;

        [JsonIgnore()]
        public string BrowserMonitoringKey => _configuration.BrowserMonitoringKey;

        [JsonProperty("browser_monitoring.monitoring_key.populated")]
        public bool BrowserMonitoringKeyPopulated => !string.IsNullOrWhiteSpace(BrowserMonitoringKey);

        [JsonProperty("browser_monitoring.use_ssl")]
        public bool BrowserMonitoringUseSsl => _configuration.BrowserMonitoringUseSsl;

        [JsonProperty("security.policies_token")]
        public string SecurityPoliciesToken => _configuration.SecurityPoliciesToken;

        [JsonProperty("security.policies_token_exists")]
        public bool SecurityPoliciesTokenExists => _configuration.SecurityPoliciesTokenExists;

        [JsonProperty("agent.allow_all_request_headers")]
        public bool AllowAllRequestHeaders => _configuration.AllowAllRequestHeaders;

        [JsonProperty("agent.attributes_enabled")]
        public bool CaptureAttributes => _configuration.CaptureAttributes;

        [JsonProperty("agent.can_use_attributes_includes")]
        public bool CanUseAttributesIncludes => _configuration.CanUseAttributesIncludes;

        [JsonProperty("agent.can_use_attributes_includes_source")]
        public string CanUseAttributesIncludesSource => _configuration.CanUseAttributesIncludesSource;

        [JsonProperty("agent.attributes_include")]
        public IEnumerable<string> CaptureAttributesIncludes => _configuration.CaptureAttributesIncludes;

        [JsonProperty("agent.attributes_exclude")]
        public IEnumerable<string> CaptureAttributesExcludes => _configuration.CaptureAttributesExcludes;

        [JsonProperty("agent.attributes_default_excludes")]
        public IEnumerable<string> CaptureAttributesDefaultExcludes => _configuration.CaptureAttributesDefaultExcludes;

        [JsonProperty("transaction_events.attributes_enabled")]
        public bool TransactionEventsAttributesEnabled => _configuration.TransactionEventsAttributesEnabled;

        [JsonProperty("transaction_events.attributes_include")]
        public HashSet<string> TransactionEventsAttributesInclude => _configuration.TransactionEventsAttributesInclude;

        [JsonProperty("transaction_events.attributes_exclude")]
        public HashSet<string> TransactionEventsAttributesExclude => _configuration.TransactionEventsAttributesExclude;

        [JsonProperty("transaction_trace.attributes_enabled")]
        public bool CaptureTransactionTraceAttributes => _configuration.CaptureTransactionTraceAttributes;

        [JsonProperty("transaction_trace.attributes_include")]
        public IEnumerable<string> CaptureTransactionTraceAttributesIncludes => _configuration.CaptureTransactionTraceAttributesIncludes;

        [JsonProperty("transaction_trace.attributes_exclude")]
        public IEnumerable<string> CaptureTransactionTraceAttributesExcludes => _configuration.CaptureTransactionTraceAttributesExcludes;

        [JsonProperty("error_collector.attributes_enabled")]
        public bool CaptureErrorCollectorAttributes => _configuration.CaptureErrorCollectorAttributes;

        [JsonProperty("error_collector.attributes_include")]
        public IEnumerable<string> CaptureErrorCollectorAttributesIncludes => _configuration.CaptureErrorCollectorAttributesIncludes;

        [JsonProperty("error_collector.attributes_exclude")]
        public IEnumerable<string> CaptureErrorCollectorAttributesExcludes => _configuration.CaptureErrorCollectorAttributesExcludes;

        [JsonProperty("browser_monitoring.attributes_enabled")]
        public bool CaptureBrowserMonitoringAttributes => _configuration.CaptureBrowserMonitoringAttributes;

        [JsonProperty("browser_monitoring.attributes_include")]
        public IEnumerable<string> CaptureBrowserMonitoringAttributesIncludes => _configuration.CaptureBrowserMonitoringAttributesIncludes;

        [JsonProperty("browser_monitoring.attributes_exclude")]
        public IEnumerable<string> CaptureBrowserMonitoringAttributesExcludes => _configuration.CaptureBrowserMonitoringAttributesExcludes;

        [JsonProperty("custom_parameters.enabled")]
        public bool CaptureCustomParameters => _configuration.CaptureCustomParameters;

        [JsonProperty("custom_parameters.source")]
        public string CaptureCustomParametersSource => _configuration.CaptureCustomParametersSource;

        [JsonProperty("collector.host")]
        public string CollectorHost => _configuration.CollectorHost;

        [JsonProperty("collector.port")]
        public int CollectorPort => _configuration.CollectorPort;

        [JsonProperty("collector.send_data_on_exit")]
        public bool CollectorSendDataOnExit => _configuration.CollectorSendDataOnExit;

        [JsonProperty("collector.send_data_on_exit_threshold")]
        public float CollectorSendDataOnExitThreshold => _configuration.CollectorSendDataOnExitThreshold;

        [JsonProperty("collector.send_environment_info")]
        public bool CollectorSendEnvironmentInfo => _configuration.CollectorSendEnvironmentInfo;

        [JsonProperty("collector.sync_startup")]
        public bool CollectorSyncStartup => _configuration.CollectorSyncStartup;

        [JsonProperty("collector.timeout")]
        public uint CollectorTimeout => _configuration.CollectorTimeout;

        [JsonProperty("collector.max_payload_size_in_bytes")]
        public int CollectorMaxPayloadSizeInBytes => _configuration.CollectorMaxPayloadSizeInBytes;

        [JsonProperty("agent.complete_transactions_on_thread")]
        public bool CompleteTransactionsOnThread => _configuration.CompleteTransactionsOnThread;

        [JsonProperty("agent.compressed_content_encoding")]
        public string CompressedContentEncoding => _configuration.CompressedContentEncoding;

        [JsonProperty("agent.configuration_version")]
        public long ConfigurationVersion => _configuration.ConfigurationVersion;

        [JsonProperty("cross_application_tracer.cross_process_id")]
        public string CrossApplicationTracingCrossProcessId => _configuration.CrossApplicationTracingCrossProcessId;

        [JsonProperty("cross_application_tracer.enabled")]
        public bool CrossApplicationTracingEnabled => _configuration.CrossApplicationTracingEnabled;

        [JsonProperty("distributed_tracing.enabled")]
        public bool DistributedTracingEnabled => _configuration.DistributedTracingEnabled;

        [JsonProperty("span_events.enabled")]
        public bool SpanEventsEnabled => _configuration.SpanEventsEnabled;

        [JsonProperty("span_events.harvest_cycle")]
        public TimeSpan SpanEventsHarvestCycle => _configuration.SpanEventsHarvestCycle;

        [JsonProperty("span_events.attributes_enabled")]
        public bool SpanEventsAttributesEnabled => _configuration.SpanEventsAttributesEnabled;

        [JsonProperty("span_events.attributes_include")]
        public HashSet<string> SpanEventsAttributesInclude => _configuration.SpanEventsAttributesInclude;

        [JsonProperty("span_events.attributes_exclude")]
        public HashSet<string> SpanEventsAttributesExclude => _configuration.SpanEventsAttributesExclude;

        [JsonProperty("infinite_tracing.trace_count_consumers")]
        public int InfiniteTracingTraceCountConsumers => _configuration.InfiniteTracingTraceCountConsumers;

        [JsonProperty("infinite_tracing.trace_observer_host")]
        public string InfiniteTracingTraceObserverHost => _configuration.InfiniteTracingTraceObserverHost;

        [JsonProperty("infinite_tracing.trace_observer_port")]
        public string InfiniteTracingTraceObserverPort => _configuration.InfiniteTracingTraceObserverPort;

        [JsonProperty("infinite_tracing.trace_observer_ssl")]
        public string InfiniteTracingTraceObserverSsl => _configuration.InfiniteTracingTraceObserverSsl;

        [JsonProperty("infinite_tracing.dev.test_flaky")]
        public float? InfiniteTracingTraceObserverTestFlaky => _configuration.InfiniteTracingTraceObserverTestFlaky;

        [JsonProperty("infinite_tracing.dev.test_flaky_code")]
        public int? InfiniteTracingTraceObserverTestFlakyCode => _configuration.InfiniteTracingTraceObserverTestFlakyCode;

        [JsonProperty("infinite_tracing.dev.test_delay_ms")]
        public int? InfiniteTracingTraceObserverTestDelayMs => _configuration.InfiniteTracingTraceObserverTestDelayMs;

        [JsonProperty("infinite_tracing.spans_queue_size")]
        public int InfiniteTracingQueueSizeSpans => _configuration.InfiniteTracingQueueSizeSpans;

        [JsonProperty("infinite_tracing.spans_partition_count")]
        public int InfiniteTracingPartitionCountSpans => _configuration.InfiniteTracingPartitionCountSpans;

        [JsonProperty("infinite_tracing.spans_batch_size")]
        public int InfiniteTracingBatchSizeSpans => _configuration.InfiniteTracingBatchSizeSpans;

        [JsonProperty("infinite_tracing.connect_timeout_ms")]
        public int InfiniteTracingTraceTimeoutMsConnect => _configuration.InfiniteTracingTraceTimeoutMsConnect;

        [JsonProperty("infinite_tracing.send_data_timeout_ms")]
        public int InfiniteTracingTraceTimeoutMsSendData => _configuration.InfiniteTracingTraceTimeoutMsSendData;

        [JsonProperty("infinite_tracing.exit_timeout_ms")]
        public int InfiniteTracingExitTimeoutMs => _configuration.InfiniteTracingExitTimeoutMs;

        [JsonProperty("infinite_tracing.compression")]
        public bool InfiniteTracingCompression => _configuration.InfiniteTracingCompression;

        [JsonProperty("agent.primary_application_id")]
        public string PrimaryApplicationId => _configuration.PrimaryApplicationId;

        [JsonProperty("agent.trusted_account_key")]
        public string TrustedAccountKey => _configuration.TrustedAccountKey;

        [JsonProperty("agent.account_id")]
        public string AccountId => _configuration.AccountId;

        [JsonProperty("datastore_tracer.name_reporting_enabled")]
        public bool DatabaseNameReportingEnabled => _configuration.DatabaseNameReportingEnabled;

        [JsonProperty("datastore_tracer.query_parameters_enabled")]
        public bool DatastoreTracerQueryParametersEnabled => _configuration.DatastoreTracerQueryParametersEnabled;

        [JsonProperty("error_collector.enabled")]
        public bool ErrorCollectorEnabled => _configuration.ErrorCollectorEnabled;

        [JsonProperty("error_collector.capture_events_enabled")]
        public bool ErrorCollectorCaptureEvents => _configuration.ErrorCollectorCaptureEvents;

        [JsonProperty("error_collector.max_samples_stored")]
        public int ErrorCollectorMaxEventSamplesStored => _configuration.ErrorCollectorMaxEventSamplesStored;

        [JsonProperty("error_collector.harvest_cycle")]
        public TimeSpan ErrorEventsHarvestCycle => _configuration.ErrorEventsHarvestCycle;

        [JsonProperty("error_collector.max_per_period")]
        public uint ErrorsMaximumPerPeriod => _configuration.ErrorsMaximumPerPeriod;

        [JsonProperty("error_collector.expected_classes")]
        public IEnumerable<string> ExpectedErrorClassesForAgentSettings => _configuration.ExpectedErrorClassesForAgentSettings;

        [JsonProperty("error_collector.expected_messages")]
        public IDictionary<string, IEnumerable<string>> ExpectedErrorMessagesForAgentSettings => _configuration.ExpectedErrorMessagesForAgentSettings;

        // The following IConfiguration property `ExpectedErrorStatusCodesForAgentSettings` actually reports the same information in a more friendly way
        [JsonIgnore()]
        public IEnumerable<MatchRule> ExpectedStatusCodes => _configuration.ExpectedStatusCodes;

        [JsonProperty("error_collector.expected_status_codes")]
        public IEnumerable<string> ExpectedErrorStatusCodesForAgentSettings => _configuration.ExpectedErrorStatusCodesForAgentSettings;

        [JsonProperty("error_collector.expected_errors_config")]
        public IDictionary<string, IEnumerable<string>> ExpectedErrorsConfiguration => _configuration.ExpectedErrorsConfiguration;

        [JsonProperty("error_collector.ignore_errors_config")]
        public IDictionary<string, IEnumerable<string>> IgnoreErrorsConfiguration => _configuration.IgnoreErrorsConfiguration;

        [JsonProperty("error_collector.ignore_classes")]
        public IEnumerable<string> IgnoreErrorClassesForAgentSettings => _configuration.IgnoreErrorClassesForAgentSettings;

        [JsonProperty("error_collector.ignore_messages")]
        public IDictionary<string, IEnumerable<string>> IgnoreErrorMessagesForAgentSettings => _configuration.IgnoreErrorMessagesForAgentSettings;

        // Serializing this Func doesn't provide us with more information than the supportability metrics
        [JsonIgnore()]
        public Func<IReadOnlyDictionary<string, object>, string> ErrorGroupCallback => _configuration.ErrorGroupCallback;

        [JsonProperty("agent.request_headers_map")]
        public Dictionary<string, string> RequestHeadersMap => _configuration.RequestHeadersMap;
                
        [JsonProperty("cross_application_tracer.encoding_key")]
        public string EncodingKey => _configuration.EncodingKey;

        [JsonProperty("agent.entity_guid")]
        public string EntityGuid => _configuration.EntityGuid;

        [JsonProperty("agent.high_security_mode_enabled")]
        public bool HighSecurityModeEnabled => _configuration.HighSecurityModeEnabled;

        [JsonProperty("agent.custom_instrumentation_editor_enabled")]
        public bool CustomInstrumentationEditorEnabled => _configuration.CustomInstrumentationEditorEnabled;

        [JsonProperty("agent.custom_instrumentation_editor_enabled_source")]
        public string CustomInstrumentationEditorEnabledSource => _configuration.CustomInstrumentationEditorEnabledSource;

        [JsonProperty("agent.strip_exception_messages")]
        public bool StripExceptionMessages => _configuration.StripExceptionMessages;

        [JsonProperty("agent.strip_exception_messages_source")]
        public string StripExceptionMessagesSource => _configuration.StripExceptionMessagesSource;

        [JsonProperty("agent.instance_reporting_enabled")]
        public bool InstanceReportingEnabled => _configuration.InstanceReportingEnabled;

        [JsonProperty("agent.instrumentation_logging_enabled")]
        public bool InstrumentationLoggingEnabled => _configuration.InstrumentationLoggingEnabled;

        [JsonProperty("agent.labels")]
        public string Labels => _configuration.Labels;

        [JsonProperty("agent.metric_name_regex_rules")]
        public IEnumerable<RegexRule> MetricNameRegexRules => _configuration.MetricNameRegexRules;

        [JsonProperty("agent.new_relic_config_file_path")]
        public string NewRelicConfigFilePath => _configuration.NewRelicConfigFilePath;

        [JsonProperty("agent.app_settings_config_file_path")]
        public string AppSettingsConfigFilePath => _configuration.AppSettingsConfigFilePath;

        [JsonIgnore()]
        public string ProxyHost => _configuration.ProxyHost;

        [JsonProperty("proxy.host.configured")]
        public bool ProxyHostConfigured => !string.IsNullOrWhiteSpace(ProxyHost);

        [JsonIgnore()]
        public string ProxyUriPath => _configuration.ProxyUriPath;

        [JsonProperty("proxy.uri_path.configured")]
        public bool ProxyUriPathConfigured => !string.IsNullOrWhiteSpace(ProxyUriPath);

        [JsonIgnore()]
        public int ProxyPort => _configuration.ProxyPort;

        [JsonProperty("proxy.port.configured")]
        public bool ProxyPortConfigured => true; // as this is an integer with a default value, it will always be 'configured'

        [JsonIgnore()]
        public string ProxyUsername => _configuration.ProxyUsername;

        [JsonProperty("proxy.username.configured")]
        public bool ProxyUsernameConfigured => !string.IsNullOrWhiteSpace(ProxyUsername);

        [JsonIgnore()]
        public string ProxyPassword => _configuration.ProxyPassword;

        [JsonProperty("proxy.password.configured")]
        public bool ProxyPasswordConfigured => !string.IsNullOrWhiteSpace(ProxyPassword);

        [JsonIgnore()]
        public string ProxyDomain => _configuration.ProxyDomain;

        [JsonProperty("proxy.domain.configured")]
        public bool ProxyDomainConfigured => !string.IsNullOrWhiteSpace(ProxyDomain);

        [JsonProperty("agent.put_for_data_sent")]
        public bool PutForDataSend => _configuration.PutForDataSend;

        [JsonProperty("slow_sql.enabled")]
        public bool SlowSqlEnabled => _configuration.SlowSqlEnabled;

        [JsonProperty("transaction_tracer.explain_threshold")]
        public TimeSpan SqlExplainPlanThreshold => _configuration.SqlExplainPlanThreshold;

        [JsonProperty("transaction_tracer.explain_enabled")]
        public bool SqlExplainPlansEnabled => _configuration.SqlExplainPlansEnabled;

        [JsonProperty("transaction_tracer.max_explain_plans")]
        public int SqlExplainPlansMax => _configuration.SqlExplainPlansMax;

        [JsonProperty("transaction_tracer.max_sql_statements")]
        public uint SqlStatementsPerTransaction => _configuration.SqlStatementsPerTransaction;

        [JsonProperty("transaction_tracer.sql_traces_per_period")]
        public int SqlTracesPerPeriod => _configuration.SqlTracesPerPeriod;

        [JsonProperty("transaction_tracer.max_stack_trace_lines")]
        public int StackTraceMaximumFrames => _configuration.StackTraceMaximumFrames;

        [JsonProperty("error_collector.ignore_status_codes")]
        public IEnumerable<string> HttpStatusCodesToIgnore => _configuration.HttpStatusCodesToIgnore;

        [JsonProperty("agent.thread_profiling_methods_to_ignore")]
        public IEnumerable<string> ThreadProfilingIgnoreMethods => _configuration.ThreadProfilingIgnoreMethods;

        [JsonProperty("custom_events.enabled")]
        public bool CustomEventsEnabled => _configuration.CustomEventsEnabled;

        [JsonProperty("custom_events.enabled_source")]
        public string CustomEventsEnabledSource => _configuration.CustomEventsEnabledSource;

        [JsonProperty("custom_events.attributes_enabled")]
        public bool CustomEventsAttributesEnabled => _configuration.CustomEventsAttributesEnabled;

        [JsonProperty("custom_events.attributes_include")]
        public HashSet<string> CustomEventsAttributesInclude => _configuration.CustomEventsAttributesInclude;

        [JsonProperty("custom_events.attributes_exclude")]
        public HashSet<string> CustomEventsAttributesExclude => _configuration.CustomEventsAttributesExclude;

        [JsonProperty("custom_events.max_samples_stored")]
        public int CustomEventsMaximumSamplesStored => _configuration.CustomEventsMaximumSamplesStored;

        [JsonProperty("custom_events.harvest_cycle")]
        public TimeSpan CustomEventsHarvestCycle => _configuration.CustomEventsHarvestCycle;

        [JsonProperty("agent.disable_samplers")]
        public bool DisableSamplers => _configuration.DisableSamplers;

        [JsonProperty("thread_profiler.enabled")]
        public bool ThreadProfilingEnabled => _configuration.ThreadProfilingEnabled;

        [JsonProperty("transaction_events.enabled")]
        public bool TransactionEventsEnabled => _configuration.TransactionEventsEnabled;

        [JsonProperty("transaction_events.max_samples_stored")]
        public int TransactionEventsMaximumSamplesStored => _configuration.TransactionEventsMaximumSamplesStored;

        [JsonProperty("transaction_events.harvest_cycle")]
        public TimeSpan TransactionEventsHarvestCycle => _configuration.TransactionEventsHarvestCycle;

        [JsonProperty("transaction_events.transactions_enabled")]
        public bool TransactionEventsTransactionsEnabled => _configuration.TransactionEventsTransactionsEnabled;

        [JsonProperty("transaction_name.regex_rules")]
        public IEnumerable<RegexRule> TransactionNameRegexRules => _configuration.TransactionNameRegexRules;

        [JsonProperty("transaction_name.whitelist_rules")]
        public IDictionary<string, IEnumerable<string>> TransactionNameWhitelistRules => _configuration.TransactionNameWhitelistRules;

        [JsonProperty("transaction_tracer.apdex_f")]
        public TimeSpan TransactionTraceApdexF => _configuration.TransactionTraceApdexF;

        [JsonProperty("transaction_tracer.apdex_t")]
        public TimeSpan TransactionTraceApdexT => _configuration.TransactionTraceApdexT;

        [JsonProperty("transaction_tracer.transaction_threshold")]
        public TimeSpan TransactionTraceThreshold => _configuration.TransactionTraceThreshold;

        [JsonProperty("transaction_tracer.enabled")]
        public bool TransactionTracerEnabled => _configuration.TransactionTracerEnabled;

        [JsonProperty("transaction_tracer.max_segments")]
        public int TransactionTracerMaxSegments => _configuration.TransactionTracerMaxSegments;

        [JsonProperty("transaction_tracer.record_sql")]
        public string TransactionTracerRecordSql => _configuration.TransactionTracerRecordSql;

        [JsonProperty("transaction_tracer.record_sql_source")]
        public string TransactionTracerRecordSqlSource => _configuration.TransactionTracerRecordSqlSource;

        [JsonProperty("transaction_tracer.max_stack_traces")]
        public int TransactionTracerMaxStackTraces => _configuration.TransactionTracerMaxStackTraces;

        [JsonProperty("agent.trusted_account_ids")]
        public IEnumerable<long> TrustedAccountIds => _configuration.TrustedAccountIds;

        [JsonProperty("agent.server_side_config_enabled")]
        public bool ServerSideConfigurationEnabled => _configuration.ServerSideConfigurationEnabled;

        [JsonProperty("agent.ignore_server_side_config")]
        public bool IgnoreServerSideConfiguration => _configuration.IgnoreServerSideConfiguration;

        [JsonProperty("agent.url_regex_rules")]
        public IEnumerable<RegexRule> UrlRegexRules => _configuration.UrlRegexRules;

        [JsonProperty("agent.request_path_exclusion_list")]
        public IEnumerable<Regex> RequestPathExclusionList => _configuration.RequestPathExclusionList;

        [JsonProperty("agent.web_transactions_apdex")]
        public IDictionary<string, double> WebTransactionsApdex => _configuration.WebTransactionsApdex;

        [JsonProperty("agent.wrapper_exception_limit")]
        public int WrapperExceptionLimit => _configuration.WrapperExceptionLimit;

        [JsonProperty("utilization.detect_aws_enabled")]
        public bool UtilizationDetectAws => _configuration.UtilizationDetectAws;

        [JsonProperty("utilization.detect_azure_enabled")]
        public bool UtilizationDetectAzure => _configuration.UtilizationDetectAzure;

        [JsonProperty("utilization.detect_gcp_enabled")]
        public bool UtilizationDetectGcp => _configuration.UtilizationDetectGcp;

        [JsonProperty("utilization.detect_pcf_enabled")]
        public bool UtilizationDetectPcf => _configuration.UtilizationDetectPcf;

        [JsonProperty("utilization.detect_docker_enabled")]
        public bool UtilizationDetectDocker => _configuration.UtilizationDetectDocker;

        [JsonProperty("utilization.detect_kubernetes_enabled")]
        public bool UtilizationDetectKubernetes => _configuration.UtilizationDetectKubernetes;

        [JsonProperty("utilization.logical_processors")]
        public int? UtilizationLogicalProcessors => _configuration.UtilizationLogicalProcessors;

        [JsonProperty("utilization.total_ram_mib")]
        public int? UtilizationTotalRamMib => _configuration.UtilizationTotalRamMib;

        [JsonProperty("utilization.billing_host")]
        public string UtilizationBillingHost => _configuration.UtilizationBillingHost;

        [JsonProperty("utilization.hostname")]
        public string UtilizationHostName => _configuration.UtilizationHostName;

        [JsonProperty("utilization.full_hostname")]
        public string UtilizationFullHostName => _configuration.UtilizationFullHostName;

        [JsonProperty("diagnostics.capture_agent_timing_enabled")]
        public bool DiagnosticsCaptureAgentTiming => _configuration.DiagnosticsCaptureAgentTiming;

        [JsonProperty("diagnostics.capture_agent_timing_frequency")]
        public int DiagnosticsCaptureAgentTimingFrequency => _configuration.DiagnosticsCaptureAgentTimingFrequency;

        [JsonProperty("agent.use_resource_based_naming_for_wcf_enabled")]
        public bool UseResourceBasedNamingForWCFEnabled => _configuration.UseResourceBasedNamingForWCFEnabled;

        [JsonProperty("agent.event_listener_samplers_enabled")]
        public bool EventListenerSamplersEnabled { get => _configuration.EventListenerSamplersEnabled; set { /* nothx */ } }

        [JsonProperty("agent.sampling_target")]
        public int? SamplingTarget => _configuration.SamplingTarget;

        [JsonProperty("span_events.max_samples_stored")]
        public int SpanEventsMaxSamplesStored => _configuration.SpanEventsMaxSamplesStored;

        [JsonProperty("agent.sampling_target_period_in_seconds")]
        public int? SamplingTargetPeriodInSeconds => _configuration.SamplingTargetPeriodInSeconds;

        [JsonProperty("agent.payload_success_metrics_enabled")]
        public bool PayloadSuccessMetricsEnabled => _configuration.PayloadSuccessMetricsEnabled;

        [JsonProperty("agent.process_host_display_name")]
        public string ProcessHostDisplayName => _configuration.ProcessHostDisplayName;

        [JsonProperty("transaction_tracer.database_statement_cache_capacity")]
        public int DatabaseStatementCacheCapacity => _configuration.DatabaseStatementCacheCapacity;

        [JsonProperty("agent.force_synchronous_timing_calculation_for_http_client")]
        public bool ForceSynchronousTimingCalculationHttpClient => _configuration.ForceSynchronousTimingCalculationHttpClient;

        [JsonProperty("agent.exclude_new_relic_header")]
        public bool ExcludeNewrelicHeader => _configuration.ExcludeNewrelicHeader;

        [JsonProperty("application_logging.enabled")]
        public bool ApplicationLoggingEnabled => _configuration.ApplicationLoggingEnabled;

        [JsonProperty("application_logging.metrics.enabled")]
        public bool LogMetricsCollectorEnabled => _configuration.LogMetricsCollectorEnabled;

        [JsonProperty("application_logging.forwarding.enabled")]
        public bool LogEventCollectorEnabled => _configuration.LogEventCollectorEnabled;

        [JsonProperty("application_logging.forwarding.max_samples_stored")]
        public int LogEventsMaxSamplesStored => _configuration.LogEventsMaxSamplesStored;

        [JsonProperty("application_logging.forwarding.log_level_denylist")]
        public HashSet<string> LogLevelDenyList => _configuration.LogLevelDenyList;

        [JsonProperty("application_logging.harvest_cycle")]
        public TimeSpan LogEventsHarvestCycle => _configuration.LogEventsHarvestCycle;

        [JsonProperty("application_logging.local_decorating.enabled")]
        public bool LogDecoratorEnabled => _configuration.LogDecoratorEnabled;

        [JsonProperty("agent.app_domain_caching_disabled")]
        public bool AppDomainCachingDisabled => _configuration.AppDomainCachingDisabled;

        [JsonProperty("agent.force_new_transaction_on_new_thread_enabled")]
        public bool ForceNewTransactionOnNewThread => _configuration.ForceNewTransactionOnNewThread;

        [JsonProperty("agent.code_level_metrics_enabled")]
        public bool CodeLevelMetricsEnabled => _configuration.CodeLevelMetricsEnabled;

        [JsonProperty("agent.app_settings")]
        public IReadOnlyDictionary<string,string> AppSettings => GetAppSettings();

        [JsonProperty("application_logging.forwarding.context_data.enabled")]
        public bool ContextDataEnabled => _configuration.ContextDataEnabled;

        [JsonProperty("application_logging.forwarding.context_data.include")]
        public IEnumerable<string> ContextDataInclude => _configuration.ContextDataInclude;

        [JsonProperty("application_logging.forwarding.context_data.exclude")]
        public IEnumerable<string> ContextDataExclude => _configuration.ContextDataExclude;

        [JsonProperty("metrics.harvest_cycle")]
        public TimeSpan MetricsHarvestCycle => _configuration.MetricsHarvestCycle;

        [JsonProperty("transaction_traces.harvest_cycle")]
        public TimeSpan TransactionTracesHarvestCycle => _configuration.TransactionTracesHarvestCycle;

        [JsonProperty("error_traces.harvest_cycle")]
        public TimeSpan ErrorTracesHarvestCycle => _configuration.ErrorTracesHarvestCycle;

        [JsonProperty("get_agent_commands.cycle")]
        public TimeSpan GetAgentCommandsCycle => _configuration.GetAgentCommandsCycle;

        [JsonProperty("default.harvest_cycle")]
        public TimeSpan DefaultHarvestCycle => _configuration.DefaultHarvestCycle;

        [JsonProperty("sql_traces.harvest_cycle")]
        public TimeSpan SqlTracesHarvestCycle => _configuration.SqlTracesHarvestCycle;

        [JsonProperty("update_loaded_modules.cycle")]
        public TimeSpan UpdateLoadedModulesCycle => _configuration.UpdateLoadedModulesCycle;

        [JsonProperty("stackexchangeredis_cleanup.cycle")]
        public TimeSpan StackExchangeRedisCleanupCycle => _configuration.StackExchangeRedisCleanupCycle;

        public IReadOnlyDictionary<string, string> GetAppSettings()
        {
            return _configuration.GetAppSettings();
        }

        #endregion
    }
}

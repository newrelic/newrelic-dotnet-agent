using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.Serialization;
using JetBrains.Annotations;
using NewRelic.Agent.Core.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace NewRelic.Agent.Core.Configuration
{
	/// <summary>
	/// from collector connectApplicationAgent(RequestContext) return value (https://github.com/newrelic/collector/blob/master/src/main/java/com/nr/collector/methods/Connect.java#L259)
	/// </summary>
	public class ServerConfiguration
	{
		[JsonProperty(PropertyName = "agent_run_id", Required = Required.Always)]
		public Object AgentRunId { get; set; }

		[JsonProperty("apdex_t")]
		public Double? ApdexT { get; set; }

		[JsonProperty("collect_analytics_events")]
		public Boolean? AnalyticsEventCollectionEnabled { get; set; }

		[JsonProperty("collect_custom_events")]
		public Boolean? CustomEventCollectionEnabled { get; set; }

		[JsonProperty("collect_errors")]
		public Boolean? ErrorCollectionEnabled { get; set; }

		[JsonProperty("collect_traces")]
		public Boolean? TraceCollectionEnabled { get; set; }

		[JsonProperty("data_report_period")]
		public Int64? DataReportPeriod { get; set; }

		[JsonProperty("encoding_key")]
		public String EncodingKey { get; set; }

		[JsonProperty("high_security")]
		public Boolean? HighSecurityEnabled { get; set; }

		[JsonProperty("instrumentation")]
		public IEnumerable<InstrumentationConfig> Instrumentation { get; set; }

		[JsonProperty("messages")]
		public IEnumerable<Message> Messages { get; set; }

		[JsonProperty("product_level")]
		public Int64? ProductLevel { get; set; }

		[JsonProperty("sampling_rate")]
		public Int32? SamplingRate { get; set; }

		[JsonProperty("web_transactions_apdex")]
		public IDictionary<String, Double> WebTransactionsApdex { get; set; }

		[JsonProperty("trusted_account_ids")]
		public IEnumerable<Int64> TrustedIds { get; set; }


		// Server Side Config

		public Boolean UsingServerSideConfig { get; private set; }

		[JsonProperty("agent_config")]
		[NotNull] public AgentConfig RpmConfig { get; set; }


		// CAT

		[JsonProperty("cross_process_id")]
		public String CatId { get; set; }


		// RUM

		[JsonProperty("application_id")]
		public String RumSettingsApplicationId { get; set; }

		[JsonProperty("beacon")]
		public String RumSettingsBeacon { get; set; }

		[JsonProperty("browser_key")]
		public String RumSettingsBrowserKey { get; set; }

		[JsonProperty("browser_monitoring.loader")]
		public String RumSettingsBrowserMonitoringLoader { get; set; }

		[JsonProperty("browser_monitoring.loader_debug")]
		public String RumSettingsBrowserMonitoringLoaderDebug { get; set; }

		[JsonProperty("browser_monitoring.loader_version")]
		public String RumSettingsBrowserMonitoringLoaderVersion { get; set; }

		[JsonProperty("episodes_url")]
		public String RumSettingsEpisodesUrl { get; set; }

		[JsonProperty("episodes_file")]
		public String RumSettingsEpisodesFile { get; set; }

		[JsonProperty("error_beacon")]
		public String RumSettingsErrorBeacon { get; set; }

		[JsonProperty("js_agent_file")]
		public String RumSettingsJavaScriptAgentFile { get; set; }

		[JsonProperty("js_agent_loader")]
		public String RumSettingsJavaScriptAgentLoader { get; set; }

		[JsonProperty("js_agent_loader_version")]
		public String RumSettingsJavaScriptAgentLoaderVersion { get; set; }

		[JsonProperty("js_errors_beta")]
		public String RumSettingsJavaScriptErrorsBeta { get; set; }


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

		[NotNull]
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
			public Boolean? CrossApplicationTracerEnabled { get; set; }

			[JsonProperty("error_collector.enabled")]
			public Boolean? ErrorCollectorEnabled { get; set; }

			[JsonProperty("error_collector.ignore_status_codes")]
			public IEnumerable<String> ErrorCollectorStatusCodesToIgnore { get; set; }

			[JsonProperty("error_collector.ignore_errors")]
			public IEnumerable<String> ErrorCollectorErrorsToIgnore { get; set; }

			[JsonProperty("error_collector.capture_events")]
			public Boolean? ErrorCollectorCaptureEvents { get; set; }

			[JsonProperty("error_collector.max_event_samples_stored")]
			public UInt32? ErrorCollectorMaxEventSamplesStored { get; set; }

			[JsonProperty("instrumentation.level")]
			public Int32? InstrumentationLevel { get; set; }

			[JsonProperty("ignored_params")]
			public IEnumerable<String> ParametersToIgnore { get; set; }

			[JsonProperty("slow_sql.enabled")]
			public Boolean? SlowSqlEnabled { get; set; }

			[JsonProperty("transaction_tracer.enabled")]
			public Boolean? TransactionTracerEnabled { get; set; }

			[JsonProperty("transaction_tracer.explain_enabled")]
			public Boolean? TransactionTracerExplainEnabled { get; set; }

			[JsonProperty("transaction_tracer.explain_threshold")]
			public Double? TransactionTracerExplainThreshold { get; set; }

			[JsonProperty("transaction_tracer.record_sql")]
			public String TransactionTracerRecordSql { get; set; }

			[JsonProperty("transaction_tracer.stack_trace_threshold")]
			public Double? TransactionTracerStackThreshold { get; set; }

			[JsonProperty("transaction_tracer.transaction_threshold")]
			public Object TransactionTracerThreshold { get; set; }

			[JsonProperty("capture_params")]
			public Boolean? CaptureParametersEnabled { get; set; }

			[JsonProperty("collect_error_events")]
			public Boolean? CollectErrorEvents { get; set; }
		}

		public class InstrumentationConfig
		{
			[JsonProperty("config")]
			public String Config { get; set; }
		}

		public class Message
		{
			[JsonProperty("message")]
			public String Text { get; set; }
			[JsonProperty("level")]
			public String Level { get; set; }
		}

		public class RegexRule
		{
			[JsonProperty("match_expression")]
			public String MatchExpression { get; set; }

			[JsonProperty("replacement")]
			public String Replacement { get; set; }

			[JsonProperty("ignore")]
			public Boolean? Ignore { get; set; }

			[JsonProperty("eval_order")]
			public Int64? EvaluationOrder { get; set; }

			[JsonProperty("terminate_chain")]
			public Boolean? TerminateChain { get; set; }

			[JsonProperty("each_segment")]
			public Boolean? EachSegment { get; set; }

			[JsonProperty("replace_all")]
			public Boolean? ReplaceAll { get; set; }
		}

		public class WhitelistRule
		{
			[JsonProperty("prefix")]
			public String Prefix { get; set; }
			[JsonProperty("terms")]
			public IEnumerable<String> Terms { get; set; }
		}

		[NotNull]
		public static ServerConfiguration FromJson([NotNull] String json)
		{
			var serverConfiguration = JsonConvert.DeserializeObject<ServerConfiguration>(json);
			Debug.Assert(serverConfiguration != null);

			serverConfiguration.UsingServerSideConfig = JsonContainsNonNullProperty(json, "agent_config");

			return serverConfiguration;
		}

		public static Boolean JsonContainsNonNullProperty([NotNull] String json, [NotNull] String propertyName)
		{
			var dictionary = JsonConvert.DeserializeObject<IDictionary<String, Object>>(json);
			Debug.Assert(dictionary != null);

			return dictionary.ContainsKey(propertyName)
				&& dictionary[propertyName] != null;
		}

		[NotNull]
		public static ServerConfiguration FromDeserializedReturnValue(Object deserializedJson)
		{
			var json = JsonConvert.SerializeObject(deserializedJson);
			return FromJson(json);
		}

		[OnError]
		public void OnError(StreamingContext context, ErrorContext errorContext)
		{
			Log.ErrorFormat("Json serializer context path: {0}. Error message: {1}", errorContext.Path, errorContext.Error.Message);
		}
	}
}

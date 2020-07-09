using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace NewRelic.Agent.Core.Configuration
{
	/// <summary>
	/// The configuration that is reported to the collector using the agent_settings command.
	/// </summary>
	public class ReportedConfiguration
	{
		[JsonProperty("apdex_t")]
		public Double? ApdexT { get; set; }

		[JsonProperty("cross_process_id")]
		public String CatId { get; set; }

		[JsonProperty("encoding_key")]
		public String EncodingKey { get; set; }

		[JsonProperty("trusted_account_ids")]
		public IEnumerable<Int64> TrustedAccountIds { get; set; }

		[JsonProperty("max_stack_trace_lines")]
		public Int32 MaxStackTraceLines { get; set; }

		[JsonProperty("using_server_side_config")]
		public Boolean UsingServerSideConfig { get; set; }

		[JsonProperty("thread_profiler.enabled")]
		public Boolean ThreadProfilerEnabled { get; set; }

		[JsonProperty("cross_application_tracer.enabled")]
		public Boolean CrossApplicationTracerEnabled { get; set; }

		[JsonProperty("error_collector.enabled")]
		public Boolean ErrorCollectorEnabled { get; set; }

		[JsonProperty("error_collector.ignore_status_codes")]
		public IEnumerable<String> ErrorCollectorIgnoreStatusCodes { get; set; }

		[JsonProperty("error_collector.ignore_errors")]
		public IEnumerable<String> ErrorCollectorIgnoreErrors { get; set; }

		[JsonProperty("transaction_tracer.stack_trace_threshold")]
		public Double TransactionTracerStackThreshold { get; set; }

		[JsonProperty("transaction_tracer.explain_enabled")]
		public Boolean TransactionTracerExplainEnabled { get; set; }

		[JsonProperty("transaction_tracer.max_sql_statements")]
		public UInt32 MaxSqlStatements { get; set; }

		[JsonProperty("transaction_tracer.max_explain_plans")]
		public Int32 MaxExplainPlans { get; set; }

		[JsonProperty("transaction_tracer.explain_threshold")]
		public Double TransactionTracerExplainThreshold { get; set; }

		[JsonProperty("transaction_tracer.transaction_threshold")]
		public Double TransactionTracerThreshold { get; set; }

		[JsonProperty("transaction_tracer.record_sql")]
		public String TransactionTracerRecordSql { get; set; }

		[JsonProperty("slow_sql.enabled")]
		public Boolean SlowSqlEnabled { get; set; }

		[JsonProperty("browser_monitoring.auto_instrument")]
		public Boolean BrowserMonitoringAutoInstrument { get; set; }
	}
}

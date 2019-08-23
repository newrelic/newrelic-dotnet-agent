using NewRelic.Agent.Configuration;
using Newtonsoft.Json;
using NUnit.Framework;
using System.Linq;
using Telerik.JustMock;
using System.Collections.Generic;
using System;

namespace NewRelic.Agent.Core.Configuration
{
	[TestFixture]
	public class AgentSettingsTests
	{
		TimeSpan ApdexT = new TimeSpan(0, 0, 10);
		const string CatId = "273070#123456";
		const string EncodingKey = "thisistheencodingkey";
		List<long> TrustedAccountIds = new List<long> { 123456, 098765 };
		const int MaxStackTraceLines = 100;
		const bool UsingServerSideConfig = false;
		const bool ThreadProfilerEnabled = false;
		const bool CrossApplicationTracerEnabled = false;
		const bool ErrorCollectorEnabled = true;
		List<string> ErrorCollectorIgnoreStatusCodes = new List<string> { "401", "404" };
		List<string> ErrorCollectorIgnoreErrors = new List<string>();
		TimeSpan TransactionTracerStackThreshold = new TimeSpan(0,0,11);
		const bool TransactionTracerExplainEnabled = false;
		TimeSpan TransactionTracerExplainThreshold = new TimeSpan(0,0,12);
		const uint MaxSqlStatements = 100;
		const int MaxExplainPlans = 10;
		TimeSpan TransactionTracerThreshold = new TimeSpan(0,0,13);
		const string TransactionTracerRecordSql = "obfuscate";
		const bool SlowSqlEnabled = false;
		const bool BrowserMonitoringAutoInstrument = true;
		const uint TransactionEventMaxSamplesStored = 10000;

		[Test]
		public void serializes_correctly()
		{
			var configuration = Mock.Create<IConfiguration>();
			Mock.Arrange(() => configuration.TransactionTraceApdexT).Returns(ApdexT);
			Mock.Arrange(() => configuration.CrossApplicationTracingCrossProcessId).Returns(CatId);
			Mock.Arrange(() => configuration.EncodingKey).Returns(EncodingKey);
			Mock.Arrange(() => configuration.TrustedAccountIds).Returns(TrustedAccountIds);
			Mock.Arrange(() => configuration.StackTraceMaximumFrames).Returns(MaxStackTraceLines);
			Mock.Arrange(() => configuration.UsingServerSideConfig).Returns(UsingServerSideConfig);
			Mock.Arrange(() => configuration.ThreadProfilingEnabled).Returns(ThreadProfilerEnabled);
			Mock.Arrange(() => configuration.CrossApplicationTracingEnabled).Returns(CrossApplicationTracerEnabled);
			Mock.Arrange(() => configuration.ErrorCollectorEnabled).Returns(ErrorCollectorEnabled);
			Mock.Arrange(() => configuration.HttpStatusCodesToIgnore).Returns(ErrorCollectorIgnoreStatusCodes);
			Mock.Arrange(() => configuration.ExceptionsToIgnore).Returns(ErrorCollectorIgnoreStatusCodes);
			Mock.Arrange(() => configuration.TransactionTracerStackThreshold).Returns(TransactionTracerStackThreshold);
			Mock.Arrange(() => configuration.SqlExplainPlansEnabled).Returns(TransactionTracerExplainEnabled);
			Mock.Arrange(() => configuration.SqlExplainPlanThreshold).Returns(TransactionTracerExplainThreshold);
			Mock.Arrange(() => configuration.SqlStatementsPerTransaction).Returns(MaxSqlStatements);
			Mock.Arrange(() => configuration.SqlExplainPlansMax).Returns(MaxExplainPlans);
			Mock.Arrange(() => configuration.TransactionTraceThreshold).Returns(TransactionTracerThreshold);
			Mock.Arrange(() => configuration.TransactionTracerRecordSql).Returns(TransactionTracerRecordSql);
			Mock.Arrange(() => configuration.SlowSqlEnabled).Returns(SlowSqlEnabled);
			Mock.Arrange(() => configuration.BrowserMonitoringAutoInstrument).Returns(BrowserMonitoringAutoInstrument);
			Mock.Arrange(() => configuration.TransactionEventsMaxSamplesStored).Returns(TransactionEventMaxSamplesStored);

			var agentSettings = new ReportedConfiguration
			{
				ApdexT = configuration.TransactionTraceApdexT.TotalSeconds,
				CatId = configuration.CrossApplicationTracingCrossProcessId,
				EncodingKey = configuration.EncodingKey,
				TrustedAccountIds = configuration.TrustedAccountIds.ToList(),
				MaxStackTraceLines = configuration.StackTraceMaximumFrames,
				UsingServerSideConfig = configuration.UsingServerSideConfig,
				ThreadProfilerEnabled = configuration.ThreadProfilingEnabled,
				CrossApplicationTracerEnabled = configuration.CrossApplicationTracingEnabled,
				ErrorCollectorEnabled = configuration.ErrorCollectorEnabled,
				ErrorCollectorIgnoreStatusCodes = configuration.HttpStatusCodesToIgnore.ToList(),
				ErrorCollectorIgnoreErrors = configuration.ExceptionsToIgnore.ToList(),
				TransactionTracerStackThreshold = configuration.TransactionTracerStackThreshold.TotalSeconds,
				TransactionTracerExplainEnabled = configuration.SqlExplainPlansEnabled,
				TransactionTracerExplainThreshold = configuration.SqlExplainPlanThreshold.TotalSeconds,
				MaxSqlStatements = configuration.SqlStatementsPerTransaction,
				MaxExplainPlans = configuration.SqlExplainPlansMax,
				TransactionTracerThreshold = configuration.TransactionTraceThreshold.TotalSeconds,
				TransactionTracerRecordSql = configuration.TransactionTracerRecordSql,
				SlowSqlEnabled = configuration.SlowSqlEnabled,
				BrowserMonitoringAutoInstrument = configuration.BrowserMonitoringAutoInstrument,
				TransactionEventMaxSamplesStored = configuration.TransactionEventsMaxSamplesStored
			};

			var json = JsonConvert.SerializeObject(agentSettings);

			const string expectedJson = @"{""apdex_t"":10.0,""cross_process_id"":""273070#123456"",""encoding_key"":""thisistheencodingkey"",""trusted_account_ids"":[123456,98765],""max_stack_trace_lines"":100,""using_server_side_config"":false,""thread_profiler.enabled"":false,""cross_application_tracer.enabled"":false,""error_collector.enabled"":true,""error_collector.ignore_status_codes"":[""401"",""404""],""error_collector.ignore_errors"":[""401"",""404""],""transaction_tracer.stack_trace_threshold"":11.0,""transaction_tracer.explain_enabled"":false,""transaction_tracer.max_sql_statements"":100,""transaction_tracer.max_explain_plans"":10,""transaction_tracer.explain_threshold"":12.0,""transaction_tracer.transaction_threshold"":13.0,""transaction_tracer.record_sql"":""obfuscate"",""slow_sql.enabled"":false,""browser_monitoring.auto_instrument"":true,""transaction_event.max_samples_stored"":10000}";

			Assert.AreEqual(expectedJson, json);
		}
	}
}

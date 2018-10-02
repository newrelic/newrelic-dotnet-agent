using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using JetBrains.Annotations;

namespace NewRelic.Agent.Configuration
{
	public interface IConfiguration
	{
		object AgentRunId { get; }
		bool AgentEnabled { get; }
		string AgentLicenseKey { get; }
		[NotNull]
		IEnumerable<string> ApplicationNames { get; }
		bool AutoStartAgent { get; }
		string BrowserMonitoringApplicationId { get; }
		bool BrowserMonitoringAutoInstrument { get; }
		string BrowserMonitoringBeaconAddress { get; }
		string BrowserMonitoringErrorBeaconAddress { get; }
		string BrowserMonitoringJavaScriptAgent { get; }
		string BrowserMonitoringJavaScriptAgentFile { get; }
		[NotNull]
		string BrowserMonitoringJavaScriptAgentLoaderType { get; }
		string BrowserMonitoringKey { get; }
		bool BrowserMonitoringUseSsl { get; }

		string SecurityPoliciesToken { get; }
		bool SecurityPoliciesTokenExists { get; }

		bool CaptureAttributes { get; }
		
		bool CanUseAttributesIncludes { get; }
		string CanUseAttributesIncludesSource { get; }

		[NotNull]
		IEnumerable<string> CaptureAttributesIncludes { get; }
		[NotNull]
		IEnumerable<string> CaptureAttributesExcludes { get; }
		[NotNull]
		IEnumerable<string> CaptureAttributesDefaultExcludes { get; }

		bool CaptureTransactionEventsAttributes { get; }
		[NotNull]
		IEnumerable<string> CaptureTransactionEventAttributesIncludes { get; }
		[NotNull]
		IEnumerable<string> CaptureTransactionEventAttributesExcludes { get; }

		bool CaptureTransactionTraceAttributes { get; }
		[NotNull]
		IEnumerable<string> CaptureTransactionTraceAttributesIncludes { get; }
		[NotNull]
		IEnumerable<string> CaptureTransactionTraceAttributesExcludes { get; }

		bool CaptureErrorCollectorAttributes { get; }
		[NotNull]
		IEnumerable<string> CaptureErrorCollectorAttributesIncludes { get; }
		[NotNull]
		IEnumerable<string> CaptureErrorCollectorAttributesExcludes { get; }

		bool CaptureBrowserMonitoringAttributes { get; }
		[NotNull]
		IEnumerable<string> CaptureBrowserMonitoringAttributesIncludes { get; }
		[NotNull]
		IEnumerable<string> CaptureBrowserMonitoringAttributesExcludes { get; }

		bool CaptureCustomParameters { get; }
		string CaptureCustomParametersSource { get; }

		bool CaptureRequestParameters { get; }

		[NotNull]
		string CollectorHost { get; }
		[NotNull]
		uint CollectorPort { get; }
		bool CollectorSendDataOnExit { get; }
		float CollectorSendDataOnExitThreshold { get; }
		bool CollectorSendEnvironmentInfo { get; }
		bool CollectorSyncStartup { get; }
		uint CollectorTimeout { get; }

		bool CompleteTransactionsOnThread { get; }

		string CompressedContentEncoding { get; }

		long ConfigurationVersion { get; }

		string CrossApplicationTracingCrossProcessId { get; }
		bool CrossApplicationTracingEnabled { get; }
		bool DistributedTracingEnabled { get; }
		bool SpanEventsEnabled { get; }
		string PrimaryApplicationId { get; }
		string TrustedAccountKey { get; }
		string AccountId { get; }
		bool DatabaseNameReportingEnabled { get; }
		bool DatastoreTracerQueryParametersEnabled { get; }
		bool ErrorCollectorEnabled { get; }
		bool ErrorCollectorCaptureEvents { get; }
		uint ErrorCollectorMaxEventSamplesStored { get; }
		uint ErrorsMaximumPerPeriod { get; }
		[NotNull]
		IEnumerable<string> ExceptionsToIgnore { get; }
		string EncodingKey { get; }
		bool HighSecurityModeEnabled { get; }

		bool CustomInstrumentationEditorEnabled { get; }
		string CustomInstrumentationEditorEnabledSource { get; }

		bool StripExceptionMessages { get; }
		string StripExceptionMessagesSource { get; }

		bool InstanceReportingEnabled { get; }
		int InstrumentationLevel { get; }
		bool InstrumentationLoggingEnabled { get; }

		string Labels { get; }

		[NotNull]
		IEnumerable<RegexRule> MetricNameRegexRules { get; }

		string NewRelicConfigFilePath { get; }

		string ProxyHost { get; }
		string ProxyUriPath { get; }
		int ProxyPort { get; }
		string ProxyUsername { get; }
		string ProxyPassword { get; }
		[NotNull]
		string ProxyDomain { get; }

		bool PutForDataSend { get; }

		bool SlowSqlEnabled { get; }
		TimeSpan SqlExplainPlanThreshold { get; }
		bool SqlExplainPlansEnabled { get; }
		int SqlExplainPlansMax { get; }
		uint SqlStatementsPerTransaction { get; }
		int SqlTracesPerPeriod { get; }
		int StackTraceMaximumFrames { get; }
		[NotNull]
		IEnumerable<string> HttpStatusCodesToIgnore { get; }
		[NotNull]
		IEnumerable<string> ThreadProfilingIgnoreMethods { get; }

		bool CustomEventsEnabled { get; }
		string CustomEventsEnabledSource { get; }

		uint CustomEventsMaxSamplesStored { get; }
		bool DisableSamplers { get; }
		bool ThreadProfilingEnabled { get; }
		bool TransactionEventsEnabled { get; }
		uint TransactionEventsMaxSamplesPerMinute { get; }
		uint TransactionEventsMaxSamplesStored { get; }
		bool TransactionEventsTransactionsEnabled { get; }
		[NotNull]
		IEnumerable<RegexRule> TransactionNameRegexRules { get; }
		[NotNull]
		IDictionary<string,IEnumerable<string>> TransactionNameWhitelistRules { get; }
		TimeSpan TransactionTraceApdexF { get; }
		TimeSpan TransactionTraceApdexT { get; }
		TimeSpan TransactionTraceThreshold { get; }
		bool TransactionTracerEnabled { get; }
		int TransactionTracerMaxSegments { get; }

		string TransactionTracerRecordSql { get; }
		string TransactionTracerRecordSqlSource { get; }

		TimeSpan TransactionTracerStackThreshold { get; }
		int TransactionTracerMaxStackTraces { get; }
		[NotNull]
		IEnumerable<long> TrustedAccountIds { get; }
		bool UsingServerSideConfig { get; }
		[NotNull]
		IEnumerable<RegexRule> UrlRegexRules { get; }
		[NotNull]
		IEnumerable<Regex> RequestPathExclusionList { get; }
		[NotNull]
		IDictionary<string, double> WebTransactionsApdex { get; }
		int WrapperExceptionLimit { get; }

		bool UtilizationDetectAws { get; }
		bool UtilizationDetectAzure { get; }
		bool UtilizationDetectGcp { get; }
		bool UtilizationDetectPcf { get; }
		bool UtilizationDetectDocker { get; }
		[CanBeNull]
		int? UtilizationLogicalProcessors { get;  }
		[CanBeNull]
		int? UtilizationTotalRamMib { get; }
		[CanBeNull]
		string UtilizationBillingHost { get; }

		bool DiagnosticsCaptureAgentTiming { get; }

		bool UseResourceBasedNamingForWCFEnabled { get; }

		//Priority Sampling... Distributed Tracing...
		int? SamplingTarget { get; }

		uint SpanEventsMaxSamplesStored { get; }

		int? SamplingTargetPeriodInSeconds { get; }

		bool PayloadSuccessMetricsEnabled { get; }

		string ProcessHostDisplayName { get; }
	}
}

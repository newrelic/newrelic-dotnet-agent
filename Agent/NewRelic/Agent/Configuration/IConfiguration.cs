using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using JetBrains.Annotations;

namespace NewRelic.Agent.Configuration
{
	public interface IConfiguration
	{
		[CanBeNull]
		Object AgentRunId { get; }
		Boolean AgentEnabled { get; }
		String AgentLicenseKey { get; }
		[NotNull]
		IEnumerable<String> ApplicationNames { get; }
		Boolean AutoStartAgent { get; }
		String BrowserMonitoringApplicationId { get; }
		Boolean BrowserMonitoringAutoInstrument { get; }
		String BrowserMonitoringBeaconAddress { get; }
		String BrowserMonitoringErrorBeaconAddress { get; }
		String BrowserMonitoringJavaScriptAgent { get; }
		String BrowserMonitoringJavaScriptAgentFile { get; }
		[NotNull]
		String BrowserMonitoringJavaScriptAgentLoaderType { get; }
		String BrowserMonitoringKey { get; }
		Boolean BrowserMonitoringUseSsl { get; }

		string SecurityPoliciesToken { get; }
		bool SecurityPoliciesTokenExists { get; }

		bool CaptureAttributes { get; }
		
		bool CanUseAttributesIncludes { get; }
		string CanUseAttributesIncludesSource { get; }

		[NotNull]
		IEnumerable<String> CaptureAttributesIncludes { get; }
		[NotNull]
		IEnumerable<String> CaptureAttributesExcludes { get; }
		[NotNull]
		IEnumerable<String> CaptureAttributesDefaultExcludes { get; }

		Boolean CaptureTransactionEventsAttributes { get; }
		[NotNull]
		IEnumerable<String> CaptureTransactionEventAttributesIncludes { get; }
		[NotNull]
		IEnumerable<String> CaptureTransactionEventAttributesExcludes { get; }

		Boolean CaptureTransactionTraceAttributes { get; }
		[NotNull]
		IEnumerable<String> CaptureTransactionTraceAttributesIncludes { get; }
		[NotNull]
		IEnumerable<String> CaptureTransactionTraceAttributesExcludes { get; }

		Boolean CaptureErrorCollectorAttributes { get; }
		[NotNull]
		IEnumerable<String> CaptureErrorCollectorAttributesIncludes { get; }
		[NotNull]
		IEnumerable<String> CaptureErrorCollectorAttributesExcludes { get; }

		Boolean CaptureBrowserMonitoringAttributes { get; }
		[NotNull]
		IEnumerable<String> CaptureBrowserMonitoringAttributesIncludes { get; }
		[NotNull]
		IEnumerable<String> CaptureBrowserMonitoringAttributesExcludes { get; }

		bool CaptureCustomParameters { get; }
		string CaptureCustomParametersSource { get; }

		Boolean CaptureRequestParameters { get; }

		[NotNull]
		String CollectorHost { get; }
		[NotNull]
		UInt32 CollectorPort { get; }
		Boolean CollectorSendDataOnExit { get; }
		Single CollectorSendDataOnExitThreshold { get; }
		Boolean CollectorSendEnvironmentInfo { get; }
		Boolean CollectorSyncStartup { get; }
		UInt32 CollectorTimeout { get; }

		Boolean CompleteTransactionsOnThread { get; }

		String CompressedContentEncoding { get; }

		Int64 ConfigurationVersion { get; }

		String CrossApplicationTracingCrossProcessId { get; }
		Boolean CrossApplicationTracingEnabled { get; }
		Boolean DatabaseNameReportingEnabled { get; }
		Boolean ErrorCollectorEnabled { get; }
		Boolean ErrorCollectorCaptureEvents { get; }
		UInt32 ErrorCollectorMaxEventSamplesStored { get; }
		UInt32 ErrorsMaximumPerPeriod { get; }
		[NotNull]
		IEnumerable<String> ExceptionsToIgnore { get; }
		String EncodingKey { get; }
		Boolean HighSecurityModeEnabled { get; }

		bool CustomInstrumentationEditorEnabled { get; }
		string CustomInstrumentationEditorEnabledSource { get; }

		bool StripExceptionMessages { get; }
		string StripExceptionMessagesSource { get; }

		Boolean InstanceReportingEnabled { get; }
		Int32 InstrumentationLevel { get; }
		Boolean InstrumentationLoggingEnabled { get; }

		String Labels { get; }

		[NotNull]
		IEnumerable<RegexRule> MetricNameRegexRules { get; }

		String NewRelicConfigFilePath { get; }

		String ProxyHost { get; }
		String ProxyUriPath { get; }
		Int32 ProxyPort { get; }
		String ProxyUsername { get; }
		String ProxyPassword { get; }
		[NotNull]
		String ProxyDomain { get; }

		Boolean PutForDataSend { get; }

		Boolean SlowSqlEnabled { get; }
		TimeSpan SqlExplainPlanThreshold { get; }
		Boolean SqlExplainPlansEnabled { get; }
		Int32 SqlExplainPlansMax { get; }
		UInt32 SqlStatementsPerTransaction { get; }
		Int32 SqlTracesPerPeriod { get; }
		Int32 StackTraceMaximumFrames { get; }
		[NotNull]
		IEnumerable<String> HttpStatusCodesToIgnore { get; }
		[NotNull]
		IEnumerable<String> ThreadProfilingIgnoreMethods { get; }

		bool CustomEventsEnabled { get; }
		string CustomEventsEnabledSource { get; }

		UInt32 CustomEventsMaxSamplesStored { get; }
		Boolean DisableSamplers { get; }
		Boolean ThreadProfilingEnabled { get; }
		Boolean TransactionEventsEnabled { get; }
		UInt32 TransactionEventsMaxSamplesPerMinute { get; }
		UInt32 TransactionEventsMaxSamplesStored { get; }
		Boolean TransactionEventsTransactionsEnabled { get; }
		[NotNull]
		IEnumerable<RegexRule> TransactionNameRegexRules { get; }
		[NotNull]
		IDictionary<String,IEnumerable<String>> TransactionNameWhitelistRules { get; }
		TimeSpan TransactionTraceApdexF { get; }
		TimeSpan TransactionTraceApdexT { get; }
		TimeSpan TransactionTraceThreshold { get; }
		Boolean TransactionTracerEnabled { get; }
		Int32 TransactionTracerMaxSegments { get; }

		string TransactionTracerRecordSql { get; }
		string TransactionTracerRecordSqlSource { get; }

		TimeSpan TransactionTracerStackThreshold { get; }
		Int32 TransactionTracerMaxStackTraces { get; }
		[NotNull]
		IEnumerable<long> TrustedAccountIds { get; }
		Boolean UsingServerSideConfig { get; }
		[NotNull]
		IEnumerable<RegexRule> UrlRegexRules { get; }
		[NotNull]
		IEnumerable<Regex> RequestPathExclusionList { get; }
		[NotNull]
		IDictionary<String, Double> WebTransactionsApdex { get; }
		Int32 WrapperExceptionLimit { get; }

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
	}
}

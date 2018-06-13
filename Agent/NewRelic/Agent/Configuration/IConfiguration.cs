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
		bool AgentEnabled { get; }
		String AgentLicenseKey { get; }
		[NotNull]
		IEnumerable<String> ApplicationNames { get; }
		bool AutoStartAgent { get; }
		String BrowserMonitoringApplicationId { get; }
		bool BrowserMonitoringAutoInstrument { get; }
		String BrowserMonitoringBeaconAddress { get; }
		String BrowserMonitoringErrorBeaconAddress { get; }
		String BrowserMonitoringJavaScriptAgent { get; }
		String BrowserMonitoringJavaScriptAgentFile { get; }
		[NotNull]
		String BrowserMonitoringJavaScriptAgentLoaderType { get; }
		String BrowserMonitoringKey { get; }
		bool BrowserMonitoringUseSsl { get; }

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

		bool CaptureTransactionEventsAttributes { get; }
		[NotNull]
		IEnumerable<String> CaptureTransactionEventAttributesIncludes { get; }
		[NotNull]
		IEnumerable<String> CaptureTransactionEventAttributesExcludes { get; }

		bool CaptureTransactionTraceAttributes { get; }
		[NotNull]
		IEnumerable<String> CaptureTransactionTraceAttributesIncludes { get; }
		[NotNull]
		IEnumerable<String> CaptureTransactionTraceAttributesExcludes { get; }

		bool CaptureErrorCollectorAttributes { get; }
		[NotNull]
		IEnumerable<String> CaptureErrorCollectorAttributesIncludes { get; }
		[NotNull]
		IEnumerable<String> CaptureErrorCollectorAttributesExcludes { get; }

		bool CaptureBrowserMonitoringAttributes { get; }
		[NotNull]
		IEnumerable<String> CaptureBrowserMonitoringAttributesIncludes { get; }
		[NotNull]
		IEnumerable<String> CaptureBrowserMonitoringAttributesExcludes { get; }

		bool CaptureCustomParameters { get; }
		string CaptureCustomParametersSource { get; }

		bool CaptureRequestParameters { get; }

		[NotNull]
		String CollectorHost { get; }
		[NotNull]
		UInt32 CollectorPort { get; }
		bool CollectorSendDataOnExit { get; }
		Single CollectorSendDataOnExitThreshold { get; }
		bool CollectorSendEnvironmentInfo { get; }
		bool CollectorSyncStartup { get; }
		UInt32 CollectorTimeout { get; }

		bool CompleteTransactionsOnThread { get; }

		String CompressedContentEncoding { get; }

		Int64 ConfigurationVersion { get; }

		String CrossApplicationTracingCrossProcessId { get; }
		bool CrossApplicationTracingEnabled { get; }
		bool DistributedTracingEnabled { get; }
		bool DatabaseNameReportingEnabled { get; }
		bool ErrorCollectorEnabled { get; }
		bool ErrorCollectorCaptureEvents { get; }
		UInt32 ErrorCollectorMaxEventSamplesStored { get; }
		UInt32 ErrorsMaximumPerPeriod { get; }
		[NotNull]
		IEnumerable<String> ExceptionsToIgnore { get; }
		String EncodingKey { get; }
		bool HighSecurityModeEnabled { get; }

		bool CustomInstrumentationEditorEnabled { get; }
		string CustomInstrumentationEditorEnabledSource { get; }

		bool StripExceptionMessages { get; }
		string StripExceptionMessagesSource { get; }

		bool InstanceReportingEnabled { get; }
		Int32 InstrumentationLevel { get; }
		bool InstrumentationLoggingEnabled { get; }

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

		bool PutForDataSend { get; }

		bool SlowSqlEnabled { get; }
		TimeSpan SqlExplainPlanThreshold { get; }
		bool SqlExplainPlansEnabled { get; }
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
		bool DisableSamplers { get; }
		bool ThreadProfilingEnabled { get; }
		bool TransactionEventsEnabled { get; }
		UInt32 TransactionEventsMaxSamplesPerMinute { get; }
		UInt32 TransactionEventsMaxSamplesStored { get; }
		bool TransactionEventsTransactionsEnabled { get; }
		[NotNull]
		IEnumerable<RegexRule> TransactionNameRegexRules { get; }
		[NotNull]
		IDictionary<String,IEnumerable<String>> TransactionNameWhitelistRules { get; }
		TimeSpan TransactionTraceApdexF { get; }
		TimeSpan TransactionTraceApdexT { get; }
		TimeSpan TransactionTraceThreshold { get; }
		bool TransactionTracerEnabled { get; }
		Int32 TransactionTracerMaxSegments { get; }

		string TransactionTracerRecordSql { get; }
		string TransactionTracerRecordSqlSource { get; }

		TimeSpan TransactionTracerStackThreshold { get; }
		Int32 TransactionTracerMaxStackTraces { get; }
		[NotNull]
		IEnumerable<long> TrustedAccountIds { get; }
		bool UsingServerSideConfig { get; }
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

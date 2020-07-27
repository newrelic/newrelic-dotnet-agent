using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace NewRelic.Agent.Configuration
{
    public interface IConfiguration
    {
        Object AgentRunId { get; }
        Boolean AgentEnabled { get; }
        String AgentLicenseKey { get; }
        IEnumerable<String> ApplicationNames { get; }
        Boolean AutoStartAgent { get; }
        String BrowserMonitoringApplicationId { get; }
        Boolean BrowserMonitoringAutoInstrument { get; }
        String BrowserMonitoringBeaconAddress { get; }
        String BrowserMonitoringErrorBeaconAddress { get; }
        String BrowserMonitoringJavaScriptAgent { get; }
        String BrowserMonitoringJavaScriptAgentFile { get; }
        String BrowserMonitoringJavaScriptAgentLoaderType { get; }
        String BrowserMonitoringKey { get; }
        Boolean BrowserMonitoringUseSsl { get; }

        Boolean CaptureAttributes { get; }
        IEnumerable<String> CaptureAttributesIncludes { get; }
        IEnumerable<String> CaptureAttributesExcludes { get; }
        IEnumerable<String> CaptureAttributesDefaultExcludes { get; }

        Boolean CaptureTransactionEventsAttributes { get; }
        IEnumerable<String> CaptureTransactionEventAttributesIncludes { get; }
        IEnumerable<String> CaptureTransactionEventAttributesExcludes { get; }

        Boolean CaptureTransactionTraceAttributes { get; }
        IEnumerable<String> CaptureTransactionTraceAttributesIncludes { get; }
        IEnumerable<String> CaptureTransactionTraceAttributesExcludes { get; }

        Boolean CaptureErrorCollectorAttributes { get; }
        IEnumerable<String> CaptureErrorCollectorAttributesIncludes { get; }
        IEnumerable<String> CaptureErrorCollectorAttributesExcludes { get; }

        Boolean CaptureBrowserMonitoringAttributes { get; }
        IEnumerable<String> CaptureBrowserMonitoringAttributesIncludes { get; }
        IEnumerable<String> CaptureBrowserMonitoringAttributesExcludes { get; }

        Boolean CaptureCustomParameters { get; }
        Boolean CaptureRequestParameters { get; }

        String CollectorHost { get; }
        String CollectorHttpProtocol { get; }
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
        IEnumerable<String> ExceptionsToIgnore { get; }
        String EncodingKey { get; }
        Boolean HighSecurityModeEnabled { get; }
        Boolean InstanceReportingEnabled { get; }
        Int32 InstrumentationLevel { get; }
        Boolean InstrumentationLoggingEnabled { get; }

        String Labels { get; }

        IEnumerable<RegexRule> MetricNameRegexRules { get; }

        String NewRelicConfigFilePath { get; }

        String ProxyHost { get; }
        String ProxyUriPath { get; }
        Int32 ProxyPort { get; }
        String ProxyUsername { get; }
        String ProxyPassword { get; }
        String ProxyDomain { get; }

        Boolean PutForDataSend { get; }

        Boolean SlowSqlEnabled { get; }
        TimeSpan SqlExplainPlanThreshold { get; }
        Boolean SqlExplainPlansEnabled { get; }
        Int32 SqlExplainPlansMax { get; }
        UInt32 SqlStatementsPerTransaction { get; }
        Int32 SqlTracesPerPeriod { get; }
        Int32 StackTraceMaximumFrames { get; }
        IEnumerable<String> HttpStatusCodesToIgnore { get; }
        IEnumerable<String> ThreadProfilingIgnoreMethods { get; }
        Boolean CustomEventsEnabled { get; }
        UInt32 CustomEventsMaxSamplesStored { get; }
        Boolean DisableSamplers { get; }
        Boolean ThreadProfilingEnabled { get; }
        Boolean TransactionEventsEnabled { get; }
        UInt32 TransactionEventsMaxSamplesPerMinute { get; }
        UInt32 TransactionEventsMaxSamplesStored { get; }
        Boolean TransactionEventsTransactionsEnabled { get; }
        IEnumerable<RegexRule> TransactionNameRegexRules { get; }
        IDictionary<String, IEnumerable<String>> TransactionNameWhitelistRules { get; }
        TimeSpan TransactionTraceApdexF { get; }
        TimeSpan TransactionTraceApdexT { get; }
        TimeSpan TransactionTraceThreshold { get; }
        Boolean TransactionTracerEnabled { get; }
        Int32 TransactionTracerMaxSegments { get; }
        String TransactionTracerRecordSql { get; }
        TimeSpan TransactionTracerStackThreshold { get; }
        Int32 TransactionTracerMaxStackTraces { get; }
        IEnumerable<long> TrustedAccountIds { get; }
        Boolean UsingServerSideConfig { get; }
        IEnumerable<RegexRule> UrlRegexRules { get; }
        IEnumerable<Regex> RequestPathExclusionList { get; }
        IDictionary<String, Double> WebTransactionsApdex { get; }
        Int32 WrapperExceptionLimit { get; }

        bool UtilizationDetectAws { get; }
        bool UtilizationDetectAzure { get; }
        bool UtilizationDetectGcp { get; }
        bool UtilizationDetectPcf { get; }
        bool UtilizationDetectDocker { get; }
        int? UtilizationLogicalProcessors { get; }
        int? UtilizationTotalRamMib { get; }
        string UtilizationBillingHost { get; }
    }
}

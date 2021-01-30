// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace NewRelic.Agent.Configuration
{
    public interface IConfiguration
    {
        IReadOnlyDictionary<string, string> GetAppSettings();
        object AgentRunId { get; }
        bool AgentEnabled { get; }
        string AgentLicenseKey { get; }
        IEnumerable<string> ApplicationNames { get; }
        bool AutoStartAgent { get; }
        string BrowserMonitoringApplicationId { get; }
        bool BrowserMonitoringAutoInstrument { get; }
        string BrowserMonitoringBeaconAddress { get; }
        string BrowserMonitoringErrorBeaconAddress { get; }
        string BrowserMonitoringJavaScriptAgent { get; }
        string BrowserMonitoringJavaScriptAgentFile { get; }
        string BrowserMonitoringJavaScriptAgentLoaderType { get; }
        string BrowserMonitoringKey { get; }
        bool BrowserMonitoringUseSsl { get; }
        string SecurityPoliciesToken { get; }
        bool SecurityPoliciesTokenExists { get; }
        bool CaptureAttributes { get; }
        bool CanUseAttributesIncludes { get; }
        string CanUseAttributesIncludesSource { get; }
        IEnumerable<string> CaptureAttributesIncludes { get; }
        IEnumerable<string> CaptureAttributesExcludes { get; }
        IEnumerable<string> CaptureAttributesDefaultExcludes { get; }
        bool TransactionEventsAttributesEnabled { get; }
        HashSet<string> TransactionEventsAttributesInclude { get; }
        HashSet<string> TransactionEventsAttributesExclude { get; }
        bool CaptureTransactionTraceAttributes { get; }
        IEnumerable<string> CaptureTransactionTraceAttributesIncludes { get; }
        IEnumerable<string> CaptureTransactionTraceAttributesExcludes { get; }
        bool CaptureErrorCollectorAttributes { get; }
        IEnumerable<string> CaptureErrorCollectorAttributesIncludes { get; }
        IEnumerable<string> CaptureErrorCollectorAttributesExcludes { get; }
        bool CaptureBrowserMonitoringAttributes { get; }
        IEnumerable<string> CaptureBrowserMonitoringAttributesIncludes { get; }
        IEnumerable<string> CaptureBrowserMonitoringAttributesExcludes { get; }
        bool CaptureCustomParameters { get; }
        string CaptureCustomParametersSource { get; }
        bool CaptureRequestParameters { get; }
        string CollectorHost { get; }
        int CollectorPort { get; }
        bool CollectorSendDataOnExit { get; }
        float CollectorSendDataOnExitThreshold { get; }
        bool CollectorSendEnvironmentInfo { get; }
        bool CollectorSyncStartup { get; }
        uint CollectorTimeout { get; }
        int CollectorMaxPayloadSizeInBytes { get; }
        bool CompleteTransactionsOnThread { get; }
        string CompressedContentEncoding { get; }
        long ConfigurationVersion { get; }
        string CrossApplicationTracingCrossProcessId { get; }
        bool CrossApplicationTracingEnabled { get; }
        bool DistributedTracingEnabled { get; }
        bool SpanEventsEnabled { get; }
        TimeSpan SpanEventsHarvestCycle { get; }
        bool SpanEventsAttributesEnabled { get; }
        HashSet<string> SpanEventsAttributesInclude { get; }
        HashSet<string> SpanEventsAttributesExclude { get; }
        int InfiniteTracingTraceCountConsumers { get; }
        string InfiniteTracingTraceObserverHost { get; }
        string InfiniteTracingTraceObserverPort { get; }
        string InfiniteTracingTraceObserverSsl { get; }
        float? InfiniteTracingTraceObserverTestFlaky { get; }
        int? InfiniteTracingTraceObserverTestFlakyCode { get; }
        int? InfiniteTracingTraceObserverTestDelayMs { get; }
        int InfiniteTracingQueueSizeSpans { get; }
        int InfiniteTracingPartitionCountSpans { get; }
        int InfiniteTracingBatchSizeSpans { get; }
        int InfiniteTracingTraceTimeoutMsConnect { get; }
        int InfiniteTracingTraceTimeoutMsSendData { get; }
        string PrimaryApplicationId { get; }
        string TrustedAccountKey { get; }
        string AccountId { get; }
        bool DatabaseNameReportingEnabled { get; }
        bool DatastoreTracerQueryParametersEnabled { get; }
        bool ErrorCollectorEnabled { get; }
        bool ErrorCollectorCaptureEvents { get; }
        int ErrorCollectorMaxEventSamplesStored { get; }
        TimeSpan ErrorEventsHarvestCycle { get; }
        uint ErrorsMaximumPerPeriod { get; }
        IEnumerable<MatchRule> ExpectedStatusCodes { get; }
        IEnumerable<string> ExpectedErrorClassesForAgentSettings { get; }
        IDictionary<string, IEnumerable<string>> ExpectedErrorMessagesForAgentSettings { get; }
        IEnumerable<string> ExpectedErrorStatusCodesForAgentSettings { get; }
        IDictionary<string, IEnumerable<string>> ExpectedErrorsConfiguration { get; }
        IEnumerable<string> IgnoreErrorsForAgentSettings { get; }
        IDictionary<string, IEnumerable<string>> IgnoreErrorsConfiguration { get; }
        IEnumerable<string> IgnoreErrorClassesForAgentSettings { get; }
        IDictionary<string, IEnumerable<string>> IgnoreErrorMessagesForAgentSettings { get; }
        Dictionary<string, string> RequestHeadersMap { get; }
        string EncodingKey { get; }
        string EntityGuid { get; }
        bool HighSecurityModeEnabled { get; }
        bool CustomInstrumentationEditorEnabled { get; }
        string CustomInstrumentationEditorEnabledSource { get; }
        bool StripExceptionMessages { get; }
        string StripExceptionMessagesSource { get; }
        bool InstanceReportingEnabled { get; }
        bool InstrumentationLoggingEnabled { get; }
        string Labels { get; }
        IEnumerable<RegexRule> MetricNameRegexRules { get; }
        string NewRelicConfigFilePath { get; }
        string ProxyHost { get; }
        string ProxyUriPath { get; }
        int ProxyPort { get; }
        string ProxyUsername { get; }
        string ProxyPassword { get; }
        string ProxyDomain { get; }
        bool PutForDataSend { get; }
        bool SlowSqlEnabled { get; }
        TimeSpan SqlExplainPlanThreshold { get; }
        bool SqlExplainPlansEnabled { get; }
        int SqlExplainPlansMax { get; }
        uint SqlStatementsPerTransaction { get; }
        int SqlTracesPerPeriod { get; }
        int StackTraceMaximumFrames { get; }
        IEnumerable<string> HttpStatusCodesToIgnore { get; }
        IEnumerable<string> ThreadProfilingIgnoreMethods { get; }
        bool CustomEventsEnabled { get; }
        string CustomEventsEnabledSource { get; }
        bool CustomEventsAttributesEnabled { get; }
        HashSet<string> CustomEventsAttributesInclude { get; }
        HashSet<string> CustomEventsAttributesExclude { get; }
        int CustomEventsMaximumSamplesStored { get; }
        TimeSpan CustomEventsHarvestCycle { get; }
        bool DisableSamplers { get; }
        bool ThreadProfilingEnabled { get; }
        bool TransactionEventsEnabled { get; }
        int TransactionEventsMaximumSamplesStored { get; }
        TimeSpan TransactionEventsHarvestCycle { get; }
        bool TransactionEventsTransactionsEnabled { get; }
        IEnumerable<RegexRule> TransactionNameRegexRules { get; }
        IDictionary<string, IEnumerable<string>> TransactionNameWhitelistRules { get; }
        TimeSpan TransactionTraceApdexF { get; }
        TimeSpan TransactionTraceApdexT { get; }
        TimeSpan TransactionTraceThreshold { get; }
        bool TransactionTracerEnabled { get; }
        int TransactionTracerMaxSegments { get; }
        string TransactionTracerRecordSql { get; }
        string TransactionTracerRecordSqlSource { get; }
        TimeSpan TransactionTracerStackThreshold { get; }
        int TransactionTracerMaxStackTraces { get; }
        IEnumerable<long> TrustedAccountIds { get; }
        bool UsingServerSideConfig { get; }
        IEnumerable<RegexRule> UrlRegexRules { get; }
        IEnumerable<Regex> RequestPathExclusionList { get; }
        IDictionary<string, double> WebTransactionsApdex { get; }
        int WrapperExceptionLimit { get; }
        bool UtilizationDetectAws { get; }
        bool UtilizationDetectAzure { get; }
        bool UtilizationDetectGcp { get; }
        bool UtilizationDetectPcf { get; }
        bool UtilizationDetectDocker { get; }
        bool UtilizationDetectKubernetes { get; }
        int? UtilizationLogicalProcessors { get; }
        int? UtilizationTotalRamMib { get; }
        string UtilizationBillingHost { get; }
        string UtilizationHostName { get; }
        string UtilizationFullHostName { get; }
        bool DiagnosticsCaptureAgentTiming { get; }
        int DiagnosticsCaptureAgentTimingFrequency { get; }
        bool UseResourceBasedNamingForWCFEnabled { get; }
        bool EventListenerSamplersEnabled { get; set; }
        int? SamplingTarget { get; }
        int SpanEventsMaxSamplesStored { get; }
        int? SamplingTargetPeriodInSeconds { get; }
        bool PayloadSuccessMetricsEnabled { get; }
        string ProcessHostDisplayName { get; }
        int DatabaseStatementCacheCapcity { get; }
        bool ForceSynchronousTimingCalculationHttpClient { get; }
        bool ExcludeNewrelicHeader { get; }
    }
}

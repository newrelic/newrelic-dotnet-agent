﻿// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace NewRelic.Agent.Configuration
{
    public interface IConfiguration
    {
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

        bool CaptureAttributes { get; }
        IEnumerable<string> CaptureAttributesIncludes { get; }
        IEnumerable<string> CaptureAttributesExcludes { get; }
        IEnumerable<string> CaptureAttributesDefaultExcludes { get; }

        bool CaptureTransactionEventsAttributes { get; }
        IEnumerable<string> CaptureTransactionEventAttributesIncludes { get; }
        IEnumerable<string> CaptureTransactionEventAttributesExcludes { get; }

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
        bool CaptureRequestParameters { get; }

        string CollectorHost { get; }
        string CollectorHttpProtocol { get; }
        int CollectorPort { get; }
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
        bool DatabaseNameReportingEnabled { get; }
        bool ErrorCollectorEnabled { get; }
        bool ErrorCollectorCaptureEvents { get; }
        uint ErrorCollectorMaxEventSamplesStored { get; }
        uint ErrorsMaximumPerPeriod { get; }
        IEnumerable<string> ExceptionsToIgnore { get; }
        string EncodingKey { get; }
        bool HighSecurityModeEnabled { get; }
        bool InstanceReportingEnabled { get; }
        int InstrumentationLevel { get; }
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
        uint CustomEventsMaxSamplesStored { get; }
        bool DisableSamplers { get; }
        bool ThreadProfilingEnabled { get; }
        bool TransactionEventsEnabled { get; }
        uint TransactionEventsMaxSamplesPerMinute { get; }
        uint TransactionEventsMaxSamplesStored { get; }
        bool TransactionEventsTransactionsEnabled { get; }
        IEnumerable<RegexRule> TransactionNameRegexRules { get; }
        IDictionary<string, IEnumerable<string>> TransactionNameWhitelistRules { get; }
        TimeSpan TransactionTraceApdexF { get; }
        TimeSpan TransactionTraceApdexT { get; }
        TimeSpan TransactionTraceThreshold { get; }
        bool TransactionTracerEnabled { get; }
        int TransactionTracerMaxSegments { get; }
        string TransactionTracerRecordSql { get; }
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
        int? UtilizationLogicalProcessors { get; }
        int? UtilizationTotalRamMib { get; }
        string UtilizationBillingHost { get; }
    }
}

// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NewRelic.Agent.Configuration;

namespace NewRelic.Agent.Core.DataTransport
{
    public class ExhaustiveTestConfiguration : IConfiguration
    {
        public object AgentRunId => "AgentRunId";

        public bool AgentEnabled => true;

        public string AgentEnabledAt => "Hardcoded in test";

        public bool ServerlessModeEnabled  => false;

        public string ServerlessFunctionName => null;

        public string ServerlessFunctionVersion => null;

        public string AgentLicenseKey => "AgentLicenseKey";

        public IEnumerable<string> ApplicationNames => new[] { "name1", "name2", "name3" };

        public string ApplicationNamesSource => "ApplicationNameSource";

        public bool AutoStartAgent => true;

        public string BrowserMonitoringApplicationId => "BrowserMonitoringApplicationId";

        public bool BrowserMonitoringAutoInstrument => true;

        public string BrowserMonitoringBeaconAddress => "BrowserMonitoringBeaconAddress";

        public string BrowserMonitoringErrorBeaconAddress => "BrowserMonitoringErrorBeaconAddress";

        public string BrowserMonitoringJavaScriptAgent => "BrowserMonitoringJavaScriptAgent";

        public string BrowserMonitoringJavaScriptAgentFile => "BrowserMonitoringJavaScriptAgentFile";

        public string BrowserMonitoringJavaScriptAgentLoaderType => "BrowserMonitoringJavaScriptAgentLoaderType";

        public string BrowserMonitoringKey => "BrowserMonitoringKey";

        public bool BrowserMonitoringUseSsl => true;

        public string SecurityPoliciesToken => "SecurityPoliciesToken";

        public bool SecurityPoliciesTokenExists => true;

        public bool AllowAllRequestHeaders => true;

        public bool CaptureAttributes => true;

        public bool CanUseAttributesIncludes => true;

        public string CanUseAttributesIncludesSource => "CanUseAttributesIncludesSource";

        public IEnumerable<string> CaptureAttributesIncludes => new[] { "include1", "include2", "include3" };

        public IEnumerable<string> CaptureAttributesExcludes => new[] { "exclude1", "exclude2", "exclude3" };

        public IEnumerable<string> CaptureAttributesDefaultExcludes => new[] { "defaultExclude1", "defaultExclude2", "defaultExclude3" };

        public bool TransactionEventsAttributesEnabled => false;

        public HashSet<string> TransactionEventsAttributesInclude => new HashSet<string> { "attributeInclude1", "attributeInclude2", "attributeInclude3" };

        public HashSet<string> TransactionEventsAttributesExclude => new HashSet<string> { "attributeExclude1", "attributeExclude2", "attributeExclude3" };

        public bool CaptureTransactionTraceAttributes => true;

        public IEnumerable<string> CaptureTransactionTraceAttributesIncludes => new[] { "include1", "include2", "include3" };

        public IEnumerable<string> CaptureTransactionTraceAttributesExcludes => new[] { "exclude1", "exclude2", "exclude3" };

        public bool CaptureErrorCollectorAttributes => false;

        public IEnumerable<string> CaptureErrorCollectorAttributesIncludes => new[] { "include1", "include2", "include3" };

        public IEnumerable<string> CaptureErrorCollectorAttributesExcludes => new[] { "exclude1", "exclude2", "exclude3" };

        public bool CaptureBrowserMonitoringAttributes => false;

        public IEnumerable<string> CaptureBrowserMonitoringAttributesIncludes => new[] { "include1", "include2", "include3" };

        public IEnumerable<string> CaptureBrowserMonitoringAttributesExcludes => new[] { "exclude1", "exclude2", "exclude3" };

        public bool CaptureCustomParameters => false;

        public string CaptureCustomParametersSource => "CaptureCustomParametersSource";

        public string CollectorHost => "CollectorHost";

        public int CollectorPort => 1234;

        public bool CollectorSendDataOnExit => true;

        public float CollectorSendDataOnExitThreshold => 4321;

        public bool CollectorSendEnvironmentInfo => true;

        public bool CollectorSyncStartup => true;

        public uint CollectorTimeout => 1234;

        public int CollectorMaxPayloadSizeInBytes => 4321;

        public bool CompleteTransactionsOnThread => true;

        public string CompressedContentEncoding => "CompressedContentEncoding";

        public long ConfigurationVersion => 1234;

        public string CrossApplicationTracingCrossProcessId => "CrossApplicationTracingCrossProcessId";

        public bool CrossApplicationTracingEnabled => true;

        public bool DistributedTracingEnabled => true;

        public bool SpanEventsEnabled => true;

        public TimeSpan SpanEventsHarvestCycle => TimeSpan.FromSeconds(1234);

        public bool SpanEventsAttributesEnabled => true;

        public HashSet<string> SpanEventsAttributesInclude => new HashSet<string> { "attributeInclude1", "attributeInclude2", "attributeInclude3" };

        public HashSet<string> SpanEventsAttributesExclude => new HashSet<string> { "attributeExclude1", "attributeExclude2", "attributeExclude3" };

        public int InfiniteTracingTraceCountConsumers => 1234;

        public string InfiniteTracingTraceObserverHost => "InfiniteTracingTraceObserverHost";

        public string InfiniteTracingTraceObserverPort => "InfiniteTracingTraceObserverPort";

        public string InfiniteTracingTraceObserverSsl => "InfiniteTracingTraceObserverSsl";

        public float? InfiniteTracingTraceObserverTestFlaky => 1234;

        public int? InfiniteTracingTraceObserverTestFlakyCode => 4321;

        public int? InfiniteTracingTraceObserverTestDelayMs => 1234;

        public int InfiniteTracingQueueSizeSpans => 4321;

        public int InfiniteTracingPartitionCountSpans => 1234;

        public int InfiniteTracingBatchSizeSpans => 4321;

        public int InfiniteTracingTraceTimeoutMsConnect => 1234;

        public int InfiniteTracingTraceTimeoutMsSendData => 4321;

        public int InfiniteTracingExitTimeoutMs => 1234;

        public bool InfiniteTracingCompression => true;

        public string PrimaryApplicationId => "PrimaryApplicationId";

        public string TrustedAccountKey => "TrustedAccountKey";

        public string AccountId => "AccountId";

        public bool DatabaseNameReportingEnabled => true;

        public bool DatastoreTracerQueryParametersEnabled => true;

        public bool ErrorCollectorEnabled => true;

        public bool ErrorCollectorCaptureEvents => true;

        public int ErrorCollectorMaxEventSamplesStored => 1234;

        public TimeSpan ErrorEventsHarvestCycle => TimeSpan.FromSeconds(1234);

        public uint ErrorsMaximumPerPeriod => 4321;

        public IEnumerable<MatchRule> ExpectedStatusCodes => new MatchRule[] { StatusCodeExactMatchRule.GenerateRule("401"), StatusCodeInRangeMatchRule.GenerateRule("10", "20") };

        public IEnumerable<string> ExpectedErrorClassesForAgentSettings => new[] { "expected1", "expected2", "expected3" };

        public IDictionary<string, IEnumerable<string>> ExpectedErrorMessagesForAgentSettings => new Dictionary<string, IEnumerable<string>>
        {
            { "first", new[] { "first1", "first2" } },
            { "second", new[] { "second1", "second2" } },
        };

        public IEnumerable<string> ExpectedErrorStatusCodesForAgentSettings => new[] { "expectedError1", "expectedError2", "expectedError3" };

        public IDictionary<string, IEnumerable<string>> ExpectedErrorsConfiguration => new Dictionary<string, IEnumerable<string>>
        {
            { "third", new[] { "third1", "third2" } },
            { "fourth", new[] { "fourth1", "fourth2" } },
        };

        public IDictionary<string, IEnumerable<string>> IgnoreErrorsConfiguration => new Dictionary<string, IEnumerable<string>>
        {
            { "fifth", new[] { "fifth1", "fifth2" } },
            { "sixth", new[] { "sixth1", "sixth2" } },
        };

        public IEnumerable<string> IgnoreErrorClassesForAgentSettings => new[] { "ignoreError1", "ignoreError2", "ignoreError3" };

        public IDictionary<string, IEnumerable<string>> IgnoreErrorMessagesForAgentSettings => new Dictionary<string, IEnumerable<string>>
        {
            { "seven", new[] { "seven1", "seven2" } },
            { "eight", new[] { "eight1", "eight2" } },
        };

        public Func<IReadOnlyDictionary<string, object>, string> ErrorGroupCallback => dict => "my error group";

        public Dictionary<string, string> RequestHeadersMap => new Dictionary<string, string>
        {
            { "one", "1" },
            { "two", "2" }
        };

        public string EncodingKey => "EncodingKey";

        public string EntityGuid => "EntityGuid";

        public bool HighSecurityModeEnabled => true;

        public bool CustomInstrumentationEditorEnabled => true;

        public string CustomInstrumentationEditorEnabledSource => "CustomInstrumentationEditorEnabledSource";

        public bool StripExceptionMessages => true;

        public string StripExceptionMessagesSource => "StripExceptionMessagesSource";

        public bool InstanceReportingEnabled => true;

        public bool InstrumentationLoggingEnabled => true;

        public string Labels => "Labels";

        public IEnumerable<RegexRule> MetricNameRegexRules => new[]
        {
            new RegexRule("match1", "replacement1", true, 1, true, true, true),
            new RegexRule("match2", "replacement2", false, 2, false, false, false)
        };

        public string NewRelicConfigFilePath => "NewRelicConfigFilePath";

        public string AppSettingsConfigFilePath => "AppSettingsConfigFilePath";

        public string ProxyHost => "ProxyHost";

        public string ProxyUriPath => "ProxyUriPath";

        public int ProxyPort => 1234;

        public string ProxyUsername => "ProxyUsername";

        public string ProxyPassword => "ProxyPassword";

        public string ProxyDomain => "ProxyDomain";

        public bool PutForDataSend => true;

        public bool SlowSqlEnabled => true;

        public TimeSpan SqlExplainPlanThreshold => TimeSpan.FromSeconds(1234);

        public bool SqlExplainPlansEnabled => true;

        public int SqlExplainPlansMax => 1234;

        public uint SqlStatementsPerTransaction => 4321;

        public int SqlTracesPerPeriod => 1234;

        public int StackTraceMaximumFrames => 4321;

        public IEnumerable<string> HttpStatusCodesToIgnore => new[] { "ignore1", "ignore2", "ignore3" };

        public IEnumerable<string> ThreadProfilingIgnoreMethods => new[] { "ignoreMethod1", "ignoreMethod2", "ignoreMethod3" };

        public bool CustomEventsEnabled => true;

        public string CustomEventsEnabledSource => "CustomEventsEnabledSource";

        public bool CustomEventsAttributesEnabled => true;

        public HashSet<string> CustomEventsAttributesInclude => new HashSet<string> { "attributeInclude1", "attributeInclude2", "attributeInclude3" };

        public HashSet<string> CustomEventsAttributesExclude => new HashSet<string> { "attributeExclude1", "attributeExclude2", "attributeExclude3" };

        public int CustomEventsMaximumSamplesStored => 1234;

        public TimeSpan CustomEventsHarvestCycle => TimeSpan.FromSeconds(1234);

        public bool DisableSamplers => true;

        public bool ThreadProfilingEnabled => true;

        public bool TransactionEventsEnabled => true;

        public int TransactionEventsMaximumSamplesStored => 4321;

        public TimeSpan TransactionEventsHarvestCycle => TimeSpan.FromSeconds(4321);

        public bool TransactionEventsTransactionsEnabled => true;

        public IEnumerable<RegexRule> TransactionNameRegexRules => new[]
        {
            new RegexRule("matchTrans1", "replacementTrans1", true, 1, true, true, true),
            new RegexRule("matchTrans2", "replacementTrans2", false, 2, false, false, false)
        };

        public IDictionary<string, IEnumerable<string>> TransactionNameWhitelistRules => new Dictionary<string, IEnumerable<string>>
        {
            { "nine", new[] { "nine1", "nine2" } },
            { "ten", new[] { "ten1", "ten2" } },
        };

        public TimeSpan TransactionTraceApdexF => TimeSpan.FromSeconds(1234);

        public TimeSpan TransactionTraceApdexT => TimeSpan.FromSeconds(4321);

        public TimeSpan TransactionTraceThreshold => TimeSpan.FromSeconds(1234);

        public bool TransactionTracerEnabled => true;

        public int TransactionTracerMaxSegments => 1234;

        public string TransactionTracerRecordSql => "TransactionTracerRecordSql";

        public string TransactionTracerRecordSqlSource => "TransactionTracerRecordSqlSource";

        public TimeSpan TransactionTracerStackThreshold => TimeSpan.FromSeconds(4321);

        public int TransactionTracerMaxStackTraces => 4321;

        public IEnumerable<long> TrustedAccountIds => new long[] { 1, 2, 3 };

        public bool ServerSideConfigurationEnabled => true;

        public bool IgnoreServerSideConfiguration => true;

        public IEnumerable<RegexRule> UrlRegexRules => new[]
        {
            new RegexRule("matchUrl1", "replacementUrl1", true, 1, true, true, true),
            new RegexRule("matchUrl2", "replacementUrl2", false, 2, false, false, false)
        };

        public IEnumerable<Regex> RequestPathExclusionList => new[]
        {
            new Regex("asdf"),
            new Regex("qwerty", RegexOptions.IgnoreCase),
            new Regex("yolo", RegexOptions.ExplicitCapture, TimeSpan.FromSeconds(10))
        };

        public IDictionary<string, double> WebTransactionsApdex => new Dictionary<string, double>
        {
            { "first", 1.0 },
            { "second", 2.0 }
        };

        public int WrapperExceptionLimit => 1234;

        public bool UtilizationDetectAws => true;

        public bool UtilizationDetectAzure => true;

        public bool UtilizationDetectGcp => true;

        public bool UtilizationDetectPcf => true;

        public bool UtilizationDetectDocker => true;

        public bool UtilizationDetectKubernetes => true;

        public bool UtilizationDetectAzureFunction => true;

        public int? UtilizationLogicalProcessors => 22;

        public int? UtilizationTotalRamMib => 33;

        public string UtilizationBillingHost => "UtilizationBillingHost";

        public string UtilizationHostName => "UtilizationHostName";

        public string UtilizationFullHostName => "UtilizationFullHostName";

        public bool DiagnosticsCaptureAgentTiming => true;

        public int DiagnosticsCaptureAgentTimingFrequency => 1234;

        public bool UseResourceBasedNamingForWCFEnabled => true;

        public bool EventListenerSamplersEnabled { get => true; set { /* nothx */ } }

        public int? SamplingTarget => 1234;

        public int SpanEventsMaxSamplesStored => 4321;

        public int? SamplingTargetPeriodInSeconds => 1234;

        public bool PayloadSuccessMetricsEnabled => true;

        public string ProcessHostDisplayName => "ProcessHostDisplayName";

        public int DatabaseStatementCacheCapacity => 1234;

        public bool ForceSynchronousTimingCalculationHttpClient => true;

        public bool EnableAspNetCore6PlusBrowserInjection => true;

        public bool ExcludeNewrelicHeader => true;

        public bool ApplicationLoggingEnabled => true;

        public bool LogMetricsCollectorEnabled => true;

        public bool LogEventCollectorEnabled => true;

        public int LogEventsMaxSamplesStored => 1234;

        public TimeSpan LogEventsHarvestCycle => TimeSpan.FromSeconds(1234);

        public bool LogDecoratorEnabled => true;

        public HashSet<string> LogLevelDenyList => new HashSet<string> { "testlevel1, testlevel2" } ;

        public bool AppDomainCachingDisabled => true;

        public bool ForceNewTransactionOnNewThread => true;

        public bool CodeLevelMetricsEnabled => true;

        public bool ContextDataEnabled => true;

        public IEnumerable<string> ContextDataInclude => new[] { "attr1", "attr2"};

        public IEnumerable<string> ContextDataExclude => new[] { "attr1", "attr2" };

        public IEnumerable<IDictionary<string, string>> IgnoredInstrumentation => new[] {
            new Dictionary<string, string> { { "assemblyName", "AssemblyToIgnore1" } },
            new Dictionary<string, string> { { "assemblyName", "AssemblyToIgnore2" }, { "className", "ClassNameToIgnore" } }
        };

        public bool DisableFileSystemWatcher => false;

        public TimeSpan MetricsHarvestCycle => TimeSpan.FromMinutes(1);

        public TimeSpan TransactionTracesHarvestCycle => TimeSpan.FromMinutes(1);

        public TimeSpan ErrorTracesHarvestCycle => TimeSpan.FromMinutes(1);

        public TimeSpan GetAgentCommandsCycle => TimeSpan.FromMinutes(1);

        public TimeSpan DefaultHarvestCycle => TimeSpan.FromMinutes(1);

        public TimeSpan SqlTracesHarvestCycle => TimeSpan.FromMinutes(1);

        public TimeSpan UpdateLoadedModulesCycle => TimeSpan.FromMinutes(1);

        public TimeSpan StackExchangeRedisCleanupCycle => TimeSpan.FromMinutes(1);

        public IReadOnlyDictionary<string, string> GetAppSettings()
        {
            return new Dictionary<string, string>
            {
                { "hello", "friend" },
                { "we", "made" },
                { "it", "to" },
                { "the", "end" }
            };
        }

        public bool LoggingEnabled => true;

        public bool AiMonitoringEnabled => true;
        public bool AiMonitoringStreamingEnabled => true;
        public bool AiMonitoringRecordContentEnabled => true;

        public Func<string, string, int> LlmTokenCountingCallback => (s1, s2) => 1234;

        public bool AzureFunctionModeDetected => true;
        public bool AzureFunctionModeEnabled => true;
        public string AzureFunctionResourceId => "AzureFunctionResourceId";
        public string AzureFunctionResourceGroupName => "AzureFunctionResourceGroupName";
        public string AzureFunctionRegion => "AzureFunctionRegion";
        public string AzureFunctionSubscriptionId => "AzureFunctionSubscriptionId";
        public string AzureFunctionServiceName => "AzureFunctionServiceName";
        public string AzureFunctionResourceIdWithFunctionName(string functionName) => $"AzureFunctionResourceId/{functionName}";

        public string LoggingLevel => "info";
    }
}

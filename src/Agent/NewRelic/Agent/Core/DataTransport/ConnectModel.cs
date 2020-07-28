using System;
using System.Collections.Generic;
using NewRelic.Agent.Core.Utilization;
using Newtonsoft.Json;

namespace NewRelic.Agent.Core.DataTransport
{
    public class ConnectModel
    {
        [JsonProperty("pid")]
        public readonly Int32 ProcessId;
        [JsonProperty("language")]
        public readonly String Language;
        [JsonProperty("host")]
        public readonly String HostName;
        [JsonProperty("app_name")]
        public readonly IEnumerable<String> AppNames;
        [JsonProperty("agent_version")]
        public readonly String AgentVersion;

        [JsonProperty("agent_version_timestamp")]
        public readonly long AgentVersionTimestamp;

        [JsonProperty("build_timestamp")]
        public readonly long BuildTimestamp;
        [JsonProperty("security_settings")]
        public readonly SecuritySettingsModel SecuritySettings;

        [JsonProperty("high_security")]
        public readonly Boolean HighSecurityModeEnabled;

        /// <summary>
        /// This identifier field is provided to avoid https://newrelic.atlassian.net/browse/DSCORE-778
        ///
        /// This identifier is used by the collector to look up the real agent. If an identifier isn't provided, the collector will create its own based on the first appname, which prevents a single daemon from connecting "a;b" and "a;c" at the same time.
        ///
        /// Providing this identifier works around this issue and allows users more flexibility in using application rollups.
        /// </summary>
        [JsonProperty("identifier")]
        public readonly String Identifier;
        [JsonProperty("labels")]
        public readonly IEnumerable<Label> Labels;
        [JsonProperty("settings")]
        public readonly JavascriptAgentSettingsModel JavascriptAgentSettings;
        [JsonProperty("utilization")]
        public readonly UtilizationSettingsModel UtilizationSettings;
        [JsonProperty("environment", NullValueHandling = NullValueHandling.Ignore)]
        public readonly Environment Environment;

        public ConnectModel(Int32 processId, String language, String hostName, IEnumerable<String> appNames, String agentVersion, long agentVersionTimestamp, SecuritySettingsModel securitySettings, Boolean highSecurityModeEnabled, String identifier, IEnumerable<Label> labels, JavascriptAgentSettingsModel javascriptAgentSettings, UtilizationSettingsModel utilizationSettings, Environment environment)
        {
            ProcessId = processId;
            Language = language;
            HostName = hostName;
            AppNames = appNames;
            AgentVersion = agentVersion;
            AgentVersionTimestamp = agentVersionTimestamp;
            BuildTimestamp = agentVersionTimestamp;
            SecuritySettings = securitySettings;
            HighSecurityModeEnabled = highSecurityModeEnabled;
            Identifier = identifier;
            Labels = labels;
            JavascriptAgentSettings = javascriptAgentSettings;
            UtilizationSettings = utilizationSettings;
            Environment = environment;
        }
    }

    public class SecuritySettingsModel
    {
        [JsonProperty("capture_params")]
        public readonly Boolean CaptureRequestParameters;

        [JsonProperty("transaction_tracer")]
        public readonly TransactionTraceSettingsModel TransactionTraceSettings;

        public SecuritySettingsModel(Boolean captureRequestParameters, TransactionTraceSettingsModel transactionTraceSettings)
        {
            CaptureRequestParameters = captureRequestParameters;
            TransactionTraceSettings = transactionTraceSettings;
        }
    }

    public class TransactionTraceSettingsModel
    {
        [JsonProperty("record_sql")]
        public readonly String RecordSql;

        public TransactionTraceSettingsModel(String recordSql)
        {
            RecordSql = recordSql;
        }
    }

    public class JavascriptAgentSettingsModel
    {
        [JsonProperty("browser_monitoring.loader_debug")]
        public readonly Boolean LoaderDebug;

        [JsonProperty("browser_monitoring.loader")]
        public readonly String Loader;

        public JavascriptAgentSettingsModel(Boolean loaderDebug, String loader)
        {
            LoaderDebug = loaderDebug;
            Loader = loader;
        }
    }
}

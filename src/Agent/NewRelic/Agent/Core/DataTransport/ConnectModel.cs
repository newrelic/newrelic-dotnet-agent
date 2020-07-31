// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using NewRelic.Agent.Core.Utilization;
using Newtonsoft.Json;

namespace NewRelic.Agent.Core.DataTransport
{
    public class ConnectModel
    {
        [JsonProperty("pid")]
        public readonly int ProcessId;
        [JsonProperty("language")]
        public readonly string Language;
        [JsonProperty("host")]
        public readonly string HostName;
        [JsonProperty("app_name")]
        public readonly IEnumerable<string> AppNames;
        [JsonProperty("agent_version")]
        public readonly string AgentVersion;

        [JsonProperty("agent_version_timestamp")]
        public readonly long AgentVersionTimestamp;

        [JsonProperty("build_timestamp")]
        public readonly long BuildTimestamp;
        [JsonProperty("security_settings")]
        public readonly SecuritySettingsModel SecuritySettings;

        [JsonProperty("high_security")]
        public readonly bool HighSecurityModeEnabled;

        /// <summary>
        /// This identifier field is provided to avoid https://newrelic.atlassian.net/browse/DSCORE-778
        ///
        /// This identifier is used by the collector to look up the real agent. If an identifier isn't provided, the collector will create its own based on the first appname, which prevents a single daemon from connecting "a;b" and "a;c" at the same time.
        ///
        /// Providing this identifier works around this issue and allows users more flexibility in using application rollups.
        /// </summary>
        [JsonProperty("identifier")]
        public readonly string Identifier;
        [JsonProperty("labels")]
        public readonly IEnumerable<Label> Labels;
        [JsonProperty("settings")]
        public readonly JavascriptAgentSettingsModel JavascriptAgentSettings;
        [JsonProperty("utilization")]
        public readonly UtilizationSettingsModel UtilizationSettings;
        [JsonProperty("environment", NullValueHandling = NullValueHandling.Ignore)]
        public readonly Environment Environment;

        public ConnectModel(int processId, string language, string hostName, IEnumerable<string> appNames, string agentVersion, long agentVersionTimestamp, SecuritySettingsModel securitySettings, bool highSecurityModeEnabled, string identifier, IEnumerable<Label> labels, JavascriptAgentSettingsModel javascriptAgentSettings, UtilizationSettingsModel utilizationSettings, Environment environment)
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
        public readonly bool CaptureRequestParameters;

        [JsonProperty("transaction_tracer")]
        public readonly TransactionTraceSettingsModel TransactionTraceSettings;

        public SecuritySettingsModel(bool captureRequestParameters, TransactionTraceSettingsModel transactionTraceSettings)
        {
            CaptureRequestParameters = captureRequestParameters;
            TransactionTraceSettings = transactionTraceSettings;
        }
    }

    public class TransactionTraceSettingsModel
    {
        [JsonProperty("record_sql")]
        public readonly string RecordSql;

        public TransactionTraceSettingsModel(string recordSql)
        {
            RecordSql = recordSql;
        }
    }

    public class JavascriptAgentSettingsModel
    {
        [JsonProperty("browser_monitoring.loader_debug")]
        public readonly bool LoaderDebug;

        [JsonProperty("browser_monitoring.loader")]
        public readonly string Loader;

        public JavascriptAgentSettingsModel(bool loaderDebug, string loader)
        {
            LoaderDebug = loaderDebug;
            Loader = loader;
        }
    }
}

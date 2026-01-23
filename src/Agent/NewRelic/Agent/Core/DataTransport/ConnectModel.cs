// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Configuration;
using NewRelic.Agent.Core.Labels;
using NewRelic.Agent.Core.Utilization;
using Newtonsoft.Json;

namespace NewRelic.Agent.Core.DataTransport;

public class ConnectModel
{
    [JsonProperty("pid")]
    public readonly int ProcessId;

    [JsonProperty("language")]
    public readonly string Language;

    [JsonProperty("display_host", NullValueHandling = NullValueHandling.Ignore)]
    public readonly string DisplayHost;

    [JsonProperty("host")]
    public readonly string HostName;

    [JsonProperty("app_name")]
    public readonly IEnumerable<string> AppNames;

    [JsonProperty("agent_version")]
    public readonly string AgentVersion;

    [JsonProperty("agent_version_timestamp")]
    public readonly long AgentVersionTimestamp;

    [JsonProperty("security_settings")]
    public readonly SecuritySettingsModel SecuritySettings;

    [JsonProperty("high_security")]
    public readonly bool HighSecurityModeEnabled;

    [JsonProperty("event_harvest_config")]
    public readonly EventHarvestConfigModel EventHarvestConfig;

    /// <summary>
    /// This identifier is used by the collector to look up the real agent. If an identifier isn't provided, the collector will create its own based on the first appname, which prevents a single daemon from connecting "a;b" and "a;c" at the same time.
    ///
    /// Providing this identifier works around this issue and allows users more flexibility in using application rollups.
    /// </summary>
    [JsonProperty("identifier")]
    public readonly string Identifier;

    [JsonProperty("labels")]
    public readonly IEnumerable<Label> Labels;

    [JsonProperty("settings")]
    public readonly ReportedConfiguration Configuration;

    [JsonProperty("metadata")]
    public readonly Dictionary<string, string> Metadata;

    [JsonProperty("utilization")]
    public readonly UtilizationSettingsModel UtilizationSettings;

    [JsonProperty("environment", NullValueHandling = NullValueHandling.Ignore)]
    public readonly Environment Environment;

    [JsonProperty("security_policies", NullValueHandling = NullValueHandling.Ignore)]
    public readonly SecurityPoliciesSettingsModel SecurityPoliciesSettings;

    public ConnectModel(int processId, string language, string displayHost, string hostName, IEnumerable<string> appNames, string agentVersion, long agentVersionTimestamp, SecuritySettingsModel securitySettings, bool highSecurityModeEnabled, string identifier, IEnumerable<Label> labels, Dictionary<string, string> metadata, UtilizationSettingsModel utilizationSettings, Environment environment, SecurityPoliciesSettingsModel securityPoliciesSettings, EventHarvestConfigModel eventHarvestConfig, ReportedConfiguration configuration)
    {
        ProcessId = processId;
        Language = language;
        DisplayHost = displayHost;
        HostName = hostName;
        AppNames = appNames;
        AgentVersion = agentVersion;
        AgentVersionTimestamp = agentVersionTimestamp;
        SecuritySettings = securitySettings;
        HighSecurityModeEnabled = highSecurityModeEnabled;
        Identifier = identifier;
        Labels = labels;
        Metadata = metadata;
        UtilizationSettings = utilizationSettings;
        Environment = environment;
        SecurityPoliciesSettings = securityPoliciesSettings;
        EventHarvestConfig = eventHarvestConfig;
        Configuration = configuration;
    }
}

public class SecuritySettingsModel
{
    [JsonProperty("transaction_tracer")]
    public readonly TransactionTraceSettingsModel TransactionTraceSettings;

    public SecuritySettingsModel(TransactionTraceSettingsModel transactionTraceSettings)
    {
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

public class SecurityPoliciesSettingsModel
{
    [JsonProperty("record_sql")]
    public readonly Dictionary<string, bool> RecordSql;

    [JsonProperty("attributes_include")]
    public readonly Dictionary<string, bool> AttributesInclude;

    [JsonProperty("allow_raw_exception_messages")]
    public readonly Dictionary<string, bool> AllowRawExceptionMessages;

    [JsonProperty("custom_events")]
    public readonly Dictionary<string, bool> CustomEvents;

    [JsonProperty("custom_parameters")]
    public readonly Dictionary<string, bool> CustomParameters;

    [JsonProperty("custom_instrumentation_editor")]
    public readonly Dictionary<string, bool> CustomInstrumentationEditor;

    public SecurityPoliciesSettingsModel(IConfiguration configuration)
    {
        if (configuration.TransactionTracerRecordSql == DefaultConfiguration.RawStringValue)
        {
            throw new ArgumentException($"{DefaultConfiguration.RawStringValue} is not a valid record_sql setting for security policies.");
        }

        RecordSql = new Dictionary<string, bool>() { { "enabled", configuration.TransactionTracerRecordSql == DefaultConfiguration.ObfuscatedStringValue } };
        AttributesInclude = new Dictionary<string, bool>() { { "enabled", configuration.CanUseAttributesIncludes } };
        AllowRawExceptionMessages = new Dictionary<string, bool>() { { "enabled", !configuration.StripExceptionMessages } };
        CustomEvents = new Dictionary<string, bool>() { { "enabled", configuration.CustomEventsEnabled } };
        CustomParameters = new Dictionary<string, bool>() { { "enabled", configuration.CaptureCustomParameters } };
        CustomInstrumentationEditor = new Dictionary<string, bool>() { { "enabled", configuration.CustomInstrumentationEditorEnabled } };
    }
}

public class EventHarvestConfigModel
{
    [JsonProperty("harvest_limits")]
    public readonly Dictionary<string, int> HarvestLimits;

    public EventHarvestConfigModel(IConfiguration configuration)
    {
        HarvestLimits = new Dictionary<string, int>() {
            { "analytic_event_data", configuration.TransactionEventsMaximumSamplesStored },
            { "custom_event_data", configuration.CustomEventsMaximumSamplesStored },
            { "error_event_data", configuration.ErrorCollectorMaxEventSamplesStored },
            { "span_event_data", configuration.SpanEventsMaxSamplesStored },
            { "log_event_data", configuration.LogEventsMaxSamplesStored },
        };
    }
}

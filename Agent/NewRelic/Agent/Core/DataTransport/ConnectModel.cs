using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using NewRelic.Agent.Core.Utilization;
using Newtonsoft.Json;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Configuration;

namespace NewRelic.Agent.Core.DataTransport
{
	public class ConnectModel
	{
		[JsonProperty("pid")]
		public readonly Int32 ProcessId;

		[NotNull]
		[JsonProperty("language")]
		public readonly String Language;

		[NotNull]
		[JsonProperty("host")]
		public readonly String HostName;

		[NotNull]
		[JsonProperty("app_name")]
		public readonly IEnumerable<String> AppNames;

		[NotNull]
		[JsonProperty("agent_version")]
		public readonly String AgentVersion;

		[NotNull]
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
		[NotNull]
		[JsonProperty("identifier")]
		public readonly String Identifier;

		[NotNull]
		[JsonProperty("labels")]
		public readonly IEnumerable<Label> Labels;

		[NotNull]
		[JsonProperty("settings")]
		public readonly JavascriptAgentSettingsModel JavascriptAgentSettings;

		[NotNull]
		[JsonProperty("utilization")]
		public readonly UtilizationSettingsModel UtilizationSettings;

		[CanBeNull]
		[JsonProperty("environment", NullValueHandling = NullValueHandling.Ignore)]
		public readonly Environment Environment;

		[CanBeNull]
		[JsonProperty("security_policies", NullValueHandling = NullValueHandling.Ignore)]
		public readonly SecurityPoliciesSettingsModel SecurityPoliciesSettings;

		public ConnectModel(Int32 processId, [NotNull] String language, [NotNull] String hostName, [NotNull] IEnumerable<String> appNames, [NotNull] String agentVersion, [NotNull] SecuritySettingsModel securitySettings, Boolean highSecurityModeEnabled, [NotNull] String identifier, [NotNull] IEnumerable<Label> labels, [NotNull] JavascriptAgentSettingsModel javascriptAgentSettings, [NotNull] UtilizationSettingsModel utilizationSettings, [CanBeNull] Environment environment, [CanBeNull] SecurityPoliciesSettingsModel securityPoliciesSettings)
		{
			ProcessId = processId;
			Language = language;
			HostName = hostName;
			AppNames = appNames;
			AgentVersion = agentVersion;
			SecuritySettings = securitySettings;
			HighSecurityModeEnabled = highSecurityModeEnabled;
			Identifier = identifier;
			Labels = labels;
			JavascriptAgentSettings = javascriptAgentSettings;
			UtilizationSettings = utilizationSettings;
			Environment = environment;
			SecurityPoliciesSettings = securityPoliciesSettings;
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

			RecordSql = new Dictionary<string, bool>() { { "enabled", configuration.TransactionTracerRecordSql == DefaultConfiguration.ObfuscatedStringValue} };
			AttributesInclude = new Dictionary<string, bool>() { { "enabled", configuration.CanUseAttributesIncludes } };
			AllowRawExceptionMessages = new Dictionary<string, bool>() { { "enabled", !configuration.StripExceptionMessages } };
			CustomEvents = new Dictionary<string, bool>() { { "enabled", configuration.CustomEventsEnabled } };
			CustomParameters = new Dictionary<string, bool>() { { "enabled", configuration.CaptureCustomParameters } };
			CustomInstrumentationEditor = new Dictionary<string, bool>() { { "enabled", configuration.CustomInstrumentationEditorEnabled } };
		}
	}
}

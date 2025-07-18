// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Config;
using NewRelic.Agent.Core.Metrics;
using NewRelic.Agent.Helpers;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Extensions.Logging;
using NewRelic.Agent.Extensions.SystemExtensions;
using NewRelic.Agent.Extensions.SystemExtensions.Collections.Generic;
using NewRelic.Agent.Core.SharedInterfaces;
using NewRelic.Agent.Core.SharedInterfaces.Web;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using NewRelic.Agent.Core.AgentHealth;

namespace NewRelic.Agent.Core.Configuration
{
    /// <summary>
    /// Default implementation of IConfiguration.  This should only be used by ConfigurationService.  If you need configuration, get it from the ConfigurationService, not here.
    /// </summary>
    public class DefaultConfiguration : IConfiguration
    {
        private const int DefaultSslPort = 443;
        private const int DefaultSqlStatementCacheCapacity = 1000;

        public static readonly string RawStringValue = Enum.GetName(typeof(configurationTransactionTracerRecordSql), configurationTransactionTracerRecordSql.raw);
        public static readonly string ObfuscatedStringValue = Enum.GetName(typeof(configurationTransactionTracerRecordSql), configurationTransactionTracerRecordSql.obfuscated);
        public static readonly string OffStringValue = Enum.GetName(typeof(configurationTransactionTracerRecordSql), configurationTransactionTracerRecordSql.off);
        private static readonly char HyphenChar = '-';

        private const string HighSecurityConfigSource = "High Security Mode";
        private const string SecurityPolicyConfigSource = "Security Policy";
        private const string LocalConfigSource = "Local Configuration";
        private const string ServerConfigSource = "Server Configuration";
        private const int MaxExptectedErrorConfigEntries = 50;
        private const int MaxIgnoreErrorConfigEntries = 50;

        private static long _currentConfigurationVersion;
        private readonly IEnvironment _environment = new EnvironmentMock();
        private readonly IProcessStatic _processStatic = new ProcessStatic();
        private readonly IHttpRuntimeStatic _httpRuntimeStatic = new HttpRuntimeStatic();
        private readonly IConfigurationManagerStatic _configurationManagerStatic = new ConfigurationManagerStaticMock();
        private readonly IDnsStatic _dnsStatic;
        private readonly IAgentHealthReporter _agentHealthReporter;

        /// <summary>
        /// Default configuration.  It will contain reasonable default values for everything and never anything more.  Useful when you don't have configuration off disk or a collector response yet.
        /// </summary>
        public static readonly DefaultConfiguration Instance = new DefaultConfiguration();
        private readonly configuration _localConfiguration = new configuration();
        private readonly ServerConfiguration _serverConfiguration = ServerConfiguration.GetDefault();
        private readonly RunTimeConfiguration _runTimeConfiguration = new RunTimeConfiguration();
        private readonly SecurityPoliciesConfiguration _securityPoliciesConfiguration = new SecurityPoliciesConfiguration();
        private readonly IBootstrapConfiguration _bootstrapConfiguration = BootstrapConfiguration.GetDefault();
        private readonly Dictionary<string, string> _newRelicAppSettings;

        public bool UseResourceBasedNamingForWCFEnabled { get; private set; }
        public bool EventListenerSamplersEnabled { get; set; }

        public TimeSpan DefaultHarvestCycle => TimeSpan.FromMinutes(1);

        /// <summary>
        /// Default configuration constructor.  It will contain reasonable default values for everything and never anything more.
        /// </summary>
        private DefaultConfiguration()
        {
            ConfigurationVersion = Interlocked.Increment(ref _currentConfigurationVersion);
        }

        protected DefaultConfiguration(IEnvironment environment, configuration localConfiguration, ServerConfiguration serverConfiguration, RunTimeConfiguration runTimeConfiguration, SecurityPoliciesConfiguration securityPoliciesConfiguration, IBootstrapConfiguration bootstrapConfiguration, IProcessStatic processStatic, IHttpRuntimeStatic httpRuntimeStatic, IConfigurationManagerStatic configurationManagerStatic, IDnsStatic dnsStatic, IAgentHealthReporter agentHealthReporter)
            : this()
        {
            _environment = environment;
            _processStatic = processStatic;
            _httpRuntimeStatic = httpRuntimeStatic;
            _configurationManagerStatic = configurationManagerStatic;
            _dnsStatic = dnsStatic;

            _utilizationFullHostName = new Lazy<string>(_dnsStatic.GetFullHostName);
            _utilizationHostName = new Lazy<string>(_dnsStatic.GetHostName);

            _agentHealthReporter = agentHealthReporter;

            if (localConfiguration != null)
            {
                _localConfiguration = localConfiguration;
            }
            if (serverConfiguration != null)
            {
                _serverConfiguration = serverConfiguration;
            }
            if (runTimeConfiguration != null)
            {
                _runTimeConfiguration = runTimeConfiguration;
            }
            if (securityPoliciesConfiguration != null)
            {
                _securityPoliciesConfiguration = securityPoliciesConfiguration;
            }
            if (_bootstrapConfiguration != null)
            {
                _bootstrapConfiguration = bootstrapConfiguration;
            }

            LogDisabledWarnings();

            _newRelicAppSettings = TransformAppSettings();

            UseResourceBasedNamingForWCFEnabled = TryGetAppSettingAsBoolWithDefault("NewRelic.UseResourceBasedNamingForWCF", false);

            EventListenerSamplersEnabled = TryGetAppSettingAsBoolWithDefault("NewRelic.EventListenerSamplersEnabled", true);

            ParseExpectedErrorConfigurations();
            ParseIgnoreErrorConfigurations();
        }

        public IReadOnlyDictionary<string, string> GetAppSettings()
        {
            return _newRelicAppSettings;
        }

        private Dictionary<string, string> TransformAppSettings()
        {
            if (_localConfiguration.appSettings == null)
                return [];

            return _localConfiguration.appSettings
                .Where(setting => setting != null)
                .Select(setting => new KeyValuePair<string, string>(setting.key, setting.value))
                .ToDictionary(IEnumerableExtensions.DuplicateKeyBehavior.KeepFirst);
        }

        private bool TryGetAppSettingAsBoolWithDefault(string key, bool defaultValue)
        {
            var value = _newRelicAppSettings.GetValueOrDefault(key);

            var parsedSuccessfully = bool.TryParse(value, out var parsedBool);
            if (!parsedSuccessfully)
                return defaultValue;

            return parsedBool;
        }

        private string TryGetAppSettingAsString(string key)
        {
            return _newRelicAppSettings.TryGetValue(key, out var valueStr) ? valueStr : null;
        }

        private float? TryGetAppSettingAsFloat(string key)
        {
            if (_newRelicAppSettings.TryGetValue(key, out var valueStr))
            {
                if (float.TryParse(valueStr, out var valueFloat))
                {
                    return valueFloat;
                }
            }
            return null;
        }

        private int TryGetAppSettingAsIntWithDefault(string key, int defaultValue)
        {
            return TryGetAppSettingAsInt(key).GetValueOrDefault(defaultValue);
        }

        private int? TryGetAppSettingAsInt(string key)
        {
            if (_newRelicAppSettings.TryGetValue(key, out var valueStr))
            {
                if (int.TryParse(valueStr, out var valueInt))
                {
                    return valueInt;
                }
            }
            return null;
        }

        public bool SecurityPoliciesTokenExists => !string.IsNullOrEmpty(SecurityPoliciesToken);

        #region IConfiguration Properties

        public object AgentRunId { get { return _serverConfiguration.AgentRunId; } }

        private static readonly object _lockObj = new object();


        public virtual bool AgentEnabled => _bootstrapConfiguration.AgentEnabled;

        public string AgentEnabledAt => _bootstrapConfiguration.AgentEnabledAt;

        public bool ServerlessModeEnabled => _bootstrapConfiguration.ServerlessModeEnabled;

        public string ServerlessFunctionName => _bootstrapConfiguration.ServerlessFunctionName;

        public string ServerlessFunctionVersion => _bootstrapConfiguration.ServerlessFunctionVersion;

        private string _agentLicenseKey;
        public virtual string AgentLicenseKey
        {
            get
            {
                _agentLicenseKey ??= TryGetLicenseKey();
                if (string.IsNullOrWhiteSpace(_agentLicenseKey) && !ServerlessModeEnabled)
                {
                    TrySetAgentControlStatus(HealthCodes.LicenseKeyMissing);
                }

                return _agentLicenseKey;
            }
        }

        private string TryGetLicenseKey()
        {
            // same order as old process - appsettings > env var(a/b) > newrelic.config
            var candidateKeys = new Dictionary<string, string>
            {
                { "AppSettings", _configurationManagerStatic.GetAppSetting(Constants.AppSettingsLicenseKey) },
                { "EnvironmentVariable", _environment.GetEnvironmentVariableFromList("NEW_RELIC_LICENSE_KEY", "NEWRELIC_LICENSEKEY") },
                { "newrelic.config", _localConfiguration.service.licenseKey }
            };

            foreach (var candidateKey in candidateKeys)
            {
                // Did we find a key?
                if (string.IsNullOrWhiteSpace(candidateKey.Value))
                {
                    continue;
                }

                // We found something, but is it a valid key?

                // If the key is the default value from newrelic.config, we return the default value
                // AgentManager.AssertAgentEnabled() relies on this behavior and will throw an exception if the key is the default value
                if (candidateKey.Value.ToLower().Contains("license"))
                {
                    // newrelic.config is the last place to look
                    return candidateKey.Value;
                }

                // Keys are only 40 characters long, but we trim to be safe
                var trimmedCandidateKey = candidateKey.Value.Trim();
                if (trimmedCandidateKey.Length != 40)
                {
                    Log.Warn($"License key from {candidateKey.Key} is not 40 characters long. Checking for other license keys.");
                    continue;
                }

                // Keys can only contain printable ASCII characters from 0x21 to 0x7E
                if (!trimmedCandidateKey.All(c => c >= 0x21 && c <= 0x7E))
                {
                    Log.Warn($"License key from {candidateKey.Key} contains invalid characters. Checking for other license keys.");
                    continue;
                }

                Log.Info($"License key from {candidateKey.Key} appears to be in the correct format.");
                return trimmedCandidateKey;
            }

            // return string.Empty instead of null to allow caching and prevent checking repeatedly
            Log.Warn("No valid license key found.");
            return string.Empty;
        }

        private IEnumerable<string> _applicationNames;
        public virtual IEnumerable<string> ApplicationNames => _applicationNames ??= GetApplicationNames();

        private string _applicationNamesSource;
        public virtual string ApplicationNamesSource => _applicationNamesSource;

        private IEnumerable<string> GetApplicationNames()
        {
            var runtimeAppNames = _runTimeConfiguration.ApplicationNames.ToList();
            if (runtimeAppNames.Any())
            {
                Log.Info("Application name from SetApplicationName API.");
                _applicationNamesSource = "API";

                return runtimeAppNames;
            }

            var appName = _configurationManagerStatic.GetAppSetting(Constants.AppSettingsAppName);
            if (appName != null)
            {
                Log.Info("Application name from web.config or app.config.");
                _applicationNamesSource = "Application Config";

                return appName.Split(StringSeparators.Comma);
            }

            appName = _environment.GetEnvironmentVariable("IISEXPRESS_SITENAME");
            if (appName != null)
            {
                Log.Info("Application name from IISEXPRESS_SITENAME Environment Variable.");
                _applicationNamesSource = "Environment Variable (IISEXPRESS_SITENAME)";

                return appName.Split(StringSeparators.Comma);
            }

            appName = _environment.GetEnvironmentVariable("NEW_RELIC_APP_NAME");
            if (appName != null)
            {
                Log.Info("Application name from NEW_RELIC_APP_NAME Environment Variable.");
                _applicationNamesSource = "Environment Variable (NEW_RELIC_APP_NAME)";

                return appName.Split(StringSeparators.Comma);
            }

            if (AzureFunctionModeDetected && AzureFunctionModeEnabled && !string.IsNullOrEmpty(AzureFunctionAppName))
            {
                Log.Info("Application name from Azure Function site name.");
                _applicationNamesSource = "Azure Function";
                return [AzureFunctionAppName];
            }

            appName = _environment.GetEnvironmentVariable("RoleName");
            if (appName != null)
            {
                Log.Info("Application name from RoleName Environment Variable.");
                _applicationNamesSource = "Environment Variable (RoleName)";

                return appName.Split(StringSeparators.Comma);
            }

            if (ServerlessModeEnabled)
            {
                if (!string.IsNullOrEmpty(ServerlessFunctionName))
                {
                    Log.Info("Application name from Lambda Function Name.");
                    _applicationNamesSource = "Environment Variable (AWS_LAMBDA_FUNCTION_NAME)";
                    return [ServerlessFunctionName];
                }
            }

            if (_localConfiguration.application.name.Count > 0)
            {
                Log.Info("Application name from newrelic.config.");
                _applicationNamesSource = "NewRelic Config";

                return _localConfiguration.application.name;
            }

            appName = GetAppPoolId();
            if (!string.IsNullOrWhiteSpace(appName))
            {
                Log.Info("Application name from Application Pool name.");
                _applicationNamesSource = "Application Pool";

                return appName.Split(StringSeparators.Comma);
            }

            if (_httpRuntimeStatic.AppDomainAppVirtualPath == null)
            {
                Log.Info("Application name from process name.");
                _applicationNamesSource = "Process Name";

                return [_processStatic.GetCurrentProcess().ProcessName];
            }

            TrySetAgentControlStatus(HealthCodes.ApplicationNameMissing);
            throw new Exception("An application name must be provided");
        }

        private string GetAppPoolId()
        {
            var appPoolId = _environment.GetEnvironmentVariableFromList("APP_POOL_ID", "ASPNETCORE_IIS_APP_POOL_ID");
            if (!string.IsNullOrEmpty(appPoolId))
                return appPoolId;

            var isW3wp = _processStatic.GetCurrentProcess().ProcessName?.Equals("w3wp", StringComparison.InvariantCultureIgnoreCase);
            if (!isW3wp.HasValue || !isW3wp.Value)
                return null;

            var commandLineArgs = _environment.GetCommandLineArgs();
            const string appPoolCommandLineArg = "-ap";
            for (var i = 0; i < commandLineArgs.Length - 1; ++i)
            {
                if (commandLineArgs[i].Equals(appPoolCommandLineArg))
                {
                    appPoolId = commandLineArgs[i + 1];
                    if (appPoolId.Length >= 3 && appPoolId[0] == '"')
                    {
                        appPoolId = appPoolId.Substring(1, appPoolId.Length - 2);
                    }

                    Log.Info($"Found application pool name from command line: {appPoolId}");
                    return appPoolId;
                }
            }

            return appPoolId;
        }

        public bool AutoStartAgent { get { return _localConfiguration.service.autoStart; } }

        public int WrapperExceptionLimit { get { return TryGetAppSettingAsIntWithDefault("WrapperExceptionLimit", 5); } }

        #region Browser Monitoring

        public virtual string BrowserMonitoringApplicationId { get { return _serverConfiguration.RumSettingsApplicationId ?? string.Empty; } }
        public virtual bool BrowserMonitoringAutoInstrument { get { return _localConfiguration.browserMonitoring.autoInstrument; } }
        public virtual string BrowserMonitoringBeaconAddress { get { return _serverConfiguration.RumSettingsBeacon ?? string.Empty; } }
        public virtual string BrowserMonitoringErrorBeaconAddress { get { return _serverConfiguration.RumSettingsErrorBeacon ?? string.Empty; } }
        public virtual string BrowserMonitoringJavaScriptAgent { get { return _serverConfiguration.RumSettingsJavaScriptAgentLoader ?? string.Empty; } }
        public virtual string BrowserMonitoringJavaScriptAgentFile { get { return _serverConfiguration.RumSettingsJavaScriptAgentFile ?? string.Empty; } }
        public virtual string BrowserMonitoringJavaScriptAgentLoaderType { get { return ServerOverrides(_serverConfiguration.RumSettingsBrowserMonitoringLoader, _localConfiguration.browserMonitoring.loader); } }
        public virtual string BrowserMonitoringKey { get { return _serverConfiguration.RumSettingsBrowserKey ?? string.Empty; } }
        public virtual bool BrowserMonitoringUseSsl { get { return HighSecurityModeOverrides(true, _localConfiguration.browserMonitoring.sslForHttp); } }

        #endregion

        private string _securityPoliciesToken;

        public virtual string SecurityPoliciesToken =>
            _securityPoliciesToken ??=
                EnvironmentOverrides(_localConfiguration.securityPoliciesToken ?? string.Empty, "NEW_RELIC_SECURITY_POLICIES_TOKEN")
                    .Trim();

        private string _processHostDisplayName;

        public string ProcessHostDisplayName
        {
            get
            {
                if (_processHostDisplayName != null)
                {
                    return _processHostDisplayName;
                }

                _processHostDisplayName = string.IsNullOrWhiteSpace(_localConfiguration.processHost.displayName)
                    ? _dnsStatic.GetHostName() : _localConfiguration.processHost.displayName;

                _processHostDisplayName = EnvironmentOverrides(_processHostDisplayName, "NEW_RELIC_PROCESS_HOST_DISPLAY_NAME").Trim();

                return _processHostDisplayName;
            }
        }

        private bool? _ignoreServerSideConfiguration;

        public bool IgnoreServerSideConfiguration
        {
            get
            {
                _ignoreServerSideConfiguration ??= EnvironmentOverrides(false, "NEW_RELIC_IGNORE_SERVER_SIDE_CONFIG");

                return _ignoreServerSideConfiguration.Value;
            }
        }

        public bool AllowAllRequestHeaders
        {
            get
            {
                return HighSecurityModeOverrides(false,
                    EnvironmentOverrides(_localConfiguration.allowAllHeaders.enabled,
                    "NEW_RELIC_ALLOW_ALL_HEADERS"));
            }
        }

        #region Attributes

        public virtual bool CaptureAttributes
        {
            get
            {
                return EnvironmentOverrides(_localConfiguration.attributes.enabled,
                    "NEW_RELIC_ATTRIBUTES_ENABLED");
            }
        }

        private BoolConfigurationItem _canUseAttributesIncludes;

        public string CanUseAttributesIncludesSource
        {
            get
            {
                _canUseAttributesIncludes ??= GetCanUseAttributesIncludesConfiguration();

                return _canUseAttributesIncludes.Source;
            }
        }

        public virtual bool CanUseAttributesIncludes
        {
            get
            {
                _canUseAttributesIncludes ??= GetCanUseAttributesIncludesConfiguration();

                return _canUseAttributesIncludes.Value;
            }
        }

        private BoolConfigurationItem GetCanUseAttributesIncludesConfiguration()
        {
            if (HighSecurityModeEnabled)
            {
                return new BoolConfigurationItem(false, HighSecurityConfigSource);
            }

            if (_securityPoliciesConfiguration.SecurityPolicyExistsFor(SecurityPoliciesConfiguration.AttributesIncludePolicyName)
                && (_securityPoliciesConfiguration.AttributesInclude.Enabled == false))
            {
                return new BoolConfigurationItem(false, SecurityPolicyConfigSource);
            }

            return new BoolConfigurationItem(CaptureAttributes, LocalConfigSource);
        }

        private IEnumerable<string> _captureAttributesIncludes;

        public virtual IEnumerable<string> CaptureAttributesIncludes =>
            CanUseAttributesIncludes
                ? _captureAttributesIncludes ??=
                    [..EnvironmentOverrides(_localConfiguration.attributes.include, "NEW_RELIC_ATTRIBUTES_INCLUDE")]
                : _captureAttributesIncludes ??= [];

        private IEnumerable<string> _captureAttributesExcludes;
        public virtual IEnumerable<string> CaptureAttributesExcludes => _captureAttributesExcludes ??= [..EnvironmentOverrides(_localConfiguration.attributes.exclude, "NEW_RELIC_ATTRIBUTES_EXCLUDE")];

        private IEnumerable<string> _captureAttributesDefaultExcludes;
        public virtual IEnumerable<string> CaptureAttributesDefaultExcludes => _captureAttributesDefaultExcludes ??= ["identity.*"];

        private bool IsAttributesAllowedByConfigurableSecurityPolicy
        {
            get
            {
                if (HighSecurityModeEnabled) return false;

                if (_securityPoliciesConfiguration.SecurityPolicyExistsFor(SecurityPoliciesConfiguration.AttributesIncludePolicyName))
                {
                    return _securityPoliciesConfiguration.AttributesInclude.Enabled;
                }

                return true;
            }
        }

        public virtual bool TransactionEventsAttributesEnabled => CaptureAttributes && _localConfiguration.transactionEvents.attributes.enabled;

        private HashSet<string> _transactionEventsAttributesInclude;
        public HashSet<string> TransactionEventsAttributesInclude =>
            _transactionEventsAttributesInclude ??= IsAttributesAllowedByConfigurableSecurityPolicy && TransactionEventsAttributesEnabled
                ? [.._localConfiguration.transactionEvents.attributes.include]
                : [];

        private HashSet<string> _transactionEventsAttributesExclude;
        public HashSet<string> TransactionEventsAttributesExclude =>
            _transactionEventsAttributesExclude ??= [.._localConfiguration.transactionEvents.attributes.exclude];

        public virtual bool CaptureTransactionTraceAttributes => ShouldCaptureTransactionTraceAttributes();

        private bool ShouldCaptureTransactionTraceAttributes()
        {
            if (_localConfiguration.attributes.enabled == false)
            {
                return false;
            }

            if (_localConfiguration.transactionTracer.attributes.enabledSpecified)
            {
                return _localConfiguration.transactionTracer.attributes.enabled;
            }

            return CaptureTransactionTraceAttributesDefault;
        }

        private IEnumerable<string> _captureTransactionTraceAttributesIncludes;

        public virtual IEnumerable<string> CaptureTransactionTraceAttributesIncludes =>
            ShouldCaptureTransactionTraceAttributesIncludes()
                ? _captureTransactionTraceAttributesIncludes ??= [.._localConfiguration.transactionTracer.attributes.include]
                : _captureTransactionTraceAttributesIncludes ??= [];

        private bool? _shouldCaptureTransactionTraceAttributesIncludes;
        private bool ShouldCaptureTransactionTraceAttributesIncludes()
        {
            if (_shouldCaptureTransactionTraceAttributesIncludes.HasValue)
            {
                return _shouldCaptureTransactionTraceAttributesIncludes.Value;
            }

            var shouldCapture = !HighSecurityModeEnabled && CaptureTransactionTraceAttributes;

            if (_securityPoliciesConfiguration.SecurityPolicyExistsFor(SecurityPoliciesConfiguration.AttributesIncludePolicyName))
            {
                shouldCapture = shouldCapture && _securityPoliciesConfiguration.AttributesInclude.Enabled;
            }

            _shouldCaptureTransactionTraceAttributesIncludes = shouldCapture;

            return _shouldCaptureTransactionTraceAttributesIncludes.Value;
        }

        private IEnumerable<string> _captureTransactionTraceAttributesExcludes;
        public virtual IEnumerable<string> CaptureTransactionTraceAttributesExcludes =>
            _captureTransactionTraceAttributesExcludes ??= [.._localConfiguration.transactionTracer.attributes.exclude];


        public virtual bool CaptureErrorCollectorAttributes => ShouldCaptureErrorCollectorAttributes();

        private bool ShouldCaptureErrorCollectorAttributes()
        {
            if (_localConfiguration.attributes.enabled == false)
            {
                return false;
            }

            if (_localConfiguration.errorCollector.attributes.enabledSpecified)
            {
                return _localConfiguration.errorCollector.attributes.enabled;
            }

            return CaptureErrorCollectorAttributesDefault;
        }


        private IEnumerable<string> _captureErrorCollectorAttributesIncludes;
        public virtual IEnumerable<string> CaptureErrorCollectorAttributesIncludes =>
            ShouldCaptureErrorCollectorAttributesIncludes()
                ? _captureErrorCollectorAttributesIncludes ??= [.. _localConfiguration.errorCollector.attributes.include]
                : _captureErrorCollectorAttributesExcludes ??= [];

        private bool? _shouldCaptureErrorCollectorAttributesIncludes;
        private bool ShouldCaptureErrorCollectorAttributesIncludes()
        {
            if (_shouldCaptureErrorCollectorAttributesIncludes.HasValue)
                return _shouldCaptureErrorCollectorAttributesIncludes.Value;

            var shouldCapture = !HighSecurityModeEnabled && CaptureErrorCollectorAttributes;

            if (_securityPoliciesConfiguration.SecurityPolicyExistsFor(SecurityPoliciesConfiguration.AttributesIncludePolicyName))
            {
                shouldCapture = shouldCapture && _securityPoliciesConfiguration.AttributesInclude.Enabled;
            }

            _shouldCaptureErrorCollectorAttributesIncludes = shouldCapture;
            return _shouldCaptureErrorCollectorAttributesIncludes.Value;
        }

        private IEnumerable<string> _captureErrorCollectorAttributesExcludes;
        public virtual IEnumerable<string> CaptureErrorCollectorAttributesExcludes => _captureErrorCollectorAttributesExcludes ??= [.. _localConfiguration.errorCollector.attributes.exclude];

        public virtual bool CaptureBrowserMonitoringAttributes => ShouldCaptureBrowserMonitorAttributes();

        private bool ShouldCaptureBrowserMonitorAttributes()
        {
            if (_localConfiguration.attributes.enabled == false)
            {
                return false;
            }

            if (_localConfiguration.browserMonitoring.attributes.enabledSpecified)
            {
                return _localConfiguration.browserMonitoring.attributes.enabled;
            }

            return CaptureBrowserMonitoringAttributesDefault;
        }

        private IEnumerable<string> _captureBrowserMonitoringAttributesIncludes;
        public virtual IEnumerable<string> CaptureBrowserMonitoringAttributesIncludes =>
            ShouldCaptureBrowserMonitoringAttributesIncludes()
                ? _captureBrowserMonitoringAttributesIncludes ??= [.. _localConfiguration.browserMonitoring.attributes.include]
                : _captureBrowserMonitoringAttributesIncludes ??= [];

        private bool? _shouldCaptureBrowserMonitoringAttributesIncludes;
        private bool ShouldCaptureBrowserMonitoringAttributesIncludes()
        {
            if (_shouldCaptureBrowserMonitoringAttributesIncludes.HasValue)
                return _shouldCaptureBrowserMonitoringAttributesIncludes.Value;

            var shouldCapture = !HighSecurityModeEnabled && CaptureBrowserMonitoringAttributes;

            if (_securityPoliciesConfiguration.SecurityPolicyExistsFor(SecurityPoliciesConfiguration.AttributesIncludePolicyName))
            {
                shouldCapture = shouldCapture && _securityPoliciesConfiguration.AttributesInclude.Enabled;
            }

            _shouldCaptureBrowserMonitoringAttributesIncludes = shouldCapture;
            return _shouldCaptureBrowserMonitoringAttributesIncludes.Value;
        }

        private IEnumerable<string> _captureBrowserMonitoringAttributesExcludes;
        public virtual IEnumerable<string> CaptureBrowserMonitoringAttributesExcludes => _captureBrowserMonitoringAttributesExcludes ??=[.. _localConfiguration.browserMonitoring.attributes.exclude];


        private BoolConfigurationItem _shouldCaptureCustomParameters;

        public string CaptureCustomParametersSource
        {
            get
            {
                _shouldCaptureCustomParameters ??= GetShouldCaptureCustomParametersConfiguration();

                return _shouldCaptureCustomParameters.Source;
            }
        }

        public virtual bool CaptureCustomParameters
        {
            get
            {
                _shouldCaptureCustomParameters ??= GetShouldCaptureCustomParametersConfiguration();

                return _shouldCaptureCustomParameters.Value;
            }
        }

        private BoolConfigurationItem GetShouldCaptureCustomParametersConfiguration()
        {
            if (HighSecurityModeEnabled)
            {
                return new BoolConfigurationItem(false, HighSecurityConfigSource);
            }

            if (_securityPoliciesConfiguration.SecurityPolicyExistsFor(SecurityPoliciesConfiguration.CustomParametersPolicyName)
                && (_securityPoliciesConfiguration.CustomParameters.Enabled == false))
            {
                return new BoolConfigurationItem(false, SecurityPolicyConfigSource);
            }

            var localConfigValue = GetLocalShouldCaptureCustomParameters();

            return new BoolConfigurationItem(localConfigValue, LocalConfigSource);
        }

        private bool GetLocalShouldCaptureCustomParameters()
        {
            return _localConfiguration.customParameters.enabledSpecified ? _localConfiguration.customParameters.enabled : CaptureCustomParametersAttributesDefault;
        }

        #endregion

        #region Collector Connection

        public virtual string CollectorHost { get { return EnvironmentOverrides(_localConfiguration.service.host, @"NEW_RELIC_HOST"); } }
        public virtual int CollectorPort => EnvironmentOverrides(_localConfiguration.service.port > 0 ? _localConfiguration.service.port : (int?)null, "NEW_RELIC_PORT") ?? DefaultSslPort;
        public virtual bool CollectorSendDataOnExit { get { return EnvironmentOverrides(_localConfiguration.service.sendDataOnExit, "NEW_RELIC_SEND_DATA_ON_EXIT"); } }
        public virtual float CollectorSendDataOnExitThreshold { get { return EnvironmentOverrides((uint?)null, "NEW_RELIC_SEND_DATA_ON_EXIT_THRESHOLD_MS") ?? _localConfiguration.service.sendDataOnExitThreshold; } }
        public virtual bool CollectorSendEnvironmentInfo { get { return _localConfiguration.service.sendEnvironmentInfo; } }
        public virtual bool CollectorSyncStartup { get { return _localConfiguration.service.syncStartup; } }
        public virtual uint CollectorTimeout { get { return (_localConfiguration.service.requestTimeout > 0) ? (uint)_localConfiguration.service.requestTimeout : CollectorSendDataOnExit ? 2000u : 60 * 2 * 1000; } }
        public virtual int CollectorMaxPayloadSizeInBytes { get { return _serverConfiguration.MaxPayloadSizeInBytes ?? MaxPayloadSizeInBytes; } }
        #endregion

        public virtual bool CompleteTransactionsOnThread { get { return _localConfiguration.service.completeTransactionsOnThread || ServerlessModeEnabled; } }

        public long ConfigurationVersion { get; private set; }

        #region Cross Application Tracing

        public virtual string CrossApplicationTracingCrossProcessId { get { return _serverConfiguration.CatId; } }

        private bool? _crossApplicationTracingEnabled;
        public virtual bool CrossApplicationTracingEnabled => _crossApplicationTracingEnabled ?? (_crossApplicationTracingEnabled = IsCatEnabled()).Value;

        private bool IsCatEnabled()
        {
            // CAT must be disabled in serverless mode
            if (ServerlessModeEnabled)
                return false;

            var localenabled = _localConfiguration.crossApplicationTracingEnabled;
            //If config.crossApplicationTracingEnabled is true or default then we want to check the
            //config.crossApplicationTracer, if that object is not null then use it's default or value
            if (localenabled && _localConfiguration.crossApplicationTracer != null)
            {
                localenabled = _localConfiguration.crossApplicationTracer.enabled;
            }

            var enabled = ServerCanDisable(_serverConfiguration.RpmConfig.CrossApplicationTracerEnabled, localenabled);

            if (enabled && CrossApplicationTracingCrossProcessId == null)
            {
                Log.Warn("CAT is enabled but CrossProcessID is null. Disabling CAT.");
                enabled = false;
            }

            return enabled;
        }

        #endregion Cross Application Tracing

        #region Span Events

        bool? _spanEventsEnabled = null;
        public virtual bool SpanEventsEnabled
        {
            get
            {
                if (!_spanEventsEnabled.HasValue)
                {
                    var enabled = ServerCanDisable(_serverConfiguration.SpanEventCollectionEnabled, EnvironmentOverrides(_localConfiguration.spanEvents.enabled, "NEW_RELIC_SPAN_EVENTS_ENABLED"));
                    _spanEventsEnabled = enabled && DistributedTracingEnabled;
                }

                return _spanEventsEnabled.Value;
            }
        }

        private TimeSpan? _spanEventsHarvestCycleOverride = null;
        public TimeSpan SpanEventsHarvestCycle
        {
            get
            {
                if (_spanEventsHarvestCycleOverride.HasValue)
                {
                    return _spanEventsHarvestCycleOverride.Value;
                }

                if (_newRelicAppSettings.TryGetValue("OverrideSpanEventsHarvestCycle", out var harvestCycle))
                {
                    if (int.TryParse(harvestCycle, out var parsedHarvestCycle) && parsedHarvestCycle > 0)
                    {
                        Log.Info("Span events harvest cycle overridden to " + parsedHarvestCycle + " seconds.");
                        _spanEventsHarvestCycleOverride = TimeSpan.FromSeconds(parsedHarvestCycle);
                        return _spanEventsHarvestCycleOverride.Value;
                    }
                }

                return ServerOverrides(_serverConfiguration.SpanEventHarvestConfig?.HarvestCycle, DefaultHarvestCycle);
            }
        }

        public bool SpanEventsAttributesEnabled => CaptureAttributes && _localConfiguration.spanEvents.attributes.enabled;

        private HashSet<string> _spanEventsAttributesInclude;
        public HashSet<string> SpanEventsAttributesInclude =>
            _spanEventsAttributesInclude ??= IsAttributesAllowedByConfigurableSecurityPolicy && SpanEventsAttributesEnabled
                ? [.._localConfiguration.spanEvents.attributes.include]
                : [];

        private HashSet<string> _spanEventsAttributesExclude;
        public virtual HashSet<string> SpanEventsAttributesExclude => _spanEventsAttributesExclude ??= [.._localConfiguration.spanEvents.attributes.exclude];

        #endregion

        #region Distributed Tracing

        private bool? _distributedTracingEnabled;
        public virtual bool DistributedTracingEnabled => _distributedTracingEnabled ?? (_distributedTracingEnabled = IsDistributedTracingEnabled()).Value;

        private bool IsDistributedTracingEnabled()
        {
            return EnvironmentOverrides(_localConfiguration.distributedTracing.enabled, "NEW_RELIC_DISTRIBUTED_TRACING_ENABLED");
        }

        public string PrimaryApplicationId => ServerlessModeEnabled ?
            EnvironmentOverrides(_localConfiguration.distributedTracing.primary_application_id, "NEW_RELIC_PRIMARY_APPLICATION_ID")
            : _serverConfiguration.PrimaryApplicationId;

        public string TrustedAccountKey => ServerlessModeEnabled ?
            EnvironmentOverrides(_localConfiguration.distributedTracing.trusted_account_key, "NEW_RELIC_TRUSTED_ACCOUNT_KEY")
            : _serverConfiguration.TrustedAccountKey;

        public string AccountId => ServerlessModeEnabled ?
            EnvironmentOverrides(_localConfiguration.distributedTracing.account_id, "NEW_RELIC_ACCOUNT_ID")
            : _serverConfiguration.AccountId;

        public int? SamplingTarget => ServerlessModeEnabled ? 10 : _serverConfiguration.SamplingTarget;

        // Faster Event Harvest configuration rules apply here, which is why ServerOverrides takes precedence over EnvironmentOverrides
        public int SpanEventsMaxSamplesStored => ServerOverrides(_serverConfiguration.SpanEventHarvestConfig?.HarvestLimit,
           EnvironmentOverrides(_localConfiguration.spanEvents.maximumSamplesStored, "NEW_RELIC_SPAN_EVENTS_MAX_SAMPLES_STORED").GetValueOrDefault());

        public int? SamplingTargetPeriodInSeconds => ServerlessModeEnabled ? 60 : _serverConfiguration.SamplingTargetPeriodInSeconds;

        public bool PayloadSuccessMetricsEnabled => _localConfiguration.distributedTracing.enableSuccessMetrics;

        public bool ExcludeNewrelicHeader => _localConfiguration.distributedTracing.excludeNewrelicHeader;

        public RemoteParentSampledBehavior RemoteParentSampledBehavior => EnvironmentOverrides(_localConfiguration.distributedTracing.sampler.remoteParentSampled.ToString(), "NEW_RELIC_DISTRIBUTED_TRACING_SAMPLER_REMOTE_PARENT_SAMPLED").ToRemoteParentSampledBehavior();

        public RemoteParentSampledBehavior RemoteParentNotSampledBehavior => EnvironmentOverrides(_localConfiguration.distributedTracing.sampler.remoteParentNotSampled.ToString(), "NEW_RELIC_DISTRIBUTED_TRACING_SAMPLER_REMOTE_PARENT_NOT_SAMPLED").ToRemoteParentSampledBehavior();

        #endregion Distributed Tracing

        #region Infinite Tracing

        private int? _infiniteTracingTimeoutMsConnect = null;
        public int InfiniteTracingTraceTimeoutMsConnect => (_infiniteTracingTimeoutMsConnect
            ?? (_infiniteTracingTimeoutMsConnect = EnvironmentOverrides(TryGetAppSettingAsIntWithDefault("InfiniteTracingTimeoutConnect", 10000), "NEW_RELIC_INFINITE_TRACING_TIMEOUT_CONNECT")).Value);

        private int? _infiniteTracingTimeoutMsSendData = null;
        public int InfiniteTracingTraceTimeoutMsSendData => (_infiniteTracingTimeoutMsSendData
            ?? (_infiniteTracingTimeoutMsSendData = EnvironmentOverrides(
                TryGetAppSettingAsIntWithDefault("InfiniteTracingTimeoutSend", 10000)
                , "NEW_RELIC_INFINITE_TRACING_TIMEOUT_SEND")).Value);

        private int? _infiniteTracingExitTimeoutMs = null;
        public int InfiniteTracingExitTimeoutMs => GetInfiniteTracingExitTimeoutMs();
        private int GetInfiniteTracingExitTimeoutMs()
        {
            const int infiniteTracingExitTimeoutMsDefault = 5000;
            return _infiniteTracingExitTimeoutMs
                ?? (_infiniteTracingExitTimeoutMs = EnvironmentOverrides(TryGetAppSettingAsIntWithDefault("InfiniteTracingExitTimeout", infiniteTracingExitTimeoutMsDefault), "NEW_RELIC_INFINITE_TRACING_EXIT_TIMEOUT")).GetValueOrDefault();
        }

        private int? _infiniteTracingCountWorkers = null;
        public int InfiniteTracingTraceCountConsumers => GetInfiniteTracingCountWorkers();

        private int GetInfiniteTracingCountWorkers()
        {
            const int countWorkersDefault = 10;
            return _infiniteTracingCountWorkers
                ?? (_infiniteTracingCountWorkers = EnvironmentOverrides(TryGetAppSettingAsIntWithDefault("InfiniteTracingSpanEventsStreamsCount", countWorkersDefault), "NEW_RELIC_INFINITE_TRACING_SPAN_EVENTS_STREAMS_COUNT")).GetValueOrDefault();
        }


        private bool _infiniteTracingObserverObtained;
        private void GetInfiniteTracingObserver()
        {
            _infiniteTracingTraceObserverHost = _environment.GetEnvironmentVariable("NEW_RELIC_INFINITE_TRACING_TRACE_OBSERVER_HOST");
            if (_infiniteTracingTraceObserverHost != null)
            {
                _infiniteTracingTraceObserverPort = _environment.GetEnvironmentVariable("NEW_RELIC_INFINITE_TRACING_TRACE_OBSERVER_PORT");
                _infiniteTracingTraceObserverSsl = _environment.GetEnvironmentVariable("NEW_RELIC_INFINITE_TRACING_TRACE_OBSERVER_SSL");
            }
            else
            {
                _infiniteTracingTraceObserverHost = _localConfiguration.infiniteTracing?.trace_observer?.host;
                _infiniteTracingTraceObserverPort = _localConfiguration.infiniteTracing?.trace_observer?.port;
                _infiniteTracingTraceObserverSsl = TryGetAppSettingAsString("InfiniteTracingTraceObserverSsl");
            }

            _infiniteTracingObserverObtained = true;
        }

        private string _infiniteTracingTraceObserverHost = null;
        public string InfiniteTracingTraceObserverHost
        {
            get
            {
                if (!_infiniteTracingObserverObtained)
                {
                    GetInfiniteTracingObserver();
                }

                return _infiniteTracingTraceObserverHost;
            }
        }

        private string _infiniteTracingTraceObserverPort = null;
        public string InfiniteTracingTraceObserverPort
        {
            get
            {
                if (!_infiniteTracingObserverObtained)
                {
                    GetInfiniteTracingObserver();
                }

                return _infiniteTracingTraceObserverPort;
            }
        }

        private string _infiniteTracingTraceObserverSsl = null;
        public string InfiniteTracingTraceObserverSsl
        {
            get
            {
                if (!_infiniteTracingObserverObtained)
                {
                    GetInfiniteTracingObserver();
                }

                return _infiniteTracingTraceObserverSsl;
            }
        }


        private int? _infiniteTracingQueueSizeSpans;
        public int InfiniteTracingQueueSizeSpans => GetInfiniteTracingQueueSizeSpans();

        private int GetInfiniteTracingQueueSizeSpans()
        {
            return _infiniteTracingQueueSizeSpans
                ?? (_infiniteTracingQueueSizeSpans = EnvironmentOverrides(_localConfiguration.infiniteTracing?.span_events?.queue_size, "NEW_RELIC_INFINITE_TRACING_SPAN_EVENTS_QUEUE_SIZE"))
                .GetValueOrDefault(100000);
        }

        private int? _infiniteTracingPartitionCountSpans;
        public int InfiniteTracingPartitionCountSpans => _infiniteTracingPartitionCountSpans
                ?? (_infiniteTracingPartitionCountSpans = EnvironmentOverrides(TryGetAppSettingAsInt("InfiniteTracingSpanEventsPartitionCount"), "NEW_RELIC_INFINITE_TRACING_SPAN_EVENTS_PARTITION_COUNT").GetValueOrDefault(62)).Value;

        private int? _infiniteTracingBatchSizeSpans;
        public int InfiniteTracingBatchSizeSpans => _infiniteTracingBatchSizeSpans
                ?? (_infiniteTracingBatchSizeSpans = EnvironmentOverrides(TryGetAppSettingAsInt("InfiniteTracingSpanEventsBatchSize"), "NEW_RELIC_INFINITE_TRACING_SPAN_EVENTS_BATCH_SIZE").GetValueOrDefault(700)).Value;

        private bool _infiniteTracingObtainedSettingsForTest;
        private void GetInfiniteTracingFlakyAndDelayTestSettings()
        {

            if (float.TryParse(_environment.GetEnvironmentVariable("NEW_RELIC_INFINITE_TRACING_SPAN_EVENTS_TEST_FLAKY"), out var flakyVal))
            {
                _infiniteTracingObserverTestFlaky = flakyVal;
            }
            else
            {
                _infiniteTracingObserverTestFlaky = TryGetAppSettingAsFloat("InfiniteTracingSpanEventsTestFlaky");
            }

            if (int.TryParse(_environment.GetEnvironmentVariable("NEW_RELIC_INFINITE_TRACING_SPAN_EVENTS_TEST_FLAKY_CODE"), out var flakyCodeVal))
            {
                _infiniteTracingObserverTestFlakyCode = flakyCodeVal;
            }
            else
            {
                _infiniteTracingObserverTestFlakyCode = TryGetAppSettingAsInt("InfiniteTracingSpanEventsTestFlakyCode");
            }

            if (int.TryParse(_environment.GetEnvironmentVariable("NEW_RELIC_INFINITE_TRACING_SPAN_EVENTS_TEST_DELAY"), out var delayVal))
            {
                _infiniteTracingObserverTestDelayMs = delayVal;
            }
            else
            {
                _infiniteTracingObserverTestDelayMs = TryGetAppSettingAsInt("InfiniteTracingSpanEventsTestDelay");
            }

            _infiniteTracingObtainedSettingsForTest = true;
        }

        private float? _infiniteTracingObserverTestFlaky;
        public float? InfiniteTracingTraceObserverTestFlaky
        {
            get
            {
                if (!_infiniteTracingObtainedSettingsForTest)
                {
                    GetInfiniteTracingFlakyAndDelayTestSettings();
                }

                return _infiniteTracingObserverTestFlaky;
            }
        }

        private int? _infiniteTracingObserverTestFlakyCode;
        public int? InfiniteTracingTraceObserverTestFlakyCode
        {
            get
            {
                if (!_infiniteTracingObtainedSettingsForTest)
                {
                    GetInfiniteTracingFlakyAndDelayTestSettings();
                }

                return _infiniteTracingObserverTestFlakyCode;
            }
        }

        private int? _infiniteTracingObserverTestDelayMs;
        public int? InfiniteTracingTraceObserverTestDelayMs
        {
            get
            {
                if (!_infiniteTracingObtainedSettingsForTest)
                {
                    GetInfiniteTracingFlakyAndDelayTestSettings();
                }

                return _infiniteTracingObserverTestDelayMs;
            }
        }

        private bool? _infiniteTracingCompression;
        public bool InfiniteTracingCompression
        {
            get
            {
                if (!_infiniteTracingCompression.HasValue)
                {
                    _infiniteTracingCompression = EnvironmentOverrides(_localConfiguration.infiniteTracing.compression, "NEW_RELIC_INFINITE_TRACING_COMPRESSION");
                }
                return _infiniteTracingCompression.Value;
            }
        }




        #endregion

        #region Errors

        public virtual bool ErrorCollectorEnabled
        {
            get
            {
                var configuredValue = ServerOverrides(_serverConfiguration.RpmConfig.ErrorCollectorEnabled, _localConfiguration.errorCollector.enabled);
                return ServerCanDisable(_serverConfiguration.ErrorCollectionEnabled, configuredValue);
            }
        }

        public virtual bool ErrorCollectorCaptureEvents
        {
            get
            {
                if (ErrorCollectorMaxEventSamplesStored == 0)
                {
                    return false;
                }
                return ServerCanDisable(_serverConfiguration.ErrorEventCollectionEnabled, _localConfiguration.errorCollector.captureEvents);
            }
        }

        public virtual int ErrorCollectorMaxEventSamplesStored
        {
            get
            {
                // Faster Event Harvest configuration rules apply here, if/when we add an environment variable for this property we need to make sure that
                // ServerOverrides takes precedence over EnvironmentOverrides
                return ServerOverrides(_serverConfiguration.EventHarvestConfig?.ErrorEventHarvestLimit(), _localConfiguration.errorCollector.maxEventSamplesStored);
            }
        }

        public TimeSpan ErrorEventsHarvestCycle
        {
            get
            {
                return ServerOverrides(_serverConfiguration.EventHarvestConfig?.ErrorEventHarvestCycle(), TimeSpan.FromMinutes(1));
            }
        }

        public virtual uint ErrorsMaximumPerPeriod { get { return 20; } }

        public IDictionary<string, IEnumerable<string>> IgnoreErrorsConfiguration { get; private set; }
        public IEnumerable<string> IgnoreErrorClassesForAgentSettings { get; private set; }
        public IDictionary<string, IEnumerable<string>> IgnoreErrorMessagesForAgentSettings { get; private set; }

        private IEnumerable<MatchRule> ParseExpectedStatusCodesArray(IEnumerable<string> expectedStatusCodeArray)
        {
            var expectedStatusCodes = new List<MatchRule>();

            if (expectedStatusCodeArray == null)
            {
                return expectedStatusCodes;
            }

            foreach (var singleCodeOrRange in expectedStatusCodeArray)
            {
                MatchRule matchRule;
                var index = singleCodeOrRange.IndexOf(HyphenChar);
                if (index != -1)
                {
                    var lowerBoundString = singleCodeOrRange.Substring(0, index).Trim();
                    var upperBoundString = singleCodeOrRange.Substring(index + 1).Trim();

                    matchRule = StatusCodeInRangeMatchRule.GenerateRule(lowerBoundString, upperBoundString);
                }
                else
                {
                    matchRule = StatusCodeExactMatchRule.GenerateRule(singleCodeOrRange);
                }

                if (matchRule == null)
                {
                    Log.Warn($"Cannot parse {singleCodeOrRange} status code. This status code format is not supported.");
                    continue;
                }

                expectedStatusCodes.Add(matchRule);
            }

            return expectedStatusCodes;
        }

        public IDictionary<string, IEnumerable<string>> ExpectedErrorsConfiguration { get; private set; }
        public IEnumerable<MatchRule> ExpectedStatusCodes { get; private set; }
        public IEnumerable<string> ExpectedErrorClassesForAgentSettings { get; private set; }
        public IDictionary<string, IEnumerable<string>> ExpectedErrorMessagesForAgentSettings { get; private set; }
        public IEnumerable<string> ExpectedErrorStatusCodesForAgentSettings { get; private set; }

        public Func<IReadOnlyDictionary<string, object>, string> ErrorGroupCallback => _runTimeConfiguration.ErrorGroupCallback;

        #endregion

        public Dictionary<string, string> RequestHeadersMap => _serverConfiguration.RequestHeadersMap;

        public virtual string EncodingKey { get { return _serverConfiguration.EncodingKey; } }

        public virtual string EntityGuid { get { return _serverConfiguration.EntityGuid; } }

        private bool? _highSecurityModeEnabled;
        public virtual bool HighSecurityModeEnabled
        {
            get
            {
                _highSecurityModeEnabled ??= EnvironmentOverrides(_localConfiguration.highSecurity.enabled, "NEW_RELIC_HIGH_SECURITY");

                return _highSecurityModeEnabled.Value;
            }
        }


        private BoolConfigurationItem _customInstrumentationEditorIsEnabled;

        public string CustomInstrumentationEditorEnabledSource
        {
            get
            {
                if (_customInstrumentationEditorIsEnabled == null)
                {
                    _customInstrumentationEditorIsEnabled = GetCustomInstrumentationEditorConfiguration();
                }

                return _customInstrumentationEditorIsEnabled.Source;
            }
        }

        public virtual bool CustomInstrumentationEditorEnabled
        {
            get
            {
                if (_customInstrumentationEditorIsEnabled == null)
                {
                    _customInstrumentationEditorIsEnabled = GetCustomInstrumentationEditorConfiguration();
                }

                return _customInstrumentationEditorIsEnabled.Value;
            }
        }

        private BoolConfigurationItem GetCustomInstrumentationEditorConfiguration()
        {
            if (HighSecurityModeEnabled)
            {
                return new BoolConfigurationItem(false, HighSecurityConfigSource);
            }

            if (_securityPoliciesConfiguration.SecurityPolicyExistsFor(SecurityPoliciesConfiguration.CustomInstrumentationEditorPolicyName)
                && (_securityPoliciesConfiguration.CustomInstrumentationEditor.Enabled == false))
            {
                return new BoolConfigurationItem(false, SecurityPolicyConfigSource);
            }

            return new BoolConfigurationItem(_localConfiguration.customInstrumentationEditor.enabled, LocalConfigSource);
        }

        private BoolConfigurationItem _shouldStripExceptionMessages;

        public string StripExceptionMessagesSource
        {
            get
            {
                if (_shouldStripExceptionMessages == null)
                {
                    _shouldStripExceptionMessages = GetShouldStripExceptionMessagesConfiguration();
                }

                return _shouldStripExceptionMessages.Source;
            }
        }

        public virtual bool StripExceptionMessages
        {
            get
            {
                if (_shouldStripExceptionMessages == null)
                {
                    _shouldStripExceptionMessages = GetShouldStripExceptionMessagesConfiguration();
                }

                return _shouldStripExceptionMessages.Value;
            }
        }

        private BoolConfigurationItem GetShouldStripExceptionMessagesConfiguration()
        {
            // true is the more secure state, which will remove raw exception messages

            if (HighSecurityModeEnabled)
            {
                return new BoolConfigurationItem(true, HighSecurityConfigSource);
            }

            if (_securityPoliciesConfiguration.SecurityPolicyExistsFor(SecurityPoliciesConfiguration.AllowRawExceptionMessagePolicyName)
                && (_securityPoliciesConfiguration.AllowRawExceptionMessage.Enabled == false))
            {
                return new BoolConfigurationItem(true, SecurityPolicyConfigSource);
            }

            return new BoolConfigurationItem(_localConfiguration.stripExceptionMessages.enabled, LocalConfigSource);
        }

        public virtual bool InstrumentationLoggingEnabled { get { return _localConfiguration.instrumentation.log; } }

        #region Labels

        private string _labels;
        private bool _labelsChecked;
        public virtual string Labels
        {
            get
            {
                if (!_labelsChecked)
                {
                    var labels = _configurationManagerStatic.GetAppSetting(Constants.AppSettingsLabels);
                    if (labels != null)
                    {
                        Log.Info("Application labels from web.config, app.config, or appsettings.json.");
                        _labels = labels;
                    }
                    else
                    {
                        _labels = EnvironmentOverrides(_localConfiguration.labels, @"NEW_RELIC_LABELS");
                    }
                    _labelsChecked = true;
                }
                return _labels;
            }
        }

        #endregion

        #region Proxy

        public virtual string ProxyHost
        {
            get
            {
                return EnvironmentOverrides(_localConfiguration.service.proxy.host, "NEW_RELIC_PROXY_HOST");
            }
        }

        public virtual string ProxyUriPath
        {
            get
            {
                return EnvironmentOverrides(_localConfiguration.service.proxy.uriPath, "NEW_RELIC_PROXY_URI_PATH");
            }
        }

        public virtual int ProxyPort
        {
            get
            {
                return EnvironmentOverrides(_localConfiguration.service.proxy.port, "NEW_RELIC_PROXY_PORT").Value;
            }
        }

        public virtual string ProxyUsername
        {
            get
            {
                return EnvironmentOverrides(_localConfiguration.service.proxy.user, "NEW_RELIC_PROXY_USER");
            }
        }


        private bool _obscuringKeyEvaluated;
        private string _obscuringKey;
        public string ObscuringKey
        {
            get
            {
                if (!_obscuringKeyEvaluated)
                {
                    _obscuringKey = EnvironmentOverrides(_localConfiguration.service.obscuringKey, "NEW_RELIC_CONFIG_OBSCURING_KEY");
                    _obscuringKeyEvaluated = true;
                }

                return _obscuringKey;
            }
        }

        private bool _proxyPasswordEvaluated;
        private string _proxyPassword;
        public virtual string ProxyPassword
        {
            get
            {
                if (!_proxyPasswordEvaluated)
                {
                    var hasObscuringKey = !string.IsNullOrWhiteSpace(ObscuringKey);
                    var passwordObfuscated = EnvironmentOverrides(_localConfiguration.service.proxy.passwordObfuscated, "NEW_RELIC_PROXY_PASS_OBFUSCATED");
                    var hasObfuscatedPassword = !string.IsNullOrWhiteSpace(passwordObfuscated);

                    if (hasObscuringKey && hasObfuscatedPassword)
                    {
                        _proxyPassword = Strings.Base64Decode(passwordObfuscated, ObscuringKey);
                    }
                    else
                    {
                        _proxyPassword = EnvironmentOverrides(_localConfiguration.service.proxy.password, "NEW_RELIC_PROXY_PASS");
                    }

                    _proxyPasswordEvaluated = true;
                }

                return _proxyPassword;
            }
        }

        public virtual string ProxyDomain
        {
            get
            {
                return EnvironmentOverrides(_localConfiguration.service.proxy.domain, "NEW_RELIC_PROXY_DOMAIN") ?? string.Empty;
            }
        }

        #endregion

        #region DataTransmission
        public bool PutForDataSend { get { return _localConfiguration.dataTransmission.putForDataSend; } }

        public string CompressedContentEncoding
        {
            get
            {
                return _localConfiguration.dataTransmission.compressedContentEncoding.ToString();
            }
        }

        #endregion

        #region DatastoreTracer
        public bool InstanceReportingEnabled { get { return _localConfiguration.datastoreTracer.instanceReporting.enabled; } }

        public bool DatabaseNameReportingEnabled { get { return _localConfiguration.datastoreTracer.databaseNameReporting.enabled; } }

        public bool DatastoreTracerQueryParametersEnabled => _localConfiguration.datastoreTracer.queryParameters.enabled && TransactionTracerRecordSql == RawStringValue;

        #endregion

        #region Sql

        public bool SlowSqlEnabled => ServerOverrides(_serverConfiguration.RpmConfig.SlowSqlEnabled, _localConfiguration.slowSql.enabled);

        public virtual TimeSpan SqlExplainPlanThreshold
        {
            get
            {
                var serverThreshold = _serverConfiguration.RpmConfig.TransactionTracerExplainThreshold;

                return serverThreshold.HasValue ?
                    TimeSpan.FromSeconds(serverThreshold.Value) :
                    TimeSpan.FromMilliseconds(_localConfiguration.transactionTracer.explainThreshold);
            }
        }

        public virtual bool SqlExplainPlansEnabled =>
            ServerOverrides(_serverConfiguration.RpmConfig.TransactionTracerExplainEnabled,
                _localConfiguration.transactionTracer.explainEnabled);

        public virtual int SqlExplainPlansMax => _localConfiguration.transactionTracer.maxExplainPlans;
        public virtual uint SqlStatementsPerTransaction => 500;
        public virtual int SqlTracesPerPeriod => 10;

        #endregion

        public virtual int StackTraceMaximumFrames => _localConfiguration.maxStackTraceLines;
        public virtual IEnumerable<string> HttpStatusCodesToIgnore { get; private set; }

        public virtual IEnumerable<string> ThreadProfilingIgnoreMethods => _localConfiguration.threadProfiling ?? [];

        #region Custom Events


        private BoolConfigurationItem _customEventsAreEnabled;

        public string CustomEventsEnabledSource
        {
            get
            {
                _customEventsAreEnabled ??= GetCustomEventsAreEnabledConfiguration();

                return _customEventsAreEnabled.Source;
            }
        }

        public virtual bool CustomEventsEnabled
        {
            get
            {
                _customEventsAreEnabled ??= GetCustomEventsAreEnabledConfiguration();

                return _customEventsAreEnabled.Value;
            }
        }

        private BoolConfigurationItem GetCustomEventsAreEnabledConfiguration()
        {
            if (HighSecurityModeEnabled)
            {
                return new BoolConfigurationItem(false, HighSecurityConfigSource);
            }

            if (_securityPoliciesConfiguration.SecurityPolicyExistsFor(SecurityPoliciesConfiguration.CustomEventsPolicyName)
                && (_securityPoliciesConfiguration.CustomEvents.Enabled == false))
            {
                return new BoolConfigurationItem(false, SecurityPolicyConfigSource);
            }

            if (_serverConfiguration.CustomEventCollectionEnabled.HasValue
                && (_serverConfiguration.CustomEventCollectionEnabled.Value == false))
            {
                return new BoolConfigurationItem(false, ServerConfigSource);
            }

            if (CustomEventsMaximumSamplesStored == 0)
            {
                return new BoolConfigurationItem(false, $"{nameof(CustomEventsMaximumSamplesStored)} set to 0");
            }

            return new BoolConfigurationItem(_localConfiguration.customEvents.enabled, LocalConfigSource);
        }

        public virtual int CustomEventsMaximumSamplesStored
        {
            get
            {
                // Faster Event Harvest configuration rules apply here, which is why ServerOverrides takes precedence over EnvironmentOverrides
                var maxValue = _localConfiguration.customEvents.maximumSamplesStored;
                return ServerOverrides(_serverConfiguration.EventHarvestConfig?.CustomEventHarvestLimit(), (int)EnvironmentOverrides(maxValue, "MAX_EVENT_SAMPLES_STORED"));
            }
        }

        public TimeSpan CustomEventsHarvestCycle => ServerOverrides(_serverConfiguration.EventHarvestConfig?.CustomEventHarvestCycle(), TimeSpan.FromMinutes(1));

        public bool CustomEventsAttributesEnabled => CaptureAttributes && _localConfiguration.customEvents.attributes.enabled;


        private HashSet<string> _customEventsAttributesInclude;
        public HashSet<string> CustomEventsAttributesInclude =>
            _customEventsAttributesInclude ??= IsAttributesAllowedByConfigurableSecurityPolicy && CustomEventsAttributesEnabled
                ? [.._localConfiguration.customEvents.attributes.include]
                : [];

        private HashSet<string> _customEventsAttributesExclude;
        public HashSet<string> CustomEventsAttributesExclude => _customEventsAttributesExclude ??= [.._localConfiguration.customEvents.attributes.exclude];

        #endregion

        public bool DisableSamplers { get { return EnvironmentOverrides(_localConfiguration.application.disableSamplers, "NEW_RELIC_DISABLE_SAMPLERS"); } }

        public bool ThreadProfilingEnabled { get { return _localConfiguration.threadProfilingEnabled; } }

        #region Transaction Events

        public virtual bool TransactionEventsEnabled
        {
            get
            {
                return TransactionEventsMaximumSamplesStored > 0 && ServerCanDisable(
                    _serverConfiguration.AnalyticsEventCollectionEnabled,
                    _localConfiguration.transactionEvents.enabled);
            }
        }

        public virtual int TransactionEventsMaximumSamplesStored
        {
            get
            {
                // Faster Event Harvest configuration rules apply here, which is why ServerOverrides takes precedence over EnvironmentOverrides
                var maxValue = _localConfiguration.transactionEvents.maximumSamplesStored;

                return ServerOverrides(_serverConfiguration.EventHarvestConfig?.TransactionEventHarvestLimit(), (int)EnvironmentOverrides(maxValue, "MAX_TRANSACTION_SAMPLES_STORED"));
            }
        }

        public TimeSpan TransactionEventsHarvestCycle => ServerOverrides(_serverConfiguration.EventHarvestConfig?.TransactionEventHarvestCycle(), TimeSpan.FromMinutes(1));

        public virtual bool TransactionEventsTransactionsEnabled => _localConfiguration.transactionEvents.transactions.enabledSpecified ? _localConfiguration.transactionEvents.transactions.enabled : TransactionEventsTransactionsEnabledDefault;

        #endregion

        #region Transaction Tracer

        public virtual TimeSpan TransactionTraceApdexF => TransactionTraceApdexT.Multiply(4);

        public virtual TimeSpan TransactionTraceApdexT =>
            // get apdex_t from environment variable if running in serverless mode
            TimeSpan.FromSeconds(ServerlessModeEnabled ? 
                EnvironmentOverrides(0.5, "NEW_RELIC_APDEX_T").GetValueOrDefault() : ServerOverrides(_serverConfiguration.ApdexT, 0.5));

        public virtual TimeSpan TransactionTraceThreshold =>
            (_serverConfiguration.RpmConfig.TransactionTracerThreshold == null)
                ? ParseTransactionThreshold(_localConfiguration.transactionTracer.transactionThreshold, TimeSpan.FromMilliseconds)
                : ParseTransactionThreshold(_serverConfiguration.RpmConfig.TransactionTracerThreshold.ToString(), TimeSpan.FromSeconds);

        public virtual bool TransactionTracerEnabled
        {
            get
            {
                var configuredValue = ServerOverrides(_serverConfiguration.RpmConfig.TransactionTracerEnabled, _localConfiguration.transactionTracer.enabled);
                return ServerCanDisable(_serverConfiguration.TraceCollectionEnabled, configuredValue);
            }
        }
        public virtual int TransactionTracerMaxSegments => _localConfiguration.transactionTracer.maxSegments;

        private RecordSqlConfigurationItem _recordSqlConfiguration;

        public string TransactionTracerRecordSqlSource
        {
            get
            {
                _recordSqlConfiguration ??= GetRecordSqlConfiguration();

                return _recordSqlConfiguration.Source;
            }
        }

        public virtual string TransactionTracerRecordSql
        {
            get
            {
                _recordSqlConfiguration ??= GetRecordSqlConfiguration();

                return _recordSqlConfiguration.Value;
            }
        }

        private RecordSqlConfigurationItem GetRecordSqlConfiguration()
        {
            if (_securityPoliciesConfiguration.SecurityPolicyExistsFor(SecurityPoliciesConfiguration.RecordSqlPolicyName))
            {
                var localRecordSql = _localConfiguration.transactionTracer.recordSql;
                var serverConfigRecordSql = _serverConfiguration.RpmConfig.TransactionTracerRecordSql;

                // "raw" is never allowed with security policies
                var policyValue = _securityPoliciesConfiguration.RecordSql.Enabled ? ObfuscatedStringValue : OffStringValue;

                var mostRestrictiveConfiguration = new RecordSqlConfigurationItem(policyValue, SecurityPolicyConfigSource)
                    .ApplyIfMoreRestrictive(serverConfigRecordSql, ServerConfigSource)
                    .ApplyIfMoreRestrictive(localRecordSql, LocalConfigSource);

                return mostRestrictiveConfiguration;
            }

            var serverOrLocalConfiguration = GetServerOverrideOrLocalRecordSqlConfiguration();

            if (HighSecurityModeEnabled)
            {
                // "raw" is never allowed with high security
                var hsmConfiguration = new RecordSqlConfigurationItem(ObfuscatedStringValue, HighSecurityConfigSource)
                    .ApplyIfMoreRestrictive(serverOrLocalConfiguration);

                return hsmConfiguration;
            }

            return serverOrLocalConfiguration;
        }

        private RecordSqlConfigurationItem GetServerOverrideOrLocalRecordSqlConfiguration()
        {
            if (string.IsNullOrWhiteSpace(_serverConfiguration.RpmConfig.TransactionTracerRecordSql) == false)
            {
                return new RecordSqlConfigurationItem(_serverConfiguration.RpmConfig.TransactionTracerRecordSql, ServerConfigSource);
            }

            var localRecordSqlString = Enum.GetName(typeof(configurationTransactionTracerRecordSql), _localConfiguration.transactionTracer.recordSql);
            return new RecordSqlConfigurationItem(localRecordSqlString, LocalConfigSource);
        }

        public virtual int TransactionTracerMaxStackTraces => _localConfiguration.transactionTracer.maxStackTrace;

        private IList<Regex> _requestPathExclusionList;
        public virtual IEnumerable<Regex> RequestPathExclusionList => _requestPathExclusionList ??= ReadUrlBlacklist(_localConfiguration);


        #endregion

        public virtual IEnumerable<long> TrustedAccountIds => _serverConfiguration.TrustedIds ?? [];

        public bool ServerSideConfigurationEnabled => _serverConfiguration.ServerSideConfigurationEnabled;

        #region Metric naming

        private IEnumerable<RegexRule> _metricNameRegexRules;
        public IEnumerable<RegexRule> MetricNameRegexRules => _metricNameRegexRules ??= GetRegexRules(_serverConfiguration.MetricNameRegexRules);

        private IEnumerable<RegexRule> _transactionNameRegexRules;
        public IEnumerable<RegexRule> TransactionNameRegexRules => _transactionNameRegexRules ??= GetRegexRules(_serverConfiguration.TransactionNameRegexRules);

        private IEnumerable<RegexRule> _rrlRegexRules;
        public IEnumerable<RegexRule> UrlRegexRules => _rrlRegexRules ??= GetRegexRules(_serverConfiguration.UrlRegexRules);

        private IDictionary<string, IEnumerable<string>> _transactionNameWhitelistRules;
        public IDictionary<string, IEnumerable<string>> TransactionNameWhitelistRules => _transactionNameWhitelistRules ??= GetWhitelistRules(_serverConfiguration.TransactionNameWhitelistRules);

        private IDictionary<string, double> _webTransactionsApdex;

        public IDictionary<string, double> WebTransactionsApdex => _webTransactionsApdex ??= _serverConfiguration.WebTransactionsApdex ?? new Dictionary<string, double>();

        #endregion Metric naming

        public string NewRelicConfigFilePath => _bootstrapConfiguration.ConfigurationFileName;
        public string AppSettingsConfigFilePath => _configurationManagerStatic.AppSettingsFilePath;

        #region Utilization

        public bool UtilizationDetectAws => EnvironmentOverrides(_localConfiguration.utilization.detectAws, "NEW_RELIC_UTILIZATION_DETECT_AWS");

        public bool UtilizationDetectAzure => EnvironmentOverrides(_localConfiguration.utilization.detectAzure, "NEW_RELIC_UTILIZATION_DETECT_AZURE");

        public bool UtilizationDetectGcp => EnvironmentOverrides(_localConfiguration.utilization.detectGcp, "NEW_RELIC_UTILIZATION_DETECT_GCP");

        public bool UtilizationDetectPcf => EnvironmentOverrides(_localConfiguration.utilization.detectPcf, "NEW_RELIC_UTILIZATION_DETECT_PCF");

        public bool UtilizationDetectDocker => EnvironmentOverrides(_localConfiguration.utilization.detectDocker, "NEW_RELIC_UTILIZATION_DETECT_DOCKER");

        public bool UtilizationDetectKubernetes => EnvironmentOverrides(_localConfiguration.utilization.detectKubernetes, "NEW_RELIC_UTILIZATION_DETECT_KUBERNETES");

        public bool UtilizationDetectAzureFunction => AzureFunctionModeEnabled && EnvironmentOverrides(_localConfiguration.utilization.detectAzureFunction, "NEW_RELIC_UTILIZATION_DETECT_AZURE_FUNCTION");

        public bool UtilizationDetectAzureAppService => EnvironmentOverrides(_localConfiguration.utilization.detectAzureAppService, "NEW_RELIC_UTILIZATION_DETECT_AZURE_APPSERVICE");

        public int? UtilizationLogicalProcessors
        {
            get
            {
                var localValue = GetNullableIntValue(_localConfiguration.utilization.logicalProcessorsSpecified, _localConfiguration.utilization.logicalProcessors);
                return EnvironmentOverrides(localValue, "NEW_RELIC_UTILIZATION_LOGICAL_PROCESSORS");
            }
        }

        public int? UtilizationTotalRamMib
        {
            get
            {
                var localValue = GetNullableIntValue(_localConfiguration.utilization.totalRamMibSpecified, _localConfiguration.utilization.totalRamMib);
                return EnvironmentOverrides(localValue, "NEW_RELIC_UTILIZATION_TOTAL_RAM_MIB");
            }
        }

        public string UtilizationBillingHost
        {
            get
            {
                var value = EnvironmentOverrides(_localConfiguration.utilization.billingHost, "NEW_RELIC_UTILIZATION_BILLING_HOSTNAME");
                return string.IsNullOrEmpty(value) ? null : value.Trim(); //Keeping IsNullOrEmpty just in case customer sets value to "".
            }
        }

        private readonly Lazy<string> _utilizationFullHostName;
        public string UtilizationFullHostName => _utilizationFullHostName.Value;

        private readonly Lazy<string> _utilizationHostName;
        public string UtilizationHostName => _utilizationHostName.Value;

        #endregion

        #region Log Events and Metrics

        public virtual bool ApplicationLoggingEnabled => EnvironmentOverrides(_localConfiguration.applicationLogging.enabled, "NEW_RELIC_APPLICATION_LOGGING_ENABLED");

        public virtual bool LogMetricsCollectorEnabled =>
            ApplicationLoggingEnabled &&
            EnvironmentOverrides(_localConfiguration.applicationLogging.metrics.enabled, "NEW_RELIC_APPLICATION_LOGGING_METRICS_ENABLED");

        public virtual bool LogEventCollectorEnabled =>
            ApplicationLoggingEnabled &&
            LogEventsMaxSamplesStored > 0 &&
            !SecurityPoliciesTokenExists &&
            HighSecurityModeOverrides(false,
                EnvironmentOverrides(_localConfiguration.applicationLogging.forwarding.enabled, "NEW_RELIC_APPLICATION_LOGGING_FORWARDING_ENABLED"));

        public bool ContextDataEnabled =>
            LogEventCollectorEnabled &&
            EnvironmentOverrides(_localConfiguration.applicationLogging.forwarding.contextData.enabled, "NEW_RELIC_APPLICATION_LOGGING_FORWARDING_CONTEXT_DATA_ENABLED");

        public IEnumerable<string> ContextDataInclude =>
            EnvironmentOverrides(_localConfiguration.applicationLogging.forwarding.contextData.include,
                    "NEW_RELIC_APPLICATION_LOGGING_FORWARDING_CONTEXT_DATA_INCLUDE")
                .Split([StringSeparators.CommaChar, ' '], StringSplitOptions.RemoveEmptyEntries);

        public IEnumerable<string> ContextDataExclude =>
            EnvironmentOverrides(_localConfiguration.applicationLogging.forwarding.contextData.exclude,
                    "NEW_RELIC_APPLICATION_LOGGING_FORWARDING_CONTEXT_DATA_EXCLUDE")
                .Split([StringSeparators.CommaChar, ' '], StringSplitOptions.RemoveEmptyEntries);

        public TimeSpan LogEventsHarvestCycle => ServerOverrides(_serverConfiguration.EventHarvestConfig?.LogEventHarvestCycle(), TimeSpan.FromSeconds(5));

        public virtual int LogEventsMaxSamplesStored =>
            // This is different from the other faster event harvest settings since we want to ensure that local values are properly distributed across the harvest.
            // Using the GetValueOrDefault to ensure that a value is provided if all other values are removed.
            ServerOverrides(_serverConfiguration.EventHarvestConfig?.LogEventHarvestLimit(),
                EnvironmentOverrides(_localConfiguration.applicationLogging.forwarding.maxSamplesStored,
                    "NEW_RELIC_APPLICATION_LOGGING_FORWARDING_MAX_SAMPLES_STORED").GetValueOrDefault(10000));

        public virtual bool LogDecoratorEnabled =>
            ApplicationLoggingEnabled &&
            EnvironmentOverrides(_localConfiguration.applicationLogging.localDecorating.enabled, "NEW_RELIC_APPLICATION_LOGGING_LOCAL_DECORATING_ENABLED");

        private HashSet<string> _logLevelDenyList;
        public virtual HashSet<string> LogLevelDenyList
        {
            get
            {
                if (_logLevelDenyList == null)
                {
                    _logLevelDenyList =
                    [
                        ..EnvironmentOverrides(_localConfiguration.applicationLogging.forwarding.logLevelDenyList,
                                  "NEW_RELIC_APPLICATION_LOGGING_FORWARDING_LOG_LEVEL_DENYLIST")
                              ?.Split([StringSeparators.CommaChar, ' '], StringSplitOptions.RemoveEmptyEntries)
                              .Select(s => s.ToUpper())
                          ?? []
                    ];

                    if (_logLevelDenyList.Count > 0)
                    {
                        var logLevels = string.Join(",", _logLevelDenyList);
                        Log.Info($"Log Level Filtering is enabled for the following levels: {logLevels}");
                    }
                }

                return _logLevelDenyList;
            }
        }

        public virtual bool LabelsEnabled =>
            LogEventCollectorEnabled &&
            EnvironmentOverrides(_localConfiguration.applicationLogging.forwarding.labels.enabled, "NEW_RELIC_APPLICATION_LOGGING_FORWARDING_LABELS_ENABLED");

        private HashSet<string> _labelsExclude;
        public virtual IEnumerable<string> LabelsExclude
        {
            get
            {
                return _labelsExclude ??= [
                    ..EnvironmentOverrides(_localConfiguration.applicationLogging.forwarding.labels.exclude,
                              "NEW_RELIC_APPLICATION_LOGGING_FORWARDING_LABELS_EXCLUDE")
                          ?.Split([StringSeparators.CommaChar, ' '], StringSplitOptions.RemoveEmptyEntries)
                      ?? []
                ];
            }
        }

        #endregion

        private IEnumerable<IDictionary<string, string>> _ignoredInstrumentation;
        public IEnumerable<IDictionary<string, string>> IgnoredInstrumentation =>
            _ignoredInstrumentation ??= _localConfiguration.instrumentation.rules
                .Select(i => new ReadOnlyDictionary<string, string>(
                    new Dictionary<string, string>
                    {
                        { "assemblyName", i.assemblyName }, { "className", i.className }
                    }
                ))
                .ToList();

        public bool DisableFileSystemWatcher =>
            ServerlessModeEnabled ||
            !LoggingEnabled ||
            EnvironmentOverrides(_localConfiguration.service.disableFileSystemWatcher, "NEW_RELIC_DISABLE_FILE_SYSTEM_WATCHER");

        #region AI Monitoring

        public bool AiMonitoringEnabled =>
            // AI Monitoring is disabled in High Security Mode and can be disabled at the account level
            !HighSecurityModeEnabled && ServerCanDisable(_serverConfiguration.AICollectionEnabled, EnvironmentOverrides(_localConfiguration.aiMonitoring.enabled, "NEW_RELIC_AI_MONITORING_ENABLED"));

        public bool AiMonitoringStreamingEnabled =>
            AiMonitoringEnabled &&
            EnvironmentOverrides(_localConfiguration.aiMonitoring.streaming.enabled, "NEW_RELIC_AI_MONITORING_STREAMING_ENABLED");

        public bool AiMonitoringRecordContentEnabled =>
            AiMonitoringEnabled &&
            EnvironmentOverrides(_localConfiguration.aiMonitoring.recordContent.enabled, "NEW_RELIC_AI_MONITORING_RECORD_CONTENT_ENABLED");

        public Func<string, string, int> LlmTokenCountingCallback => _runTimeConfiguration.LlmTokenCountingCallback;

        #region Azure function support

        public bool AzureFunctionModeDetected => _bootstrapConfiguration.AzureFunctionModeDetected;

        private bool? _azureFunctionModeEnabled;
        public bool AzureFunctionModeEnabled =>
            _azureFunctionModeEnabled ??= EnvironmentOverrides(TryGetAppSettingAsBoolWithDefault("AzureFunctionModeEnabled", true), "NEW_RELIC_AZURE_FUNCTION_MODE_ENABLED");

        // Code to support AWS Lambda APM mode for transaction naming [ By default it is disabled ]
        private bool? _awsLambdaApmModeEnabled;
        public bool AwsLambdaApmModeEnabled =>
            _awsLambdaApmModeEnabled ??= EnvironmentOverrides(TryGetAppSettingAsBoolWithDefault("AwsLambdaApmModeEnabled", false), "NEW_RELIC_APM_LAMBDA_MODE");

        public string AzureFunctionResourceId
        {
            get
            {
                var websiteResourceGroup = AzureFunctionResourceGroupName;
                var subscriptionId = AzureFunctionSubscriptionId;

                if (string.IsNullOrEmpty(websiteResourceGroup) || string.IsNullOrEmpty(subscriptionId))
                {
                    return string.Empty;
                }

                return $"/subscriptions/{subscriptionId}/resourceGroups/{websiteResourceGroup}/providers/Microsoft.Web/sites/{(string.IsNullOrEmpty(AzureFunctionAppName) ? "unknown" : AzureFunctionAppName)}";
            }
        }

        public string AzureFunctionResourceIdWithFunctionName(string functionName)
        {
            return string.IsNullOrEmpty(AzureFunctionResourceId) || string.IsNullOrEmpty(functionName)
                ? string.Empty
                : $"{AzureFunctionResourceId}/functions/{functionName}";
        }

        public string AzureFunctionResourceGroupName
        {
            get
            {
                // WEBSITE_RESOURCE_GROUP doesn't seem to always be available for Linux.
                var websiteResourceGroup = _environment.GetEnvironmentVariable("WEBSITE_RESOURCE_GROUP");
                if (!string.IsNullOrEmpty(websiteResourceGroup))
                {
                    return websiteResourceGroup; // Must be Windows function
                }

                // The WEBSITE_OWNER_NAME variable also has the resource group name, but we need to parse it out.
                // Must be a Linux function.
                var websiteOwnerName = _environment.GetEnvironmentVariable("WEBSITE_OWNER_NAME");
                if (string.IsNullOrEmpty(websiteOwnerName))
                {
                    return websiteOwnerName; // This should not happen, but just in case.
                }

                var idx = websiteOwnerName.IndexOf("+", StringComparison.Ordinal);
                if (idx <= 0)
                {
                    return websiteOwnerName; // This means that the format of the WEBSITE_OWNER_NAME is not as expected (subscription+resourcegroup-region-Linux).
                }

                // We should have a WEBSITE_OWNER_NAME in the expected format here.
                idx += 1; // move past the "+"
                var resourceData = websiteOwnerName.Substring(idx, websiteOwnerName.Length - idx - 6); // -6 to remove the "-Linux" suffix.

                // Remove the region from the resourceData.
                return resourceData.Substring(0, resourceData.LastIndexOf("-", StringComparison.Ordinal));
            }
        }

        public string AzureFunctionRegion => _environment.GetEnvironmentVariable("REGION_NAME");

        public string AzureFunctionSubscriptionId
        {
            get
            {
                var websiteOwnerName = _environment.GetEnvironmentVariable("WEBSITE_OWNER_NAME") ?? string.Empty;
                var idx = websiteOwnerName.IndexOf("+", StringComparison.Ordinal);
                return idx > 0 ? websiteOwnerName.Substring(0, idx) : websiteOwnerName;
            }
        }

        public string AzureFunctionAppName => _environment.GetEnvironmentVariable("WEBSITE_SITE_NAME");
        #endregion

        #endregion

        public virtual bool AppDomainCachingDisabled => EnvironmentOverrides(false, "NEW_RELIC_DISABLE_APPDOMAIN_CACHING");

        public virtual bool ForceNewTransactionOnNewThread => EnvironmentOverrides(_localConfiguration.service.forceNewTransactionOnNewThread, "NEW_RELIC_FORCE_NEW_TRANSACTION_ON_NEW_THREAD");

        private bool? _diagnosticsCaptureAgentTiming;
        public bool DiagnosticsCaptureAgentTiming
        {
            get
            {
                if (_diagnosticsCaptureAgentTiming == null)
                {
                    UpdateDiagnosticsAgentTimingSettings();
                }

                return _diagnosticsCaptureAgentTiming!.Value;

            }
        }

        private int? _diagnosticsCaptureAgentTimingFrequency;
        public int DiagnosticsCaptureAgentTimingFrequency
        {
            get
            {
                if (_diagnosticsCaptureAgentTimingFrequency == null)
                {
                    UpdateDiagnosticsAgentTimingSettings();
                }

                return _diagnosticsCaptureAgentTimingFrequency!.Value;

            }
        }

        private void UpdateDiagnosticsAgentTimingSettings()
        {
            var captureTiming = _localConfiguration.diagnostics.captureAgentTiming;
            var configFreq = _localConfiguration.diagnostics.captureAgentTimingFrequency;

            if (configFreq <= 0)
            {
                captureTiming = false;
            }

            _diagnosticsCaptureAgentTiming = captureTiming;
            _diagnosticsCaptureAgentTimingFrequency = configFreq;
        }

        private bool? _forceSynchronousTimingCalculationHttpClient;
        public bool ForceSynchronousTimingCalculationHttpClient => _forceSynchronousTimingCalculationHttpClient ??= TryGetAppSettingAsBoolWithDefault("ForceSynchronousTimingCalculation.HttpClient", false);

        private bool? _enableAspNetCore6PlusBrowserInjection;
        public bool EnableAspNetCore6PlusBrowserInjection =>
            _enableAspNetCore6PlusBrowserInjection ??= TryGetAppSettingAsBoolWithDefault("EnableAspNetCore6PlusBrowserInjection", true);

        private TimeSpan? _metricsHarvestCycleOverride = null;
        public TimeSpan MetricsHarvestCycle
        {
            get
            {
                if (_metricsHarvestCycleOverride.HasValue)
                {
                    return _metricsHarvestCycleOverride.Value;
                }

                if (_newRelicAppSettings.TryGetValue("OverrideMetricsHarvestCycle", out var harvestCycle))
                {
                    if (int.TryParse(harvestCycle, out var parsedHarvestCycle) && parsedHarvestCycle > 0)
                    {
                        Log.Info("Metrics harvest cycle overridden to " + parsedHarvestCycle + " seconds.");
                        _metricsHarvestCycleOverride = TimeSpan.FromSeconds(parsedHarvestCycle);
                        return _metricsHarvestCycleOverride.Value;
                    }
                }

                return DefaultHarvestCycle;
            }
        }

        private TimeSpan? _transactionTraceHarvestCycleOverride = null;
        public TimeSpan TransactionTracesHarvestCycle
        {
            get
            {
                if (_transactionTraceHarvestCycleOverride.HasValue)
                {
                    return _transactionTraceHarvestCycleOverride.Value;
                }

                if (_newRelicAppSettings.TryGetValue("OverrideTransactionTracesHarvestCycle", out var harvestCycle))
                {
                    if (int.TryParse(harvestCycle, out var parsedHarvestCycle) && parsedHarvestCycle > 0)
                    {
                        Log.Info("Transaction traces harvest cycle overridden to " + parsedHarvestCycle + " seconds.");
                        _transactionTraceHarvestCycleOverride = TimeSpan.FromSeconds(parsedHarvestCycle);
                        return _transactionTraceHarvestCycleOverride.Value;
                    }
                }

                return DefaultHarvestCycle;
            }
        }

        private TimeSpan? _errorTracesHarvestCycleOverride = null;
        public TimeSpan ErrorTracesHarvestCycle
        {
            get
            {
                if (_errorTracesHarvestCycleOverride.HasValue)
                {
                    return _errorTracesHarvestCycleOverride.Value;
                }

                if (_newRelicAppSettings.TryGetValue("OverrideErrorTracesHarvestCycle", out var harvestCycle))
                {
                    if (int.TryParse(harvestCycle, out var parsedHarvestCycle) && parsedHarvestCycle > 0)
                    {
                        Log.Info("Error traces harvest cycle overridden to " + parsedHarvestCycle + " seconds.");
                        _errorTracesHarvestCycleOverride = TimeSpan.FromSeconds(parsedHarvestCycle);
                        return _errorTracesHarvestCycleOverride.Value;
                    }
                }

                return DefaultHarvestCycle;
            }
        }

        private TimeSpan? _getAgentCommandsCycleOverride = null;
        public TimeSpan GetAgentCommandsCycle
        {
            get
            {
                if (_getAgentCommandsCycleOverride.HasValue)
                {
                    return _getAgentCommandsCycleOverride.Value;
                }

                if (_newRelicAppSettings.TryGetValue("OverrideGetAgentCommandsCycle", out var harvestCycle))
                {
                    if (int.TryParse(harvestCycle, out var parsedHarvestCycle) && parsedHarvestCycle > 0)
                    {
                        Log.Info("Get agent commands cycle overridden to " + parsedHarvestCycle + " seconds.");
                        _getAgentCommandsCycleOverride = TimeSpan.FromSeconds(parsedHarvestCycle);
                        return _getAgentCommandsCycleOverride.Value;
                    }
                }

                return DefaultHarvestCycle;
            }
        }

        private TimeSpan? _sqlTracesHarvestCycleOverride = null;
        public TimeSpan SqlTracesHarvestCycle
        {
            get
            {
                if (_sqlTracesHarvestCycleOverride.HasValue)
                {
                    return _sqlTracesHarvestCycleOverride.Value;
                }

                if (_newRelicAppSettings.TryGetValue("OverrideSqlTracesHarvestCycle", out var harvestCycle))
                {
                    if (int.TryParse(harvestCycle, out var parsedHarvestCycle) && parsedHarvestCycle > 0)
                    {
                        Log.Info("SQL traces harvest cycle overridden to " + parsedHarvestCycle + " seconds.");
                        _sqlTracesHarvestCycleOverride = TimeSpan.FromSeconds(parsedHarvestCycle);
                        return _sqlTracesHarvestCycleOverride.Value;
                    }
                }

                return DefaultHarvestCycle;
            }
        }

        private TimeSpan? _updateLoadedModulesCycleOverride = null;
        public TimeSpan UpdateLoadedModulesCycle
        {
            get
            {
                if (_updateLoadedModulesCycleOverride.HasValue)
                {
                    return _updateLoadedModulesCycleOverride.Value;
                }

                if (_newRelicAppSettings.TryGetValue("OverrideUpdateLoadedModulesCycle", out var harvestCycle))
                {
                    if (int.TryParse(harvestCycle, out var parsedHarvestCycle) && parsedHarvestCycle > 0)
                    {
                        Log.Info("Update loaded modules cycle overridden to " + parsedHarvestCycle + " seconds.");
                        _updateLoadedModulesCycleOverride = TimeSpan.FromSeconds(parsedHarvestCycle);
                        return _updateLoadedModulesCycleOverride.Value;
                    }
                }

                return DefaultHarvestCycle;
            }
        }

        private TimeSpan? _stackExchangeRedisCleanupCycleOverride = null;
        public TimeSpan StackExchangeRedisCleanupCycle
        {
            get
            {
                if (_stackExchangeRedisCleanupCycleOverride.HasValue)
                {
                    return _stackExchangeRedisCleanupCycleOverride.Value;
                }

                if (_newRelicAppSettings.TryGetValue("OverrideStackExchangeRedisCleanupCycle", out var harvestCycle))
                {
                    if (int.TryParse(harvestCycle, out var parsedHarvestCycle) && parsedHarvestCycle > 0)
                    {
                        Log.Info("StackExchange.Redis cleanup cycle overridden to " + parsedHarvestCycle + " seconds.");
                        _stackExchangeRedisCleanupCycleOverride = TimeSpan.FromSeconds(parsedHarvestCycle);
                        return _stackExchangeRedisCleanupCycleOverride.Value;
                    }
                }

                return DefaultHarvestCycle;
            }
        }

        public bool GCSamplerV2Enabled => _bootstrapConfiguration.GCSamplerV2Enabled;

        #region Agent Control

        public bool AgentControlEnabled => _bootstrapConfiguration.AgentControlEnabled;

        public string HealthDeliveryLocation => _bootstrapConfiguration.HealthDeliveryLocation;

        public int HealthFrequency => _bootstrapConfiguration.HealthFrequency;

        #endregion Agent Control

        #endregion Properties

        #region Helpers

        private TimeSpan ParseTransactionThreshold(string threshold, Func<double, TimeSpan> numberToTimeSpanConverter)
        {
            return string.IsNullOrEmpty(threshold)
                ? TransactionTraceApdexF
                : double.TryParse(threshold, out var parsedTransactionThreshold)
                    ? numberToTimeSpanConverter(parsedTransactionThreshold)
                    : TransactionTraceApdexF;
        }

        private static bool ServerCanDisable(bool? server, bool local)
        {
            return server == null ? local : server.Value && local;
        }

        private static string ServerOverrides(string server, string local)
        {
            return server ?? local ?? string.Empty;
        }

        private static T ServerOverrides<T>(T? server, T local) where T : struct
        {
            return server ?? local;
        }

        private T HighSecurityModeOverrides<T>(T overriddenValue, T originalValue)
        {
            return HighSecurityModeEnabled ? overriddenValue : originalValue;
        }

        private static T ServerOverrides<T>(T server, T local) where T : class
        {
            return server ?? local;
        }

        private IEnumerable<string> EnvironmentOverrides(IEnumerable<string> local, params string[] environmentVariableNames)
        {
            var envValue = _environment.GetEnvironmentVariableFromList(environmentVariableNames ?? []);

            // took this approach to eliminate a null object issue when combining the different calls together. Also a bit easier to read.
            if (string.IsNullOrWhiteSpace(envValue))
            {
                return local;
            }

            char[] delimiters = [',', ' '];
            return envValue
                .Split(delimiters, StringSplitOptions.RemoveEmptyEntries)
                .ToList();
        }

        private string EnvironmentOverrides(string local, params string[] environmentVariableNames)
        {
            return EnvironmentOverrides(_environment, local, environmentVariableNames);
        }

        public static string EnvironmentOverrides(IEnvironment environment, string local, params string[] environmentVariableNames)
        {
            var envValue = environment.GetEnvironmentVariableFromList(environmentVariableNames ?? []);

            // if we get a null, we use local - should not get whitespace
            if (string.IsNullOrWhiteSpace(envValue))
            {
                return local;
            }

            return envValue.Trim();
        }

        private uint? EnvironmentOverrides(uint? local, params string[] environmentVariableNames)
        {
            var env = _environment.GetEnvironmentVariableFromList(environmentVariableNames ?? []);

            return uint.TryParse(env, out uint parsedValue) ? parsedValue : local;
        }

        private int? EnvironmentOverrides(int? local, params string[] environmentVariableNames)
        {
            return EnvironmentOverrides(_environment, local, environmentVariableNames);
        }

        public static int? EnvironmentOverrides(IEnvironment environment, int? local, params string[] environmentVariableNames)
        {
            var env = environment.GetEnvironmentVariableFromList(environmentVariableNames ?? []);

            return int.TryParse(env, out int parsedValue) ? parsedValue : local;
        }

        private double? EnvironmentOverrides(double? local, params string[] environmentVariableNames)
        {
            var env = _environment.GetEnvironmentVariableFromList(environmentVariableNames ?? []);

            return double.TryParse(env, out double parsedValue) ? parsedValue : local;
        }

        private bool EnvironmentOverrides(bool local, params string[] environmentVariableNames)
        {
            return EnvironmentOverrides(_environment, local, environmentVariableNames);
        }

        public static bool EnvironmentOverrides(IEnvironment environment, bool local, params string[] environmentVariableNames)
        {
            var env = environment.GetEnvironmentVariableFromList(environmentVariableNames ?? []);

            env = env?.ToLower();

            if (bool.TryParse(env, out var parsedValue))
            {
                return parsedValue;
            }

            return env switch
            {
                "0" => false,
                "1" => true,
                _ => local
            };
        }

        private IList<Regex> ReadUrlBlacklist(configuration config)
        {
            var list = new List<Regex>();

            if (config.browserMonitoring.requestPathsExcluded is { Count: > 0 })
            {
                foreach (var p in config.browserMonitoring.requestPathsExcluded)
                {
                    try
                    {
                        Regex regex = new Regex(p.regex,
                            RegexOptions.Compiled |
                            RegexOptions.Multiline |
                            RegexOptions.IgnoreCase);
                        list.Add(regex);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "A Browser Monitoring Request Path failed regular expression parsing: {0}",
                            p.regex);
                    }
                }
            }

            return list;
        }

        public static IEnumerable<RegexRule> GetRegexRules(IEnumerable<ServerConfiguration.RegexRule> rules)
        {
            if (rules == null)
                return [];

            return rules
                .Select(TryGetRegexRule)
                .Where(rule => rule != null)
                .Select(rule => rule.Value)
                .ToList();
        }

        private static RegexRule? TryGetRegexRule(ServerConfiguration.RegexRule rule)
        {
            if (rule?.MatchExpression == null)
                return null;

            var matchExpression = rule.MatchExpression;
            var replacement = UpdateRegexForDotNet(rule.Replacement);
            var ignore = rule.Ignore ?? false;
            var evaluationOrder = rule.EvaluationOrder ?? 0;
            var terminateChain = rule.TerminateChain ?? false;
            var eachSegment = rule.EachSegment ?? false;
            var replaceAll = rule.ReplaceAll ?? false;

            return new RegexRule(matchExpression, replacement, ignore, evaluationOrder, terminateChain, eachSegment, replaceAll);
        }

        private static string UpdateRegexForDotNet(string replacement)
        {
            if (string.IsNullOrEmpty(replacement))
                return replacement;

            //search for \1, \2 etc, and replace with $1, $2, etc
            var backreferencePattern = new Regex(@"\\(\d+)");
            return backreferencePattern.Replace(replacement, "$$$1");
        }

        public static IDictionary<string, IEnumerable<string>> GetWhitelistRules(IEnumerable<ServerConfiguration.WhitelistRule> whitelistRules)
        {
            return whitelistRules == null
                ? []
                : whitelistRules
                    .Where(rule => rule != null)
                    .Select(TryGetValidPrefixAndTerms)
                    .Where(rule => rule != null)
                    .Select(rule => rule.Value)
                    .ToDictionary(IEnumerableExtensions.DuplicateKeyBehavior.KeepLast);
        }

        private static KeyValuePair<string, IEnumerable<string>>? TryGetValidPrefixAndTerms(ServerConfiguration.WhitelistRule rule)
        {
            if (rule.Terms == null)
            {
                Log.Warn("Ignoring transaction_segment_term with null terms for prefix '{0}'", rule.Prefix);
                return null;
            }

            var prefix = TryGetValidPrefix(rule.Prefix);
            if (prefix == null)
            {
                Log.Warn("Ignoring transaction_segment_term with invalid prefix '{0}'", rule.Prefix);
                return null;
            }

            var terms = rule.Terms;
            return new KeyValuePair<string, IEnumerable<string>>(prefix, terms);
        }

        private void ParseExpectedErrorConfigurations()
        {
            var expectedErrorInfo = _serverConfiguration.RpmConfig.ErrorCollectorExpectedMessages?.ToDictionary(IEnumerableExtensions.DuplicateKeyBehavior.KeepFirst);

            if (expectedErrorInfo == null)
            {
                expectedErrorInfo = _localConfiguration.errorCollector.expectedMessages
                .Where(x => x.message != null)
                .Select(x => new KeyValuePair<string, IEnumerable<string>>(x.name, x.message))
                .ToDictionary(IEnumerableExtensions.DuplicateKeyBehavior.KeepFirst);
            }

            //Keeping the original expected messages configuration for agent setting report on connect before
            //the expectedErrorInfo dictionary gets mixed up between expected messages and expected classes configurations.
            var expectedMessages = new Dictionary<string, IEnumerable<string>>(expectedErrorInfo);

            var expectedClasses = ServerOverrides(_serverConfiguration.RpmConfig.ErrorCollectorExpectedClasses, _localConfiguration.errorCollector.expectedClasses.errorClass);

            var count = expectedErrorInfo.Count;

            foreach (var className in expectedClasses)
            {
                if (expectedErrorInfo.ContainsKey(className))
                {
                    expectedErrorInfo[className] = Enumerable.Empty<string>();
                    Log.Warn($"Expected Errors - {className} class is specified in both errorCollector.expectedClasses and errorCollector.expectedMessages configurations. Any errors of this class will be marked as expected.");
                }
                else if (count >= MaxExptectedErrorConfigEntries)
                {
                    Log.Warn($"Expected Errors - {className} Exceeds the limit of {MaxExptectedErrorConfigEntries} and will be ignored.");
                }
                else
                {
                    expectedErrorInfo[className] = Enumerable.Empty<string>();
                    count++;
                }
            }

            var expectedStatusCodesArrayLocal = _localConfiguration.errorCollector.expectedStatusCodes?.Split(StringSeparators.Comma, StringSplitOptions.RemoveEmptyEntries);
            var expectedStatusCodesArrayServer = _serverConfiguration.RpmConfig.ErrorCollectorExpectedStatusCodes;

            var expectedStatusCodesArray = EnvironmentOverrides(ServerOverrides(expectedStatusCodesArrayServer, expectedStatusCodesArrayLocal), "NEW_RELIC_ERROR_COLLECTOR_EXPECTED_ERROR_CODES");

            ExpectedStatusCodes = ParseExpectedStatusCodesArray(expectedStatusCodesArray);
            ExpectedErrorStatusCodesForAgentSettings = expectedStatusCodesArray ?? [];

            ExpectedErrorsConfiguration = new ReadOnlyDictionary<string, IEnumerable<string>>(expectedErrorInfo);
            ExpectedErrorMessagesForAgentSettings = new ReadOnlyDictionary<string, IEnumerable<string>>(expectedMessages);
            ExpectedErrorClassesForAgentSettings = expectedClasses;
        }

        private void ParseIgnoreErrorConfigurations()
        {
            var ignoreErrorInfo = _serverConfiguration.RpmConfig.ErrorCollectorIgnoreMessages?
                .ToDictionary(IEnumerableExtensions.DuplicateKeyBehavior.KeepFirst);

            if (ignoreErrorInfo == null)
            {
                ignoreErrorInfo = _localConfiguration.errorCollector.ignoreMessages
                .Where(x => x.message != null)
                .Select(x => new KeyValuePair<string, IEnumerable<string>>(x.name, x.message))
                .ToDictionary(IEnumerableExtensions.DuplicateKeyBehavior.KeepFirst);
            }

            //Keeping the original ignore messages configuration for agent setting report on connect before
            //the ignoreErrorInfo dictionary gets mixed up between ignore messages and ignore classes configurations.
            var ignoreMessages = new Dictionary<string, IEnumerable<string>>(ignoreErrorInfo);

            var ignoreClasses = ServerOverrides(_serverConfiguration.RpmConfig.ErrorCollectorIgnoreClasses, _localConfiguration.errorCollector.ignoreClasses.errorClass);

            var count = ignoreErrorInfo.Count;

            foreach (var className in ignoreClasses)
            {
                if (ignoreErrorInfo.ContainsKey(className))
                {
                    ignoreErrorInfo[className] = Enumerable.Empty<string>();
                    Log.Warn($"Ignore Errors - {className} class is specified in both errorCollector.ignoreClasses and errorCollector.ingoreMessages configurations. Any errors of this class will be ignored.");
                }
                else if (count >= MaxIgnoreErrorConfigEntries)
                {
                    Log.Warn($"Ignore Errors - {className} Exceeds the limit of {MaxIgnoreErrorConfigEntries} and will be ignored.");
                }
                else
                {
                    ignoreErrorInfo.Add(className, Enumerable.Empty<string>());
                    count++;
                }
            }

            IEnumerable<string> ignoreStatusCodes = EnvironmentOverrides(_serverConfiguration.RpmConfig.ErrorCollectorStatusCodesToIgnore, "NEW_RELIC_ERROR_COLLECTOR_IGNORE_ERROR_CODES");
            if (ignoreStatusCodes == null)
            {
                ignoreStatusCodes = _localConfiguration.errorCollector.ignoreStatusCodes.code
                    .Select(x => x.ToString(CultureInfo.InvariantCulture))
                    .ToList();
            }

            foreach (var code in ignoreStatusCodes)
            {
                if (!ignoreErrorInfo.ContainsKey(code))
                {
                    ignoreErrorInfo.Add(code, Enumerable.Empty<string>());
                }
            }

            IgnoreErrorsConfiguration = new ReadOnlyDictionary<string, IEnumerable<string>>(ignoreErrorInfo);
            IgnoreErrorMessagesForAgentSettings = new ReadOnlyDictionary<string, IEnumerable<string>>(ignoreMessages);
            IgnoreErrorClassesForAgentSettings = ignoreClasses;
            HttpStatusCodesToIgnore = ignoreStatusCodes;
        }


        private static string TryGetValidPrefix(string prefix)
        {
            if (prefix == null)
                return null;

            // A single trailing slash on prefix can be ignored
            prefix = prefix.TrimEnd(MetricNames.PathSeparatorChar, 1);

            // Prefixes should always be exactly two segments
            if (prefix.Count(c => c == MetricNames.PathSeparatorChar) != 1)
                return null;

            return prefix;
        }

        private static int? GetNullableIntValue(bool specified, int value) => specified ? value : null;

        // Since the configuration is initialized before the AgentHealthReporter, needed a way to not call it till it was ready 
        private void TrySetAgentControlStatus((bool IsHealthy, string Code, string Status) healthStatus)
        {
            if (_agentHealthReporter == null)
            {
                Log.Debug("DefaultConfiguration: Unable to set Agent Control status {status} because agent health reporter has not been initialized.", healthStatus);
                return;
            }

            _agentHealthReporter.SetAgentControlStatus(healthStatus);
        }

        #endregion

        #region deprecated/disabled parameter group settings

        private void LogDisabledWarnings()
        {
            // analyticsEvents.*
            if (_localConfiguration.analyticsEvents.transactions.enabledSpecified)
            {
                LogDisabledPropertyUse("analyticsEvents.transactions.enabled", "transactionEvents.transactions.enabled");
            }
            if (_localConfiguration.analyticsEvents.enabledSpecified)
            {
                LogDisabledPropertyUse("analyticsEvents.enabled", "transactionEvents.enabled");
            }
            if (_localConfiguration.analyticsEvents.captureAttributesSpecified)
            {
                LogDisabledPropertyUse("analyticsEvents.captureAttributes", "transaction_events.attributes.enabled");
            }
            if (_localConfiguration.analyticsEvents.maximumSamplesStoredSpecified)
            {
                LogDisabledPropertyUse("analyticsEvents.maximumSamplesStored", "transaction_events.maximumSamplesStored");
            }
            if (_localConfiguration.analyticsEvents.maximumSamplesPerMinuteSpecified)
            {
                LogDisabledPropertyUse("analyticsEvents.maximumSamplesPerMinute");
            }

            // transactionEvents.maximumSamplesPerMinute
            if (_localConfiguration.transactionEvents.maximumSamplesPerMinuteSpecified)
            {
                LogDisabledPropertyUse("transactionEvents.maximumSamplesPerMinute");
            }

            // parameterGroups.*
            // parameterGroups.responseHeaderParameters.* has no replacement equivalent, the others
            // are replaced with attributes.include/exclude
            if (_localConfiguration.parameterGroups.identityParameters.enabledSpecified)
            {
                LogDisabledPropertyUse("parameterGroups.identityParameters.enabled", "attributes.include");
            }
            if (_localConfiguration.parameterGroups.identityParameters?.ignore?.Count > 0)
            {
                LogDisabledPropertyUse("parameterGroups.identityParameters.ignore", "attributes.exclude");
            }
            if (_localConfiguration.parameterGroups.responseHeaderParameters.enabledSpecified)
            {
                LogDisabledPropertyUse("parameterGroups.responseHeaderParameters.enabled");
            }
            if (_localConfiguration.parameterGroups.responseHeaderParameters?.ignore?.Count > 0)
            {
                LogDisabledPropertyUse("parameterGroups.responseHeaderParameters.ignore");
            }
            if (_localConfiguration.parameterGroups.customParameters.enabledSpecified)
            {
                LogDisabledPropertyUse("parameterGroups.customParameters.enabled", "attributes.include");
            }
            if (_localConfiguration.parameterGroups.customParameters?.ignore?.Count > 0)
            {
                LogDisabledPropertyUse("parameterGroups.customParameters.ignore", "attributes.exclude");
            }
            if (_localConfiguration.parameterGroups.requestHeaderParameters.enabledSpecified)
            {
                LogDisabledPropertyUse("parameterGroups.requestHeaderParameters.enabled", "attributes.include");
            }
            if (_localConfiguration.parameterGroups.requestHeaderParameters?.ignore?.Count > 0)
            {
                LogDisabledPropertyUse("parameterGroups.requestHeaderParameters.ignore", "attributes.exclude");
            }

            //requestParameters.*
            //requestParameters.ignore
            //requestParameters.enabled
            if (_localConfiguration.requestParameters?.ignore?.Count > 0)
            {
                LogDisabledPropertyUse("requestParameters.ignore", "attributes.exclude");
            }
            if (_localConfiguration.requestParameters?.enabledSpecified == true)
            {
                LogDisabledPropertyUse("requestParameters.enabled", "request.parameters.* in attributes.include");
            }


            //transactionTracer.captureAttributes
            if (_localConfiguration.transactionTracer.captureAttributesSpecified)
            {
                LogDisabledPropertyUse("transactionTracer.captureAttributes", "transactionTracer.attributes.enabled");
            }
            //errorCollector.captureAttributes
            if (_localConfiguration.errorCollector.captureAttributesSpecified)
            {
                LogDisabledPropertyUse("errorCollector.captureAttributes", "errorCollector.attributes.enabled");
            }
            //browserMonitoring.captureAttributes
            if (_localConfiguration.browserMonitoring.captureAttributesSpecified)
            {
                LogDisabledPropertyUse("browserMonitoring.captureAttributes", "browserMonitoring.attributes.enabled");
            }

            //errorColelctor.ignoreErrors
            if (_localConfiguration.errorCollector.ignoreErrors?.exception?.Any() ?? false)
            {
                LogDisabledPropertyUse("errorCollector.ignoreErrors", "errorCollector.ignoreClasses");
            }
        }

        // This method is now unused as all previously deprecated config properties have been fully disabled.
        // However, we may wish to deprecate more config properties in the future, so it seems prudent to
        // leave this code in here, commented out.
        //private void LogDeprecatedPropertyUse(string deprecatedPropertyName, string newPropertyName)
        //{
        //    Log.WarnFormat("Deprecated configuration property '{0}'.  Use '{1}'.  See https://docs.newrelic.com/docs/agents/net-agent/configuration/net-agent-configuration/ for details.", deprecatedPropertyName, newPropertyName);
        //}

        private void LogDisabledPropertyUse(string disabledPropertyName, string newPropertyName = "")
        {
            var replacementText = "No replacement is available";
            if (newPropertyName != "")
            {
                replacementText = $"Use {newPropertyName} instead.";
            }
            Log.Warn("Configuration property '{0}' is disabled (unused) and will be removed from the config schema in a future release.  {1}  See https://docs.newrelic.com/docs/agents/net-agent/configuration/net-agent-configuration/ for details.", disabledPropertyName, replacementText);
        }

        int? _databaseStatementCacheCapacity = null;
        public int DatabaseStatementCacheCapacity => _databaseStatementCacheCapacity ?? (_databaseStatementCacheCapacity =
            TryGetAppSettingAsIntWithDefault("SqlStatementCacheCapacity", DefaultSqlStatementCacheCapacity)).Value;

        private bool? _codeLevelMetricsEnabled;
        public bool CodeLevelMetricsEnabled => _codeLevelMetricsEnabled ??= EnvironmentOverrides(_localConfiguration.codeLevelMetrics.enabled, "NEW_RELIC_CODE_LEVEL_METRICS_ENABLED");

        #endregion

        #region Cloud
        private string _awsAccountId;
        public string AwsAccountId => _awsAccountId ??= EnvironmentOverrides(_localConfiguration.cloud.aws.accountId, "NEW_RELIC_CLOUD_AWS_ACCOUNT_ID");

        #endregion

        #region Otel Bridge

        private static readonly string[] DefaultIncludedActivitySources = ["NewRelic.Agent"];

        private List<string> _includedActivitySources;
        public List<string> IncludedActivitySources
        {
            get
            {
                if (_includedActivitySources == null)
                {
                    var includedActivitySources = DefaultIncludedActivitySources.ToList();

                    var appSetting = TryGetAppSettingAsString("OpenTelemetry.ActivitySource.Include");
                    if (!string.IsNullOrEmpty(appSetting))
                    {
                        includedActivitySources.AddRange(appSetting.Split(','));
                    }

                    _includedActivitySources = includedActivitySources;
                }

                return _includedActivitySources;
            }
        }

        private List<string> _excludedActivitySources;
        public List<string> ExcludedActivitySources
        {
            get
            {
                if (_excludedActivitySources == null)
                {
                    _excludedActivitySources = [];

                    var appSetting = TryGetAppSettingAsString("OpenTelemetry.ActivitySource.Exclude");
                    if (!string.IsNullOrEmpty(appSetting))
                    {
                        _excludedActivitySources.AddRange(appSetting.Split(','));
                    }
                }

                return _excludedActivitySources;
            }
        }

        public bool OpenTelemetryBridgeEnabled => EnvironmentOverrides(TryGetAppSettingAsBoolWithDefault("OpenTelemetry.Enabled", false), "NEW_RELIC_OPEN_TELEMETRY_BRIDGE_ENABLED");

        public int MaxCustomInstrumentationSupportabilityMetrics => 25; // in case we want to make this configurable in the future

        #endregion

        public static bool GetLoggingEnabledValue(IEnvironment environment, configurationLog localLogConfiguration)
        {
            return EnvironmentOverrides(environment, localLogConfiguration.enabled, "NEW_RELIC_LOG_ENABLED");
        }

        private bool? _loggingEnabled;
        public bool LoggingEnabled => _loggingEnabled ??= GetLoggingEnabledValue(_environment, _localConfiguration.log);

        public static string GetLoggingLevelValue(IEnvironment environment, configurationLog localLogConfiguration, bool isLoggingEnabled)
        {
            var logLevel = "off";
            if (isLoggingEnabled)
            {
                logLevel = EnvironmentOverrides(environment, localLogConfiguration.level, "NEW_RELIC_LOG_LEVEL", "NEWRELIC_LOG_LEVEL").ToUpper();
            }

            return logLevel;
        }

        private string _loggingLevel;
        public string LoggingLevel => _loggingLevel ??= GetLoggingLevelValue(_environment, _localConfiguration.log, LoggingEnabled);

        private const bool CaptureTransactionTraceAttributesDefault = true;
        private const bool CaptureErrorCollectorAttributesDefault = true;
        private const bool CaptureBrowserMonitoringAttributesDefault = false;
        private const bool CaptureCustomParametersAttributesDefault = true;

        private const bool TransactionEventsTransactionsEnabledDefault = true;
        private const int MaxPayloadSizeInBytes = 1000000; // 1 MiB
    }
}

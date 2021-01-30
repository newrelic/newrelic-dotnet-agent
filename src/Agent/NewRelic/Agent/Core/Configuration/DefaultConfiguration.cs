// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Config;
using NewRelic.Agent.Core.Metric;
using NewRelic.Agent.Helpers;
using NewRelic.Core;
using NewRelic.Core.Logging;
using NewRelic.Memoization;
using NewRelic.SystemExtensions;
using NewRelic.SystemExtensions.Collections.Generic;
using NewRelic.SystemInterfaces;
using NewRelic.SystemInterfaces.Web;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

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
        private const int DefaultSpanEventsMaxSamplesStored = 1000;
        private readonly IEnvironment _environment = new EnvironmentMock();
        private readonly IProcessStatic _processStatic = new ProcessStatic();
        private readonly IHttpRuntimeStatic _httpRuntimeStatic = new HttpRuntimeStatic();
        private readonly IConfigurationManagerStatic _configurationManagerStatic = new ConfigurationManagerStaticMock();
        private readonly IDnsStatic _dnsStatic;

        /// <summary>
        /// Default configuration.  It will contain reasonable default values for everything and never anything more.  Useful when you don't have configuration off disk or a collector response yet.
        /// </summary>
        public static readonly DefaultConfiguration Instance = new DefaultConfiguration();
        private readonly configuration _localConfiguration = new configuration();
        private readonly ServerConfiguration _serverConfiguration = ServerConfiguration.GetDefault();
        private readonly RunTimeConfiguration _runTimeConfiguration = new RunTimeConfiguration();
        private readonly SecurityPoliciesConfiguration _securityPoliciesConfiguration = new SecurityPoliciesConfiguration();
        private Dictionary<string, string> _newRelicAppSettings { get; }

        public bool UseResourceBasedNamingForWCFEnabled { get; }
        public bool EventListenerSamplersEnabled { get; set; }

        /// <summary>
        /// Default configuration constructor.  It will contain reasonable default values for everything and never anything more.
        /// </summary>
        private DefaultConfiguration()
        {
            ConfigurationVersion = Interlocked.Increment(ref _currentConfigurationVersion);
        }

        protected DefaultConfiguration(IEnvironment environment, configuration localConfiguration, ServerConfiguration serverConfiguration, RunTimeConfiguration runTimeConfiguration, SecurityPoliciesConfiguration securityPoliciesConfiguration, IProcessStatic processStatic, IHttpRuntimeStatic httpRuntimeStatic, IConfigurationManagerStatic configurationManagerStatic, IDnsStatic dnsStatic)
            : this()
        {
            _environment = environment;
            _processStatic = processStatic;
            _httpRuntimeStatic = httpRuntimeStatic;
            _configurationManagerStatic = configurationManagerStatic;
            _dnsStatic = dnsStatic;

            _utilizationFullHostName = new Lazy<string>(_dnsStatic.GetFullHostName);
            _utilizationHostName = new Lazy<string>(_dnsStatic.GetHostName);



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

            LogDeprecationWarnings();

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
                return new Dictionary<string, string>();

            return _localConfiguration.appSettings
                .Where(setting => setting != null)
                .Select(setting => new KeyValuePair<string, string>(setting.key, setting.value))
                .ToDictionary(IEnumerableExtensions.DuplicateKeyBehavior.KeepFirst);
        }

        private bool TryGetAppSettingAsBoolWithDefault(string key, bool defaultValue)
        {
            var value = _newRelicAppSettings.GetValueOrDefault(key);

            bool parsedBool;
            var parsedSuccessfully = bool.TryParse(value, out parsedBool);
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

        public virtual bool AgentEnabled
        {
            get
            {
                var agentEnabledAsString = _configurationManagerStatic.GetAppSetting("NewRelic.AgentEnabled");

                bool agentEnabled;
                if (!bool.TryParse(agentEnabledAsString, out agentEnabled))
                    return _localConfiguration.agentEnabled;

                return agentEnabled;
            }
        }

        private string _agentLicenseKey;
        public virtual string AgentLicenseKey
        {
            get
            {
                if (_agentLicenseKey != null)
                    return _agentLicenseKey;

                _agentLicenseKey = _configurationManagerStatic.GetAppSetting("NewRelic.LicenseKey")
                    ?? EnvironmentOverrides(_localConfiguration.service.licenseKey, "NEW_RELIC_LICENSE_KEY", "NEWRELIC_LICENSEKEY");

                if (_agentLicenseKey != null)
                    _agentLicenseKey = _agentLicenseKey.Trim();

                return _agentLicenseKey;
            }
        }

        private IEnumerable<string> _applicationNames;
        public virtual IEnumerable<string> ApplicationNames { get { return _applicationNames ?? (_applicationNames = GetApplicationNames()); } }

        private IEnumerable<string> GetApplicationNames()
        {
            var runtimeAppNames = _runTimeConfiguration.ApplicationNames.ToList();
            if (runtimeAppNames.Any())
            {
                Log.Info("Application name from SetApplicationName API.");
                return runtimeAppNames;
            }

            var appName = _configurationManagerStatic.GetAppSetting("NewRelic.AppName");
            if (appName != null)
            {
                Log.Info("Application name from web.config or app.config.");
                return appName.Split(StringSeparators.Comma);
            }

            appName = _environment.GetEnvironmentVariable("IISEXPRESS_SITENAME");
            if (appName != null)
            {
                Log.Info("Application name from IISEXPRESS_SITENAME Environment Variable.");
                return appName.Split(StringSeparators.Comma);
            }

            appName = _environment.GetEnvironmentVariable("NEW_RELIC_APP_NAME");
            if (appName != null)
            {
                Log.Info("Application name from NEW_RELIC_APP_NAME Environment Variable.");
                return appName.Split(StringSeparators.Comma);
            }

            appName = _environment.GetEnvironmentVariable("RoleName");
            if (appName != null)
            {
                Log.Info("Application name from RoleName Environment Variable.");
                return appName.Split(StringSeparators.Comma);
            }

            if (_localConfiguration.application.name.Count > 0)
            {
                Log.Info("Application name from newrelic.config.");
                return _localConfiguration.application.name;
            }

            appName = GetAppPoolId();
            if (!string.IsNullOrWhiteSpace(appName))
            {
                Log.Info("Application name from Application Pool name.");
                return appName.Split(StringSeparators.Comma);
            }

            if (_httpRuntimeStatic.AppDomainAppVirtualPath == null)
            {
                Log.Info("Application name from process name.");
                return new List<string> { _processStatic.GetCurrentProcess().ProcessName };
            }

            throw new Exception("An application name must be provided");
        }

        private string GetAppPoolId()
        {
            var appPoolId = _environment.GetEnvironmentVariable("APP_POOL_ID");
            if (!string.IsNullOrEmpty(appPoolId)) return appPoolId;

            var isW3wp = _processStatic.GetCurrentProcess().ProcessName?.Equals("w3wp", StringComparison.InvariantCultureIgnoreCase);
            if (!isW3wp.HasValue || !isW3wp.Value) return appPoolId;

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

        public virtual string SecurityPoliciesToken
        {
            get
            {
                if (_securityPoliciesToken != null)
                {
                    return _securityPoliciesToken;
                }

                _securityPoliciesToken = EnvironmentOverrides(_localConfiguration.securityPoliciesToken ?? string.Empty,
                        "NEW_RELIC_SECURITY_POLICIES_TOKEN")
                    .Trim();

                return _securityPoliciesToken;
            }
        }

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

        #region Attributes

        public virtual bool CaptureAttributes => _localConfiguration.attributes.enabled;

        private BoolConfigurationItem _canUseAttributesIncludes;

        public string CanUseAttributesIncludesSource
        {
            get
            {
                if (_canUseAttributesIncludes == null)
                {
                    _canUseAttributesIncludes = GetCanUseAttributesIncludesConfiguration();
                }

                return _canUseAttributesIncludes.Source;
            }
        }

        public virtual bool CanUseAttributesIncludes
        {
            get
            {
                if (_canUseAttributesIncludes == null)
                {
                    _canUseAttributesIncludes = GetCanUseAttributesIncludesConfiguration();
                }

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

        public virtual IEnumerable<string> CaptureAttributesIncludes
        {
            get
            {
                if (CanUseAttributesIncludes)
                {
                    return Memoizer.Memoize(ref _captureAttributesIncludes, () => new HashSet<string>(_localConfiguration.attributes.include));
                }

                return Memoizer.Memoize(ref _captureAttributesIncludes, () => new List<string>());
            }
        }

        private IEnumerable<string> _captureAttributesExcludes;

        public virtual IEnumerable<string> CaptureAttributesExcludes
        {
            get
            {
                return Memoizer.Memoize(ref _captureAttributesExcludes, () =>
                {
                    var configExcludes = _localConfiguration.attributes.exclude;
                    var deprecatedDisabledExcludes = GetDeprecatedExplicitlyDisabledParameters();
                    var deprecatedIgnoredExcludes = GetDeprecatedIgnoreParameters();
                    var allExcludes = configExcludes
                        .Concat(deprecatedDisabledExcludes)
                        .Concat(deprecatedIgnoredExcludes);

                    return new HashSet<string>(allExcludes);
                });
            }
        }

        private IEnumerable<string> _captureAttributesDefaultExcludes;

        public virtual IEnumerable<string> CaptureAttributesDefaultExcludes
        {
            get
            {
                return Memoizer.Memoize(ref _captureAttributesDefaultExcludes, () => new HashSet<string> { "identity.*" });
            }
        }

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

        public virtual bool TransactionEventsAttributesEnabled =>
            CaptureAttributes
            && _localConfiguration.transactionEvents.attributes.enabled
            && (!_localConfiguration.analyticsEvents.captureAttributesSpecified || _localConfiguration.analyticsEvents.captureAttributes);

        private HashSet<string> _transactionEventsAttributesInclude;
        public HashSet<string> TransactionEventsAttributesInclude
        {
            get
            {
                if (_transactionEventsAttributesInclude == null)
                {
                    _transactionEventsAttributesInclude = IsAttributesAllowedByConfigurableSecurityPolicy && TransactionEventsAttributesEnabled
                        ? new HashSet<string>(_localConfiguration.transactionEvents.attributes.include)
                        : new HashSet<string>();
                }

                return _transactionEventsAttributesInclude;
            }
        }

        private HashSet<string> _transactionEventsAttributesExclude;
        public HashSet<string> TransactionEventsAttributesExclude
        {
            get
            {
                if (_transactionEventsAttributesExclude == null)
                {
                    _transactionEventsAttributesExclude = new HashSet<string>(_localConfiguration.transactionEvents.attributes.exclude);
                }

                return _transactionEventsAttributesExclude;
            }
        }

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

            if (_localConfiguration.transactionTracer.captureAttributesSpecified)
            {
                return _localConfiguration.transactionTracer.captureAttributes;
            }

            return CaptureTransactionTraceAttributesDefault;
        }

        private IEnumerable<string> _captureTransactionTraceAttributesIncludes;

        public virtual IEnumerable<string> CaptureTransactionTraceAttributesIncludes
        {
            get
            {
                if (ShouldCaptureTransactionTraceAttributesIncludes())
                {
                    return Memoizer.Memoize(ref _captureTransactionTraceAttributesIncludes, () =>
                    {
                        var includes = new HashSet<string>(_localConfiguration.transactionTracer.attributes.include);

                        if (CaptureRequestParameters)
                        {
                            includes.Add("request.parameters.*");
                        }

                        if (DeprecatedCaptureIdentityParameters)
                        {
                            includes.Add("identity.*");
                        }

                        return includes;
                    });
                }

                return Memoizer.Memoize(ref _captureTransactionTraceAttributesIncludes, () => new List<string>());
            }
        }

        private bool ShouldCaptureTransactionTraceAttributesIncludes()
        {
            var shouldCapture = !HighSecurityModeEnabled && CaptureTransactionTraceAttributes;

            if (_securityPoliciesConfiguration.SecurityPolicyExistsFor(SecurityPoliciesConfiguration.AttributesIncludePolicyName))
            {
                shouldCapture = shouldCapture && _securityPoliciesConfiguration.AttributesInclude.Enabled;
            }

            return shouldCapture;
        }

        public virtual IEnumerable<string> CaptureTransactionTraceAttributesExcludes
        {
            get
            {
                return Memoizer.Memoize(ref _captureTransactionTraceAttributesExcludes, () => new HashSet<string>(_localConfiguration.transactionTracer.attributes.exclude));
            }
        }
        private IEnumerable<string> _captureTransactionTraceAttributesExcludes;


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

            if (_localConfiguration.errorCollector.captureAttributesSpecified)
            {
                return _localConfiguration.errorCollector.captureAttributes;
            }

            return CaptureErrorCollectorAttributesDefault;
        }


        private IEnumerable<string> _captureErrorCollectorAttributesIncludes;

        public virtual IEnumerable<string> CaptureErrorCollectorAttributesIncludes
        {
            get
            {
                if (ShouldCaptureErrorCollectorAttributesIncludes())
                {
                    return Memoizer.Memoize(ref _captureErrorCollectorAttributesIncludes, () =>
                    {
                        var includes = new HashSet<string>(_localConfiguration.errorCollector.attributes.include);

                        if (CaptureRequestParameters)
                        {
                            includes.Add("request.parameters.*");
                        }

                        if (DeprecatedCaptureIdentityParameters)
                        {
                            includes.Add("identity.*");
                        }

                        return includes;
                    });
                }

                return Memoizer.Memoize(ref _captureErrorCollectorAttributesExcludes, () => new List<string>());
            }
        }

        private bool ShouldCaptureErrorCollectorAttributesIncludes()
        {
            var shouldCapture = !HighSecurityModeEnabled && CaptureErrorCollectorAttributes;

            if (_securityPoliciesConfiguration.SecurityPolicyExistsFor(SecurityPoliciesConfiguration.AttributesIncludePolicyName))
            {
                shouldCapture = shouldCapture && _securityPoliciesConfiguration.AttributesInclude.Enabled;
            }

            return shouldCapture;
        }

        private IEnumerable<string> _captureErrorCollectorAttributesExcludes;

        public virtual IEnumerable<string> CaptureErrorCollectorAttributesExcludes
        {
            get
            {
                return Memoizer.Memoize(ref _captureErrorCollectorAttributesExcludes, () => new HashSet<string>(_localConfiguration.errorCollector.attributes.exclude));
            }
        }

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

            if (_localConfiguration.browserMonitoring.captureAttributesSpecified)
            {
                return _localConfiguration.browserMonitoring.captureAttributes;
            }

            return CaptureBrowserMonitoringAttributesDefault;
        }

        private IEnumerable<string> _captureBrowserMonitoringAttributesIncludes;

        public virtual IEnumerable<string> CaptureBrowserMonitoringAttributesIncludes
        {
            get
            {
                if (ShouldCaptureBrowserMonitoringAttributesIncludes())
                {
                    return Memoizer.Memoize(ref _captureBrowserMonitoringAttributesIncludes, () => new HashSet<string>(_localConfiguration.browserMonitoring.attributes.include));
                }

                return Memoizer.Memoize(ref _captureBrowserMonitoringAttributesIncludes, () => new List<string>());
            }
        }

        private bool ShouldCaptureBrowserMonitoringAttributesIncludes()
        {
            var shouldCapture = !HighSecurityModeEnabled && CaptureBrowserMonitoringAttributes;

            if (_securityPoliciesConfiguration.SecurityPolicyExistsFor(SecurityPoliciesConfiguration.AttributesIncludePolicyName))
            {
                shouldCapture = shouldCapture && _securityPoliciesConfiguration.AttributesInclude.Enabled;
            }

            return shouldCapture;
        }

        public virtual IEnumerable<string> CaptureBrowserMonitoringAttributesExcludes
        {
            get
            {
                return Memoizer.Memoize(ref _captureBrowserMonitoringAttributesExcludes, () => new HashSet<string>(_localConfiguration.browserMonitoring.attributes.exclude));
            }
        }
        private IEnumerable<string> _captureBrowserMonitoringAttributesExcludes;


        private BoolConfigurationItem _shouldCaptureCustomParameters;

        public string CaptureCustomParametersSource
        {
            get
            {
                if (_shouldCaptureCustomParameters == null)
                {
                    _shouldCaptureCustomParameters = GetShouldCaptureCustomParametersConfiguration();
                }

                return _shouldCaptureCustomParameters.Source;
            }
        }

        public virtual bool CaptureCustomParameters
        {
            get
            {
                if (_shouldCaptureCustomParameters == null)
                {
                    _shouldCaptureCustomParameters = GetShouldCaptureCustomParametersConfiguration();
                }

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

        /// This method combines logic for the deprecated parameterGroups.customParameters and the 
        /// newer customParameters added for implementation of Language Agent Security Policies (LASP). 
        /// parameterGroups.customParameters will be removed with a future major version release, but customParameters 
        /// must remain in order maintain the requirements of LASP and High Security Mode (HSM).  
        private bool GetLocalShouldCaptureCustomParameters()
        {
            var deprecatedSpecified = _localConfiguration.parameterGroups.customParameters.enabledSpecified;

            if ((_localConfiguration.customParameters.enabledSpecified == false)
                && (deprecatedSpecified == false))
            {
                return CaptureCustomParametersAttributesDefault;
            }

            var localConfigValue = true;
            if (_localConfiguration.customParameters.enabledSpecified)
            {
                localConfigValue = _localConfiguration.customParameters.enabled;
            }

            if (_localConfiguration.parameterGroups.customParameters.enabledSpecified)
            {
                LogDeprecatedPropertyUse("parameterGroups.customParameters", "customParameters");
                localConfigValue = localConfigValue && _localConfiguration.parameterGroups.customParameters.enabled;
            }

            return localConfigValue;
        }

        public virtual bool CaptureRequestParameters
        {
            get
            {
                var localAttributeValue = false;
                if (_localConfiguration.requestParameters.enabledSpecified)
                {
                    localAttributeValue = _localConfiguration.requestParameters.enabled;
                }
                var serverAttributeValue = _serverConfiguration.RpmConfig.CaptureParametersEnabled;
                var enabled = HighSecurityModeOverrides(false, ServerOverrides(serverAttributeValue, localAttributeValue));
                return enabled;
            }
        }

        #endregion

        #region Collector Connection

        public virtual string CollectorHost { get { return EnvironmentOverrides(_localConfiguration.service.host, @"NEW_RELIC_HOST"); } }
        public virtual int CollectorPort => EnvironmentOverrides(_localConfiguration.service.port > 0 ? _localConfiguration.service.port : (int?)null, "NEW_RELIC_PORT") ?? DefaultSslPort;
        public virtual bool CollectorSendDataOnExit { get { return _localConfiguration.service.sendDataOnExit; } }
        public virtual float CollectorSendDataOnExitThreshold { get { return _localConfiguration.service.sendDataOnExitThreshold; } }
        public virtual bool CollectorSendEnvironmentInfo { get { return _localConfiguration.service.sendEnvironmentInfo; } }
        public virtual bool CollectorSyncStartup { get { return _localConfiguration.service.syncStartup; } }
        public virtual uint CollectorTimeout { get { return (_localConfiguration.service.requestTimeout > 0) ? (uint)_localConfiguration.service.requestTimeout : CollectorSendDataOnExit ? 2000u : 60 * 2 * 1000; } }
        public virtual int CollectorMaxPayloadSizeInBytes { get { return _serverConfiguration.MaxPayloadSizeInBytes ?? MaxPayloadSizeInBytes; } }
        #endregion

        public virtual bool CompleteTransactionsOnThread { get { return _localConfiguration.service.completeTransactionsOnThread; } }

        public long ConfigurationVersion { get; private set; }

        #region Cross Application Tracing

        public virtual string CrossApplicationTracingCrossProcessId { get { return _serverConfiguration.CatId; } }

        private bool? _crossApplicationTracingEnabled;
        public virtual bool CrossApplicationTracingEnabled => _crossApplicationTracingEnabled ?? (_crossApplicationTracingEnabled = IsCatEnabled()).Value;

        private bool IsCatEnabled()
        {
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

        public TimeSpan SpanEventsHarvestCycle
        {
            get
            {
                return ServerOverrides(_serverConfiguration.EventHarvestConfig?.SpanEventHarvestCycle(), TimeSpan.FromMinutes(1));
            }
        }

        public bool SpanEventsAttributesEnabled => CaptureAttributes && _localConfiguration.spanEvents.attributes.enabled;

        private HashSet<string> _spanEventsAttributesInclude;
        public HashSet<string> SpanEventsAttributesInclude
        {
            get
            {
                if (_spanEventsAttributesInclude == null)
                {
                    _spanEventsAttributesInclude = IsAttributesAllowedByConfigurableSecurityPolicy && SpanEventsAttributesEnabled
                        ? new HashSet<string>(_localConfiguration.spanEvents.attributes.include)
                        : new HashSet<string>();
                }

                return _spanEventsAttributesInclude;
            }
        }

        private HashSet<string> _spanEventsAttributesExclude;
        public virtual HashSet<string> SpanEventsAttributesExclude
        {
            get
            {
                if (_spanEventsAttributesExclude == null)
                {
                    _spanEventsAttributesExclude = new HashSet<string>(_localConfiguration.spanEvents.attributes.exclude);
                }

                return _spanEventsAttributesExclude;
            }
        }

        #endregion

        #region Distributed Tracing

        private bool? _distributedTracingEnabled;
        public virtual bool DistributedTracingEnabled => _distributedTracingEnabled ?? (_distributedTracingEnabled = IsDistributedTracingEnabled()).Value;

        private bool IsDistributedTracingEnabled()
        {
            return EnvironmentOverrides(_localConfiguration.distributedTracing.enabled, "NEW_RELIC_DISTRIBUTED_TRACING_ENABLED");
        }

        public string PrimaryApplicationId => _serverConfiguration.PrimaryApplicationId;

        public string TrustedAccountKey => _serverConfiguration.TrustedAccountKey;

        public string AccountId => _serverConfiguration.AccountId;

        public int? SamplingTarget => _serverConfiguration.SamplingTarget;

        public int SpanEventsMaxSamplesStored => ServerOverrides(_serverConfiguration.EventHarvestConfig?.SpanEventHarvestLimit(), DefaultSpanEventsMaxSamplesStored);
        public int? SamplingTargetPeriodInSeconds => _serverConfiguration.SamplingTargetPeriodInSeconds;

        public bool PayloadSuccessMetricsEnabled => _localConfiguration.distributedTracing.enableSuccessMetrics;

        public bool ExcludeNewrelicHeader => _localConfiguration.distributedTracing.excludeNewrelicHeader;

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

        public virtual IEnumerable<string> IgnoreErrorsForAgentSettings { get; private set; }

        public IDictionary<string, IEnumerable<string>> IgnoreErrorsConfiguration { get; private set; }
        public IEnumerable<string> IgnoreErrorClassesForAgentSettings { get; private set; }
        public IDictionary<string, IEnumerable<string>> IgnoreErrorMessagesForAgentSettings { get; private set; }

        private IEnumerable<MatchRule> ParseExpectedStatusCodesArray(IEnumerable<string> expectedStatusCodeArray)
        {
            var expectedStatusCodes = new List<MatchRule>();

            if(expectedStatusCodeArray == null)
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

                if(matchRule == null)
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

        #endregion

        public Dictionary<string, string> RequestHeadersMap => _serverConfiguration.RequestHeadersMap;

        public virtual string EncodingKey { get { return _serverConfiguration.EncodingKey; } }

        public virtual string EntityGuid { get { return _serverConfiguration.EntityGuid; } }

        public virtual bool HighSecurityModeEnabled => _localConfiguration.highSecurity.enabled;


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

        public virtual string Labels
        {
            get
            {
                return EnvironmentOverrides(_localConfiguration.labels, @"NEW_RELIC_LABELS");
            }
        }

        #endregion

        #region Proxy

        public virtual string ProxyHost { get { return _localConfiguration.service.proxy.host; } }
        public virtual string ProxyUriPath { get { return _localConfiguration.service.proxy.uriPath; } }
        public virtual int ProxyPort { get { return _localConfiguration.service.proxy.port; } }
        public virtual string ProxyUsername { get { return _localConfiguration.service.proxy.user; } }

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
                    var hasObfuscatedPassword = !string.IsNullOrWhiteSpace(_localConfiguration.service.proxy.passwordObfuscated);

                    if (hasObscuringKey && hasObfuscatedPassword)
                    {
                         _proxyPassword = Strings.Base64Decode(_localConfiguration.service.proxy.passwordObfuscated, ObscuringKey);
                    }
                    else
                    {
                        _proxyPassword = _localConfiguration.service.proxy.password;
                    }

                    _proxyPasswordEvaluated = true;
                }

                return _proxyPassword;
            }
        }

        public virtual string ProxyDomain { get { return _localConfiguration.service.proxy.domain ?? string.Empty; } }

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

        public bool SlowSqlEnabled
        {
            get { return ServerOverrides(_serverConfiguration.RpmConfig.SlowSqlEnabled, _localConfiguration.slowSql.enabled); }
        }

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

        public virtual bool SqlExplainPlansEnabled
        {
            get
            {
                return ServerOverrides(_serverConfiguration.RpmConfig.TransactionTracerExplainEnabled,
                    _localConfiguration.transactionTracer.explainEnabled);
            }
        }

        public virtual int SqlExplainPlansMax { get { return _localConfiguration.transactionTracer.maxExplainPlans; } }
        public virtual uint SqlStatementsPerTransaction { get { return 500; } }
        public virtual int SqlTracesPerPeriod { get { return 10; } }

        #endregion

        public virtual int StackTraceMaximumFrames { get { return _localConfiguration.maxStackTraceLines; } }
        public virtual IEnumerable<string> HttpStatusCodesToIgnore { get; private set; }

        public virtual IEnumerable<string> ThreadProfilingIgnoreMethods { get { return _localConfiguration.threadProfiling ?? new List<string>(); } }

        #region Custom Events


        private BoolConfigurationItem _customEventsAreEnabled;

        public string CustomEventsEnabledSource
        {
            get
            {
                if (_customEventsAreEnabled == null)
                {
                    _customEventsAreEnabled = GetCustomEventsAreEnabledConfiguration();
                }

                return _customEventsAreEnabled.Source;
            }
        }

        public virtual bool CustomEventsEnabled
        {
            get
            {
                if (_customEventsAreEnabled == null)
                {
                    _customEventsAreEnabled = GetCustomEventsAreEnabledConfiguration();
                }

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
                return (int)EnvironmentOverrides(
                    ServerOverrides(_serverConfiguration.EventHarvestConfig?.CustomEventHarvestLimit(),
                        _localConfiguration.customEvents.maximumSamplesStored),
                    "MAX_EVENT_SAMPLES_STORED");
            }
        }

        public TimeSpan CustomEventsHarvestCycle
        {
            get
            {
                return ServerOverrides(_serverConfiguration.EventHarvestConfig?.CustomEventHarvestCycle(), TimeSpan.FromMinutes(1));
            }
        }

        public bool CustomEventsAttributesEnabled => CaptureAttributes && _localConfiguration.customEvents.attributes.enabled;


        private HashSet<string> _customEventsAttributesInclude;
        public HashSet<string> CustomEventsAttributesInclude
        {
            get
            {
                if (_customEventsAttributesInclude == null)
                {
                    _customEventsAttributesInclude = IsAttributesAllowedByConfigurableSecurityPolicy && CustomEventsAttributesEnabled
                        ? new HashSet<string>(_localConfiguration.customEvents.attributes.include)
                        : new HashSet<string>();
                }

                return _customEventsAttributesInclude;
            }
        }

        private HashSet<string> _customEventsAttributesExclude;
        public HashSet<string> CustomEventsAttributesExclude
        {
            get
            {
                if (_customEventsAttributesExclude == null)
                {
                    _customEventsAttributesExclude = new HashSet<string>(_localConfiguration.customEvents.attributes.exclude);
                }

                return _customEventsAttributesExclude;
            }
        }


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
                    _localConfiguration.transactionEvents.enabled
                    && (!_localConfiguration.analyticsEvents.enabledSpecified || _localConfiguration.analyticsEvents.enabled));
            }
        }

        public virtual int TransactionEventsMaximumSamplesStored
        {
            get
            {
                var maxValue = _localConfiguration.transactionEvents.maximumSamplesStored;
                if (_localConfiguration.analyticsEvents.maximumSamplesStoredSpecified)
                {
                    LogDeprecatedPropertyUse("analyticsEvents.maximumSamplesStored", "transactionEvents.maximumSamplesStored");
                    maxValue = _localConfiguration.analyticsEvents.maximumSamplesStored;
                }

                return (int)EnvironmentOverrides(
                    ServerOverrides(_serverConfiguration.EventHarvestConfig?.TransactionEventHarvestLimit(), maxValue),
                    "MAX_TRANSACTION_SAMPLES_STORED");
            }
        }

        public TimeSpan TransactionEventsHarvestCycle
        {
            get
            {
                return ServerOverrides(_serverConfiguration.EventHarvestConfig?.TransactionEventHarvestCycle(), TimeSpan.FromMinutes(1));
            }
        }

        public virtual bool TransactionEventsTransactionsEnabled
        {
            get
            {
                var enabled = TransactionEventsTransactionsEnabledDefault;
                if (_localConfiguration.transactionEvents.transactions.enabledSpecified)
                {
                    enabled = _localConfiguration.transactionEvents.transactions.enabled;
                }
                if (_localConfiguration.analyticsEvents.transactions.enabledSpecified)
                {
                    LogDeprecatedPropertyUse("analyticsEvents.transactions.enabled", "transactionEvents.transactions.enabled");
                    enabled = _localConfiguration.analyticsEvents.transactions.enabled;
                }
                return enabled;
            }
        }

        #endregion

        #region Transaction Tracer

        public virtual TimeSpan TransactionTraceApdexF { get { return TransactionTraceApdexT.Multiply(4); } }
        public virtual TimeSpan TransactionTraceApdexT { get { return TimeSpan.FromSeconds(ServerOverrides(_serverConfiguration.ApdexT, 0.5)); } }

        public virtual TimeSpan TransactionTraceThreshold
        {
            get
            {
                return (_serverConfiguration.RpmConfig.TransactionTracerThreshold == null)
                    ? ParseTransactionThreshold(_localConfiguration.transactionTracer.transactionThreshold, TimeSpan.FromMilliseconds)
                    : ParseTransactionThreshold(_serverConfiguration.RpmConfig.TransactionTracerThreshold.ToString(), TimeSpan.FromSeconds);
            }
        }

        public virtual bool TransactionTracerEnabled
        {
            get
            {
                var configuredValue = ServerOverrides(_serverConfiguration.RpmConfig.TransactionTracerEnabled, _localConfiguration.transactionTracer.enabled);
                return ServerCanDisable(_serverConfiguration.TraceCollectionEnabled, configuredValue);
            }
        }
        public virtual int TransactionTracerMaxSegments { get { return _localConfiguration.transactionTracer.maxSegments; } }

        private RecordSqlConfigurationItem _recordSqlConfiguration;

        public string TransactionTracerRecordSqlSource
        {
            get
            {
                if (_recordSqlConfiguration == null)
                {
                    _recordSqlConfiguration = GetRecordSqlConfiguration();
                }

                return _recordSqlConfiguration.Source;
            }
        }

        public virtual string TransactionTracerRecordSql
        {
            get
            {
                if (_recordSqlConfiguration == null)
                {
                    _recordSqlConfiguration = GetRecordSqlConfiguration();
                }

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

        public virtual TimeSpan TransactionTracerStackThreshold { get { return ServerOverrides((TimeSpanExtensions.FromSeconds(_serverConfiguration.RpmConfig.TransactionTracerStackThreshold)), TimeSpan.FromMilliseconds(_localConfiguration.transactionTracer.stackTraceThreshold)); } }
        public virtual int TransactionTracerMaxStackTraces { get { return _localConfiguration.transactionTracer.maxStackTrace; } }
        public virtual IEnumerable<Regex> RequestPathExclusionList
        {
            get
            {
                if (_requestPathExclusionList == null)
                {
                    _requestPathExclusionList = ReadUrlBlacklist(_localConfiguration);
                }

                return _requestPathExclusionList;
            }
        }

        private IList<Regex> _requestPathExclusionList;

        #endregion

        public virtual IEnumerable<long> TrustedAccountIds { get { return _serverConfiguration.TrustedIds ?? new List<long>(); } }

        public bool UsingServerSideConfig { get { return _serverConfiguration.UsingServerSideConfig; } }

        #region Metric naming

        private IEnumerable<RegexRule> _metricNameRegexRules;
        public IEnumerable<RegexRule> MetricNameRegexRules
        {
            get { return _metricNameRegexRules ?? (_metricNameRegexRules = GetRegexRules(_serverConfiguration.MetricNameRegexRules)); }
        }

        private IEnumerable<RegexRule> _transactionNameRegexRules;
        public IEnumerable<RegexRule> TransactionNameRegexRules
        {
            get { return _transactionNameRegexRules ?? (_transactionNameRegexRules = GetRegexRules(_serverConfiguration.TransactionNameRegexRules)); }
        }

        private IEnumerable<RegexRule> _rrlRegexRules;
        public IEnumerable<RegexRule> UrlRegexRules
        {
            get { return _rrlRegexRules ?? (_rrlRegexRules = GetRegexRules(_serverConfiguration.UrlRegexRules)); }
        }

        private IDictionary<string, IEnumerable<string>> _transactionNameWhitelistRules;
        public IDictionary<string, IEnumerable<string>> TransactionNameWhitelistRules
        {
            get { return _transactionNameWhitelistRules ?? (_transactionNameWhitelistRules = GetWhitelistRules(_serverConfiguration.TransactionNameWhitelistRules)); }
        }

        private IDictionary<string, double> _webTransactionsApdex;

        public IDictionary<string, double> WebTransactionsApdex
        {
            get { return _webTransactionsApdex ?? (_webTransactionsApdex = _serverConfiguration.WebTransactionsApdex ?? new Dictionary<string, double>()); }
        }

        #endregion Metric naming

        public string NewRelicConfigFilePath { get { return _localConfiguration.ConfigurationFileName; } }

        #region Utilization

        public bool UtilizationDetectAws
        {
            get { return _localConfiguration.utilization.detectAws; }
        }

        public bool UtilizationDetectAzure
        {
            get { return _localConfiguration.utilization.detectAzure; }
        }

        public bool UtilizationDetectGcp
        {
            get { return _localConfiguration.utilization.detectGcp; }
        }

        public bool UtilizationDetectPcf
        {
            get { return _localConfiguration.utilization.detectPcf; }
        }

        public bool UtilizationDetectDocker
        {
            get { return _localConfiguration.utilization.detectDocker; }
        }

        public bool UtilizationDetectKubernetes
        {
            get { return _localConfiguration.utilization.detectKubernetes; }
        }

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

        private bool? _diagnosticsCaptureAgentTiming;
        public bool DiagnosticsCaptureAgentTiming
        {
            get
            {
                if (_diagnosticsCaptureAgentTiming == null)
                {
                    UpdateDiagnosticsAgentTimingSettings();
                }

                return _diagnosticsCaptureAgentTiming.Value;

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

                return _diagnosticsCaptureAgentTimingFrequency.Value;

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
        public bool ForceSynchronousTimingCalculationHttpClient
        {
            get
            {
                return _forceSynchronousTimingCalculationHttpClient.HasValue
                    ? _forceSynchronousTimingCalculationHttpClient.Value
                    : (_forceSynchronousTimingCalculationHttpClient = TryGetAppSettingAsBoolWithDefault("ForceSynchronousTimingCalculation.HttpClient", false)).Value;
            }
        }

        #endregion

        #region Helpers

        private TimeSpan ParseTransactionThreshold(string threshold, Func<double, TimeSpan> numberToTimeSpanConverter)
        {
            if (string.IsNullOrEmpty(threshold))
                return TransactionTraceApdexF;

            double parsedTransactionThreshold;
            return double.TryParse(threshold, out parsedTransactionThreshold)
                ? numberToTimeSpanConverter(parsedTransactionThreshold)
                : TransactionTraceApdexF;
        }

        private static bool ServerCanDisable(bool? server, bool local)
        {
            if (server == null) return local;
            return server.Value && local;
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

        private string EnvironmentOverrides(string local, params string[] environmentVariableNames)
        {
            return (environmentVariableNames ?? Enumerable.Empty<string>())
                .Select(_environment.GetEnvironmentVariable)
                .Where(value => value != null)
                .FirstOrDefault()
                ?? local;
        }

        private uint? EnvironmentOverrides(uint? local, params string[] environmentVariableNames)
        {
            var env = environmentVariableNames
                .Select(_environment.GetEnvironmentVariable)
                .FirstOrDefault(value => value != null);

            return uint.TryParse(env, out uint parsedValue) ? parsedValue : local;
        }

        private int? EnvironmentOverrides(int? local, params string[] environmentVariableNames)
        {
            var env = environmentVariableNames
                .Select(_environment.GetEnvironmentVariable)
                .FirstOrDefault(value => value != null);

            return int.TryParse(env, out int parsedValue) ? parsedValue : local;
        }

        private bool EnvironmentOverrides(bool local, params string[] environmentVariableNames)
        {
            var env = environmentVariableNames
                .Select(_environment.GetEnvironmentVariable)
                .FirstOrDefault(value => value != null);

            return bool.TryParse(env, out var parsedValue) ? parsedValue : local;

        }

        private IList<Regex> ReadUrlBlacklist(configuration config)
        {
            var list = new List<Regex>();

            if (config.browserMonitoring.requestPathsExcluded != null && config.browserMonitoring.requestPathsExcluded.Count > 0)
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
                        Log.ErrorFormat("A Browser Monitoring Request Path failed regular expression parsing: {0}",
                            p.regex, ex);
                    }
                }
            }

            return list;
        }

        public static IEnumerable<RegexRule> GetRegexRules(IEnumerable<ServerConfiguration.RegexRule> rules)
        {
            if (rules == null)
                return new List<RegexRule>();

            return rules
                .Select(TryGetRegexRule)
                .Where(rule => rule != null)
                .Select(rule => rule.Value)
                .ToList();
        }

        private static RegexRule? TryGetRegexRule(ServerConfiguration.RegexRule rule)
        {
            if (rule == null)
                return null;
            if (rule.MatchExpression == null)
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
            if (whitelistRules == null)
                return new Dictionary<string, IEnumerable<string>>();

            return whitelistRules
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
                Log.WarnFormat("Ignoring transaction_segment_term with null terms for prefix '{0}'", rule.Prefix);
                return null;
            }

            var prefix = TryGetValidPrefix(rule.Prefix);
            if (prefix == null)
            {
                Log.WarnFormat("Ignoring transaction_segment_term with invalid prefix '{0}'", rule.Prefix);
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

            var expectedStatusCodesArray = ServerOverrides(expectedStatusCodesArrayServer, expectedStatusCodesArrayLocal);

            ExpectedStatusCodes = ParseExpectedStatusCodesArray(expectedStatusCodesArray);
            ExpectedErrorStatusCodesForAgentSettings = expectedStatusCodesArray ?? new string[0];

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

            var ignoreClassesFromErrorCollectorIgnoreErrorsConfig = ServerOverrides(_serverConfiguration.RpmConfig.ErrorCollectorErrorsToIgnore, _localConfiguration.errorCollector.ignoreErrors.exception);

            var ignoreClasses = ServerOverrides(_serverConfiguration.RpmConfig.ErrorCollectorIgnoreClasses, _localConfiguration.errorCollector.ignoreClasses.errorClass)
                .Concat(ignoreClassesFromErrorCollectorIgnoreErrorsConfig);

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

            var ignoreStatusCodes = _serverConfiguration.RpmConfig.ErrorCollectorStatusCodesToIgnore;
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

            IgnoreErrorsForAgentSettings = ignoreClassesFromErrorCollectorIgnoreErrorsConfig;
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

        private static int? GetNullableIntValue(bool specified, int value)
        {
            return specified ? value : default(int?);
        }

        #endregion

        #region deprecated parameter group settings

        private void LogDeprecationWarnings()
        {
            if (_localConfiguration.analyticsEvents.captureAttributesSpecified)
            {
                LogDeprecatedPropertyUse("analyticsEvents.captureAttributes", "transaction_events.attributes.enabled");
            }
            if (_localConfiguration.transactionTracer.captureAttributesSpecified)
            {
                LogDeprecatedPropertyUse("transactionTracer.captureAttributes", "transactionTracer.attributes.enabled");
            }
            if (_localConfiguration.errorCollector.captureAttributesSpecified)
            {
                LogDeprecatedPropertyUse("errorCollector.captureAttributes", "errorCollector.attributes.enabled");
            }
            if (_localConfiguration.browserMonitoring.captureAttributesSpecified)
            {
                LogDeprecatedPropertyUse("browserMonitoring.captureAttributes", "browserMonitoring.attributes.enabled");
            }
            if (_localConfiguration.analyticsEvents.enabledSpecified)
            {
                LogDeprecatedPropertyUse("analyticsEvents.enabled", "transactionEvents.enabled");
            }
        }

        private void LogDeprecatedPropertyUse(string deprecatedPropertyName, string newPropertyName)
        {
            Log.WarnFormat("Deprecated configuration property '{0}'.  Use '{1}'.  See http://docs.newrelic.com for details.", deprecatedPropertyName, newPropertyName);
        }

        private IEnumerable<string> GetDeprecatedExplicitlyDisabledParameters()
        {
            var disabledProperties = new List<string>();

            if (_localConfiguration.parameterGroups != null)
            {
                if (_localConfiguration.parameterGroups.responseHeaderParameters != null &&
                    _localConfiguration.parameterGroups.responseHeaderParameters.enabledSpecified &&
                    !_localConfiguration.parameterGroups.responseHeaderParameters.enabled)
                {
                    LogDeprecatedPropertyUse("parameterGroups.responseHeaderParameters.enabled", "attributes.exclude");
                    disabledProperties.Add("response.headers.*");
                }

                // Log the deprecated parameter, disabling custom parameters is handled separately in AttributeService
                if (_localConfiguration.parameterGroups.customParameters != null &&
                    _localConfiguration.parameterGroups.customParameters.enabledSpecified &&
                    !_localConfiguration.parameterGroups.customParameters.enabled)
                {
                    LogDeprecatedPropertyUse("parameterGroups.customParameters.enabled", "attributes.exclude");
                }

                if (_localConfiguration.parameterGroups.requestHeaderParameters != null &&
                    _localConfiguration.parameterGroups.requestHeaderParameters.enabledSpecified &&
                    !_localConfiguration.parameterGroups.requestHeaderParameters.enabled)
                {
                    LogDeprecatedPropertyUse("parameterGroups.requestHeaderParameters.enabled", "attributes.exclude");
                    disabledProperties.Add("request.headers.*");
                }
            }
            return disabledProperties;
        }

        private bool DeprecatedCaptureIdentityParameters
        {
            get
            {
                var localAttributeValue = false;
                if (_localConfiguration.parameterGroups.identityParameters.enabledSpecified)
                {
                    localAttributeValue = _localConfiguration.parameterGroups.identityParameters.enabled;
                }
                return localAttributeValue;
            }
        }

        int? _databaseStatementCacheCapcity = null;

        public int DatabaseStatementCacheCapcity => _databaseStatementCacheCapcity ?? (_databaseStatementCacheCapcity =
            TryGetAppSettingAsIntWithDefault("SqlStatementCacheCapacity", DefaultSqlStatementCacheCapacity)).Value;

        private IEnumerable<string> GetDeprecatedIgnoreParameters()
        {
            var ignoreParameters = new List<string>();
            ignoreParameters.AddRange(DeprecatedIgnoreCustomParameters());
            ignoreParameters.AddRange(DeprecatedIgnoreIdentityParameters().Select(param => "identity." + param));
            ignoreParameters.AddRange(DeprecatedIgnoreResponseHeaderParameters().Select(param => "response.headers." + param));
            ignoreParameters.AddRange(DeprecatedIgnoreRequestHeaderParameters().Select(param => "request.headers." + param));
            ignoreParameters.AddRange(DeprecatedIgnoreRequestParameters().Select(param => "request.parameters." + param));

            return ignoreParameters.Distinct();
        }

        private IEnumerable<string> DeprecatedIgnoreCustomParameters()
        {
            if (_localConfiguration.parameterGroups != null
                && _localConfiguration.parameterGroups.customParameters != null
                && _localConfiguration.parameterGroups.customParameters.ignore != null
                && _localConfiguration.parameterGroups.customParameters.ignore.Count > 0)
            {
                LogDeprecatedPropertyUse("parameterGroups.customParameters.ignore", "attributes.exclude");
                return _localConfiguration.parameterGroups.customParameters.ignore;
            }
            return Enumerable.Empty<string>();
        }

        private IEnumerable<string> DeprecatedIgnoreIdentityParameters()
        {
            if (_localConfiguration.parameterGroups != null
                && _localConfiguration.parameterGroups.identityParameters != null
                && _localConfiguration.parameterGroups.identityParameters.ignore != null
                && _localConfiguration.parameterGroups.identityParameters.ignore.Count > 0)
            {
                LogDeprecatedPropertyUse("parameterGroups.identityParameters.ignore", "attributes.exclude");
                return _localConfiguration.parameterGroups.identityParameters.ignore;
            }
            return Enumerable.Empty<string>();
        }

        private IEnumerable<string> DeprecatedIgnoreResponseHeaderParameters()
        {
            if (_localConfiguration.parameterGroups != null
                && _localConfiguration.parameterGroups.responseHeaderParameters != null
                && _localConfiguration.parameterGroups.responseHeaderParameters.ignore != null
                && _localConfiguration.parameterGroups.responseHeaderParameters.ignore.Count > 0)
            {
                LogDeprecatedPropertyUse("parameterGroups.responseHeaderParameters.ignore", "attributes.exclude");
                return _localConfiguration.parameterGroups.responseHeaderParameters.ignore;
            }
            return Enumerable.Empty<string>();
        }

        private IEnumerable<string> DeprecatedIgnoreRequestHeaderParameters()
        {
            if (_localConfiguration.parameterGroups != null
                && _localConfiguration.parameterGroups.requestHeaderParameters != null
                && _localConfiguration.parameterGroups.requestHeaderParameters.ignore != null
                && _localConfiguration.parameterGroups.requestHeaderParameters.ignore.Count > 0)
            {
                LogDeprecatedPropertyUse("parameterGroups.requestHeaderParameters.ignore", "attributes.exclude");
                return _localConfiguration.parameterGroups.requestHeaderParameters.ignore;
            }
            return Enumerable.Empty<string>();
        }

        private IEnumerable<string> DeprecatedIgnoreRequestParameters()
        {
            if (_localConfiguration.requestParameters != null
                && _localConfiguration.requestParameters.ignore != null
                && _localConfiguration.requestParameters.ignore.Count > 0)
            {
                LogDeprecatedPropertyUse("requestParameters.ignore", "attributes.exclude");
                var requestParams = ServerOverrides(_serverConfiguration.RpmConfig.ParametersToIgnore, _localConfiguration.requestParameters.ignore);
                return requestParams;
            }
            return Enumerable.Empty<string>();
        }

        #endregion

        private const bool CaptureTransactionTraceAttributesDefault = true;
        private const bool CaptureErrorCollectorAttributesDefault = true;
        private const bool CaptureBrowserMonitoringAttributesDefault = false;
        private const bool CaptureCustomParametersAttributesDefault = true;

        private const bool TransactionEventsTransactionsEnabledDefault = true;
        private const int MaxPayloadSizeInBytes = 1000000; // 1 MiB
    }
}

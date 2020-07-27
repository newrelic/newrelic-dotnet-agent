using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Config;
using NewRelic.Agent.Core.Logging;
using NewRelic.Agent.Core.Metric;
using NewRelic.Memoization;
using NewRelic.SystemExtensions;
using NewRelic.SystemExtensions.Collections.Generic;
using NewRelic.SystemInterfaces;
using NewRelic.SystemInterfaces.Web;

namespace NewRelic.Agent.Core.Configuration
{
    /// <summary>
    /// Default implementation of IConfiguration.  This should only be used by ConfigurationService.  If you need configuration, get it from the ConfigurationService, not here.
    /// </summary>
    public class DefaultConfiguration : IConfiguration
    {
#if NETSTANDARD2_0
        public const string NewRelicHomeEnvironmentVariable = "CORECLR_NEWRELIC_HOME";
        public const string NewRelicInstallPathEnvironmentVariable = "CORECLR_NEWRELIC_INSTALL_PATH";
#else
        public const string NewRelicHomeEnvironmentVariable = "NEWRELIC_HOME";
        public const string NewRelicInstallPathEnvironmentVariable = "NEWRELIC_INSTALL_PATH";
#endif

        private static long _currentConfigurationVersion;
        private readonly IEnvironment _environment = new EnvironmentMock();
        private readonly IProcessStatic _processStatic = new ProcessStatic();
        private readonly IHttpRuntimeStatic _httpRuntimeStatic = new HttpRuntimeStatic();
        private readonly IConfigurationManagerStatic _configurationManagerStatic = new ConfigurationManagerStaticMock();

        /// <summary>
        /// Default configuration.  It will contain reasonable default values for everything and never anything more.  Useful when you don't have configuration off disk or a collector response yet.
        /// </summary>
        public static readonly DefaultConfiguration Instance = new DefaultConfiguration();
        private readonly configuration _localConfiguration = new configuration();
        private readonly ServerConfiguration _serverConfiguration = ServerConfiguration.GetDefault();
        private readonly RunTimeConfiguration _runTimeConfiguration = new RunTimeConfiguration();

        /// <summary>
        /// _localConfiguration.AppSettings will be loaded into this field
        /// </summary>
        private readonly IDictionary<string, string> _appSettings;

        /// <summary>
        /// Default configuration constructor.  It will contain reasonable default values for everything and never anything more.
        /// </summary>
        private DefaultConfiguration()
        {
            ConfigurationVersion = Interlocked.Increment(ref _currentConfigurationVersion);
        }

        protected DefaultConfiguration(IEnvironment environment, configuration localConfiguration, ServerConfiguration serverConfiguration, RunTimeConfiguration runTimeConfiguration, IProcessStatic processStatic, IHttpRuntimeStatic httpRuntimeStatic, IConfigurationManagerStatic configurationManagerStatic)
            : this()
        {
            _environment = environment;
            _processStatic = processStatic;
            _httpRuntimeStatic = httpRuntimeStatic;
            _configurationManagerStatic = configurationManagerStatic;

            if (localConfiguration != null)
                _localConfiguration = localConfiguration;
            if (serverConfiguration != null)
                _serverConfiguration = serverConfiguration;
            if (runTimeConfiguration != null)
                _runTimeConfiguration = runTimeConfiguration;

            LogDeprecationWarnings();

            _appSettings = TransformAppSettings();
        }
        private IDictionary<string, string> TransformAppSettings()
        {
            if (_localConfiguration.appSettings == null)
                return new Dictionary<string, string>();

            return _localConfiguration.appSettings
                .Where(setting => setting != null)
                .Select(setting => new KeyValuePair<string, string>(setting.key, setting.value))
                .ToDictionary(IEnumerableExtensions.DuplicateKeyBehavior.KeepFirst);
        }

        private bool TryGetAppSettingAsBooleanWithDefault(string key, bool defaultValue)
        {
            var value = _appSettings.GetValueOrDefault(key);

            bool parsedBoolean;
            var parsedSuccessfully = bool.TryParse(value, out parsedBoolean);
            if (!parsedSuccessfully)
                return defaultValue;

            return parsedBoolean;
        }

        private int TryGetAppSettingAsIntWithDefault(string key, int defaultValue)
        {
            var value = _appSettings.GetValueOrDefault(key);

            int parsedInt;
            var parsedSuccessfully = int.TryParse(value, out parsedInt);
            if (!parsedSuccessfully)
                return defaultValue;

            return parsedInt;
        }


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
                return runtimeAppNames;

            var appName = _configurationManagerStatic.GetAppSetting("NewRelic.AppName")
                ?? _environment.GetEnvironmentVariable("IISEXPRESS_SITENAME")
                ?? _environment.GetEnvironmentVariable("NEW_RELIC_APP_NAME")
                ?? _environment.GetEnvironmentVariable("RoleName");
            if (appName != null)
                return appName.Split(',');

            if (_localConfiguration.application.name.Count > 0)
                return _localConfiguration.application.name;

            appName = _environment.GetEnvironmentVariable("APP_POOL_ID");
            if (appName != null)
                return appName.Split(',');

            if (_httpRuntimeStatic.AppDomainAppVirtualPath == null)
                return new List<string> { _processStatic.GetCurrentProcess().ProcessName };

            throw new Exception("An application name must be provided");
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

        #region Attributes

        public virtual bool CaptureAttributes { get { return _localConfiguration.attributes.enabled; } }

        public virtual IEnumerable<string> CaptureAttributesIncludes
        {
            get
            {
                return Memoizer.Memoize(ref _captureAttributesIncludes, () => new HashSet<string>(_localConfiguration.attributes.include));
            }
        }
        private IEnumerable<string> _captureAttributesIncludes;

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
        private IEnumerable<string> _captureAttributesExcludes;

        public virtual IEnumerable<string> CaptureAttributesDefaultExcludes
        {
            get
            {
                return Memoizer.Memoize(ref _captureAttributesDefaultExcludes, () => new HashSet<string> { "service.request.*", "identity.*" });
            }
        }
        private IEnumerable<string> _captureAttributesDefaultExcludes;

        public virtual bool CaptureTransactionEventsAttributes
        {
            get
            {
                if (_localConfiguration.transactionEvents.attributes.enabledSpecified)
                {
                    return _localConfiguration.transactionEvents.attributes.enabled;
                }
                else if (_localConfiguration.analyticsEvents.captureAttributesSpecified)
                {
                    return _localConfiguration.analyticsEvents.captureAttributes;
                }
                else
                {
                    return CaptureTransactionEventsAttributesDefault;
                }
            }
        }

        public virtual IEnumerable<string> CaptureTransactionEventAttributesIncludes
        {
            get
            {
                return Memoizer.Memoize(ref _captureTransactionEventAttributesIncludes, () => new HashSet<string>(_localConfiguration.transactionEvents.attributes.include));
            }
        }
        private IEnumerable<string> _captureTransactionEventAttributesIncludes;

        public virtual IEnumerable<string> CaptureTransactionEventAttributesExcludes
        {
            get
            {
                return Memoizer.Memoize(ref _captureTransactionEventAttributesExcludes, () => new HashSet<string>(_localConfiguration.transactionEvents.attributes.exclude));
            }
        }
        private IEnumerable<string> _captureTransactionEventAttributesExcludes;

        public virtual bool CaptureTransactionTraceAttributes
        {
            get
            {
                if (_localConfiguration.transactionTracer.attributes.enabledSpecified)
                {
                    return _localConfiguration.transactionTracer.attributes.enabled;
                }
                else if (_localConfiguration.transactionTracer.captureAttributesSpecified)
                {
                    return _localConfiguration.transactionTracer.captureAttributes;
                }
                else
                {
                    return CaptureTransactionTraceAttributesDefault;
                }
            }
        }

        public virtual IEnumerable<string> CaptureTransactionTraceAttributesIncludes
        {
            get
            {
                return Memoizer.Memoize(ref _captureTransactionTraceAttributesIncludes, () =>
                {
                    var includes = new HashSet<string>(_localConfiguration.transactionTracer.attributes.include);
                    if (CaptureRequestParameters)
                        includes.Add("request.parameters.*");
                    if (DeprecatedCaptureServiceRequestParameters)
                        includes.Add("service.request.*");
                    if (DeprecatedCaptureIdentityParameters)
                        includes.Add("identity.*");
                    return includes;
                });
            }
        }
        private IEnumerable<string> _captureTransactionTraceAttributesIncludes;

        public virtual IEnumerable<string> CaptureTransactionTraceAttributesExcludes
        {
            get
            {
                return Memoizer.Memoize(ref _captureTransactionTraceAttributesExcludes, () => new HashSet<string>(_localConfiguration.transactionTracer.attributes.exclude));
            }
        }
        private IEnumerable<string> _captureTransactionTraceAttributesExcludes;

        public virtual bool CaptureErrorCollectorAttributes
        {
            get
            {
                if (_localConfiguration.errorCollector.attributes.enabledSpecified)
                {
                    return _localConfiguration.errorCollector.attributes.enabled;
                }
                else if (_localConfiguration.errorCollector.captureAttributesSpecified)
                {
                    return _localConfiguration.errorCollector.captureAttributes;
                }
                else
                {
                    return CaptureErrorCollectorAttributesDefault;
                }
            }
        }

        public virtual IEnumerable<string> CaptureErrorCollectorAttributesIncludes
        {
            get
            {
                return Memoizer.Memoize(ref _captureErrorCollectorAttributesIncludes, () =>
                {
                    var includes = new HashSet<string>(_localConfiguration.errorCollector.attributes.include);
                    if (CaptureRequestParameters)
                        includes.Add("request.parameters.*");
                    if (DeprecatedCaptureServiceRequestParameters)
                        includes.Add("service.request.*");
                    if (DeprecatedCaptureIdentityParameters)
                        includes.Add("identity.*");
                    return includes;
                });
            }
        }
        private IEnumerable<string> _captureErrorCollectorAttributesIncludes;

        public virtual IEnumerable<string> CaptureErrorCollectorAttributesExcludes
        {
            get
            {
                return Memoizer.Memoize(ref _captureErrorCollectorAttributesExcludes, () => new HashSet<string>(_localConfiguration.errorCollector.attributes.exclude));
            }
        }
        private IEnumerable<string> _captureErrorCollectorAttributesExcludes;

        public virtual bool CaptureBrowserMonitoringAttributes
        {
            get
            {
                if (_localConfiguration.browserMonitoring.attributes.enabledSpecified)
                {
                    return _localConfiguration.browserMonitoring.attributes.enabled;
                }
                else if (_localConfiguration.browserMonitoring.captureAttributesSpecified)
                {
                    return _localConfiguration.browserMonitoring.captureAttributes;
                }
                else
                {
                    return CaptureBrowserMonitoringAttributesDefault;
                }
            }
        }

        public virtual IEnumerable<string> CaptureBrowserMonitoringAttributesIncludes
        {
            get
            {
                return Memoizer.Memoize(ref _captureBrowserMonitoringAttributesIncludes, () => new HashSet<string>(_localConfiguration.browserMonitoring.attributes.include));
            }
        }
        private IEnumerable<string> _captureBrowserMonitoringAttributesIncludes;

        public virtual IEnumerable<string> CaptureBrowserMonitoringAttributesExcludes
        {
            get
            {
                return Memoizer.Memoize(ref _captureBrowserMonitoringAttributesExcludes, () => new HashSet<string>(_localConfiguration.browserMonitoring.attributes.exclude));
            }
        }
        private IEnumerable<string> _captureBrowserMonitoringAttributesExcludes;

        // Deprecated: This configuration property should be replaced by Attribute Include/Exclude functionality.  However, because the attribute
        // keys for custom parameters are not prefixed with something, ("user.*") there's currently no way to deprecate the property in the config file.
        public virtual bool CaptureCustomParameters
        {
            get
            {
                if (_localConfiguration.parameterGroups != null && _localConfiguration.parameterGroups.customParameters != null &&
                    !_localConfiguration.parameterGroups.customParameters.enabledSpecified)
                {
                    return HighSecurityModeOverrides(false, DeprecatedCustomParametersEnabledDefault);
                }
                return HighSecurityModeOverrides(false, _localConfiguration.parameterGroups.customParameters.enabled);
            }
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
        public virtual string CollectorHttpProtocol { get { return (HighSecurityModeEnabled || _localConfiguration.service.ssl) ? "https" : "http"; } }
        public virtual uint CollectorPort { get { return (uint)(_localConfiguration.service.port > 0 ? _localConfiguration.service.port : ((CollectorHttpProtocol == "https") ? 443 : 80)); } }
        public virtual bool CollectorSendDataOnExit { get { return _localConfiguration.service.sendDataOnExit; } }
        public virtual float CollectorSendDataOnExitThreshold { get { return _localConfiguration.service.sendDataOnExitThreshold; } }
        public virtual bool CollectorSendEnvironmentInfo { get { return _localConfiguration.service.sendEnvironmentInfo; } }
        public virtual bool CollectorSyncStartup { get { return _localConfiguration.service.syncStartup; } }
        public virtual uint CollectorTimeout { get { return (_localConfiguration.service.requestTimeout > 0) ? (uint)_localConfiguration.service.requestTimeout : CollectorSendDataOnExit ? 2000u : 60 * 2 * 1000; } }

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
                // If CollectErrorEvents is false then it takes precedence.
                var configuredValue = _serverConfiguration.RpmConfig.CollectErrorEvents ?? true;
                if (configuredValue == true)
                {
                    configuredValue = ServerOverrides(_serverConfiguration.RpmConfig.ErrorCollectorCaptureEvents, _localConfiguration.errorCollector.captureEvents);
                }
                return configuredValue;
            }
        }

        public virtual uint ErrorCollectorMaxEventSamplesStored
        {
            get
            {
                var configuredValue = ServerOverrides((int?)_serverConfiguration.RpmConfig.ErrorCollectorMaxEventSamplesStored, _localConfiguration.errorCollector.maxEventSamplesStored);
                return (uint)configuredValue;
            }
        }

        public virtual uint ErrorsMaximumPerPeriod { get { return 20; } }
        public virtual IEnumerable<string> ExceptionsToIgnore { get { return ServerOverrides(_serverConfiguration.RpmConfig.ErrorCollectorErrorsToIgnore, _localConfiguration.errorCollector.ignoreErrors.exception); } }

        #endregion

        public virtual string EncodingKey { get { return _serverConfiguration.EncodingKey; } }

        public virtual bool HighSecurityModeEnabled { get { return _localConfiguration.highSecurity.enabled; } }
        public virtual int InstrumentationLevel { get { return ServerOverrides(_serverConfiguration.RpmConfig.InstrumentationLevel, 3); } }
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
        public virtual string ProxyPassword { get { return _localConfiguration.service.proxy.password; } }
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
        public virtual IEnumerable<string> HttpStatusCodesToIgnore
        {
            get
            {
                var localStatusCodesToIgnore = new List<string>();
                foreach (var localCode in _localConfiguration.errorCollector.ignoreStatusCodes.code)
                {
                    localStatusCodesToIgnore.Add(localCode.ToString(CultureInfo.InvariantCulture));
                }
                return ServerOverrides(_serverConfiguration.RpmConfig.ErrorCollectorStatusCodesToIgnore, localStatusCodesToIgnore);
            }
        }
        public virtual IEnumerable<string> ThreadProfilingIgnoreMethods { get { return _localConfiguration.threadProfiling ?? new List<string>(); } }

        #region Custom Events

        public virtual bool CustomEventsEnabled
        {
            get
            {
                if (HighSecurityModeEnabled)
                    return false;

                return ServerCanDisable(_serverConfiguration.CustomEventCollectionEnabled, _localConfiguration.customEvents.enabled);
            }
        }

        public virtual uint CustomEventsMaxSamplesStored { get { return 10000; } }

        #endregion

        public bool DisableSamplers { get { return _localConfiguration.application.disableSamplers; } }

        public bool ThreadProfilingEnabled { get { return _localConfiguration.threadProfilingEnabled; } }

        #region Transaction Events

        public virtual bool TransactionEventsEnabled
        {
            get
            {
                if (_localConfiguration.transactionEvents.enabledSpecified)
                    return ServerCanDisable(_serverConfiguration.AnalyticsEventCollectionEnabled, _localConfiguration.transactionEvents.enabled);

                if (_localConfiguration.analyticsEvents.enabledSpecified)
                    return ServerCanDisable(_serverConfiguration.AnalyticsEventCollectionEnabled, _localConfiguration.analyticsEvents.enabled);

                return ServerCanDisable(_serverConfiguration.AnalyticsEventCollectionEnabled, TransactionEventsEnabledDefault);
            }
        }
        public virtual uint TransactionEventsMaxSamplesPerMinute
        {
            get
            {
                uint maxValue = TransactionEventsMaxSamplesPerMinuteDefault;
                if (_localConfiguration.transactionEvents.maximumSamplesPerMinuteSpecified)
                {
                    maxValue = Math.Min(_localConfiguration.transactionEvents.maximumSamplesPerMinute, 10000);
                }
                if (_localConfiguration.analyticsEvents.maximumSamplesPerMinuteSpecified)
                {
                    LogDeprecatedPropertyUse("analyticsEvents.maximumSamplesPerMinute", "transactionEvents.maximumSamplesPerMinute");
                    maxValue = Math.Min(_localConfiguration.analyticsEvents.maximumSamplesPerMinute, 10000);
                }
                return maxValue;
            }
        }

        public virtual uint TransactionEventsMaxSamplesStored
        {
            get
            {
                uint maxValue = TransactionEventsMaxSamplesStoredDefault;
                if (_localConfiguration.transactionEvents.maximumSamplesStoredSpecified)
                {
                    maxValue = _localConfiguration.transactionEvents.maximumSamplesStored;
                }
                if (_localConfiguration.analyticsEvents.maximumSamplesStoredSpecified)
                {
                    LogDeprecatedPropertyUse("analyticsEvents.maximumSamplesStored", "transactionEvents.maximumSamplesStored");
                    maxValue = _localConfiguration.analyticsEvents.maximumSamplesStored;
                }
                return maxValue;
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

        public virtual string TransactionTracerRecordSql
        {
            get
            {
                var highSecurityValue = Enum.GetName(typeof(configurationTransactionTracerRecordSql), configurationTransactionTracerRecordSql.obfuscated);
                var localAttributeValue = Enum.GetName(typeof(configurationTransactionTracerRecordSql), _localConfiguration.transactionTracer.recordSql);
                var serverAttributeValue = _serverConfiguration.RpmConfig.TransactionTracerRecordSql;
                var serverOrLocalAttributeValue = ServerOverrides(serverAttributeValue, localAttributeValue);

                // don't let high security mode override "off" with "obfuscated".
                if (serverOrLocalAttributeValue.Equals("off", StringComparison.InvariantCultureIgnoreCase))
                    highSecurityValue = "off";

                return HighSecurityModeOverrides(highSecurityValue, serverOrLocalAttributeValue);
            }
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

        // Not currently used.
        public bool UtilizationDetectAws
        {
            get { return _localConfiguration.utilization.detectAws; }
        }

        // Not currently used.
        public bool UtilizationDetectAzure
        {
            get { return _localConfiguration.utilization.detectAzure; }
        }

        // Not currently used.
        public bool UtilizationDetectGcp
        {
            get { return _localConfiguration.utilization.detectGcp; }
        }

        // Not currently used.
        public bool UtilizationDetectPcf
        {
            get { return _localConfiguration.utilization.detectPcf; }
        }

        // Not currently used.
        public bool UtilizationDetectDocker
        {
            get { return _localConfiguration.utilization.detectDocker; }
        }

        public int? UtilizationLogicalProcessors
        {
            get
            {
                var value = _configurationManagerStatic.GetAppSettingInt("NewRelic.Utilization.LogicalProcessors");

                if (value == null)
                {
                    var localValue = GetNullableIntValue(_localConfiguration.utilization.logicalProcessorsSpecified,
                        _localConfiguration.utilization.logicalProcessors);

                    value = EnvironmentOverrides(localValue, "NEW_RELIC_UTILIZATION_LOGICAL_PROCESSORS");
                }

                return value;
            }
        }

        public int? UtilizationTotalRamMib
        {
            get
            {
                var value = _configurationManagerStatic.GetAppSettingInt("NewRelic.Utilization.TotalRamMib");

                if (value == null)
                {
                    var localValue = GetNullableIntValue(_localConfiguration.utilization.totalRamMibSpecified,
                        _localConfiguration.utilization.totalRamMib);

                    value = EnvironmentOverrides(localValue, "NEW_RELIC_UTILIZATION_TOTAL_RAM_MIB");
                }

                return value;
            }
        }

        public string UtilizationBillingHost
        {
            get
            {
                var value = _configurationManagerStatic.GetAppSetting("NewRelic.Utilization.BillingHost")
                    ?? EnvironmentOverrides(_localConfiguration.utilization.billingHost, "NEW_RELIC_UTILIZATION_BILLING_HOSTNAME");

                return string.IsNullOrEmpty(value) ? null : value.Trim(); //Keeping IsNullOrEmpty just in case customer sets value to "".
            }
        }

        #endregion

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
            Debug.Assert(local != null);
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
        private int? EnvironmentOverrides(int? local, params string[] environmentVariableNames)
        {
            var env = environmentVariableNames
                .Select(_environment.GetEnvironmentVariable)
                .FirstOrDefault(value => value != null);

            return int.TryParse(env, out int parsedValue) ? parsedValue : local;
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
                if (_localConfiguration.parameterGroups.serviceRequestParameters != null &&
                    _localConfiguration.parameterGroups.serviceRequestParameters.enabledSpecified &&
                    !_localConfiguration.parameterGroups.serviceRequestParameters.enabled)
                {
                    LogDeprecatedPropertyUse("parameterGroups.serviceRequestParameters.enabled", "attributes.exclude");
                    disabledProperties.Add("service.request.*");
                }

            }
            return disabledProperties;
        }

        private bool DeprecatedCaptureServiceRequestParameters
        {
            get
            {
                var localAttributeValue = false;
                if (_localConfiguration.parameterGroups.serviceRequestParameters.enabledSpecified)
                {
                    localAttributeValue = _localConfiguration.parameterGroups.serviceRequestParameters.enabled;
                }
                return localAttributeValue;
            }
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
        private IEnumerable<string> GetDeprecatedIgnoreParameters()
        {
            var ignoreParameters = new List<string>();
            ignoreParameters.AddRange(DeprecatedIgnoreCustomParameters());
            ignoreParameters.AddRange(DeprecatedIgnoreIdentityParameters().Select(param => "identity." + param));
            ignoreParameters.AddRange(DeprecatedIgnoreResponseHeaderParameters().Select(param => "response.headers." + param));
            ignoreParameters.AddRange(DeprecatedIgnoreRequestHeaderParameters().Select(param => "request.headers." + param));
            ignoreParameters.AddRange(DeprecatedIgnoreServiceRequestParameters().Select(param => "service.request." + param));
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
        private IEnumerable<string> DeprecatedIgnoreServiceRequestParameters()
        {
            if (_localConfiguration.parameterGroups != null
                && _localConfiguration.parameterGroups.serviceRequestParameters != null
                && _localConfiguration.parameterGroups.serviceRequestParameters.ignore != null
                && _localConfiguration.parameterGroups.serviceRequestParameters.ignore.Count > 0)
            {
                LogDeprecatedPropertyUse("parameterGroups.serviceRequestParameters.ignore", "attributes.exclude");
                return _localConfiguration.parameterGroups.serviceRequestParameters.ignore;
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

        private const bool DeprecatedCaptureIdentityParametersDefault = true;
        private const bool DeprecatedResponseHeaderParametersEnabledDefault = true;
        private const bool DeprecatedCustomParametersEnabledDefault = true;
        private const bool DeprecatedRequestHeaderParametersEnabledDefault = true;
        private const bool DeprecatedServiceRequestParametersEnabledDefault = false;
        private const bool DeprecatedRequestParametersEnabledDefault = false;

        private const bool CaptureTransactionEventsAttributesDefault = true;
        private const bool CaptureTransactionTraceAttributesDefault = true;
        private const bool CaptureErrorCollectorAttributesDefault = true;
        private const bool CaptureBrowserMonitoringAttributesDefault = false;

        private const bool TransactionEventsEnabledDefault = true;
        private const bool TransactionEventsTransactionsEnabledDefault = true;
        private const uint TransactionEventsMaxSamplesPerMinuteDefault = 10000;
        private const uint TransactionEventsMaxSamplesStoredDefault = 10000;

    }
}

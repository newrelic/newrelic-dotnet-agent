using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using JetBrains.Annotations;
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

        private static Int64 _currentConfigurationVersion;

        [NotNull]
        private readonly IEnvironment _environment = new EnvironmentMock();

        [NotNull]
        private readonly IProcessStatic _processStatic = new ProcessStatic();

        [NotNull]
        private readonly IHttpRuntimeStatic _httpRuntimeStatic = new HttpRuntimeStatic();

        [NotNull]
        private readonly IConfigurationManagerStatic _configurationManagerStatic = new ConfigurationManagerStaticMock();

        /// <summary>
        /// Default configuration.  It will contain reasonable default values for everything and never anything more.  Useful when you don't have configuration off disk or a collector response yet.
        /// </summary>
        [NotNull]
        public static readonly DefaultConfiguration Instance = new DefaultConfiguration();
        [NotNull]
        private readonly configuration _localConfiguration = new configuration();
        [NotNull]
        private readonly ServerConfiguration _serverConfiguration = ServerConfiguration.GetDefault();
        [NotNull]
        private readonly RunTimeConfiguration _runTimeConfiguration = new RunTimeConfiguration();

        /// <summary>
        /// _localConfiguration.AppSettings will be loaded into this field
        /// </summary>
        [NotNull]
        private readonly IDictionary<String, String> _appSettings;

        /// <summary>
        /// Default configuration constructor.  It will contain reasonable default values for everything and never anything more.
        /// </summary>
        private DefaultConfiguration()
        {
            ConfigurationVersion = Interlocked.Increment(ref _currentConfigurationVersion);
        }

        protected DefaultConfiguration([NotNull] IEnvironment environment, configuration localConfiguration, ServerConfiguration serverConfiguration, RunTimeConfiguration runTimeConfiguration, [NotNull] IProcessStatic processStatic, [NotNull] IHttpRuntimeStatic httpRuntimeStatic, [NotNull] IConfigurationManagerStatic configurationManagerStatic)
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

        [NotNull]
        private IDictionary<String, String> TransformAppSettings()
        {
            if (_localConfiguration.appSettings == null)
                return new Dictionary<String, String>();

            return _localConfiguration.appSettings
                .Where(setting => setting != null)
                .Select(setting => new KeyValuePair<String, String>(setting.key, setting.value))
                .ToDictionary(IEnumerableExtensions.DuplicateKeyBehavior.KeepFirst);
        }

        private Boolean TryGetAppSettingAsBooleanWithDefault([NotNull] String key, Boolean defaultValue)
        {
            var value = _appSettings.GetValueOrDefault(key);

            Boolean parsedBoolean;
            var parsedSuccessfully = Boolean.TryParse(value, out parsedBoolean);
            if (!parsedSuccessfully)
                return defaultValue;

            return parsedBoolean;
        }

        private Int32 TryGetAppSettingAsIntWithDefault([NotNull] String key, Int32 defaultValue)
        {
            var value = _appSettings.GetValueOrDefault(key);

            Int32 parsedInt;
            var parsedSuccessfully = Int32.TryParse(value, out parsedInt);
            if (!parsedSuccessfully)
                return defaultValue;

            return parsedInt;
        }

        // ReSharper disable PossibleNullReferenceException

        #region IConfiguration Properties

        public Object AgentRunId { get { return _serverConfiguration.AgentRunId; } }

        public virtual Boolean AgentEnabled
        {
            get
            {
                var agentEnabledAsString = _configurationManagerStatic.GetAppSetting("NewRelic.AgentEnabled");

                Boolean agentEnabled;
                if (!Boolean.TryParse(agentEnabledAsString, out agentEnabled))
                    return _localConfiguration.agentEnabled;

                return agentEnabled;
            }
        }

        private String _agentLicenseKey;
        public virtual String AgentLicenseKey
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

        private IEnumerable<String> _applicationNames;
        public virtual IEnumerable<String> ApplicationNames { get { return _applicationNames ?? (_applicationNames = GetApplicationNames()); } }

        [NotNull]
        private IEnumerable<String> GetApplicationNames()
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
                return new List<String> { _processStatic.GetCurrentProcess().ProcessName };

            throw new Exception("An application name must be provided");
        }

        public Boolean AutoStartAgent { get { return _localConfiguration.service.autoStart; } }

        public Int32 WrapperExceptionLimit { get { return TryGetAppSettingAsIntWithDefault("WrapperExceptionLimit", 5); } }



        #region Browser Monitoring

        public virtual String BrowserMonitoringApplicationId { get { return _serverConfiguration.RumSettingsApplicationId ?? String.Empty; } }
        public virtual Boolean BrowserMonitoringAutoInstrument { get { return _localConfiguration.browserMonitoring.autoInstrument; } }
        public virtual String BrowserMonitoringBeaconAddress { get { return _serverConfiguration.RumSettingsBeacon ?? String.Empty; } }
        public virtual String BrowserMonitoringErrorBeaconAddress { get { return _serverConfiguration.RumSettingsErrorBeacon ?? String.Empty; } }
        public virtual String BrowserMonitoringJavaScriptAgent { get { return _serverConfiguration.RumSettingsJavaScriptAgentLoader ?? String.Empty; } }
        public virtual String BrowserMonitoringJavaScriptAgentFile { get { return _serverConfiguration.RumSettingsJavaScriptAgentFile ?? String.Empty; } }
        public virtual String BrowserMonitoringJavaScriptAgentLoaderType { get { return ServerOverrides(_serverConfiguration.RumSettingsBrowserMonitoringLoader, _localConfiguration.browserMonitoring.loader); } }
        public virtual String BrowserMonitoringKey { get { return _serverConfiguration.RumSettingsBrowserKey ?? String.Empty; } }
        public virtual Boolean BrowserMonitoringUseSsl { get { return HighSecurityModeOverrides(true, _localConfiguration.browserMonitoring.sslForHttp); } }

        #endregion

        #region Attributes

        public virtual Boolean CaptureAttributes { get { return _localConfiguration.attributes.enabled; } }

        public virtual IEnumerable<String> CaptureAttributesIncludes
        {
            get
            {
                return Memoizer.Memoize(ref _captureAttributesIncludes, () => new HashSet<String>(_localConfiguration.attributes.include));
            }
        }
        private IEnumerable<String> _captureAttributesIncludes;

        public virtual IEnumerable<String> CaptureAttributesExcludes
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

                    return new HashSet<String>(allExcludes);
                });
            }
        }
        private IEnumerable<String> _captureAttributesExcludes;

        public virtual IEnumerable<String> CaptureAttributesDefaultExcludes
        {
            get
            {
                return Memoizer.Memoize(ref _captureAttributesDefaultExcludes, () => new HashSet<String> { "service.request.*", "identity.*" });
            }
        }
        private IEnumerable<String> _captureAttributesDefaultExcludes;

        public virtual Boolean CaptureTransactionEventsAttributes
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

        public virtual IEnumerable<String> CaptureTransactionEventAttributesIncludes
        {
            get
            {
                return Memoizer.Memoize(ref _captureTransactionEventAttributesIncludes, () => new HashSet<String>(_localConfiguration.transactionEvents.attributes.include));
            }
        }
        private IEnumerable<String> _captureTransactionEventAttributesIncludes;

        public virtual IEnumerable<String> CaptureTransactionEventAttributesExcludes
        {
            get
            {
                return Memoizer.Memoize(ref _captureTransactionEventAttributesExcludes, () => new HashSet<String>(_localConfiguration.transactionEvents.attributes.exclude));
            }
        }
        private IEnumerable<String> _captureTransactionEventAttributesExcludes;

        public virtual Boolean CaptureTransactionTraceAttributes
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

        public virtual IEnumerable<String> CaptureTransactionTraceAttributesIncludes
        {
            get
            {
                return Memoizer.Memoize(ref _captureTransactionTraceAttributesIncludes, () =>
                {
                    var includes = new HashSet<String>(_localConfiguration.transactionTracer.attributes.include);
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
        private IEnumerable<String> _captureTransactionTraceAttributesIncludes;

        public virtual IEnumerable<String> CaptureTransactionTraceAttributesExcludes
        {
            get
            {
                return Memoizer.Memoize(ref _captureTransactionTraceAttributesExcludes, () => new HashSet<String>(_localConfiguration.transactionTracer.attributes.exclude));
            }
        }
        private IEnumerable<String> _captureTransactionTraceAttributesExcludes;

        public virtual Boolean CaptureErrorCollectorAttributes
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

        public virtual IEnumerable<String> CaptureErrorCollectorAttributesIncludes
        {
            get
            {
                return Memoizer.Memoize(ref _captureErrorCollectorAttributesIncludes, () =>
                {
                    var includes = new HashSet<String>(_localConfiguration.errorCollector.attributes.include);
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
        private IEnumerable<String> _captureErrorCollectorAttributesIncludes;

        public virtual IEnumerable<String> CaptureErrorCollectorAttributesExcludes
        {
            get
            {
                return Memoizer.Memoize(ref _captureErrorCollectorAttributesExcludes, () => new HashSet<String>(_localConfiguration.errorCollector.attributes.exclude));
            }
        }
        private IEnumerable<String> _captureErrorCollectorAttributesExcludes;

        public virtual Boolean CaptureBrowserMonitoringAttributes
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

        public virtual IEnumerable<String> CaptureBrowserMonitoringAttributesIncludes
        {
            get
            {
                return Memoizer.Memoize(ref _captureBrowserMonitoringAttributesIncludes, () => new HashSet<String>(_localConfiguration.browserMonitoring.attributes.include));
            }
        }
        private IEnumerable<String> _captureBrowserMonitoringAttributesIncludes;

        public virtual IEnumerable<String> CaptureBrowserMonitoringAttributesExcludes
        {
            get
            {
                return Memoizer.Memoize(ref _captureBrowserMonitoringAttributesExcludes, () => new HashSet<String>(_localConfiguration.browserMonitoring.attributes.exclude));
            }
        }
        private IEnumerable<String> _captureBrowserMonitoringAttributesExcludes;

        // Deprecated: This configuration property should be replaced by Attribute Include/Exclude functionality.  However, because the attribute
        // keys for custom parameters are not prefixed with something, ("user.*") there's currently no way to deprecate the property in the config file.
        public virtual Boolean CaptureCustomParameters
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

        public virtual Boolean CaptureRequestParameters
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

        public virtual String CollectorHost { get { return EnvironmentOverrides(_localConfiguration.service.host, @"NEW_RELIC_HOST"); } }
        public virtual String CollectorHttpProtocol { get { return (HighSecurityModeEnabled || _localConfiguration.service.ssl) ? "https" : "http"; } }
        public virtual UInt32 CollectorPort { get { return (UInt32)(_localConfiguration.service.port > 0 ? _localConfiguration.service.port : ((CollectorHttpProtocol == "https") ? 443 : 80)); } }
        public virtual Boolean CollectorSendDataOnExit { get { return _localConfiguration.service.sendDataOnExit; } }
        public virtual Single CollectorSendDataOnExitThreshold { get { return _localConfiguration.service.sendDataOnExitThreshold; } }
        public virtual Boolean CollectorSendEnvironmentInfo { get { return _localConfiguration.service.sendEnvironmentInfo; } }
        public virtual Boolean CollectorSyncStartup { get { return _localConfiguration.service.syncStartup; } }
        public virtual UInt32 CollectorTimeout { get { return (_localConfiguration.service.requestTimeout > 0) ? (UInt32)_localConfiguration.service.requestTimeout : CollectorSendDataOnExit ? 2000u : 60 * 2 * 1000; } }

        #endregion

        public virtual Boolean CompleteTransactionsOnThread { get { return _localConfiguration.service.completeTransactionsOnThread; } }

        public Int64 ConfigurationVersion { get; private set; }

        #region Cross Application Tracing

        public virtual String CrossApplicationTracingCrossProcessId { get { return _serverConfiguration.CatId; } }

        private Boolean? _crossApplicationTracingEnabled;
        public virtual Boolean CrossApplicationTracingEnabled => _crossApplicationTracingEnabled ?? (_crossApplicationTracingEnabled = IsCatEnabled()).Value;

        private Boolean IsCatEnabled()
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

        public virtual Boolean ErrorCollectorEnabled
        {
            get
            {
                var configuredValue = ServerOverrides(_serverConfiguration.RpmConfig.ErrorCollectorEnabled, _localConfiguration.errorCollector.enabled);
                return ServerCanDisable(_serverConfiguration.ErrorCollectionEnabled, configuredValue);
            }
        }

        public virtual Boolean ErrorCollectorCaptureEvents
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

        public virtual UInt32 ErrorCollectorMaxEventSamplesStored
        {
            get
            {
                var configuredValue = ServerOverrides((Int32?)_serverConfiguration.RpmConfig.ErrorCollectorMaxEventSamplesStored, _localConfiguration.errorCollector.maxEventSamplesStored);
                return (UInt32)configuredValue;
            }
        }

        public virtual UInt32 ErrorsMaximumPerPeriod { get { return 20; } }
        public virtual IEnumerable<String> ExceptionsToIgnore { get { return ServerOverrides(_serverConfiguration.RpmConfig.ErrorCollectorErrorsToIgnore, _localConfiguration.errorCollector.ignoreErrors.exception); } }

        #endregion

        public virtual String EncodingKey { get { return _serverConfiguration.EncodingKey; } }

        public virtual Boolean HighSecurityModeEnabled { get { return _localConfiguration.highSecurity.enabled; } }
        public virtual Int32 InstrumentationLevel { get { return ServerOverrides(_serverConfiguration.RpmConfig.InstrumentationLevel, 3); } }
        public virtual Boolean InstrumentationLoggingEnabled { get { return _localConfiguration.instrumentation.log; } }

        #region Labels

        public virtual String Labels
        {
            get
            {
                return EnvironmentOverrides(_localConfiguration.labels, @"NEW_RELIC_LABELS");
            }
        }

        #endregion

        #region Proxy

        public virtual String ProxyHost { get { return _localConfiguration.service.proxy.host; } }
        public virtual String ProxyUriPath { get { return _localConfiguration.service.proxy.uriPath; } }
        public virtual Int32 ProxyPort { get { return _localConfiguration.service.proxy.port; } }
        public virtual String ProxyUsername { get { return _localConfiguration.service.proxy.user; } }
        public virtual String ProxyPassword { get { return _localConfiguration.service.proxy.password; } }
        public virtual String ProxyDomain { get { return _localConfiguration.service.proxy.domain ?? String.Empty; } }

        #endregion

        #region DataTransmission
        public Boolean PutForDataSend { get { return _localConfiguration.dataTransmission.putForDataSend; } }

        public String CompressedContentEncoding
        {
            get
            {
                return _localConfiguration.dataTransmission.compressedContentEncoding.ToString();
            }
        }

        #endregion

        #region DatastoreTracer
        public Boolean InstanceReportingEnabled { get { return _localConfiguration.datastoreTracer.instanceReporting.enabled; } }

        public Boolean DatabaseNameReportingEnabled { get { return _localConfiguration.datastoreTracer.databaseNameReporting.enabled; } }

        #endregion

        #region Sql

        public Boolean SlowSqlEnabled
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

        public virtual Boolean SqlExplainPlansEnabled
        {
            get
            {
                return ServerOverrides(_serverConfiguration.RpmConfig.TransactionTracerExplainEnabled,
                    _localConfiguration.transactionTracer.explainEnabled);
            }
        }

        public virtual Int32 SqlExplainPlansMax { get { return _localConfiguration.transactionTracer.maxExplainPlans; } }
        public virtual UInt32 SqlStatementsPerTransaction { get { return 500; } }
        public virtual Int32 SqlTracesPerPeriod { get { return 10; } }

        #endregion

        public virtual Int32 StackTraceMaximumFrames { get { return _localConfiguration.maxStackTraceLines; } }
        public virtual IEnumerable<String> HttpStatusCodesToIgnore
        {
            get
            {
                var localStatusCodesToIgnore = new List<String>();
                foreach (var localCode in _localConfiguration.errorCollector.ignoreStatusCodes.code)
                {
                    localStatusCodesToIgnore.Add(localCode.ToString(CultureInfo.InvariantCulture));
                }
                return ServerOverrides(_serverConfiguration.RpmConfig.ErrorCollectorStatusCodesToIgnore, localStatusCodesToIgnore);
            }
        }
        public virtual IEnumerable<String> ThreadProfilingIgnoreMethods { get { return _localConfiguration.threadProfiling ?? new List<String>(); } }

        #region Custom Events

        public virtual Boolean CustomEventsEnabled
        {
            get
            {
                if (HighSecurityModeEnabled)
                    return false;

                return ServerCanDisable(_serverConfiguration.CustomEventCollectionEnabled, _localConfiguration.customEvents.enabled);
            }
        }

        public virtual UInt32 CustomEventsMaxSamplesStored { get { return 10000; } }

        #endregion

        public bool DisableSamplers { get { return _localConfiguration.application.disableSamplers; } }

        public Boolean ThreadProfilingEnabled { get { return _localConfiguration.threadProfilingEnabled; } }

        #region Transaction Events

        public virtual Boolean TransactionEventsEnabled
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
        public virtual UInt32 TransactionEventsMaxSamplesPerMinute
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

        public virtual UInt32 TransactionEventsMaxSamplesStored
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

        public virtual Boolean TransactionEventsTransactionsEnabled
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

        public virtual Boolean TransactionTracerEnabled
        {
            get
            {
                var configuredValue = ServerOverrides(_serverConfiguration.RpmConfig.TransactionTracerEnabled, _localConfiguration.transactionTracer.enabled);
                return ServerCanDisable(_serverConfiguration.TraceCollectionEnabled, configuredValue);
            }
        }
        public virtual Int32 TransactionTracerMaxSegments { get { return _localConfiguration.transactionTracer.maxSegments; } }

        public virtual String TransactionTracerRecordSql
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
        public virtual Int32 TransactionTracerMaxStackTraces { get { return _localConfiguration.transactionTracer.maxStackTrace; } }
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

        public virtual IEnumerable<Int64> TrustedAccountIds { get { return _serverConfiguration.TrustedIds ?? new List<Int64>(); } }

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

        private IDictionary<String, IEnumerable<String>> _transactionNameWhitelistRules;
        public IDictionary<String, IEnumerable<String>> TransactionNameWhitelistRules
        {
            get { return _transactionNameWhitelistRules ?? (_transactionNameWhitelistRules = GetWhitelistRules(_serverConfiguration.TransactionNameWhitelistRules)); }
        }

        private IDictionary<String, Double> _webTransactionsApdex;

        public IDictionary<String, Double> WebTransactionsApdex
        {
            get { return _webTransactionsApdex ?? (_webTransactionsApdex = _serverConfiguration.WebTransactionsApdex ?? new Dictionary<String, Double>()); }
        }

        #endregion Metric naming

        public String NewRelicConfigFilePath { get { return _localConfiguration.ConfigurationFileName; } }

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

                return String.IsNullOrEmpty(value) ? null : value.Trim(); //Keeping IsNullOrEmpty just in case customer sets value to "".
            }
        }

        #endregion

        #endregion

        #region Helpers

        // ReSharper restore PossibleNullReferenceException
        private TimeSpan ParseTransactionThreshold(String threshold, [NotNull] Func<Double, TimeSpan> numberToTimeSpanConverter)
        {
            if (String.IsNullOrEmpty(threshold))
                return TransactionTraceApdexF;

            Double parsedTransactionThreshold;
            return Double.TryParse(threshold, out parsedTransactionThreshold)
                ? numberToTimeSpanConverter(parsedTransactionThreshold)
                : TransactionTraceApdexF;
        }

        private static Boolean ServerCanDisable(Boolean? server, Boolean local)
        {
            if (server == null) return local;
            return server.Value && local;
        }

        [NotNull]
        private static String ServerOverrides(String server, String local)
        {
            return server ?? local ?? String.Empty;
        }

        private static T ServerOverrides<T>(T? server, T local) where T : struct
        {
            return server ?? local;
        }

        private T HighSecurityModeOverrides<T>(T overriddenValue, T originalValue)
        {
            return HighSecurityModeEnabled ? overriddenValue : originalValue;
        }

        [NotNull]
        private static T ServerOverrides<T>(T server, T local) where T : class
        {
            Debug.Assert(local != null);
            return server ?? local;
        }

        [CanBeNull]
        private String EnvironmentOverrides(String local, params String[] environmentVariableNames)
        {
            return (environmentVariableNames ?? Enumerable.Empty<String>())
                .Select(_environment.GetEnvironmentVariable)
                .Where(value => value != null)
                .FirstOrDefault()
                ?? local;
        }

        [CanBeNull]
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

        [NotNull]
        public static IEnumerable<RegexRule> GetRegexRules([CanBeNull] IEnumerable<ServerConfiguration.RegexRule> rules)
        {
            if (rules == null)
                return new List<RegexRule>();

            return rules
                .Select(TryGetRegexRule)
                .Where(rule => rule != null)
                .Select(rule => rule.Value)
                .ToList();
        }

        private static RegexRule? TryGetRegexRule([CanBeNull] ServerConfiguration.RegexRule rule)
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

        [CanBeNull]
        private static String UpdateRegexForDotNet([CanBeNull] String replacement)
        {
            if (string.IsNullOrEmpty(replacement))
                return replacement;

            //search for \1, \2 etc, and replace with $1, $2, etc
            var backreferencePattern = new Regex(@"\\(\d+)");
            return backreferencePattern.Replace(replacement, "$$$1");
        }

        [NotNull]
        public static IDictionary<String, IEnumerable<String>> GetWhitelistRules([CanBeNull] IEnumerable<ServerConfiguration.WhitelistRule> whitelistRules)
        {
            if (whitelistRules == null)
                return new Dictionary<String, IEnumerable<String>>();

            return whitelistRules
                .Where(rule => rule != null)
                .Select(TryGetValidPrefixAndTerms)
                .Where(rule => rule != null)
                .Select(rule => rule.Value)
                .ToDictionary(IEnumerableExtensions.DuplicateKeyBehavior.KeepLast);
        }

        private static KeyValuePair<String, IEnumerable<String>>? TryGetValidPrefixAndTerms([NotNull] ServerConfiguration.WhitelistRule rule)
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
            return new KeyValuePair<String, IEnumerable<String>>(prefix, terms);
        }


        [CanBeNull]
        private static String TryGetValidPrefix([CanBeNull] String prefix)
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

        [NotNull]
        private IEnumerable<String> GetDeprecatedExplicitlyDisabledParameters()
        {
            var disabledProperties = new List<String>();

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

        private Boolean DeprecatedCaptureServiceRequestParameters
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

        private Boolean DeprecatedCaptureIdentityParameters
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

        [NotNull]
        private IEnumerable<String> GetDeprecatedIgnoreParameters()
        {
            var ignoreParameters = new List<String>();
            ignoreParameters.AddRange(DeprecatedIgnoreCustomParameters());
            ignoreParameters.AddRange(DeprecatedIgnoreIdentityParameters().Select(param => "identity." + param));
            ignoreParameters.AddRange(DeprecatedIgnoreResponseHeaderParameters().Select(param => "response.headers." + param));
            ignoreParameters.AddRange(DeprecatedIgnoreRequestHeaderParameters().Select(param => "request.headers." + param));
            ignoreParameters.AddRange(DeprecatedIgnoreServiceRequestParameters().Select(param => "service.request." + param));
            ignoreParameters.AddRange(DeprecatedIgnoreRequestParameters().Select(param => "request.parameters." + param));

            return ignoreParameters.Distinct();
        }

        [NotNull]
        private IEnumerable<String> DeprecatedIgnoreCustomParameters()
        {
            if (_localConfiguration.parameterGroups != null
                && _localConfiguration.parameterGroups.customParameters != null
                && _localConfiguration.parameterGroups.customParameters.ignore != null
                && _localConfiguration.parameterGroups.customParameters.ignore.Count > 0)
            {
                LogDeprecatedPropertyUse("parameterGroups.customParameters.ignore", "attributes.exclude");
                return _localConfiguration.parameterGroups.customParameters.ignore;
            }
            return Enumerable.Empty<String>();
        }

        [NotNull]
        private IEnumerable<String> DeprecatedIgnoreIdentityParameters()
        {
            if (_localConfiguration.parameterGroups != null
                && _localConfiguration.parameterGroups.identityParameters != null
                && _localConfiguration.parameterGroups.identityParameters.ignore != null
                && _localConfiguration.parameterGroups.identityParameters.ignore.Count > 0)
            {
                LogDeprecatedPropertyUse("parameterGroups.identityParameters.ignore", "attributes.exclude");
                return _localConfiguration.parameterGroups.identityParameters.ignore;
            }
            return Enumerable.Empty<String>();
        }

        [NotNull]
        private IEnumerable<String> DeprecatedIgnoreResponseHeaderParameters()
        {
            if (_localConfiguration.parameterGroups != null
                && _localConfiguration.parameterGroups.responseHeaderParameters != null
                && _localConfiguration.parameterGroups.responseHeaderParameters.ignore != null
                && _localConfiguration.parameterGroups.responseHeaderParameters.ignore.Count > 0)
            {
                LogDeprecatedPropertyUse("parameterGroups.responseHeaderParameters.ignore", "attributes.exclude");
                return _localConfiguration.parameterGroups.responseHeaderParameters.ignore;
            }
            return Enumerable.Empty<String>();
        }

        [NotNull]
        private IEnumerable<String> DeprecatedIgnoreRequestHeaderParameters()
        {
            if (_localConfiguration.parameterGroups != null
                && _localConfiguration.parameterGroups.requestHeaderParameters != null
                && _localConfiguration.parameterGroups.requestHeaderParameters.ignore != null
                && _localConfiguration.parameterGroups.requestHeaderParameters.ignore.Count > 0)
            {
                LogDeprecatedPropertyUse("parameterGroups.requestHeaderParameters.ignore", "attributes.exclude");
                return _localConfiguration.parameterGroups.requestHeaderParameters.ignore;
            }
            return Enumerable.Empty<String>();
        }

        [NotNull]
        private IEnumerable<String> DeprecatedIgnoreServiceRequestParameters()
        {
            if (_localConfiguration.parameterGroups != null
                && _localConfiguration.parameterGroups.serviceRequestParameters != null
                && _localConfiguration.parameterGroups.serviceRequestParameters.ignore != null
                && _localConfiguration.parameterGroups.serviceRequestParameters.ignore.Count > 0)
            {
                LogDeprecatedPropertyUse("parameterGroups.serviceRequestParameters.ignore", "attributes.exclude");
                return _localConfiguration.parameterGroups.serviceRequestParameters.ignore;
            }
            return Enumerable.Empty<String>();
        }

        [NotNull]
        private IEnumerable<String> DeprecatedIgnoreRequestParameters()
        {
            if (_localConfiguration.requestParameters != null
                && _localConfiguration.requestParameters.ignore != null
                && _localConfiguration.requestParameters.ignore.Count > 0)
            {
                LogDeprecatedPropertyUse("requestParameters.ignore", "attributes.exclude");
                var requestParams = ServerOverrides(_serverConfiguration.RpmConfig.ParametersToIgnore, _localConfiguration.requestParameters.ignore);
                return requestParams;
            }
            return Enumerable.Empty<String>();
        }

        #endregion

        private const Boolean DeprecatedCaptureIdentityParametersDefault = true;
        private const Boolean DeprecatedResponseHeaderParametersEnabledDefault = true;
        private const Boolean DeprecatedCustomParametersEnabledDefault = true;
        private const Boolean DeprecatedRequestHeaderParametersEnabledDefault = true;
        private const Boolean DeprecatedServiceRequestParametersEnabledDefault = false;
        private const Boolean DeprecatedRequestParametersEnabledDefault = false;

        private const Boolean CaptureTransactionEventsAttributesDefault = true;
        private const Boolean CaptureTransactionTraceAttributesDefault = true;
        private const Boolean CaptureErrorCollectorAttributesDefault = true;
        private const Boolean CaptureBrowserMonitoringAttributesDefault = false;

        private const Boolean TransactionEventsEnabledDefault = true;
        private const Boolean TransactionEventsTransactionsEnabledDefault = true;
        private const UInt32 TransactionEventsMaxSamplesPerMinuteDefault = 10000;
        private const UInt32 TransactionEventsMaxSamplesStoredDefault = 10000;

    }
}

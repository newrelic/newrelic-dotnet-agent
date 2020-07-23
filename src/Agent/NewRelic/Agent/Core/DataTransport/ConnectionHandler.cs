using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using JetBrains.Annotations;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.Configuration;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Exceptions;
using NewRelic.Agent.Core.Logging;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Core.Utilization;
using NewRelic.SystemExtensions.Collections.Generic;
using NewRelic.SystemInterfaces;

namespace NewRelic.Agent.Core.DataTransport
{
    /// <summary>
    /// The <see cref="ConnectionHandler"/> understands the business logic of *how* to connect, disconnect, send data, etc. It is the companion of <see cref="ConnectionManager"/> which knows *when* to connect, disconnect, etc.
    /// 
    /// This class is *NOT* thread safe -- calling any of the public methods on this class in parallel has undefined results and can leave it in a corrupted state.
    /// </summary>
    public class ConnectionHandler : ConfigurationBasedService, IConnectionHandler
    {
        [NotNull]
        private static readonly Dictionary<String, Action<String>> ServerLogLevelMap = new Dictionary<String, Action<String>>
        {
            {"INFO", Log.Info},
            {"WARN", Log.Warn},
            {"ERROR", Log.Error},
            {"VERBOSE", Log.Finest}
        };

        [NotNull]
        private readonly ISerializer _serializer;

        [NotNull]
        private readonly ICollectorWireFactory _collectorWireFactory;

        [NotNull]
        private ConnectionInfo _connectionInfo;

        [NotNull]
        private ICollectorWire _dataRequestWire;

        [NotNull]
        private readonly IProcessStatic _processStatic;

        [NotNull]
        private readonly IDnsStatic _dnsStatic;

        [NotNull]
        private readonly ILabelsService _labelsService;

        [NotNull]
        private readonly ISystemInfo _systemInfo;

        [NotNull]
        private readonly Environment _environment;

        [NotNull]
        private readonly IAgentHealthReporter _agentHealthReporter;

        public ConnectionHandler([NotNull] ISerializer serializer, [NotNull] ICollectorWireFactory collectorWireFactory, [NotNull] IProcessStatic processStatic, [NotNull] IDnsStatic dnsStatic, [NotNull] ILabelsService labelsService, [NotNull] Environment environment, [NotNull] ISystemInfo systemInfo, [NotNull] IAgentHealthReporter agentHealthReporter)
        {
            _serializer = serializer;
            _collectorWireFactory = collectorWireFactory;
            _processStatic = processStatic;
            _dnsStatic = dnsStatic;
            _labelsService = labelsService;
            _environment = environment;
            _systemInfo = systemInfo;
            _agentHealthReporter = agentHealthReporter;

            _connectionInfo = new ConnectionInfo(_configuration);
            _dataRequestWire = new NoOpCollectorWire();
        }

        #region Public API

        public void Connect()
        {
            // Need to disable before connecting so that we can easily tell that we just finished connecting during a configuration update
            Disable();

            try
            {
                _connectionInfo = SendRedirectHostRequest();
                var serverConfiguration = SendConnectRequest();
                EventBus<ServerConfigurationUpdatedEvent>.Publish(new ServerConfigurationUpdatedEvent(serverConfiguration));
                _dataRequestWire = _collectorWireFactory.GetCollectorWire(_configuration);
                SendAgentSettings();
                Log.Info("Agent fully connected.");
            }
            catch (Exception e)
            {
                Disable();
                Log.Error($"Unable to connect to the New Relic service at {_connectionInfo} : {e}");
                throw;
            }
        }

        public void Disconnect()
        {
            if (_configuration.AgentRunId != null)
                SendShutdownCommand();

            Disable();
        }

        private Object SendDataRequest([NotNull] String method, [NotNull] params Object[] data)
        {
            return SendDataOverWire<Object>(_dataRequestWire, method, data);
        }

        public T SendDataRequest<T>(String method, params Object[] data)
        {
            return SendDataOverWire<T>(_dataRequestWire, method, data);
        }

        #endregion Public API

        #region Connect helper methods

        [NotNull]
        private ConnectionInfo SendRedirectHostRequest()
        {
            _connectionInfo = new ConnectionInfo(_configuration);
            var redirectHost = SendNonDataRequest<String>("get_redirect_host");
            return new ConnectionInfo(_configuration, redirectHost);
        }

        [NotNull]
        private ServerConfiguration SendConnectRequest()
        {
            var connectParameters = GetConnectParameters();
            var responseMap = SendNonDataRequest<Dictionary<String, Object>>("connect", connectParameters);
            if (responseMap == null)
                throw new Exception("Empty connect result payload");

            Log.InfoFormat("Agent {0} connected to {1}:{2}", GetIdentifier(), _connectionInfo.Host, _connectionInfo.Port);

            var serverConfiguration = ServerConfiguration.FromDeserializedReturnValue(responseMap);
            LogConfigurationMessages(serverConfiguration);

            return serverConfiguration;
        }

        private static void LogConfigurationMessages([NotNull] ServerConfiguration serverConfiguration)
        {
            if (serverConfiguration.HighSecurityEnabled == true)
                Log.Info("The agent is in high security mode.  No request parameters will be collected and sql obfuscation is enabled.");

            if (serverConfiguration.Messages == null)
                return;

            foreach (var message in serverConfiguration.Messages)
            {
                if (String.IsNullOrEmpty(message?.Level))
                    continue;
                if (String.IsNullOrEmpty(message.Text))
                    continue;

                var logMethod = ServerLogLevelMap.GetValueOrDefault(message.Level) ?? Log.Info;
                logMethod(message.Text);
            }
        }

        [NotNull]
        private ConnectModel GetConnectParameters()
        {
            var identifier = GetIdentifier();
            var appNames = _configuration.ApplicationNames.ToList();
            if (!appNames.Any())
                appNames.Add(identifier);

            Log.InfoFormat("Your New Relic Application Name(s): {0}", String.Join(":", appNames.ToArray()));

            return new ConnectModel(
                _processStatic.GetCurrentProcess().Id,
                "dotnet",
                _dnsStatic.GetHostName(),
                appNames,
                AgentInstallConfiguration.AgentVersion,
                GetAgentVersionTimestamp(),
                new SecuritySettingsModel
                    (
                    _configuration.CaptureRequestParameters,
                    new TransactionTraceSettingsModel(_configuration.TransactionTracerRecordSql)
                    ),
                _configuration.HighSecurityModeEnabled,
                identifier,
                _labelsService.Labels,
                GetJsAgentSettings(),
                new UtilizationStore(_systemInfo, _dnsStatic, _configuration, _agentHealthReporter).GetUtilizationSettings(),
                _configuration.CollectorSendEnvironmentInfo ? _environment : null);
        }

        private long GetAgentVersionTimestamp()
        {
            var timestamp = AgentInstallConfiguration.AgentVersionTimestamp.ToUnixTimeMilliseconds();
            return timestamp;
        }

        [NotNull]
        private String GetIdentifier()
        {
            var appNames = String.Join(":", _configuration.ApplicationNames.ToArray());

#if NETSTANDARD2_0
			return $"{Path.GetFileName(_processStatic.GetCurrentProcess().MainModuleFileName)}{appNames}";
#else

            return HttpRuntime.AppDomainAppId != null
                ? $"{HttpRuntime.AppDomainAppId}:{_environment.AppDomainAppPath}{appNames}"
                : $"{Path.GetFileName(_processStatic.GetCurrentProcess().MainModuleFileName)}{appNames}";
#endif
        }

        [NotNull]
        private JavascriptAgentSettingsModel GetJsAgentSettings()
        {
            var loader = "rum";
            if (_configuration.BrowserMonitoringJavaScriptAgentLoaderType.Equals("full", StringComparison.InvariantCultureIgnoreCase))
                loader = "full";
            else if (_configuration.BrowserMonitoringJavaScriptAgentLoaderType.Equals("none", StringComparison.InvariantCultureIgnoreCase))
                loader = "none";

            return new JavascriptAgentSettingsModel(false, loader);
        }

        private void SendAgentSettings()
        {
            var agentSettings = new ReportedConfiguration
            {
                ApdexT = _configuration.TransactionTraceApdexT.TotalSeconds,
                CatId = _configuration.CrossApplicationTracingCrossProcessId,
                EncodingKey = _configuration.EncodingKey,
                TrustedAccountIds = _configuration.TrustedAccountIds.ToList(),
                MaxStackTraceLines = _configuration.StackTraceMaximumFrames,
                UsingServerSideConfig = _configuration.UsingServerSideConfig,
                ThreadProfilerEnabled = _configuration.ThreadProfilingEnabled,
                CrossApplicationTracerEnabled = _configuration.CrossApplicationTracingEnabled,
                ErrorCollectorEnabled = _configuration.ErrorCollectorEnabled,
                ErrorCollectorIgnoreStatusCodes = _configuration.HttpStatusCodesToIgnore.ToList(),
                ErrorCollectorIgnoreErrors = _configuration.ExceptionsToIgnore.ToList(),
                TransactionTracerStackThreshold = _configuration.TransactionTracerStackThreshold.TotalSeconds,
                TransactionTracerExplainEnabled = _configuration.SqlExplainPlansEnabled,
                TransactionTracerExplainThreshold = _configuration.SqlExplainPlanThreshold.TotalSeconds,
                MaxSqlStatements = _configuration.SqlStatementsPerTransaction,
                MaxExplainPlans = _configuration.SqlExplainPlansMax,
                TransactionTracerThreshold = _configuration.TransactionTraceThreshold.TotalSeconds,
                TransactionTracerRecordSql = _configuration.TransactionTracerRecordSql,
                SlowSqlEnabled = _configuration.SlowSqlEnabled,
                BrowserMonitoringAutoInstrument = _configuration.BrowserMonitoringAutoInstrument
            };

            try
            {
                SendDataRequest("agent_settings", agentSettings);
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }
        }

        private void SendShutdownCommand()
        {
            try
            {
                SendNonDataRequest("shutdown");
            }
            catch (Exception ex)
            {
                Log.Error($"Shutdown error: {ex}");
            }
        }

        private void Disable()
        {
            _dataRequestWire = new NoOpCollectorWire();

            if (_configuration.AgentRunId == null)
                return;

            EventBus<ServerConfigurationUpdatedEvent>.Publish(new ServerConfigurationUpdatedEvent(ServerConfiguration.GetDefault()));
        }

        #endregion Connect helper methods

        #region Data transfer helper methods

        private T SendNonDataRequest<T>([NotNull] String method, [NotNull] params Object[] data)
        {
            var wire = _collectorWireFactory.GetCollectorWire(_configuration);
            return SendDataOverWire<T>(wire, method, data);
        }

        private void SendNonDataRequest([NotNull] String method, [NotNull] params Object[] data)
        {
            SendNonDataRequest<Object>(method, data);
        }

        [CanBeNull]
        private T SendDataOverWire<T>([NotNull] ICollectorWire wire, [NotNull] String method, [NotNull] params Object[] data)
        {
            try
            {
                var serializedData = _serializer.Serialize(data);
                var responseBody = wire.SendData(method, _connectionInfo, serializedData);
                return ParseResponse<T>(responseBody);
            }
            catch (InstructionException ex)
            {
                Log.DebugFormat("Received a {0} instruction invoking method \"{1}\"", ex.GetType().Name, method);

                var forceDisconnectException = ex as ForceDisconnectException;
                if (forceDisconnectException != null)
                    Log.InfoFormat("Shutting down: {0}", ex.Message);

                throw;
            }
            catch (Exception ex)
            {
                Log.DebugFormat("An error occurred invoking method \"{0}\": {1}", method, ex);
                throw;
            }
        }

        [CanBeNull]
        private T ParseResponse<T>([NotNull] String responseBody)
        {
            var responseEnvelope = _serializer.Deserialize<CollectorResponseEnvelope<T>>(responseBody);
            if (responseEnvelope.CollectorExceptionEnvelope != null)
                throw responseEnvelope.CollectorExceptionEnvelope.Exception;

            return responseEnvelope.ReturnValue;
        }

        #endregion Data transfer helper methods

        #region Event handlers

        protected override void OnConfigurationUpdated(ConfigurationUpdateSource configurationUpdateSource)
        {
            // It is *CRITICAL* that this method never do anything more complicated than clearing data and starting and ending subscriptions.
            // If this method ends up trying to send data synchronously (even indirectly via the EventBus or RequestBus) then the user's application will deadlock (!!!).
        }

        #endregion

        public override void Dispose()
        {
            Disable();
            base.Dispose();
        }
    }
}

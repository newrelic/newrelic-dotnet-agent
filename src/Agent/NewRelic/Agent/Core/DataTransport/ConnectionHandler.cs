// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.Configuration;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Labels;
using NewRelic.Agent.Core.Metric;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Core.Utilization;
using NewRelic.Core.Logging;
using NewRelic.SystemExtensions.Collections.Generic;
using NewRelic.SystemInterfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Web;

namespace NewRelic.Agent.Core.DataTransport
{
    /// <summary>
    /// The <see cref="ConnectionHandler"/> understands the business logic of *how* to connect, disconnect, send data, etc. It is the companion of <see cref="ConnectionManager"/> which knows *when* to connect, disconnect, etc.
    /// 
    /// This class is *NOT* thread safe -- calling any of the public methods on this class in parallel has undefined results and can leave it in a corrupted state.
    /// </summary>
    public class ConnectionHandler : ConfigurationBasedService, IConnectionHandler
    {
        private static readonly Dictionary<string, Action<string>> ServerLogLevelMap = new Dictionary<string, Action<string>>
        {
            {"INFO", Log.Info},
            {"WARN", Log.Warn},
            {"ERROR", Log.Error},
            {"VERBOSE", Log.Finest}
        };

        private readonly ISerializer _serializer;
        private readonly ICollectorWireFactory _collectorWireFactory;
        private ConnectionInfo _connectionInfo;
        private ICollectorWire _dataRequestWire;
        private readonly IProcessStatic _processStatic;
        private readonly IDnsStatic _dnsStatic;
        private readonly ILabelsService _labelsService;
        private readonly ISystemInfo _systemInfo;
        private readonly Environment _environment;
        private readonly IAgentHealthReporter _agentHealthReporter;
        private readonly IEnvironment _environmentVariableHelper;

        public ConnectionHandler(ISerializer serializer, ICollectorWireFactory collectorWireFactory, IProcessStatic processStatic, IDnsStatic dnsStatic, ILabelsService labelsService, Environment environment, ISystemInfo systemInfo, IAgentHealthReporter agentHealthReporter, IEnvironment environmentVariableHelper)
        {
            _serializer = serializer;
            _collectorWireFactory = collectorWireFactory;
            _processStatic = processStatic;
            _dnsStatic = dnsStatic;
            _labelsService = labelsService;
            _environment = environment;
            _systemInfo = systemInfo;
            _agentHealthReporter = agentHealthReporter;
            _environmentVariableHelper = environmentVariableHelper;

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
                ValidateNotBothHsmAndSecurityPolicies(_configuration);

                var preconnectResult = SendPreconnectRequest();
                _connectionInfo = new ConnectionInfo(_configuration, preconnectResult.RedirectHost);

                ValidateAgentTokenSettingToPoliciesReceived(preconnectResult.SecurityPolicies);

                if (_configuration.SecurityPoliciesTokenExists)
                {
                    ValidateAgentExpectedSecurityPoliciesExist(preconnectResult.SecurityPolicies);
                    ValidateAllRequiredPoliciesFromServerExist(preconnectResult.SecurityPolicies);

                    var securityPoliciesConfiguration = new SecurityPoliciesConfiguration(preconnectResult.SecurityPolicies);
                    EventBus<SecurityPoliciesConfigurationUpdatedEvent>.Publish(new SecurityPoliciesConfigurationUpdatedEvent(securityPoliciesConfiguration));
                }

                var serverConfiguration = SendConnectRequest();
                EventBus<ServerConfigurationUpdatedEvent>.Publish(new ServerConfigurationUpdatedEvent(serverConfiguration));

                LogSecurityPolicySettingsOnceAllSettingsResolved();
                GenerateFasterEventHarvestConfigMetrics(serverConfiguration.EventHarvestConfig);

                _dataRequestWire = _collectorWireFactory.GetCollectorWire(_configuration, serverConfiguration.RequestHeadersMap, _agentHealthReporter);
                SendAgentSettings();

                EventBus<AgentConnectedEvent>.Publish(new AgentConnectedEvent());
                Log.Info("Agent fully connected.");
            }

            catch (Exception e)
            {
                Disable();
                Log.Error($"Unable to connect to the New Relic service at {_connectionInfo} : {e}");
                throw;
            }
        }

        private void LogSecurityPolicySettingsOnceAllSettingsResolved()
        {
            if (_configuration.SecurityPoliciesTokenExists == false)
            {
                return;
            }

            Log.DebugFormat("Setting applied: {{\"record_sql\": \"{0}\"}}. Source: {1}", _configuration.TransactionTracerRecordSql, _configuration.TransactionTracerRecordSqlSource);
            Log.DebugFormat("Setting applied: {{\"attributes_include\": {0}}}. Source: {1}", _configuration.CanUseAttributesIncludes, _configuration.CanUseAttributesIncludesSource);
            Log.DebugFormat("Setting applied: {{\"allow_raw_exception_messages\": {0}}}. Source: {1}", !_configuration.StripExceptionMessages, _configuration.StripExceptionMessagesSource);
            Log.DebugFormat("Setting applied: {{\"custom_events\": {0}}}. Source: {1}", _configuration.CustomEventsEnabled, _configuration.CustomEventsEnabledSource);
            Log.DebugFormat("Setting applied: {{\"custom_parameters\": {0}}}. Source: {1}", _configuration.CaptureCustomParameters, _configuration.CaptureCustomParametersSource);
            Log.DebugFormat("Setting applied: {{\"custom_instrumentation_editor\": {0}}}. Source: {1}", _configuration.CustomInstrumentationEditorEnabled, _configuration.CustomInstrumentationEditorEnabledSource);
        }

        public void Disconnect()
        {
            if (!string.IsNullOrEmpty(_configuration.AgentRunId?.ToString()))
                SendShutdownCommand();

            Disable();
        }

        public T SendDataRequest<T>(string method, params object[] data)
        {
            return SendDataOverWire<T>(_dataRequestWire, method, data);
        }

        #endregion Public API

        #region Connect helper methods

        private PreconnectResult SendPreconnectRequest()
        {
            _connectionInfo = new ConnectionInfo(_configuration);

            var payload = new Dictionary<string, object>
            {
                { "high_security", _configuration.HighSecurityModeEnabled }
            };

            if (_configuration.SecurityPoliciesTokenExists)
            {
                payload["security_policies_token"] = _configuration.SecurityPoliciesToken;
            }

            var result = SendNonDataRequest<PreconnectResult>("preconnect", payload);
            return result;
        }

        private static void ValidateNotBothHsmAndSecurityPolicies(IConfiguration configuration)
        {
            if (configuration.HighSecurityModeEnabled && configuration.SecurityPoliciesTokenExists)
            {
                const string errorMessage = @"Security Policies and High Security Mode cannot both be present in the agent configuration. If Security Policies have been set for your account, please ensure the securityPoliciesToken is set but highSecurity is disabled (default).";
                throw new SecurityPoliciesValidationException(errorMessage);
            }
        }

        private static void ValidateAgentExpectedSecurityPoliciesExist(Dictionary<string, SecurityPolicyState> securityPoliciesFromServer)
        {
            var missingExpectedPolicies = SecurityPoliciesConfiguration.GetMissingExpectedSeverPolicyNames(securityPoliciesFromServer);

            if (missingExpectedPolicies.Count > 0)
            {
                var formattedMissingExpectedPolicies = string.Join(", ", missingExpectedPolicies);
                var errorMessage = $"The agent did not receive one or more security policies that it expected and will shut down: {formattedMissingExpectedPolicies}. Please contact support.";
                throw new SecurityPoliciesValidationException(errorMessage);
            }
        }

        private static void ValidateAllRequiredPoliciesFromServerExist(Dictionary<string, SecurityPolicyState> securityPoliciesFromServer)
        {
            var missingRequiredPolicies = SecurityPoliciesConfiguration.GetMissingRequiredPolicies(securityPoliciesFromServer);

            if (missingRequiredPolicies.Count > 0)
            {
                var formattedMissingRequiredPolicies = string.Join(", ", missingRequiredPolicies);
                var errorMessage = $"The agent received one or more required security policies that it does not recognize and will shut down: {formattedMissingRequiredPolicies}. Please check if a newer agent version supports these policies or contact support.";
                throw new SecurityPoliciesValidationException(errorMessage);
            }
        }

        private void ValidateAgentTokenSettingToPoliciesReceived(Dictionary<string, SecurityPolicyState> securityPoliciesFromServer)
        {
            // LASP is not enabled, but security policies received from server
            if (!_configuration.SecurityPoliciesTokenExists && securityPoliciesFromServer != null && securityPoliciesFromServer.Count > 0)
            {
                var policiesReceived = string.Join(", ", securityPoliciesFromServer.Keys);
                var errorMessage = $"The agent received one or more security policies without a security policies token defined and will shut down: {policiesReceived}. Please configure your security policies token or contact support.";

                throw new SecurityPoliciesValidationException(errorMessage);
            }

            // LASP is enabled, but no policies from server
            if (_configuration.SecurityPoliciesTokenExists &&
                (securityPoliciesFromServer == null || securityPoliciesFromServer.Count == 0))
            {
                const string errorMessage = "The agent has a security policies token defined but did not receive any policies from the server and will shut down. Please verify local and server configuration or contact support.";

                throw new SecurityPoliciesValidationException(errorMessage);
            }
        }

        private ServerConfiguration SendConnectRequest()
        {
            var connectParameters = GetConnectParameters();
            var responseMap = SendNonDataRequest<Dictionary<string, object>>("connect", connectParameters);
            if (responseMap == null)
                throw new Exception("Empty connect result payload");

            Log.InfoFormat("Agent {0} connected to {1}:{2}", GetIdentifier(), _connectionInfo.Host, _connectionInfo.Port);

            var serverConfiguration = ServerConfiguration.FromDeserializedReturnValue(responseMap);
            LogConfigurationMessages(serverConfiguration);

            return serverConfiguration;
        }

        private static void LogConfigurationMessages(ServerConfiguration serverConfiguration)
        {
            if (serverConfiguration.HighSecurityEnabled == true)
                Log.Info("The agent is in high security mode.  No request parameters will be collected and sql obfuscation is enabled.");

            if (serverConfiguration.Messages == null)
                return;

            foreach (var message in serverConfiguration.Messages)
            {
                if (string.IsNullOrEmpty(message?.Level))
                    continue;
                if (string.IsNullOrEmpty(message.Text))
                    continue;

                var logMethod = ServerLogLevelMap.GetValueOrDefault(message.Level) ?? Log.Info;
                logMethod(message.Text);
            }
        }

        private ConnectModel GetConnectParameters()
        {
            var identifier = GetIdentifier();
            var appNames = _configuration.ApplicationNames.ToList();
            if (!appNames.Any())
                appNames.Add(identifier);

            Log.InfoFormat("Your New Relic Application Name(s): {0}", string.Join(":", appNames.ToArray()));

            var metadata = _environmentVariableHelper.GetEnvironmentVariablesWithPrefix("NEW_RELIC_METADATA_");

            return new ConnectModel(
                _processStatic.GetCurrentProcess().Id,
                "dotnet",
                _configuration.ProcessHostDisplayName,
                _dnsStatic.GetHostName(),
                appNames,
                AgentInstallConfiguration.AgentVersion,
                AgentInstallConfiguration.AgentVersionTimestamp,
                new SecuritySettingsModel
                    (
                    _configuration.CaptureRequestParameters,
                    new TransactionTraceSettingsModel(_configuration.TransactionTracerRecordSql)
                    ),
                _configuration.HighSecurityModeEnabled,
                identifier,
                _labelsService.Labels,
                GetJsAgentSettings(),
                metadata ?? new Dictionary<string, string>(),
                new UtilizationStore(_systemInfo, _dnsStatic, _configuration, _agentHealthReporter).GetUtilizationSettings(),
                _configuration.CollectorSendEnvironmentInfo ? _environment : null,
                _configuration.SecurityPoliciesTokenExists ? new SecurityPoliciesSettingsModel(_configuration) : null,
                new EventHarvestConfigModel(_configuration)
            );
        }

        private string GetIdentifier()
        {
            var appNames = string.Join(":", _configuration.ApplicationNames.ToArray());

#if NETSTANDARD2_0
			return $"{Path.GetFileName(_processStatic.GetCurrentProcess().MainModuleFileName)}{appNames}";
#else

            return HttpRuntime.AppDomainAppId != null
                ? $"{HttpRuntime.AppDomainAppId}:{_environment.AppDomainAppPath}{appNames}"
                : $"{Path.GetFileName(_processStatic.GetCurrentProcess().MainModuleFileName)}{appNames}";
#endif
        }

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
                DistributedTracingEnabled = _configuration.DistributedTracingEnabled,
                ErrorCollectorEnabled = _configuration.ErrorCollectorEnabled,
                ErrorCollectorIgnoreStatusCodes = _configuration.HttpStatusCodesToIgnore.ToList(),
                ErrorCollectorIgnoreErrors = _configuration.IgnoreErrorsForAgentSettings.ToList(),
                ErrorCollectorIgnoreClasses = _configuration.IgnoreErrorClassesForAgentSettings,
                ErrorCollectorIgnoreMessages = _configuration.IgnoreErrorMessagesForAgentSettings,
                ErrorCollectorExpectedClasses = _configuration.ExpectedErrorClassesForAgentSettings,
                ErrorCollectorExpectedMessages = _configuration.ExpectedErrorMessagesForAgentSettings,
                ErrorCollectorExpectedStatusCodes = _configuration.ExpectedErrorStatusCodesForAgentSettings,
                TransactionTracerStackThreshold = _configuration.TransactionTracerStackThreshold.TotalSeconds,
                TransactionTracerExplainEnabled = _configuration.SqlExplainPlansEnabled,
                TransactionTracerExplainThreshold = _configuration.SqlExplainPlanThreshold.TotalSeconds,
                MaxSqlStatements = _configuration.SqlStatementsPerTransaction,
                MaxExplainPlans = _configuration.SqlExplainPlansMax,
                TransactionTracerThreshold = _configuration.TransactionTraceThreshold.TotalSeconds,
                TransactionTracerRecordSql = _configuration.TransactionTracerRecordSql,
                SlowSqlEnabled = _configuration.SlowSqlEnabled,
                BrowserMonitoringAutoInstrument = _configuration.BrowserMonitoringAutoInstrument,
                TransactionEventMaxSamplesStored = _configuration.TransactionEventsMaximumSamplesStored
            };

            try
            {
                SendDataOverWire<object>(_dataRequestWire, "agent_settings", agentSettings);
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }
        }

        private void GenerateFasterEventHarvestConfigMetrics(EventHarvestConfig eventHarvestConfig)
        {
            if (eventHarvestConfig == null) return;

            if (!eventHarvestConfig.ReportPeriodMs.HasValue) return;

            _agentHealthReporter.ReportSupportabilityCountMetric(MetricNames.SupportabilityEventHarvestReportPeriod, eventHarvestConfig.ReportPeriodMs.Value);

            var fasterEventHarvestEnabledTypes = new List<string>();

            if (GenerateHarvestLimitMetricIfAvailable(MetricNames.SupportabilityEventHarvestErrorEventHarvestLimit, eventHarvestConfig.ErrorEventHarvestLimit()))
            {
                fasterEventHarvestEnabledTypes.Add("Error events");
            }

            if (GenerateHarvestLimitMetricIfAvailable(MetricNames.SupportabilityEventHarvestCustomEventHarvestLimit, eventHarvestConfig.CustomEventHarvestLimit()))
            {
                fasterEventHarvestEnabledTypes.Add("Custom events");
            }

            if (GenerateHarvestLimitMetricIfAvailable(MetricNames.SupportabilityEventHarvestTransactionEventHarvestLimit, eventHarvestConfig.TransactionEventHarvestLimit()))
            {
                fasterEventHarvestEnabledTypes.Add("Transaction events");
            }

            if (GenerateHarvestLimitMetricIfAvailable(MetricNames.SupportabilityEventHarvestSpanEventHarvestLimit, eventHarvestConfig.SpanEventHarvestLimit()))
            {
                fasterEventHarvestEnabledTypes.Add("Span events");
            }

            if (fasterEventHarvestEnabledTypes.Count > 0)
            {
                Log.InfoFormat("The following events will be harvested every {1}ms: {0}", string.Join(", ", fasterEventHarvestEnabledTypes), eventHarvestConfig.ReportPeriodMs);
            }
        }

        private bool GenerateHarvestLimitMetricIfAvailable(string metricName, int? harvestLimit)
        {
            if (!harvestLimit.HasValue) return false;

            _agentHealthReporter.ReportSupportabilityCountMetric(metricName, unchecked(harvestLimit.Value));
            return true;
        }

        private void SendShutdownCommand()
        {
            try
            {
                SendDataOverWire<object>(_dataRequestWire, "shutdown");
            }
            catch (Exception ex)
            {
                Log.Error($"Shutdown error: {ex}");
            }
        }

        private void Disable()
        {
            _dataRequestWire = new NoOpCollectorWire();

            if (string.IsNullOrEmpty(_configuration.AgentRunId?.ToString()))
                return;

            EventBus<ServerConfigurationUpdatedEvent>.Publish(new ServerConfigurationUpdatedEvent(ServerConfiguration.GetDefault()));
        }

        #endregion Connect helper methods

        #region Data transfer helper methods

        private T SendNonDataRequest<T>(string method, params object[] data)
        {
            var wire = _collectorWireFactory.GetCollectorWire(_configuration, _agentHealthReporter);
            return SendDataOverWire<T>(wire, method, data);
        }

        private T SendDataOverWire<T>(ICollectorWire wire, string method, params object[] data)
        {
            try
            {
                var serializedData = _serializer.Serialize(data);
                var responseBody = wire.SendData(method, _connectionInfo, serializedData);
                return ParseResponse<T>(responseBody);
            }
            catch (Exceptions.HttpException ex)
            {
                Log.DebugFormat("Received a {0} {1} response invoking method \"{2}\"", (int)ex.StatusCode, ex.StatusCode, method);

                if (ex.StatusCode == HttpStatusCode.Gone)
                {
                    Log.Info("The server has requested that the agent disconnect. The agent is shutting down.");
                }

                throw;
            }
            catch (Exception ex)
            {
                Log.DebugFormat("An error occurred invoking method \"{0}\": {1}", method, ex);
                throw;
            }
        }

        private T ParseResponse<T>(string responseBody)
        {
            var responseEnvelope = _serializer.Deserialize<CollectorResponseEnvelope<T>>(responseBody);
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

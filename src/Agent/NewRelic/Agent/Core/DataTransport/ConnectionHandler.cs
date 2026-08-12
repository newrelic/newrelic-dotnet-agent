// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.Configuration;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Labels;
using NewRelic.Agent.Core.Metrics;
using NewRelic.Agent.Core.SharedInterfaces;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Core.Utilization;
using NewRelic.Agent.Extensions.Logging;
using NewRelic.Agent.Extensions.SystemExtensions.Collections.Generic;
using Newtonsoft.Json;
#if NETFRAMEWORK
using System.Web;
#endif

namespace NewRelic.Agent.Core.DataTransport;

/// <summary>
/// The <see cref="ConnectionHandler"/> understands the business logic of *how* to connect, disconnect, send data, etc. It is the companion of <see cref="ConnectionManager"/> which knows *when* to connect, disconnect, etc.
/// 
/// This class is *NOT* thread safe -- calling any of the public methods on this class in parallel has undefined results and can leave it in a corrupted state.
/// </summary>
public class ConnectionHandler : ConfigurationBasedService, IConnectionHandler
{
    private static readonly Dictionary<string, Action<string, object[]>> ServerLogLevelMap = new Dictionary<string, Action<string, object[]>>
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
    private readonly IFileWrapper _fileWrapper;

    public ConnectionHandler(ISerializer serializer, ICollectorWireFactory collectorWireFactory, IProcessStatic processStatic, IDnsStatic dnsStatic, ILabelsService labelsService, Environment environment, ISystemInfo systemInfo, IAgentHealthReporter agentHealthReporter, IEnvironment environmentVariableHelper, IFileWrapper fileWrapper, ICollectorWire dataRequestWire = null)
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
        _fileWrapper = fileWrapper;

        _connectionInfo = new ConnectionInfo(_configuration);
        _dataRequestWire = dataRequestWire ??  new NoOpCollectorWire();
    }

    #region Public API

    public void Connect()
    {
        // Need to disable before connecting so that we can easily tell that we just finished connecting during a configuration update
        Disable();

        try
        {
            if (_configuration.SecurityPoliciesTokenExists)
            {
                Log.Warn("The 'securityPoliciesToken' configuration option is deprecated and no longer honored. Language Agent Security Policies (LASP) support has been removed from the agent. Please remove this setting from your configuration.");
            }

            LogTlsConfiguration();

            var preconnectResult = SendPreconnectRequest();
            _connectionInfo = new ConnectionInfo(_configuration, preconnectResult.RedirectHost);

            var serverConfiguration = SendConnectRequest();
            EventBus<ServerConfigurationUpdatedEvent>.Publish(new ServerConfigurationUpdatedEvent(serverConfiguration));

            GenerateFasterEventHarvestConfigMetrics(serverConfiguration.EventHarvestConfig);

            GenerateSpanEventsHarvestLimitMetrics(serverConfiguration.SpanEventHarvestConfig);

            _dataRequestWire = _collectorWireFactory.GetCollectorWire(_configuration, serverConfiguration.RequestHeadersMap, _agentHealthReporter);
            SendAgentSettings();

            EventBus<AgentConnectedEvent>.Publish(new AgentConnectedEvent { ConnectInfo = _connectionInfo });
            Log.Info("Agent fully connected.");
            _agentHealthReporter.SetAgentControlStatus(HealthCodes.Healthy);
        }

        catch (Exception e)
        {
            Disable();
            Log.Error(e, $"Unable to connect to the New Relic service at {_connectionInfo}");
            throw;
        }
    }

    private void LogTlsConfiguration()
    {
        Log.Info($"Current TLS Configuration (System.Net.ServicePointManager.SecurityProtocol): {System.Net.ServicePointManager.SecurityProtocol}");
    }

    private void GenerateSpanEventsHarvestLimitMetrics(SingleEventHarvestConfig spanEventHarvestConfig)
    {
        if (spanEventHarvestConfig != null)
        {
            _agentHealthReporter.ReportSupportabilityCountMetric(MetricNames.SupportabilitySpanEventsLimit, spanEventHarvestConfig.HarvestLimit);
        }
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

        var result = SendNonDataRequest<PreconnectResult>("preconnect", payload);
        return result;
    }

    private ServerConfiguration SendConnectRequest()
    {
        var connectParameters = GetConnectParameters();
        var responseMap = SendNonDataRequest<Dictionary<string, object>>("connect", connectParameters);
        if (responseMap == null)
            throw new Exception("Empty connect result payload");

        Log.Info("Agent {0} connected to {1}:{2}", GetIdentifier(), _connectionInfo.Host, _connectionInfo.Port);

        var serverConfiguration = ServerConfiguration.FromDeserializedReturnValue(responseMap, _configuration.IgnoreServerSideConfiguration);
        LogConfigurationMessages(serverConfiguration);

        return serverConfiguration;
    }

    private void LogConfigurationMessages(ServerConfiguration serverConfiguration)
    {
        if (serverConfiguration.HighSecurityEnabled == true)
        {
            Log.Info("The agent is in high security mode.  No request parameters will be collected and sql obfuscation is enabled.");
        }

        if (serverConfiguration.ServerSideConfigurationEnabled)
        {
            if (_configuration.IgnoreServerSideConfiguration)
            {
                Log.Info("Server-Side Configuration is enabled, but the agent is configured to ignore it.");
            }
            else
            {
                Log.Info("Server-Side Configuration is enabled.");
            }
        }

        if (serverConfiguration.Messages == null)
        {
            return;
        }

        foreach (var message in serverConfiguration.Messages)
        {
            if (string.IsNullOrEmpty(message?.Level))
                continue;
            if (string.IsNullOrEmpty(message.Text))
                continue;

            var logMethod = ServerLogLevelMap.GetValueOrDefault(message.Level) ?? Log.Info;
            logMethod(message.Text, null);
        }
    }

    private ConnectModel GetConnectParameters()
    {
        var identifier = GetIdentifier();
        var appNames = _configuration.ApplicationNames.ToList();
        if (!appNames.Any())
            appNames.Add(identifier);

        Log.Info("Your New Relic Application Name(s): {0}", string.Join(":", appNames.ToArray()));

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
                new TransactionTraceSettingsModel(_configuration.TransactionTracerRecordSql)
            ),
            _configuration.HighSecurityModeEnabled,
            identifier,
            _labelsService.Labels,
            metadata ?? new Dictionary<string, string>(),
            new UtilizationStore(_systemInfo, _dnsStatic, _configuration, _agentHealthReporter, _fileWrapper).GetUtilizationSettings(),
            _configuration.CollectorSendEnvironmentInfo ? _environment : null,
            new EventHarvestConfigModel(_configuration),
            new ReportedConfiguration(_configuration)
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

    private void SendAgentSettings()
    {
        var agentSettings = new ReportedConfiguration(_configuration);

        try
        {
            SendDataOverWire<object>(_dataRequestWire, "agent_settings", agentSettings);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "SendAgentSettings() failed");
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

        if (fasterEventHarvestEnabledTypes.Count > 0)
        {
            Log.Info("The following events will be harvested every {1}ms: {0}", string.Join(", ", fasterEventHarvestEnabledTypes), eventHarvestConfig.ReportPeriodMs);
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
            Log.Error(ex, "Shutdown error");
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
        var requestGuid = Guid.NewGuid();
        string serializedData;
        try
        {
            serializedData = _serializer.Serialize(data);
        }
        catch (JsonSerializationException ex)
        {
            Log.Debug("Request({0}): Exception occurred serializing request data: {1}", requestGuid, ex); // log message only since exception is rethrown
            throw;
        }

        try
        {
            var responseBody = wire.SendData(method, _connectionInfo, serializedData, requestGuid);
            return ParseResponse<T>(responseBody);
        }
        catch (DataTransport.HttpException ex)
        {
            Log.Debug("Request({0}): Received a {1} {2} response invoking method \"{3}\" with payload \"{4}\"", requestGuid, (int)ex.StatusCode, ex.StatusCode, method, serializedData);

            if (ex.StatusCode == HttpStatusCode.Gone)
                Log.Info(ex, "Request({0}): The server has requested that the agent disconnect. The agent is shutting down.", requestGuid);

            SetAgentControlStatus(requestGuid, method, ex);

            throw;
        }
        catch (Exception ex)
        {
            Log.Debug("Request({0}): An error occurred invoking method \"{1}\" with payload \"{2}\": {3}", requestGuid, method, serializedData, ex); // log message only since exception is rethrown

            SetAgentControlStatus(requestGuid, method, null);

            throw;
        }
    }

    private void SetAgentControlStatus(Guid requestGuid, string method, HttpException httpException)
    {
        if (method.Equals("connect"))
        {
            _agentHealthReporter.SetAgentControlStatus(HealthCodes.FailedToConnect);
        }
        else
        {
            if (httpException == null) // this shouldn't happen, but...
            {
                _agentHealthReporter.SetAgentControlStatus(HealthCodes.HttpError, "unknown", method);
                return;
            }

            switch (httpException.StatusCode)
            {
                case HttpStatusCode.Unauthorized:
                    _agentHealthReporter.SetAgentControlStatus(HealthCodes.LicenseKeyInvalid);
                    break;
                case HttpStatusCode.Gone:
                    _agentHealthReporter.SetAgentControlStatus(HealthCodes.ForceDisconnect);
                    break;
                case HttpStatusCode.ProxyAuthenticationRequired:
                    _agentHealthReporter.SetAgentControlStatus(HealthCodes.HttpProxyError, httpException.StatusCode.ToString());
                    break;
                default:
                    _agentHealthReporter.SetAgentControlStatus(HealthCodes.HttpError, httpException.StatusCode.ToString(), method);
                    break;
            }
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

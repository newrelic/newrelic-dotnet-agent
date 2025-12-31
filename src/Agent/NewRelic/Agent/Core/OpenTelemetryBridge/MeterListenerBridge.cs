// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Core.DataTransport;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Logging;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Extensions.Logging;

namespace NewRelic.Agent.Core.OpenTelemetryBridge
{
    /// <summary>
    /// Orchestrates the OpenTelemetry bridging process by coordinating the lifecycle
    /// of the OTLP exporter and the meter listener.
    /// </summary>
    public class MeterListenerBridge : ConfigurationBasedService
    {
        private OpenTelemetrySDKLogger _sdkLogger;
        private IConnectionInfo _connectionInfo;
        private string _currentEntityGuid;

        private readonly IMeterBridgingService _meterBridgingService;
        private readonly IOtlpExporterConfigurationService _otlpConfigurationService;

        public MeterListenerBridge(
            IMeterBridgingService meterBridgingService,
            IOtlpExporterConfigurationService otlpConfigurationService)
        {
            _meterBridgingService = meterBridgingService;
            _otlpConfigurationService = otlpConfigurationService;

            _subscriptions.Add<AgentConnectedEvent>(OnAgentConnected);
            _subscriptions.Add<ServerConfigurationUpdatedEvent>(OnServerConfigurationUpdated);
            _subscriptions.Add<PreCleanShutdownEvent>(OnPreCleanShutdown);

            // Start listening immediately to catch instruments created early in app lifecycle
            // OTLP exporter will be configured later when agent connects
            if (_configuration.OpenTelemetryMetricsEnabled)
            {
                _meterBridgingService.StartListening(null);
            }
        }

        private void OnAgentConnected(AgentConnectedEvent agentConnectedEvent)
        {
            _connectionInfo = agentConnectedEvent.ConnectInfo;
            _currentEntityGuid = _configuration.EntityGuid;

            ConfigureOtlpExporter();
        }

        private void OnPreCleanShutdown(PreCleanShutdownEvent preCleanShutdownEvent)
        {
            Stop();
        }

        private void OnServerConfigurationUpdated(ServerConfigurationUpdatedEvent serverConfigurationUpdatedEvent)
        {
            var newEntityGuid = serverConfigurationUpdatedEvent.Configuration.EntityGuid;

            if (!string.IsNullOrEmpty(_currentEntityGuid) &&
                !string.IsNullOrEmpty(newEntityGuid) &&
                _currentEntityGuid != newEntityGuid)
            {
                _currentEntityGuid = newEntityGuid;

                if (_connectionInfo != null && _configuration.OpenTelemetryMetricsEnabled)
                {
                    _otlpConfigurationService.GetOrCreateMeterProvider(_connectionInfo, _currentEntityGuid);
                }
            }
            else if (string.IsNullOrEmpty(_currentEntityGuid) && !string.IsNullOrEmpty(newEntityGuid))
            {
                _currentEntityGuid = newEntityGuid;
            }
        }

        protected override void OnConfigurationUpdated(ConfigurationUpdateSource configurationUpdateSource)
        {
        }

        public void ConfigureOtlpExporter()
        {
            if (!_configuration.OpenTelemetryMetricsEnabled)
            {
                return;
            }

            if (_connectionInfo == null)
            {
                return;
            }

            if (_sdkLogger == null)
            {
                _sdkLogger = new OpenTelemetrySDKLogger();
            }

            _otlpConfigurationService.GetOrCreateMeterProvider(_connectionInfo, _currentEntityGuid);
        }

        public void Stop()
        {
            _meterBridgingService.StopListening();
            _otlpConfigurationService.Dispose();

            if (_sdkLogger != null)
            {
                _sdkLogger.Dispose();
                _sdkLogger = null;
            }
        }

        public override void Dispose()
        {
            Stop();
            base.Dispose();
        }
    }
}

// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Core.DataTransport;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Logging;
using NewRelic.Agent.Core.OpenTelemetryBridge.Metrics.Interfaces;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Extensions.Logging;

namespace NewRelic.Agent.Core.OpenTelemetryBridge.Metrics;

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

        // Only update if we have a new non-empty GUID that's different from current
        if (string.IsNullOrEmpty(newEntityGuid) || _currentEntityGuid == newEntityGuid)
        {
            return;
        }

        _currentEntityGuid = newEntityGuid;
            
        // Create provider if we have connection info and metrics enabled
        if (_connectionInfo != null && _configuration.OpenTelemetryMetricsEnabled)
        {
            _otlpConfigurationService.GetOrCreateMeterProvider(_connectionInfo, _currentEntityGuid);
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

    public void Start()
    {
        if (_configuration.OpenTelemetryMetricsEnabled)
        {
            if (_configuration.EventListenerSamplersEnabled)
            {
                Log.Warn("NewRelic.EventListenerSamplersEnabled is true while OpenTelemetry metrics are enabled. " +
                         "This may cause EventSource conflicts. Set NewRelic.EventListenerSamplersEnabled=false to prevent conflicts.");
            }

            // Start the meter listener BEFORE creating the MeterProvider so that
            // bridged ILRepacked instruments already exist when MeterProvider.Build()
            // runs.  Build() starts the SDK's internal MeterListener which enumerates
            // all published ILRepacked instruments and enables them for collection.
            //
            // In non-serverless mode the provider is built later via OnAgentConnected,
            // so instruments naturally exist before the provider.  In serverless mode
            // GetOrCreateMeterProvider() builds immediately, so we must ensure the
            // bridged instruments are published first — otherwise the SDK's listener
            // sees zero instruments at Build() time and sync instruments that are
            // published later may not be picked up reliably through the ILRepacked
            // static notification path.
            _meterBridgingService.StartListening(_otlpConfigurationService);
            _otlpConfigurationService.GetOrCreateMeterProvider();
        }
    }

    public void Stop()
    {
        _meterBridgingService.StopListening();
        _otlpConfigurationService.Dispose();

        // Do not dispose _sdkLogger to avoid potential EventListener conflicts.
        // Note: EventPipe-based samplers (GCSamplerNetCore, ThreadStatsSampler) are automatically
        // disabled when OpenTelemetry metrics are enabled (see DefaultConfiguration.EventListenerSamplersEnabled),
        // which prevents conflicts. However, we still avoid disposing the logger as a defensive measure.
        // EventListener has singleton semantics per EventSource - disposing can disrupt subscriptions.
        // The SDK logger is lightweight and will be cleaned up when the AppDomain unloads.
        // See: https://github.com/newrelic/newrelic-dotnet-agent/issues/234
        if (_sdkLogger != null)
        {
            // _sdkLogger.Dispose(); // Intentionally not disposing - see comment above
            _sdkLogger = null;
        }
    }

    public override void Dispose()
    {
        Stop();
        base.Dispose();
    }
}

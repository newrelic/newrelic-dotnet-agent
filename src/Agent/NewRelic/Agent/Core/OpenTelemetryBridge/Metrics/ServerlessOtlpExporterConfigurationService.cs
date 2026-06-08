// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Linq;
using System.Net.Http;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.DataTransport;
using NewRelic.Agent.Core.OpenTelemetryBridge.Metrics.Interfaces;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;

namespace NewRelic.Agent.Core.OpenTelemetryBridge.Metrics;

/// <summary>
/// <see cref="IOtlpExporterConfigurationService"/> implementation for serverless (Lambda) mode.
///
/// Instead of sending metrics to a real OTLP endpoint, this service builds a <see cref="MeterProvider"/>
/// backed by a <see cref="OtlpInterceptingMessageHandler"/> that intercepts the OTLP HTTP export and stores
/// the raw protobuf bytes in memory. At each Lambda invocation end, <c>FlushServerlessDataEvent</c>
/// triggers <c>ForceFlush()</c>, which harvests all instruments (including observables) with full SDK
/// aggregation and serializes them to protobuf. The bytes are written to the /tmp/newrelic-telemetry
/// payload under "otlp_payload" for the Lambda extension to read, augment (entity.guid etc.), and
/// forward to the New Relic OTLP ingest endpoint.
/// </summary>
public sealed class ServerlessOtlpExporterConfigurationService : IOtlpExporterConfigurationService
{
    private readonly IConfigurationService _configurationService;
    private readonly IServerlessModeDataTransportService _dataTransportService;
    private readonly object _meterProviderLock = new object();
    private MeterProvider _meterProvider;
    private HttpClient _httpClient;
    private OtlpInterceptingMessageHandler _handler;

    public ServerlessOtlpExporterConfigurationService(
        IConfigurationService configurationService,
        IServerlessModeDataTransportService dataTransportService)
    {
        _configurationService = configurationService;
        _dataTransportService = dataTransportService;
    }

    public HttpClient HttpClient => _httpClient;

    /// <summary>
    /// Builds the serverless <see cref="MeterProvider"/> the first time it is called.
    /// Subsequent calls are no-ops (connection info is not needed in serverless mode).
    /// </summary>
    public object GetOrCreateMeterProvider()
    {
        if (_meterProvider != null)
            return _meterProvider;

        lock (_meterProviderLock)
        {
            if (_meterProvider != null)
                return _meterProvider;

            var config = _configurationService.Configuration;

            _handler = new OtlpInterceptingMessageHandler();
            _httpClient = new HttpClient(_handler);

            _meterProvider = Sdk.CreateMeterProviderBuilder()
                .ConfigureResource(r => r
                    .AddService(config.ApplicationNames.First())
                    .AddTelemetrySdk())
                .AddMeter("*")
                .AddOtlpExporter((exporterOptions, readerOptions) =>
                {
                    // Endpoint URL is a required non-null field for SDK initialization, but the actual
                    // value does not matter — OtlpInterceptingMessageHandler intercepts every SendAsync()
                    // call before any real network I/O occurs.
                    exporterOptions.Endpoint = new Uri("https://otlp.nr-data.net/v1/metrics");
                    // Protocol MUST be HttpProtobuf: it controls how the SDK serializes the metrics.
                    // The Lambda extension will forward the raw protobuf bytes to New Relic OTLP ingest.
                    exporterOptions.Protocol = OtlpExportProtocol.HttpProtobuf;
                    // HttpClientFactory MUST be set to inject our capturing handler.
                    // Without this the SDK creates its own HttpClient with a real transport
                    // and the bytes would never be captured.
                    exporterOptions.HttpClientFactory = () => _httpClient;

                    // Disable the periodic export timer; metrics are harvested only via ForceFlush()
                    // which is triggered by FlushServerlessDataEvent at Lambda invocation end.
                    readerOptions.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = int.MaxValue;

                    // Delta temporality ensures every ForceFlush() exports all accumulated measurements
                    // since the last collection, including the very first collection.
                    // Without this (Cumulative default), the first ForceFlush() may not report
                    // non-observable instruments that were recorded before the first export cycle.
                    readerOptions.TemporalityPreference = MetricReaderTemporalityPreference.Delta;
                })
                .Build();

            _dataTransportService.SetOtelPayloadFunc(() =>
            {
                _meterProvider?.ForceFlush();
                return _handler?.Drain();
            });

            return _meterProvider;
        }
    }

    /// <summary>
    /// Serverless mode does not use connection info — delegates to <see cref="GetOrCreateMeterProvider()"/>.
    /// </summary>
    public object GetOrCreateMeterProvider(IConnectionInfo connectionInfo, string entityGuid)
        => GetOrCreateMeterProvider();

    /// <summary>
    /// No-op in serverless mode. Provider recreation based on entity GUID changes is not applicable
    /// as the Lambda extension stamps entity attributes before forwarding.
    /// </summary>
    public void RecreateMeterProvider() { }

    public void Dispose()
    {
        _meterProvider?.ForceFlush();
        _meterProvider?.Dispose();
        _meterProvider = null;
        _httpClient?.Dispose();
        _httpClient = null;
        _handler = null;
    }
}

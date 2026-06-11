// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.DataTransport;
using NewRelic.Agent.Core.Metrics;
using NewRelic.Agent.Core.OpenTelemetryBridge.Metrics.Interfaces;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;

namespace NewRelic.Agent.Core.OpenTelemetryBridge.Metrics;

public class OtlpExporterConfigurationService : DisposableService, IOtlpExporterConfigurationService
{
    private readonly IConfigurationService _configurationService;
    private readonly IOtelBridgeSupportabilityMetricCounters _supportabilityMetricCounters;
    private readonly IAgentHealthReporter _agentHealthReporter;
    private readonly MeterBridgeConfiguration _bridgeConfiguration;

    // Reflection cache for setting the base-2 exponential histogram aggregation (see
    // TrySetBase2ExponentialHistogramAggregation). Resolved once and reused across recreations.
    private static PropertyInfo _defaultHistogramAggregationProperty;
    private static object _base2ExponentialAggregationValue;
    private static bool _histogramAggregationReflectionInitialized;

    private MeterProvider _meterProvider;
    private readonly object _meterProviderLock = new object();
    private HttpClient _httpClient;
        
    // Tracking state to detect changes that require recreation
    private IConnectionInfo _lastConnectionInfo;
    private string _lastEntityGuid;

    public OtlpExporterConfigurationService(
        IConfigurationService configurationService, 
        IOtelBridgeSupportabilityMetricCounters supportabilityMetricCounters,
        IAgentHealthReporter agentHealthReporter,
        MeterBridgeConfiguration bridgeConfiguration)
    {
        _configurationService = configurationService;
        _supportabilityMetricCounters = supportabilityMetricCounters;
        _agentHealthReporter = agentHealthReporter;
        _bridgeConfiguration = bridgeConfiguration;
    }

    public HttpClient HttpClient => _httpClient;

    public object GetOrCreateMeterProvider() => GetOrCreateMeterProvider(_lastConnectionInfo, _lastEntityGuid);

    public object GetOrCreateMeterProvider(IConnectionInfo connectionInfo, string entityGuid)
    {
        if (connectionInfo == null)
        {
            return null;
        }

        if (_meterProvider != null && ConnectionInfoEquals(_lastConnectionInfo, connectionInfo) && _lastEntityGuid == entityGuid)
        {
            return _meterProvider;
        }

        lock (_meterProviderLock)
        {
            if (_meterProvider != null && ConnectionInfoEquals(_lastConnectionInfo, connectionInfo) && _lastEntityGuid == entityGuid)
            {
                return _meterProvider;
            }

            if (entityGuid != _lastEntityGuid)
            {
                _supportabilityMetricCounters?.Record(OtelBridgeSupportabilityMetric.EntityGuidChanged);
            }

            _lastConnectionInfo = connectionInfo;
            _lastEntityGuid = entityGuid;

            RecreateMeterProviderInternal();
            return _meterProvider;
        }
    }

    public void RecreateMeterProvider()
    {
        lock (_meterProviderLock)
        {
            RecreateMeterProviderInternal();
        }
    }

    private void RecreateMeterProviderInternal()
    {
        _meterProvider?.Dispose();
        _httpClient?.Dispose();
        _httpClient = null;

        if (_lastConnectionInfo == null) return;

        var endpoint = _bridgeConfiguration.BuildOtlpEndpoint(_lastConnectionInfo);
        var config = _configurationService.Configuration;
            
        _httpClient = CreateHttpClientWithProxyAndRetry(_lastConnectionInfo);

        var providerBuilder = Sdk.CreateMeterProviderBuilder()
            .ConfigureResource(r => r
                .AddService(config.ApplicationNames.First())
                .AddTelemetrySdk()
                .AddAttributes(new[] { new KeyValuePair<string, object>("entity.guid", _lastEntityGuid ?? config.EntityGuid) }))
            .AddMeter("*")
            .AddOtlpExporter((exporterOptions, metricReaderOptions) =>
            {
                exporterOptions.Endpoint = endpoint;
                exporterOptions.Protocol = OtlpExportProtocol.HttpProtobuf;
                exporterOptions.Headers = $"api-key={config.AgentLicenseKey}";
                exporterOptions.HttpClientFactory = () => _httpClient;

                metricReaderOptions.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = config.OpenTelemetryMetricsExportIntervalMs;
                metricReaderOptions.PeriodicExportingMetricReaderOptions.ExportTimeoutMilliseconds = config.OpenTelemetryMetricsExportTimeoutMs;
                metricReaderOptions.TemporalityPreference = MetricReaderTemporalityPreference.Delta;
                TrySetBase2ExponentialHistogramAggregation(metricReaderOptions);
            });

        _meterProvider = providerBuilder.Build();
        _supportabilityMetricCounters?.Record(OtelBridgeSupportabilityMetric.MeterProviderRecreated);
    }

    /// <summary>
    /// Sets the metric reader's default histogram aggregation to base-2 exponential bucket histograms,
    /// which the NR OTLP ingest endpoint prefers over explicit-bucket histograms.
    /// </summary>
    /// <remarks>
    /// In SDK 1.15.3 <c>MetricReaderOptions.DefaultHistogramAggregation</c> (a nullable
    /// <c>MetricReaderHistogramAggregation</c>) and the enum itself are internal in every target framework
    /// - the public surface is gated behind an experimental compile flag the shipped package does not set -
    /// so the value is set via reflection. Customer <c>AddView</c> overrides still take precedence
    /// (SDK-guaranteed). Failures are non-fatal: the SDK default (explicit bucket histogram) is used instead.
    /// </remarks>
    private static void TrySetBase2ExponentialHistogramAggregation(MetricReaderOptions metricReaderOptions)
    {
        try
        {
            if (!_histogramAggregationReflectionInitialized)
            {
                _histogramAggregationReflectionInitialized = true;

                var property = typeof(MetricReaderOptions).GetProperty("DefaultHistogramAggregation",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (property != null)
                {
                    var enumType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
                    if (enumType.IsEnum && Enum.IsDefined(enumType, "Base2ExponentialBucketHistogram"))
                    {
                        _base2ExponentialAggregationValue = Enum.Parse(enumType, "Base2ExponentialBucketHistogram");
                        _defaultHistogramAggregationProperty = property;
                    }
                }

                if (_defaultHistogramAggregationProperty == null)
                {
                    Log.Debug("Could not resolve MetricReaderOptions.DefaultHistogramAggregation via reflection; OTLP histogram instruments will use the SDK default (explicit bucket) aggregation.");
                }
                else
                {
                    Log.Debug("Successfully resolved MetricReaderOptions.DefaultHistogramAggregation via reflection; OTLP histogram instruments will use base-2 exponential bucket aggregation.");
                }
            }

            _defaultHistogramAggregationProperty?.SetValue(metricReaderOptions, _base2ExponentialAggregationValue);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Failed to set base-2 exponential histogram aggregation on OTLP metric reader options; falling back to SDK default.");
        }
    }

    private HttpClient CreateHttpClientWithProxyAndRetry(IConnectionInfo connectionInfo)
    {
        try
        {
            var httpClientHandler = new HttpClientHandler();

            if (connectionInfo?.Proxy != null)
            {
                httpClientHandler.Proxy = connectionInfo.Proxy;
                httpClientHandler.UseProxy = true;
            }
            else
            {
                httpClientHandler.UseProxy = false;
            }

#if NETSTANDARD2_0_OR_GREATER
                var retryHandler = new CustomRetryHandler { InnerHandler = httpClientHandler };
                var auditHandler = new OtlpAuditHandler(_agentHealthReporter) { InnerHandler = retryHandler };
#else
            var auditHandler = new OtlpAuditHandler(_agentHealthReporter) { InnerHandler = httpClientHandler };
#endif
            var httpClient = new HttpClient(auditHandler);
            httpClient.Timeout = TimeSpan.FromMilliseconds(_configurationService.Configuration.OpenTelemetryMetricsExportTimeoutMs);
            httpClient.DefaultRequestHeaders.Add("User-Agent", $"NewRelic-DotNet-Agent/{AgentInstallConfiguration.AgentVersion ?? "Unknown"}");

            return httpClient;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to create HttpClient for OTLP exporter.");
            return new HttpClient();
        }
    }

    public override void Dispose()
    {
        _meterProvider?.Dispose();
        _httpClient?.Dispose();
        base.Dispose();
    }

    /// <summary>
    /// Compares connection info by value instead of reference to avoid unnecessary provider recreation.
    /// </summary>
    private static bool ConnectionInfoEquals(IConnectionInfo a, IConnectionInfo b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a == null || b == null) return false;
        return a.Host == b.Host && a.Port == b.Port && a.HttpProtocol == b.HttpProtocol;
    }
}

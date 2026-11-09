// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Diagnostics.Metrics;
using System.Net.Http;
using System.Threading;
using NewRelic.Agent.Core.OtelDistro;
using NewRelic.Agent.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.AutoInstrumentation.PluginApi;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;

namespace NewRelic.OtelDistro.Plugin;

/// <summary>
/// OpenTelemetry .NET auto-instrumentation plugin (OTEL_DOTNET_AUTO_PLUGINS) that owns a dedicated OTLP
/// <c>MeterProvider</c> exporting the application's Meters to New Relic. It never starts Core:
/// <see cref="Plugin"/> does, and this plugin does nothing if the distro agent is not running or OpenTelemetry
/// metrics are disabled. Its provider is independent of the upstream SDK's MeterProvider: the configuration
/// (endpoint, headers, resource, interval) comes from Core's <see cref="DistroMetricsSettings"/>, which
/// re-exposes the already-connected agent's settings and signals via <see cref="DistroMetricsSettings.Changed"/>
/// when the provider must be recreated. Kept out of <see cref="Plugin"/> on purpose: this type touches the OTel
/// SDK metrics/exporter types directly, so a type-load problem here must not stop Core from starting.
/// </summary>
public class MetricsPlugin : IPlugin
{
    private const string LogPrefix = "[NewRelic.OtelDistro.Plugin.MetricsPlugin]";

    private static int _loggedError;

    // Serialises build / rebuild / dispose so a Changed event (on the connect/server-config thread) and the
    // shutdown flush (on the shutdown thread) can never race over the current provider or its HttpClient.
    private readonly object _lock = new object();

    private DistroMetricsSettings _metrics;

    // The current provider and the HttpClient it was handed. The OTLP exporter does not own a
    // factory-supplied HttpClient, so the plugin keeps the client it created and disposes it together with the
    // provider on every rebuild and on the shutdown flush.
    private MeterProvider _currentProvider;
    private HttpClient _currentHttpClient;

    // Set once the agent shuts down (under _lock). Prevents a Changed event racing shutdown from rebuilding a
    // provider that nothing would ever flush or dispose.
    private bool _shutdown;

    public void Initializing()
    {
        // Upstream invokes this with no try/catch; an exception would surface in the auto-instrumentation host.
        // Everything the metrics provider needs (Core types, the OTel SDK/exporter types) is touched inside the
        // called method so a type-load failure happens inside this try rather than while JIT-compiling this one.
        try
        {
            Setup();
        }
        catch (Exception ex)
        {
            LogOnce(ex, nameof(Initializing));
        }
    }

    public void Initialized()
    {
    }

    private void Setup()
    {
        // Plugin.Initializing() (registered earlier in OTEL_DOTNET_AUTO_PLUGINS) has already started Core, so
        // DistroAgentHost.Current is set by the time this runs.
        var agent = DistroAgentHost.Current;
        if (agent == null)
        {
            return;
        }

        var metrics = agent.Metrics;
        if (metrics == null || !metrics.Enabled)
        {
            return;
        }

        _metrics = metrics;

        // Recreate the provider whenever the connect/resource settings materially change.
        metrics.Changed += OnMetricsChanged;

        // Tear down the provider when the agent shuts down. On a clean shutdown (flush == true) the provider is
        // disposed first so its periodic reader force-flushes one final export while the HttpClient is still
        // usable; on a non-clean shutdown or a bare dispose (flush == false) the HttpClient is disposed first so
        // that final flush's export fails fast instead of shipping after Core is gone.
        metrics.RegisterShutdown(OnShutdown);

        // Connect is async and can complete before this plugin's Initializing() runs, in which case no Changed
        // event is coming and the provider must be built now.
        if (metrics.IsConnected)
        {
            BuildOrRebuild();
        }
    }

    // Runs on whatever thread DistroMetricsSettings publishes Changed from (connect / server-config). Catch-all:
    // an exporter rebuild failure must never propagate back into Core's event dispatch.
    private void OnMetricsChanged()
    {
        try
        {
            BuildOrRebuild();
        }
        catch (Exception ex)
        {
            LogOnce(ex, nameof(OnMetricsChanged));
        }
    }

    private void BuildOrRebuild()
    {
        var metrics = _metrics;
        if (metrics == null)
        {
            return;
        }

        lock (_lock)
        {
            if (_shutdown)
            {
                // A Changed event raced shutdown: do not build a provider nothing will ever flush or dispose.
                return;
            }

            // Dispose the prior provider (flushes it) and then its HttpClient before replacing them.
            DisposeCurrentUnderLock();

            // The exporter calls HttpClientFactory once at build time; create the client here, keep it, and hand
            // that same instance back so the plugin (not the exporter) owns its lifetime. The SyncToAsyncHandler
            // is injected as the outermost handler so the exporter's synchronous HttpClient.Send re-enters the
            // agent's async chain (audit, retry, transport) instead of bypassing it.
            var httpClient = metrics.CreateHttpClient(new SyncToAsyncHandler());

            MeterProvider provider;
            try
            {
                provider = Sdk.CreateMeterProviderBuilder()
                    .ConfigureResource(r => r
                        .Clear()
                        .AddService(metrics.ServiceName)
                        .AddTelemetrySdk()
                        .AddAttributes(metrics.ResourceAttributes))
                    .AddMeter("*")
                    // Drop every instrument whose meter is filtered out by metrics include/exclude; otherwise
                    // default histograms to base-2 exponential bucket aggregation (NR OTLP ingest prefers it) and
                    // leave other instruments on their default aggregation.
                    .AddView(instrument =>
                        !metrics.ShouldEnableMeter(instrument.Meter.Name)
                            ? MetricStreamConfiguration.Drop
                            : (instrument.GetType().IsGenericType
                               && instrument.GetType().GetGenericTypeDefinition() == typeof(Histogram<>)
                                ? new Base2ExponentialBucketHistogramConfiguration()
                                : null))
                    .AddOtlpExporter((exporterOptions, metricReaderOptions) =>
                    {
                        exporterOptions.Endpoint = metrics.Endpoint;
                        exporterOptions.Protocol = OtlpExportProtocol.HttpProtobuf;
                        exporterOptions.Compression = OtlpExportCompression.GZip;
                        exporterOptions.Headers = metrics.Headers;
                        exporterOptions.HttpClientFactory = () => httpClient;

                        metricReaderOptions.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = metrics.ExportIntervalMs;
                        metricReaderOptions.PeriodicExportingMetricReaderOptions.ExportTimeoutMilliseconds = metrics.ExportTimeoutMs;
                        metricReaderOptions.TemporalityPreference = MetricReaderTemporalityPreference.Delta;
                    })
                    .Build();
            }
            catch
            {
                // Build never takes ownership of the HttpClient, so it would leak if the builder throws; dispose
                // it and let the catch-all callers (OnMetricsChanged / Initializing) log once.
                httpClient.Dispose();
                throw;
            }

            _currentProvider = provider;
            _currentHttpClient = httpClient;
        }
    }

    // Registered shutdown callback. flush == true (clean shutdown): dispose the provider first so its reader
    // force-flushes a final export while the HttpClient is still usable, then dispose the client. flush == false
    // (non-clean shutdown / bare dispose): dispose the HttpClient first so the provider's shutdown-flush export
    // fails fast rather than shipping with the license key after Core is torn down. Sets _shutdown under the lock
    // so a Changed event racing shutdown cannot rebuild a provider nothing will flush or dispose. The Debug line
    // on the flush path is the agent-log proof that the clean-shutdown flush actually reached this plugin.
    // Catch-all and lock-guarded.
    private void OnShutdown(bool flush)
    {
        try
        {
            lock (_lock)
            {
                _shutdown = true;

                if (flush)
                {
                    Log.Debug("OTel distro metrics plugin: shutdown flush invoked; disposing the MeterProvider for a final export.");
                    _currentProvider?.Dispose();
                    _currentHttpClient?.Dispose();
                }
                else
                {
                    Log.Debug("OTel distro metrics plugin: shutdown teardown invoked; disposing the MeterProvider without a final export.");
                    _currentHttpClient?.Dispose();
                    _currentProvider?.Dispose();
                }

                _currentProvider = null;
                _currentHttpClient = null;
            }
        }
        catch (Exception ex)
        {
            LogOnce(ex, nameof(OnShutdown));
        }
    }

    private void DisposeCurrentUnderLock()
    {
        // Dispose the provider first: that force-flushes and shuts down its periodic reader (the final export)
        // while its HttpClient is still usable, then the client is disposed.
        _currentProvider?.Dispose();
        _currentHttpClient?.Dispose();
        _currentProvider = null;
        _currentHttpClient = null;
    }

    private static void LogOnce(Exception ex, string member)
    {
        if (Interlocked.Exchange(ref _loggedError, 1) == 0)
        {
            Console.Error.WriteLine($"{LogPrefix} {member} failed; OTLP metrics export is not running: {ex}");
        }
    }
}

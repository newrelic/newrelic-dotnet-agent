// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Threading;
using OpenTelemetry.AutoInstrumentation.PluginApi;
using OpenTelemetry.AutoInstrumentation.PluginApi.Telemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace NewRelic.OtelDistro.Plugin;

/// <summary>
/// OpenTelemetry .NET auto-instrumentation plugin (OTEL_DOTNET_AUTO_PLUGINS) that keeps New Relic the sampling
/// decider once the upstream SDK is turned on (OTEL_DOTNET_AUTO_SETUP_SDK=true). Forces the TracerProvider's own
/// sampler to AlwaysOff so the SDK records every Activity regardless of its own decision; NR's <c>DistroActivitySink</c>
/// (via <c>ActivitySource</c>'s listener callback, which runs before the SDK's sampler) is still the one that decides
/// whether a span is sampled, matching agent-mode behavior (F64/F74). Kept out of <see cref="Plugin"/> on purpose:
/// this type touches OTel SDK trace types directly, so a type-load problem here must not stop Core from starting.
/// </summary>
public class SamplerPlugin : IPlugin, ITelemetryPlugin
{
    private const string LogPrefix = "[NewRelic.OtelDistro.Plugin.SamplerPlugin]";

    private static int _loggedError;

    public void Initializing()
    {
    }

    public void Initialized()
    {
    }

    public void TracerProviderInitialized(TracerProvider tracerProvider)
    {
    }

    public void MeterProviderInitialized(MeterProvider meterProvider)
    {
    }

    public TracerProviderBuilder BeforeConfigureTracerProvider(TracerProviderBuilder builder)
    {
        return builder;
    }

    public MeterProviderBuilder BeforeConfigureMeterProvider(MeterProviderBuilder builder)
    {
        return builder;
    }

    public TracerProviderBuilder AfterConfigureTracerProvider(TracerProviderBuilder builder)
    {
        try
        {
            // AlwaysOff here does not drop spans: ActivitySource already took the sampling decision from
            // DistroActivitySink.ShouldSample's listener callback (the most permissive route), which runs before
            // the SDK sampler is ever consulted. This only prevents the SDK sampler from overriding/duplicating
            // that decision, so NR stays the sole sampling decider.
            builder.SetSampler(new AlwaysOffSampler());
        }
        catch (Exception ex)
        {
            LogOnce(ex, nameof(AfterConfigureTracerProvider));
        }

        return builder;
    }

    public MeterProviderBuilder AfterConfigureMeterProvider(MeterProviderBuilder builder)
    {
        return builder;
    }

    public ResourceBuilder ConfigureResource(ResourceBuilder builder)
    {
        return builder;
    }

    private static void LogOnce(Exception ex, string member)
    {
        if (Interlocked.Exchange(ref _loggedError, 1) == 0)
        {
            Console.Error.WriteLine($"{LogPrefix} {member} failed: {ex}");
        }
    }
}

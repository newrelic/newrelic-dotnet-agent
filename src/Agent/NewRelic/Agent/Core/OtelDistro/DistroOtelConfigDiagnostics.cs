// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using NewRelic.Agent.Extensions.Logging;

namespace NewRelic.Agent.Core.OtelDistro;

/// <summary>
/// Reports OTel configuration that defeats New Relic's distro defaults, judged from a <see cref="DistroOtelConfigSnapshot"/>:
/// file-based (YAML) configuration active, OTel trace instrumentation that New Relic disabled but is enabled again, exporters
/// that are not <c>none</c>, or settings that could not be read. Each finding is logged and counted through
/// <see cref="DistroConfigMetrics"/> (queued until Core's sink is set). Never throws.
/// </summary>
public static class DistroOtelConfigDiagnostics
{
    public const string FileBasedConfigActiveMetricName = "Supportability/Dotnet/OtelDistro/Config/FileBasedConfigActive";
    public const string OtelInstrumentationReenabledMetricPrefix = "Supportability/Dotnet/OtelDistro/Config/OtelInstrumentationReenabled/";
    public const string DetectionUnavailableMetricName = "Supportability/Dotnet/OtelDistro/Config/DetectionUnavailable";

    private const string NoneExporter = "none";

    /// <summary>
    /// Judges <paramref name="snapshot"/> against <paramref name="disableIds"/>, the OTel trace instrumentation IDs New
    /// Relic turns off. A null snapshot reports detection as unavailable; a clean snapshot reports nothing.
    /// </summary>
    public static void Report(DistroOtelConfigSnapshot snapshot, IReadOnlyList<string> disableIds)
    {
        try
        {
            // Each finding records its metric before it logs, and each log call is guarded, so a throwing logger can
            // neither drop a metric nor stop the later checks.
            if (snapshot == null)
            {
                DistroConfigMetrics.Record(DetectionUnavailableMetricName);
                TryLog(() => Log.Warn("OTel distro: could not read OpenTelemetry's configuration (the OpenTelemetry version may differ from the one New Relic was built against); skipping the OTel configuration checks."));
                return;
            }

            if (snapshot.FileBasedConfigActive)
            {
                DistroConfigMetrics.Record(FileBasedConfigActiveMetricName);
                TryLog(() => Log.Error("OTel distro: OpenTelemetry file-based (YAML) configuration is active, so OpenTelemetry ignores its OTEL_* environment variables and New Relic cannot enforce its defaults: OTel exporters, OTel instrumentation IDs, OTel propagators and OTel log processors are controlled by the YAML. OpenTelemetry may export data on its own, and instrument libraries New Relic also instruments (duplicate or missing telemetry); log forwarding stops if the YAML has no log processors. Set OTEL_EXPERIMENTAL_FILE_BASED_CONFIGURATION_ENABLED=false, or configure New Relic through its own settings."));
            }

            // Part of the settings could not be read (OTel version drift): the checks above and below still run on what
            // was read, the unread part is reported as unavailable.
            if (snapshot.PartialDetectionFailure != null)
            {
                DistroConfigMetrics.Record(DetectionUnavailableMetricName);
                TryLog(() => Log.Warn("OTel distro: could not read part of OpenTelemetry's configuration ({0}); the OpenTelemetry version may differ from the one New Relic was built against. The OTel configuration checks that depend on it are skipped.", snapshot.PartialDetectionFailure));
            }

            ReportReenabledInstrumentation(snapshot.EnabledTraceInstrumentationIds, disableIds);
            ReportExporters(snapshot.Exporters);
        }
        catch (Exception)
        {
            // Diagnostics must never reach OTel start-up.
        }
    }

    private static void ReportReenabledInstrumentation(IReadOnlyCollection<string> enabledIds, IReadOnlyList<string> disableIds)
    {
        if (enabledIds == null || disableIds == null)
        {
            return;
        }

        var enabled = new HashSet<string>(enabledIds, StringComparer.OrdinalIgnoreCase);
        var reported = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var id in disableIds)
        {
            if (string.IsNullOrWhiteSpace(id) || !enabled.Contains(id) || !reported.Add(id))
            {
                continue;
            }

            DistroConfigMetrics.Record(OtelInstrumentationReenabledMetricPrefix + id);
            TryLog(() => Log.Warn("OTel distro: OTel trace instrumentation {0} is enabled although New Relic turns it off so New Relic instruments the same library; OTel may take precedence over New Relic there (duplicate or missing telemetry).", id));
        }
    }

    private static void ReportExporters(IReadOnlyDictionary<string, string> exporters)
    {
        if (exporters == null)
        {
            return;
        }

        foreach (var exporter in exporters)
        {
            var value = exporter.Value;
            if (string.IsNullOrWhiteSpace(value) || string.Equals(value.Trim(), NoneExporter, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            DistroConfigMetrics.Record(DistroOtelEnvironmentDefaults.ExporterNotNoneMetricPrefix + exporter.Key);
            TryLog(() => Log.Error("OTel distro: the OTel {0} exporter is '{1}', not 'none'; OpenTelemetry may send {0} data to its own backend alongside New Relic.", exporter.Key, value));
        }
    }

    private static void TryLog(Action log)
    {
        try
        {
            log();
        }
        catch (Exception)
        {
            // A failing logger must not stop the remaining checks.
        }
    }
}

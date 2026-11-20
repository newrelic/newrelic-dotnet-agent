// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;

namespace NewRelic.Agent.Core.OtelDistro;

/// <summary>
/// What the OTel auto-instrumentation settings say at <c>Initialized()</c>, as read by the plugin's reflection adapter and
/// judged by <see cref="DistroOtelConfigDiagnostics"/>. A null snapshot means the adapter could not read the settings.
/// </summary>
public sealed class DistroOtelConfigSnapshot
{
    /// <summary>True when OTel's file-based (YAML) configuration is active, so OTel ignores its OTEL_* environment variables.</summary>
    public bool FileBasedConfigActive { get; set; }

    /// <summary>IDs of the OTel trace instrumentations that are enabled (for example ADONET).</summary>
    public IReadOnlyCollection<string> EnabledTraceInstrumentationIds { get; set; }

    /// <summary>Signal name (Traces, Metrics, Logs) to configured exporter name (for example none, otlp).</summary>
    public IReadOnlyDictionary<string, string> Exporters { get; set; }

    /// <summary>
    /// What part of the settings the adapter could not read (the rest of the snapshot is still valid; an unread part is
    /// null or missing); null when everything was read.
    /// </summary>
    public string PartialDetectionFailure { get; set; }
}

// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Api;
using NewRelic.Agent.Configuration;

namespace NewRelic.Agent.Core.OtelDistro;

/// <summary>
/// A running NR Core started without the NR profiler, for the OpenTelemetry auto-instrumentation distro plugin.
/// </summary>
public interface IDistroAgent : IDisposable
{
    DistroActivitySink ActivitySink { get; }

    DistroLogSink LogSink { get; }

    DistroMetricsSettings Metrics { get; }

    IAgentApi AgentApi { get; }

    IConfiguration Configuration { get; }

    int LoadedWrapperCount { get; }

    string ExtensionsDirectory { get; }

    /// <summary>
    /// Creates the XML toggles for the plugin's compiled sites, reading and watching <see cref="ExtensionsDirectory"/> and
    /// its runtime subfolder (unless <c>DisableFileSystemWatcher</c> is set). The agent owns the instance and disposes it on
    /// <see cref="Shutdown"/> and <see cref="Dispose"/>. A second call returns the instance the first call created (its
    /// arguments are ignored); a call after shutdown or dispose, or one that fails (logged), returns null. Never throws.
    /// </summary>
    DistroXmlToggles CreateXmlToggles(DistroInstrumentationSites sites, DistroShippedXmlManifest manifest, DistroRegisterSites registerSites);

    /// <summary>
    /// Clean shutdown (publishes the pre-clean/clean shutdown events, then disposes). Idempotent.
    /// </summary>
    void Shutdown();

    /// <summary>
    /// Only releases resources (config subscription, event subscriptions, container) and stops the activity and log sinks.
    /// It is not a clean shutdown: no final harvest, no shutdown events, no shutdown log. Call <see cref="Shutdown"/> for that.
    /// </summary>
    new void Dispose();
}

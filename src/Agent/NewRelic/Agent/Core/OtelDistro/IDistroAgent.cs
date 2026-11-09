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
    /// Clean shutdown (publishes the pre-clean/clean shutdown events, then disposes). Idempotent.
    /// </summary>
    void Shutdown();

    /// <summary>
    /// Only releases resources (config subscription, event subscriptions, container) and stops the activity and log sinks.
    /// It is not a clean shutdown: no final harvest, no shutdown events, no shutdown log. Call <see cref="Shutdown"/> for that.
    /// </summary>
    new void Dispose();
}

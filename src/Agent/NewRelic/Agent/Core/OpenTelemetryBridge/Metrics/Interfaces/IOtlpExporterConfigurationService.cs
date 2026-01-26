// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Net.Http;
using NewRelic.Agent.Core.DataTransport;

namespace NewRelic.Agent.Core.OpenTelemetryBridge.Metrics.Interfaces;

/// <summary>
/// Manages the lifecycle of the OpenTelemetry MeterProvider and its OTLP exporter configuration.
/// </summary>
public interface IOtlpExporterConfigurationService : IDisposable
{
    /// <summary>
    /// Gets the current MeterProvider instance, creating or recreating it if necessary.
    /// </summary>
    object GetOrCreateMeterProvider();

    /// <summary>
    /// Gets or creates the MeterProvider with specific connection info and entity GUID.
    /// </summary>
    object GetOrCreateMeterProvider(IConnectionInfo connectionInfo, string entityGuid);

    /// <summary>
    /// Forces a recreation of the MeterProvider (e.g., when EntityGuid changes).
    /// </summary>
    void RecreateMeterProvider();

    /// <summary>
    /// Gets the HttpClient used for OTLP exports.
    /// </summary>
    HttpClient HttpClient { get; }
}
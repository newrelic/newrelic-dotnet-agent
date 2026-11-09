// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using NewRelic.Agent.Api;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.DataTransport;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.OpenTelemetryBridge.Metrics;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Extensions.Logging;

namespace NewRelic.Agent.Core.OtelDistro;

/// <summary>
/// The settings the OTel distro metrics plugin (its own OTLP <c>MeterProvider</c>) reads: endpoint, headers,
/// resource attributes, export interval/timeout, a meter filter delegate, and a <see cref="Changed"/> signal
/// telling the plugin when it must recreate its provider. Deliberately does not reference Core's bundled
/// (ILRepacked) OTel SDK or any agent-mode-only type (<see cref="MeterBridgeConfiguration"/>,
/// <see cref="OtlpExporterConfigurationService"/>): those derive <c>ConfigurationBasedService</c>, whose ctor
/// subscribes to agent-mode configuration events, an agent-mode side effect this distro-only class must not
/// trigger. Runs on whatever thread publishes the connect/server-config events: never throws.
/// </summary>
public sealed class DistroMetricsSettings
{
    // One immutable record of "what we're connected with right now". A connect/update builds a fresh instance and
    // swaps it in with a single reference assignment, so each individual property read (Endpoint,
    // ResourceAttributes, CreateHttpClient, ...) returns a value from one coherent snapshot, never a half-built
    // one. Readers are only eventually consistent across reads, though: the metrics plugin reads several of these
    // properties while handling a Changed event, and a concurrent update can swap the reference between two of
    // those reads, so the plugin can momentarily mix an old Endpoint with new ResourceAttributes. That is
    // reconciled by the Changed the swap also raises, which rebuilds the provider against the newer snapshot.
    private sealed class Snapshot
    {
        public IConnectionInfo ConnectInfo;
        public Uri Endpoint;
        public string ServiceName;
        public IReadOnlyList<KeyValuePair<string, object>> ResourceAttributes;
        public int ExportIntervalMs;
        public int ExportTimeoutMs;
        public string EntityGuid;
        public IReadOnlyDictionary<string, string> SourceResourceAttributes;
    }

    private readonly IAgent _agent;
    private readonly Action<AgentConnectedEvent> _onConnected;
    private readonly Action<ServerConfigurationUpdatedEvent> _onServerConfigurationUpdated;
    private readonly object _updateLock = new object();

    private volatile bool _disabled;
    private volatile Snapshot _snapshot;
    private Action<bool> _teardownCallback;
    private int _loggedChangedHandlerError;

    public event Action Changed;

    /// <summary>True once <see cref="Disable"/> has run.</summary>
    public bool IsDisabled => _disabled;

    public bool Enabled => !_disabled && _agent.Configuration.OpenTelemetryMetricsEnabled;

    public bool IsConnected => _snapshot != null;

    public Uri Endpoint => _snapshot?.Endpoint;

    public string ServiceName => _snapshot?.ServiceName;

    public IReadOnlyList<KeyValuePair<string, object>> ResourceAttributes => _snapshot?.ResourceAttributes;

    public int ExportIntervalMs => _snapshot?.ExportIntervalMs ?? 0;

    public int ExportTimeoutMs => _snapshot?.ExportTimeoutMs ?? 0;

    // Built fresh on every read from the current configuration -- never cached or logged -- so the license key
    // is never held anywhere but inside this one formatted string, and a later key rotation is picked up without
    // DistroMetricsSettings needing to track it as part of the Changed comparison.
    public string Headers => $"api-key={_agent.Configuration.AgentLicenseKey}";

    public DistroMetricsSettings(IAgent agent)
    {
        _agent = agent ?? throw new ArgumentNullException(nameof(agent));

        _onConnected = OnConnected;
        _onServerConfigurationUpdated = OnServerConfigurationUpdated;

        EventBus<AgentConnectedEvent>.Subscribe(_onConnected);
        EventBus<ServerConfigurationUpdatedEvent>.Subscribe(_onServerConfigurationUpdated);
    }

    public bool ShouldEnableMeter(string meterName) => MeterFilterHelpers.ShouldEnableInstrumentsInMeter(_agent.Configuration, meterName);

    /// <summary>
    /// Stops feeding the metrics plugin: unsubscribes from connect/server-config events so neither
    /// <see cref="Changed"/> nor a later connect/update can fire again. Called when the agent shuts down.
    /// Irreversible and idempotent. Does not block a later <see cref="Shutdown"/> -- shutdown still needs the
    /// teardown (and, when clean, the final export) to run after the sinks and this settings object are disabled.
    /// </summary>
    public void Disable()
    {
        if (_disabled)
        {
            return;
        }

        _disabled = true;

        EventBus<AgentConnectedEvent>.Unsubscribe(_onConnected);
        EventBus<ServerConfigurationUpdatedEvent>.Unsubscribe(_onServerConfigurationUpdated);
    }

    /// <summary>
    /// Registers the callback <see cref="Shutdown"/> runs to tear down the plugin's <c>MeterProvider</c>. Set by
    /// the metrics plugin once it has built its provider. The <c>flush</c> argument the manager passes is
    /// <see langword="true"/> for a clean shutdown (force one final export, then dispose) and
    /// <see langword="false"/> for a non-clean shutdown or a bare dispose (dispose without a final export, because
    /// Core is already being torn down and must not ship again).
    /// </summary>
    public void RegisterShutdown(Action<bool> teardown)
    {
        _teardownCallback = teardown;
    }

    /// <summary>
    /// Runs the registered teardown callback exactly once -- the callback is cleared as it is taken, so a second
    /// <see cref="Shutdown"/> call (or a racing concurrent one) is a no-op rather than a double teardown. No-op
    /// when no callback is registered (including before connect, since the plugin only registers one once it has
    /// a provider). <paramref name="flush"/> is forwarded to the callback: <see langword="true"/> asks for a final
    /// export before dispose, <see langword="false"/> for dispose only. Never throws: a failing exporter must not
    /// block agent shutdown.
    /// </summary>
    public void Shutdown(bool flush)
    {
        try
        {
            Interlocked.Exchange(ref _teardownCallback, null)?.Invoke(flush);
        }
        catch (Exception exception)
        {
            Log.Warn(exception, "OTel distro metrics settings: the registered shutdown callback failed.");
        }
    }

    /// <summary>
    /// Composes the delegating-handler chain <see cref="CreateHttpClient()"/> wraps in an <see cref="HttpClient"/>:
    /// <see cref="OtlpAuditHandler"/> (outermost) -> <see cref="CustomRetryHandler"/> -> a proxy-configured
    /// <see cref="HttpClientHandler"/> (innermost). Exposed publicly (beyond what the metrics plugin consumes) so
    /// unit tests can walk <see cref="DelegatingHandler.InnerHandler"/> directly instead of reflecting into
    /// <see cref="HttpClient"/>'s own private handler field, which is not a stable target across the net11.0 and
    /// net481 test frameworks this class ships for.
    /// </summary>
    public HttpMessageHandler BuildHttpMessageHandlerChain()
    {
        var connectInfo = _snapshot?.ConnectInfo;
        var httpClientHandler = new HttpClientHandler();

        if (connectInfo?.Proxy != null)
        {
            httpClientHandler.Proxy = connectInfo.Proxy;
            httpClientHandler.UseProxy = true;
        }
        else
        {
            httpClientHandler.UseProxy = false;
        }

        // 2s is fixed rather than derived from ExportTimeoutMs for the same reason agent mode's
        // OtlpExporterConfigurationService.CreateHttpClientWithProxyAndRetry fixes it: the handler is built once
        // per connect/update while the timeout can change at runtime.
        var retryHandler = new CustomRetryHandler(null, TimeSpan.FromSeconds(2)) { InnerHandler = httpClientHandler };
        return new OtlpAuditHandler(null) { InnerHandler = retryHandler };
    }

    public HttpClient CreateHttpClient() => CreateHttpClient(null);

    /// <summary>
    /// Builds the OTLP exporter's <see cref="HttpClient"/> (timeout = export timeout, NR User-Agent) over the
    /// audit -> retry -> transport chain. An optional <paramref name="outermostHandler"/> is wrapped around that
    /// chain as the new outermost handler: the metrics plugin passes a sync-to-async bridge there, because the
    /// OTLP exporter exports synchronously via <c>HttpClient.Send</c> on .NET (Core) -- a path that walks
    /// <c>InnerHandler.Send</c> and so skips every handler overriding only <c>SendAsync</c> (audit, retry). That
    /// bridge must be compiled for a runtime that exposes the synchronous <c>Send</c> override, which this
    /// netstandard2.0 assembly is not, so it lives in the net8.0 plugin and is injected here.
    /// </summary>
    public HttpClient CreateHttpClient(DelegatingHandler outermostHandler)
    {
        try
        {
            HttpMessageHandler handler = BuildHttpMessageHandlerChain();
            if (outermostHandler != null)
            {
                outermostHandler.InnerHandler = handler;
                handler = outermostHandler;
            }

            var httpClient = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromMilliseconds(ExportTimeoutMs)
            };
            httpClient.DefaultRequestHeaders.Add("User-Agent", $"NewRelic-DotNet-Agent/{AgentInstallConfiguration.AgentVersion ?? "Unknown"}");

            return httpClient;
        }
        catch (Exception exception)
        {
            Log.Error(exception, "OTel distro metrics settings failed to create an HttpClient for the OTLP exporter.");
            return new HttpClient();
        }
    }

    private void OnConnected(AgentConnectedEvent connectedEvent)
    {
        if (_disabled)
        {
            return;
        }

        try
        {
            var connectInfo = connectedEvent?.ConnectInfo;
            if (connectInfo == null)
            {
                return;
            }

            lock (_updateLock)
            {
                var configuration = _agent.Configuration;
                var entityGuid = configuration.EntityGuid;
                var resourceAttributes = CopyResourceAttributes(configuration.OtlpResourceAttributes);

                var previous = _snapshot;
                if (previous != null
                    && ConnectionInfoEquals(previous.ConnectInfo, connectInfo)
                    && previous.EntityGuid == entityGuid
                    && ResourceAttributesEqual(previous.SourceResourceAttributes, resourceAttributes))
                {
                    // A reconnect that resolved to the exact same endpoint, entity and resource attributes: the
                    // metrics plugin's provider does not need to be recreated.
                    return;
                }

                _snapshot = BuildSnapshot(connectInfo, configuration, entityGuid, resourceAttributes);
            }

            RaiseChanged();
        }
        catch (Exception exception)
        {
            Log.Warn(exception, "OTel distro metrics settings failed to process an agent-connected event.");
        }
    }

    private void OnServerConfigurationUpdated(ServerConfigurationUpdatedEvent updatedEvent)
    {
        if (_disabled)
        {
            return;
        }

        try
        {
            lock (_updateLock)
            {
                var previous = _snapshot;
                if (previous == null)
                {
                    // No connect yet: nothing to recreate the provider against.
                    return;
                }

                var configuration = _agent.Configuration;
                var entityGuid = configuration.EntityGuid;
                var resourceAttributes = CopyResourceAttributes(configuration.OtlpResourceAttributes);

                if (previous.EntityGuid == entityGuid && ResourceAttributesEqual(previous.SourceResourceAttributes, resourceAttributes))
                {
                    return;
                }

                _snapshot = BuildSnapshot(previous.ConnectInfo, configuration, entityGuid, resourceAttributes);
            }

            RaiseChanged();
        }
        catch (Exception exception)
        {
            Log.Warn(exception, "OTel distro metrics settings failed to process a server configuration update.");
        }
    }

    private static Snapshot BuildSnapshot(IConnectionInfo connectInfo, IConfiguration configuration, string entityGuid, IReadOnlyDictionary<string, string> resourceAttributes)
    {
        return new Snapshot
        {
            ConnectInfo = connectInfo,
            Endpoint = BuildEndpoint(connectInfo),
            ServiceName = configuration.ApplicationNames?.FirstOrDefault(),
            ExportIntervalMs = configuration.OpenTelemetryMetricsExportIntervalMs,
            ExportTimeoutMs = configuration.OpenTelemetryMetricsExportTimeoutMs,
            ResourceAttributes = BuildResourceAttributeList(entityGuid, resourceAttributes),
            EntityGuid = entityGuid,
            SourceResourceAttributes = resourceAttributes
        };
    }

    private void RaiseChanged()
    {
        try
        {
            Changed?.Invoke();
        }
        catch (Exception exception)
        {
            if (Interlocked.Exchange(ref _loggedChangedHandlerError, 1) == 0)
            {
                Log.Warn(exception, "OTel distro metrics settings: a Changed handler threw. Later failures will not be logged.");
            }
        }
    }

    // Same comparison rules as OtlpExporterConfigurationService.ConnectionInfoEquals / IsUnchanged /
    // ResourceAttributesEqual; re-implemented locally since those are private there. Both callers only ever pass
    // non-null connection info (a prior snapshot's and the freshly validated connect event's), so there is no
    // null branch -- the ReferenceEquals shortcut handles the same-instance reconnect.
    private static bool ConnectionInfoEquals(IConnectionInfo a, IConnectionInfo b)
    {
        if (ReferenceEquals(a, b))
        {
            return true;
        }

        return a.Host == b.Host && a.Port == b.Port && a.HttpProtocol == b.HttpProtocol;
    }

    // Null and empty are equivalent (no server attributes is indistinguishable from an empty map), matching
    // OtlpExporterConfigurationService.ResourceAttributesEqual's Count-based comparison.
    private static bool ResourceAttributesEqual(IReadOnlyDictionary<string, string> a, IReadOnlyDictionary<string, string> b)
    {
        var countA = a?.Count ?? 0;
        var countB = b?.Count ?? 0;

        if (countA != countB)
        {
            return false;
        }

        if (countA == 0)
        {
            return true;
        }

        foreach (var pair in a)
        {
            if (!b.TryGetValue(pair.Key, out var value) || value != pair.Value)
            {
                return false;
            }
        }

        return true;
    }

    // Copies the live configuration dictionary so a Snapshot never holds a reference the configuration system
    // could still be mutating, and so a later equality check compares against a frozen set of attributes.
    private static IReadOnlyDictionary<string, string> CopyResourceAttributes(IReadOnlyDictionary<string, string> source)
    {
        return source == null ? null : source.ToDictionary(pair => pair.Key, pair => pair.Value);
    }

    private static IReadOnlyList<KeyValuePair<string, object>> BuildResourceAttributeList(string entityGuid, IReadOnlyDictionary<string, string> resourceAttributes)
    {
        return OtlpExporterConfigurationService.BuildResourceAttributes(entityGuid, resourceAttributes).ToList();
    }

    private static Uri BuildEndpoint(IConnectionInfo connectionInfo)
    {
        return new UriBuilder(connectionInfo.HttpProtocol, connectionInfo.Host, connectionInfo.Port, "/v1/metrics").Uri;
    }
}

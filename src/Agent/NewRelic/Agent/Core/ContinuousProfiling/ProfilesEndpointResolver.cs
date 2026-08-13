// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Core.DataTransport;

namespace NewRelic.Agent.Core.ContinuousProfiling;

/// <summary>
/// Resolves the OTLP/HTTP endpoint that continuous-profiling data is posted to. The endpoint is
/// always the collector connection's resolved host+port -- the same <see cref="IConnectionInfo"/>
/// published on <c>AgentConnectedEvent</c> after preconnect -- with the profiles signal path
/// (<see cref="ProfilesPath"/>) appended. This mirrors <c>MeterBridgeConfiguration.BuildOtlpEndpoint</c>,
/// which the OTel Dimensional Metrics bridge already uses (unconditionally) for <c>/v1/metrics</c>.
///
/// There is no other resolution path: no dedicated OTLP host, no config surface. Nothing can be
/// resolved before the agent's first successful connect.
/// </summary>
public static class ProfilesEndpointResolver
{
    // OTLP profiles signal path. New Relic's OTLP ingest flattens every signal to /v1/<signal>
    // (/v1/traces, /v1/metrics, /v1/logs, /v1/profiles) -- confirmed against core-data-platform/otlp-ingest#135.
    // NB: "v1development" is only the OTel proto *package* version (alpha signal), NOT the URL path.
    private const string ProfilesPath = "/v1/profiles";

    /// <summary>
    /// Builds the profiles endpoint from the collector connection's resolved host+port -- the same
    /// <see cref="IConnectionInfo"/> published on <c>AgentConnectedEvent</c> after preconnect. Null if
    /// <paramref name="connectionInfo"/> or its host is unavailable.
    /// </summary>
    public static string ResolveFromConnectionInfo(IConnectionInfo connectionInfo)
    {
        if (connectionInfo == null || string.IsNullOrEmpty(connectionInfo.Host))
            return null;

        return $"{connectionInfo.HttpProtocol}://{connectionInfo.Host}:{connectionInfo.Port}{ProfilesPath}";
    }
}

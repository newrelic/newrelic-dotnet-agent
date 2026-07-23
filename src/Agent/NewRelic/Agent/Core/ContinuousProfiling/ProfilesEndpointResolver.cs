// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Text.RegularExpressions;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Extensions.Logging;

namespace NewRelic.Agent.Core.ContinuousProfiling;

/// <summary>
/// Resolves the OTLP/HTTP endpoint that continuous-profiling data WOULD be posted to. This is the
/// "ingest details TBD" seam for the prototype: kept minimal and derivable so no newrelic.config /
/// XSD surface is added. Resolution order:
///   1. A full or host-only override from the <see cref="EndpointOverrideEnvVar"/> environment variable.
///   2. Otherwise a region-aware New Relic OTLP host derived from the license key's region prefix,
///      mirroring the region logic in <c>ConnectionInfo.GetCollectorHost</c>.
/// The profiles signal path (<see cref="ProfilesPath"/>) is appended when the override is host-only.
/// </summary>
public static class ProfilesEndpointResolver
{
    /// <summary>Environment variable that overrides the resolved endpoint (full URL or host-only).</summary>
    public const string EndpointOverrideEnvVar = "NEW_RELIC_OTLP_ENDPOINT";

    // OTLP profiles signal path. New Relic's OTLP ingest flattens every signal to /v1/<signal>
    // (/v1/traces, /v1/metrics, /v1/logs, /v1/profiles) -- confirmed against core-data-platform/otlp-ingest#135.
    // NB: "v1development" is only the OTel proto *package* version (alpha signal), NOT the URL path.
    private const string ProfilesPath = "/v1/profiles";

    private const string UsOtlpHost = "otlp.nr-data.net";
    private const string RegionalOtlpHostTemplate = "otlp.{0}.nr-data.net";

    // Same shape as ConnectionInfo.accountRegionRegex: the region prefix of a license key is the
    // leading run up to and including the first 'x' (e.g. "eu01x..." -> region "eu01").
    private static readonly Regex AccountRegionRegex = new Regex("^.+?x");

    /// <summary>
    /// Resolves the profiles endpoint. <paramref name="environmentReader"/> is injected for testability;
    /// production callers pass a reader over the process environment.
    /// </summary>
    public static string Resolve(IConfiguration configuration, Func<string, string> environmentReader)
    {
        var overrideValue = ReadOverride(environmentReader);
        if (!string.IsNullOrWhiteSpace(overrideValue))
        {
            return NormalizeOverride(overrideValue.Trim());
        }

        return $"https://{ResolveHost(configuration)}{ProfilesPath}";
    }

    private static string ReadOverride(Func<string, string> environmentReader)
    {
        if (environmentReader == null)
            return null;

        try
        {
            return environmentReader(EndpointOverrideEnvVar);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "[ContinuousProfiling] Failed to read {0}; falling back to the derived endpoint.", EndpointOverrideEnvVar);
            return null;
        }
    }

    private static string NormalizeOverride(string value)
    {
        // A host-only override (no explicit path) gets the profiles signal path appended; a full URL
        // that already names a path is honored verbatim.
        if (Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
            (string.IsNullOrEmpty(uri.AbsolutePath) || uri.AbsolutePath == "/"))
        {
            return $"{uri.GetLeftPart(UriPartial.Authority)}{ProfilesPath}";
        }

        return value;
    }

    private static string ResolveHost(IConfiguration configuration)
    {
        var licenseKey = configuration?.AgentLicenseKey;
        if (!string.IsNullOrEmpty(licenseKey))
        {
            var match = AccountRegionRegex.Match(licenseKey);
            if (match.Success)
            {
                var region = match.Value.TrimEnd('x');
                if (!string.IsNullOrEmpty(region))
                {
                    return string.Format(RegionalOtlpHostTemplate, region);
                }
            }
        }

        return UsOtlpHost;
    }
}

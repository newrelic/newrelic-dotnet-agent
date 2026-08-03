// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using NuGet.Versioning;

namespace CompatibilityDocs.Derivation;

public class VersionResolver
{
    // Accumulates the highest NuGetVersion seen per (packageId, platform). The minimum is
    // now curated in the YAML, so only the latest tested version is derived.
    public virtual IReadOnlyDictionary<(string PackageId, Platform Platform), string> BuildIndex(
        IEnumerable<PackageRef> refs)
    {
        var accs = new Dictionary<(string, Platform), (NuGetVersion Max, string Raw)>();

        foreach (var r in refs)
        {
            if (!NuGetVersion.TryParse(r.Version, out var parsed))
                continue;

            foreach (var platform in PlatformsFor(r.Tfm))
            {
                var key = (r.PackageId.ToLowerInvariant(), platform);
                if (!accs.TryGetValue(key, out var acc) || parsed > acc.Max)
                    accs[key] = (parsed, r.Version);
            }
        }

        var result = new Dictionary<(string, Platform), string>();
        foreach (var (key, acc) in accs)
            result[key] = acc.Raw;
        return result;
    }

    private static IEnumerable<Platform> PlatformsFor(string? tfm)
    {
        if (tfm == null)
        {
            yield return Platform.Core;
            yield return Platform.Framework;
            yield break;
        }
        var p = PlatformParser.TfmToPlatform(tfm);
        if (p.HasValue)
            yield return p.Value;
    }
}

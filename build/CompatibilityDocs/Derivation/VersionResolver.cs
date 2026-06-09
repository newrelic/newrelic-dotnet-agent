using System.Collections.Generic;
using NuGet.Versioning;

namespace CompatibilityDocs.Derivation;

public class VersionResolver
{
    // Accumulates lowest/highest NuGetVersion seen per (packageId, platform).
    private sealed class Acc
    {
        public string PackageId = "";
        public NuGetVersion? MinVer;
        public NuGetVersion? MaxVer;
        public string MinRaw = "";
        public string MaxRaw = "";
    }

    public virtual IReadOnlyDictionary<(string PackageId, Platform Platform), VersionRange> BuildIndex(
        IEnumerable<PackageRef> refs)
    {
        var accs = new Dictionary<(string, Platform), Acc>();

        foreach (var r in refs)
        {
            if (!NuGetVersion.TryParse(r.Version, out var parsed))
                continue;

            foreach (var platform in PlatformsFor(r.Tfm))
            {
                var key = (r.PackageId.ToLowerInvariant(), platform);
                if (!accs.TryGetValue(key, out var acc))
                {
                    acc = new Acc { PackageId = r.PackageId };
                    accs[key] = acc;
                }

                if (acc.MinVer == null || parsed < acc.MinVer)
                {
                    acc.MinVer = parsed;
                    acc.MinRaw = r.Version;
                }
                if (acc.MaxVer == null || parsed > acc.MaxVer)
                {
                    acc.MaxVer = parsed;
                    acc.MaxRaw = r.Version;
                }
            }
        }

        var result = new Dictionary<(string, Platform), VersionRange>();
        foreach (var ((_, platform), acc) in accs)
            result[(acc.PackageId.ToLowerInvariant(), platform)] = new VersionRange(acc.MinRaw, acc.MaxRaw);
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

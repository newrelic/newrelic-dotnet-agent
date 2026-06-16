using System.Collections.Generic;
using System.Linq;

namespace CompatibilityDocs.Schema;

// A curated version value that is either a single scalar (applies to every tab the
// package declares) or a per-tab map keyed by "core"/"framework". Used for both the
// curated minimum and the curated (manual) latest version.
public sealed class VersionSpec
{
    private readonly string? _single;
    private readonly IReadOnlyDictionary<string, string>? _byTab;

    private VersionSpec(string? single, IReadOnlyDictionary<string, string>? byTab)
    {
        _single = single;
        _byTab = byTab;
    }

    public static VersionSpec Single(string value) => new(value, null);

    public static VersionSpec Map(IReadOnlyDictionary<string, string> byTab) => new(null, byTab);

    public bool IsMap => _byTab != null;

    public IEnumerable<string> Tabs => _byTab?.Keys ?? Enumerable.Empty<string>();

    // Returns the version for the given tab: the scalar for the single form, or the tab's
    // entry (null when absent) for the map form.
    public string? For(string tab) =>
        _byTab != null
            ? (_byTab.TryGetValue(tab, out var v) ? v : null)
            : _single;
}

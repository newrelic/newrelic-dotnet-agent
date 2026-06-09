using System.Collections.Generic;
using System.Linq;
using System.Text;
using CompatibilityDocs.Derivation;
using CompatibilityDocs.Schema;

namespace CompatibilityDocs.Rendering;

public class MarkdownRenderer
{
    private const string Banner =
        "<!-- GENERATED FILE — do not edit by hand.\n" +
        "     Source: build/CompatibilityDocs/compatibility.yaml\n" +
        "     Regenerate: dotnet run --project build/CompatibilityDocs -->";

    private static readonly (Platform Platform, string Heading)[] Sections =
    {
        (Platform.Core, "## .NET Core"),
        (Platform.Framework, "## .NET Framework"),
    };

    private readonly NoteRenderer _notes;

    public MarkdownRenderer(NoteRenderer notes) => _notes = notes;

    public virtual string Render(CompatibilityModel model, IReadOnlyDictionary<(string, Platform), VersionRange> versions)
    {
        var sb = new StringBuilder();
        sb.Append(Banner).Append("\n\n");
        sb.Append("# .NET agent automatic instrumentation compatibility").Append('\n');

        foreach (var (platform, heading) in Sections)
        {
            sb.Append('\n').Append(heading).Append('\n');
            foreach (var cat in model.Categories.Where(c => c.Tabs.Contains(PlatformTab(platform))))
                RenderCategory(sb, cat, platform, versions);
        }
        return sb.ToString();
    }

    private void RenderCategory(StringBuilder sb, Category cat, Platform platform,
        IReadOnlyDictionary<(string, Platform), VersionRange> versions)
    {
        var libs = cat.Libraries.Where(l => EffectiveTabs(cat, l).Contains(PlatformTab(platform))).ToList();
        if (libs.Count == 0) return;

        sb.Append("\n### ").Append(cat.Title).Append('\n');
        if (!string.IsNullOrEmpty(cat.Intro))
            sb.Append('\n').Append(cat.Intro).Append('\n');

        var curated = libs.Where(l => l.SupportedVersions is { Count: > 0 }).ToList();
        var tableLibs = libs
            .Where(l => l.SupportedVersions is not { Count: > 0 } && (l.Packages.Count > 0 || l.Methods.Count > 0))
            .ToList();

        if (curated.Count > 0)
        {
            sb.Append('\n');
            foreach (var lib in curated)
            {
                var agent = string.IsNullOrEmpty(lib.MinAgentVersion) ? "" : $" (min agent v{lib.MinAgentVersion})";
                sb.Append("- ").Append(lib.Name).Append(": ")
                  .Append(string.Join(", ", lib.SupportedVersions!)).Append(agent).Append('\n');
            }
        }

        if (tableLibs.Count > 0)
        {
            sb.Append('\n');
            sb.Append("| Library | NuGet package | Minimum version | Latest verified | Min agent version | Notes |\n");
            sb.Append("| --- | --- | --- | --- | --- | --- |\n");
            foreach (var lib in tableLibs)
                RenderLibraryRows(sb, lib, platform, versions);
        }

        foreach (var fn in cat.Footnotes)
            sb.Append('\n').Append(fn).Append('\n');
    }

    private void RenderLibraryRows(StringBuilder sb, Library lib, Platform platform,
        IReadOnlyDictionary<(string, Platform), VersionRange> versions)
    {
        // Instrumented methods are a library-level property; fold them into the Notes
        // cell of the library's first row, each method on its own line via <br>.
        var methodsList = lib.Methods.Count > 0
            ? "Instruments:<br>" + string.Join("<br>", lib.Methods.Select(m => $"`{m}`"))
            : "";

        var packages = lib.Packages.Where(p => p.Tabs.Contains(PlatformTab(platform))).ToList();
        if (packages.Count > 0)
        {
            for (var i = 0; i < packages.Count; i++)
                RenderRow(sb, lib, packages[i], platform, versions, i == 0 ? methodsList : "");
        }
        else if (lib.Methods.Count > 0)
        {
            // Method-only library (no NuGet package), or one whose packages don't apply
            // to this platform: a single row with dashes for the version columns.
            var agent = string.IsNullOrEmpty(lib.MinAgentVersion) ? "—" : lib.MinAgentVersion;
            AppendRow(sb, lib.Name, "—", "—", "—", agent, Combine(RenderNotes(lib.Notes), methodsList));
        }
    }

    private void RenderRow(StringBuilder sb, Library lib, Package pkg, Platform platform,
        IReadOnlyDictionary<(string, Platform), VersionRange> versions, string methodsList)
    {
        string min = "—", latest = "—";
        if (pkg.VersionSource == "manual")
        {
            min = pkg.MinVersion ?? "—";
            latest = pkg.LatestVersion ?? "—";
        }
        else if (versions.TryGetValue((pkg.Id.ToLowerInvariant(), platform), out var range))
        {
            min = range.Min ?? "—";
            latest = range.Latest ?? "—";
        }

        var pkgCell = string.IsNullOrEmpty(pkg.NugetUrl) ? pkg.Id : $"[{pkg.Id}]({pkg.NugetUrl})";
        var agent = string.IsNullOrEmpty(lib.MinAgentVersion) ? "—" : lib.MinAgentVersion;

        AppendRow(sb, lib.Name, pkgCell, min, latest, agent,
            Combine(RenderNotes(pkg.Notes.Concat(lib.Notes)), methodsList));
    }

    private string RenderNotes(IEnumerable<Note> notes)
    {
        // A note's text may come from a YAML block scalar (> or |), which can carry
        // embedded or trailing newlines. A markdown table cell must be a single line,
        // so trim each part and strip any residual newlines. Separate notes are joined
        // with <br> so each renders on its own line rather than running together.
        var parts = notes.Select(n => _notes.Render(n).Trim());
        return string.Join("<br>", parts)
            .Replace("\r", " ").Replace("\n", " ").Replace("|", "\\|");
    }

    private static string Combine(string notes, string methods)
    {
        if (string.IsNullOrEmpty(methods)) return notes;
        if (string.IsNullOrEmpty(notes)) return methods;
        return notes + "<br>" + methods;
    }

    private static void AppendRow(StringBuilder sb, string library, string pkgCell,
        string min, string latest, string agent, string notesCell)
    {
        sb.Append("| ").Append(library)
          .Append(" | ").Append(pkgCell)
          .Append(" | ").Append(min)
          .Append(" | ").Append(latest)
          .Append(" | ").Append(agent)
          .Append(" | ").Append(notesCell)
          .Append(" |\n");
    }

    private static IEnumerable<string> EffectiveTabs(Category cat, Library lib)
        => lib.Tabs is { Count: > 0 } ? lib.Tabs : cat.Tabs;

    private static string PlatformTab(Platform p) => p == Platform.Core ? "core" : "framework";
}

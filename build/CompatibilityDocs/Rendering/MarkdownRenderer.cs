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

        sb.Append('\n').Append(RenderToc(model));

        foreach (var (platform, heading) in Sections)
        {
            sb.Append('\n').Append(heading).Append('\n');
            foreach (var cat in model.Categories.Where(c => c.Tabs.Contains(PlatformTab(platform))))
                RenderCategory(sb, cat, platform, versions);
        }
        return sb.ToString();
    }

    // A two-level table of contents: each platform, then the categories that actually
    // render under it. Anchors are computed with GitHub's heading-slug rules, including
    // the "-1" disambiguation suffix that category titles get when they appear under both
    // platforms — so the counter must walk headings in the same order the body emits them
    // (platform, then its categories) for Core before Framework.
    private string RenderToc(CompatibilityModel model)
    {
        var seen = new Dictionary<string, int>();
        var sb = new StringBuilder();
        sb.Append("## Contents\n");
        foreach (var (platform, heading) in Sections)
        {
            var platformTitle = heading[3..]; // strip leading "## "
            var platformSlug = Slug(seen, platformTitle);
            var catLinks = model.Categories
                .Where(c => CategoryRenders(c, platform))
                .Select(c => $"[{c.Title}](#{Slug(seen, c.Title)})")
                .ToList();
            sb.Append("- [").Append(platformTitle).Append("](#").Append(platformSlug).Append(')');
            if (catLinks.Count > 0)
                sb.Append(" — ").Append(string.Join(" · ", catLinks));
            sb.Append('\n');
        }
        return sb.ToString();
    }

    private static bool CategoryRenders(Category cat, Platform platform)
    {
        var tab = PlatformTab(platform);
        return cat.Tabs.Contains(tab) && cat.Libraries.Any(l => EffectiveTabs(cat, l).Contains(tab));
    }

    // GitHub anchor slug: lowercase, drop characters other than letters/digits/space/-/_,
    // spaces to hyphens; repeated slugs get -1, -2, … in order of appearance.
    private static string Slug(Dictionary<string, int> seen, string text)
    {
        var b = new StringBuilder();
        foreach (var ch in text.ToLowerInvariant())
            if (char.IsLetterOrDigit(ch) || ch == '-' || ch == '_')
                b.Append(ch);
            else if (ch == ' ')
                b.Append('-');
        var baseSlug = b.ToString();
        if (seen.TryGetValue(baseSlug, out var n))
        {
            seen[baseSlug] = n + 1;
            return $"{baseSlug}-{n}";
        }
        seen[baseSlug] = 1;
        return baseSlug;
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
            sb.Append("| Library | NuGet package | Versions tested | Min agent version | Notes |\n");
            sb.Append("| --- | --- | --- | --- | --- |\n");
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
        // cell of the library's first row inside a collapsed <details> block so the long
        // method names don't force the table wide. Customers expand to see the list.
        var methodsCell = lib.Methods.Count > 0
            ? $"<details><summary>Instrumented methods ({lib.Methods.Count})</summary><ul>"
              + string.Concat(lib.Methods.Select(m => $"<li><code>{m}</code></li>"))
              + "</ul></details>"
            : "";

        var packages = lib.Packages.Where(p => p.Tabs.Contains(PlatformTab(platform))).ToList();
        if (packages.Count > 0)
        {
            for (var i = 0; i < packages.Count; i++)
                RenderRow(sb, lib, packages[i], platform, versions, i == 0 ? methodsCell : "");
        }
        else if (lib.Methods.Count > 0)
        {
            // Method-only library (no NuGet package), or one whose packages don't apply
            // to this platform: a single row with a dash for the versions column.
            var agent = string.IsNullOrEmpty(lib.MinAgentVersion) ? "—" : lib.MinAgentVersion;
            AppendRow(sb, lib.Name, "—", "—", agent, RenderNotesCell(lib.Notes, methodsCell));
        }
    }

    private void RenderRow(StringBuilder sb, Library lib, Package pkg, Platform platform,
        IReadOnlyDictionary<(string, Platform), VersionRange> versions, string methodsCell)
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

        AppendRow(sb, lib.Name, pkgCell, VersionCell(min, latest), agent,
            RenderNotesCell(pkg.Notes.Concat(lib.Notes), methodsCell));
    }

    // Collapses the separate min/latest values into a single "Versions tested" cell:
    // a "min – latest" range, a single value when they match, or "—" when neither is known.
    private static string VersionCell(string min, string latest)
    {
        if (min == "—" && latest == "—") return "—";
        if (min == latest) return min;
        return $"{min} – {latest}";
    }

    // The Notes cell is an HTML bullet list of the notes (HTML lists, unlike markdown "- "
    // syntax, render inside a table cell), followed by the collapsible instrumented-methods
    // block — which sits after the list, not as a bullet itself. Returns "" when empty.
    private string RenderNotesCell(IEnumerable<Note> notes, string methodsCell)
    {
        var items = notes
            .Select(n => Sanitize(_notes.Render(n).Trim()))
            .Where(s => s.Length > 0)
            .ToList();
        var list = items.Count > 0
            ? "<ul>" + string.Concat(items.Select(i => $"<li>{i}</li>")) + "</ul>"
            : "";
        return list + methodsCell;
    }

    // A note's text may come from a YAML block scalar (> or |), which can carry embedded
    // or trailing newlines. A markdown table cell must stay on one line, so strip residual
    // newlines and escape pipes.
    private static string Sanitize(string s) =>
        s.Replace("\r", " ").Replace("\n", " ").Replace("|", "\\|");

    private static void AppendRow(StringBuilder sb, string library, string pkgCell,
        string versions, string agent, string notesCell)
    {
        sb.Append("| ").Append(library)
          .Append(" | ").Append(pkgCell)
          .Append(" | ").Append(versions)
          .Append(" | ").Append(agent)
          .Append(" | ").Append(notesCell)
          .Append(" |\n");
    }

    private static IEnumerable<string> EffectiveTabs(Category cat, Library lib)
        => lib.Tabs is { Count: > 0 } ? lib.Tabs : cat.Tabs;

    private static string PlatformTab(Platform p) => p == Platform.Core ? "core" : "framework";
}

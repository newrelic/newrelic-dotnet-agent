using System.Collections.Generic;

namespace CompatibilityDocs.Schema;

public class Package
{
    public string Id { get; set; } = "";
    public string? NugetUrl { get; set; }
    public List<string> Tabs { get; set; } = new();
    public string VersionSource { get; set; } = "derived";
    public VersionSpec? MinVersion { get; set; }
    public VersionSpec? LatestVersion { get; set; }
    // Optional per-package minimum agent version. When present it overrides the library's
    // MinAgentVersion for this package's rows — used when one package in a family requires a
    // newer agent than its siblings (e.g. a NuGet driver vs. a built-in assembly).
    public VersionSpec? MinAgentVersion { get; set; }
    public List<Note> Notes { get; set; } = new();
}

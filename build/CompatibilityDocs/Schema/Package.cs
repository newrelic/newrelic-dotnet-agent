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
    public List<Note> Notes { get; set; } = new();
}

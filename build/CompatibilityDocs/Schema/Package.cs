using System.Collections.Generic;

namespace CompatibilityDocs.Schema;

public class Package
{
    public string Id { get; set; } = "";
    public string? NugetUrl { get; set; }
    public List<string> Tabs { get; set; } = new();
    public string VersionSource { get; set; } = "derived";
    public string? MinVersion { get; set; }
    public string? LatestVersion { get; set; }
    public List<Note> Notes { get; set; } = new();
}

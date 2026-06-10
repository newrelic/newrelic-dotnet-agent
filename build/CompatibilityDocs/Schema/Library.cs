using System.Collections.Generic;

namespace CompatibilityDocs.Schema;

public class Library
{
    public string Name { get; set; } = "";
    public List<string>? Tabs { get; set; }            // null => inherit category tabs
    public List<Package> Packages { get; set; } = new();
    public List<string>? SupportedVersions { get; set; } // app-framework entries
    public VersionSpec? MinAgentVersion { get; set; }
    public List<string> Methods { get; set; } = new();
    public List<Note> Notes { get; set; } = new();
}

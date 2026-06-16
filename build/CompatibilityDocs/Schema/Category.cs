using System.Collections.Generic;

namespace CompatibilityDocs.Schema;

public class Category
{
    public string Key { get; set; } = "";
    public string Title { get; set; } = "";
    public List<string> Tabs { get; set; } = new();
    public string? Intro { get; set; }
    public List<string> Footnotes { get; set; } = new();
    public List<Library> Libraries { get; set; } = new();
}

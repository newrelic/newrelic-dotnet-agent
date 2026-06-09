using System.Collections.Generic;

namespace CompatibilityDocs.Schema;

public class Note
{
    public string Type { get; set; } = "";
    public string? SinceVersion { get; set; }
    public string? AgentVersion { get; set; }
    public string? Version { get; set; }
    public string? AboveVersion { get; set; }
    public string? Text { get; set; }
    public List<string>? Versions { get; set; }
}

using System.Text.Json.Serialization;

namespace Dotty
{
    public class ProjectInfo
    {
        [JsonPropertyName("projectFile")]
        public string ProjectFile { get; set; }
    }
}

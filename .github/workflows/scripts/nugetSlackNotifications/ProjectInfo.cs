using System.Text.Json.Serialization;

namespace nugetSlackNotifications
{
    public class ProjectInfo
    {
        [JsonPropertyName("projectFile")]
        public string ProjectFile { get; set; }
    }
}

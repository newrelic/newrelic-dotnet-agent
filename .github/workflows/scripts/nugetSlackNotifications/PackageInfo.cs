using System.Text.Json.Serialization;

namespace nugetSlackNotifications
{
    public class PackageInfo
    {
        [JsonPropertyName("packageName")]
        public string PackageName { get; set; }
        [JsonPropertyName("ignorePatch")]
        public bool IgnorePatch { get; set; }
        [JsonPropertyName("ignoreMinor")]
        public bool IgnoreMinor { get; set; }
        [JsonPropertyName("ignoreMajor")]
        public bool IgnoreMajor { get; set; }
        [JsonPropertyName("ignoreReason")]
        public string IgnoreReason {get; set;}
    }
}

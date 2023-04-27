using Newtonsoft.Json;
using System.IO;

namespace ArtifactBuilder
{
    public class AgentInfo
    {

        public const string AgentInfoFilename = "agentinfo.json";

        [JsonProperty(PropertyName = "install_type", NullValueHandling = NullValueHandling.Ignore)]
        public string InstallType { get; set; }

        [JsonProperty(PropertyName = "azure_site_extension", NullValueHandling = NullValueHandling.Ignore)]
        public bool AzureSiteExtension { get; set; }

        public void WriteToDisk(string filePath)
        {
            using (var file = File.CreateText(Path.Join(filePath, AgentInfoFilename)))
            {
                var serializer = new JsonSerializer();
                serializer.Serialize(file, this);
            }
        }
    }
}

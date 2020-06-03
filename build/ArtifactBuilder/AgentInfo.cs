using Newtonsoft.Json;
using System.IO;

namespace ArtifactBuilder
{
    public class AgentInfo
    {
        [JsonProperty(PropertyName = "install_type", NullValueHandling = NullValueHandling.Ignore)]
        public string InstallType { get; set; }

        [JsonProperty(PropertyName = "azure_site_extension", NullValueHandling = NullValueHandling.Ignore)]
        public bool AzureSiteExtension { get; set; }

        public void WriteToDisk(string filePath)
        {
            using (var file = File.CreateText($@"{filePath}\agentinfo.json"))
            {
                var serializer = new JsonSerializer();
                serializer.Serialize(file, this);
            }
        }
    }
}

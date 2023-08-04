using Newtonsoft.Json;

namespace NugetVersionDeprecator;

internal class AgentRelease
{
    [JsonProperty("eolDate")]
    public DateTime EolDate { get; set; }
    [JsonProperty("version")]
    public string Version { get; set; }
}

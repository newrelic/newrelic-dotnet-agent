using Newtonsoft.Json;

namespace NewRelic.Agent.Core.Utilization
{
    public interface IVendorModel
    {
        [JsonIgnore]
        string VendorName { get; }

        [JsonProperty("id")]
        string Id { get; }

    }
}

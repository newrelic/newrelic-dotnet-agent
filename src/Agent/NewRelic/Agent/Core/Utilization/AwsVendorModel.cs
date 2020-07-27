using Newtonsoft.Json;

namespace NewRelic.Agent.Core.Utilization
{
    public class AwsVendorModel : IVendorModel
    {
        private readonly string _id;
        private readonly string _type;
        private readonly string _zone;

        public AwsVendorModel(string id, string type, string zone)
        {
            _id = id;
            _type = type;
            _zone = zone;
        }

        public string VendorName { get { return "aws"; } }

        [JsonProperty("id")]
        public string Id { get { return _id; } }

        [JsonProperty("type")]
        public string Type { get { return _type; } }

        [JsonProperty("zone")]
        public string Zone { get { return _zone; } }
    }
}

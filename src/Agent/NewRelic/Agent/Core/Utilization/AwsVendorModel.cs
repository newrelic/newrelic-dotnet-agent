using System;
using Newtonsoft.Json;

namespace NewRelic.Agent.Core.Utilization
{
    public class AwsVendorModel : IVendorModel
    {
        private readonly String _id;
        private readonly String _type;
        private readonly String _zone;

        public AwsVendorModel(String id, String type, String zone)
        {
            _id = id;
            _type = type;
            _zone = zone;
        }

        public String VendorName { get { return "aws"; } }

        [JsonProperty("id")]
        public String Id { get { return _id; } }

        [JsonProperty("type")]
        public String Type { get { return _type; } }

        [JsonProperty("zone")]
        public String Zone { get { return _zone; } }
    }
}

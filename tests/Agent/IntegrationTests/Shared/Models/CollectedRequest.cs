using System;
using System.Collections.Generic;

namespace NewRelic.Agent.IntegrationTests.Shared.Models
{
    public class CollectedRequest
    {
        public string Method { get; set; }
        public IEnumerable<KeyValuePair<String, String>> Querystring { get; set; }
        public byte[] RequestBody { get; set; }
        public ICollection<String> ContentEncoding { get; set; }
    }
}

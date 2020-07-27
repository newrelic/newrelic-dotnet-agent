using System;
using System.Collections.Generic;

namespace NewRelic.Agent.IntegrationTests.Shared.Models
{
    public class CollectedRequest
    {
        public string Method { get; set; }
        public IEnumerable<KeyValuePair<string, string>> Querystring { get; set; }
        public byte[] RequestBody { get; set; }
        public ICollection<string> ContentEncoding { get; set; }
    }
}

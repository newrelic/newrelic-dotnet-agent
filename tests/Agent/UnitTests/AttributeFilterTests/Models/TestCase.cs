using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace AttributeFilterTests.Models
{
    public class TestCase
    {
        [JsonProperty(PropertyName = "testname")]
        [NotNull]
        public String TestName;

        [JsonProperty(PropertyName = "config")]
        [NotNull]
        public Configuration Configuration;

        [JsonProperty(PropertyName = "input_key")]
        [NotNull]
        public String AttributeKey;

        [JsonProperty(PropertyName = "input_default_destinations")]
        [NotNull]
        public IEnumerable<Destinations> AttributeDestinations;

        [JsonProperty(PropertyName = "expected_destinations")]
        [NotNull]
        public IEnumerable<Destinations> ExpectedDestinations;

        public override string ToString()
        {
            return TestName;
        }
    }
}

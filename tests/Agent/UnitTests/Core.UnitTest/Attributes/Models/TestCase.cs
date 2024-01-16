// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using Newtonsoft.Json;

namespace NewRelic.Agent.Core.Attributes.Tests.Models
{
    public class TestCase
    {
        [JsonProperty(PropertyName = "testname")]
        public string TestName;

        [JsonProperty(PropertyName = "config")]
        public Configuration Configuration;

        [JsonProperty(PropertyName = "input_key")]
        public string AttributeKey;

        [JsonProperty(PropertyName = "input_default_destinations")]
        public IEnumerable<Destinations> AttributeDestinations;

        [JsonProperty(PropertyName = "expected_destinations")]
        public IEnumerable<Destinations> ExpectedDestinations;

        public override string ToString()
        {
            return TestName;
        }
    }
}

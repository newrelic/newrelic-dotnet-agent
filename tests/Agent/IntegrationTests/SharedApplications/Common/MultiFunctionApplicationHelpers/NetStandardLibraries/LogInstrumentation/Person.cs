// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using Newtonsoft.Json;

namespace MultiFunctionApplicationHelpers.NetStandardLibraries.LogInstrumentation
{
    public class Person
    {
        [JsonProperty("name", NullValueHandling = NullValueHandling.Include)]
        public string Name { get; set; }

        [JsonProperty("id", NullValueHandling = NullValueHandling.Include)]
        public int? Id { get; set; }
    }
}

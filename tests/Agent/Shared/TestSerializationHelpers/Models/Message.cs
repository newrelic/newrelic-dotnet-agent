// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using Newtonsoft.Json;

namespace TestSerializationHelpers.Models
{
    public class ConnectResponseMessage
    {
        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("level")]
        public string Level { get; set; }
    }
}

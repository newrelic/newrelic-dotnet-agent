/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/

using Newtonsoft.Json;

namespace NewRelic.Agent.IntegrationTestHelpers.Models
{
    public class ConnectResponseMessage
    {
        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("level")]
        public string Level { get; set; }
    }
}

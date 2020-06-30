/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace NewRelic.Agent.IntegrationTestHelpers.Models
{
    public class VendorData
    {
        [JsonProperty("location")]
        public string Location { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("vmId")]
        public string VmId { get; set; }

        [JsonProperty("vmSize")]
        public string VmSize { get; set; }

    }
}

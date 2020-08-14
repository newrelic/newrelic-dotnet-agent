// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace NewRelic.Agent.IntegrationTestHelpers.Models
{
    public class UtilizationData
    {

        [JsonProperty("metadata_version")]
        public int MetaDataVersion { get; set; }

        [JsonProperty("logical_processors")]
        public int LogicalProcessors { get; set; }

        [JsonProperty("total_ram_mib")]
        public int TotalRamMib { get; set; }

        [JsonProperty("hostname")]
        public string HostName { get; set; }

        [JsonProperty("vendors")]
        public IDictionary<string, VendorData> Vendors { get; set; }

    }
}

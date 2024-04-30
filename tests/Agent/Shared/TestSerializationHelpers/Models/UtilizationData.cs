// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Collections.Generic;
using Newtonsoft.Json;

namespace NewRelic.Agent.Tests.TestSerializationHelpers.Models
{
    public class UtilizationData
    {

        [JsonProperty("metadata_version")]
        public int MetaDataVersion { get; set; }

        [JsonProperty("logical_processors")]
        public int LogicalProcessors { get; set; }

        [JsonProperty("total_ram_mib")]
        public int? TotalRamMib { get; set; } // this property is nullable because it doesn't get sent on Linux as of October 2021

        [JsonProperty("hostname")]
        public string HostName { get; set; }

        [JsonProperty("vendors")]
        public IDictionary<string, VendorData> Vendors { get; set; }

    }
}

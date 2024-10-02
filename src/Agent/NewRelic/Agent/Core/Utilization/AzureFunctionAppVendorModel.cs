// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using Newtonsoft.Json;

namespace NewRelic.Agent.Core.Utilization
{
    public class AzureFunctionVendorModel : IVendorModel
    {
        public string VendorName => "azurefunction";

        [JsonProperty("faas.app_name", NullValueHandling = NullValueHandling.Ignore)]
        public string AppName { get; }

        [JsonProperty("cloud.region", NullValueHandling = NullValueHandling.Ignore)]
        public string CloudRegion {get;}

        public AzureFunctionVendorModel(string appName, string cloudRegion)
        {
            AppName = appName;
            CloudRegion = cloudRegion;
        }
    }
}

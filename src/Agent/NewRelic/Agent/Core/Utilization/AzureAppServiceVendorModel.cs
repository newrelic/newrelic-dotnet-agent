// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using Newtonsoft.Json;

namespace NewRelic.Agent.Core.Utilization;

public class AzureAppServiceVendorModel : IVendorModel
{
    public string VendorName => "azureappservice";

    [JsonProperty("cloud.resource_id", NullValueHandling = NullValueHandling.Ignore)]
    public string CloudResourceId {get;}

    public AzureAppServiceVendorModel(string cloudResourceId)
    {
        CloudResourceId = cloudResourceId;
    }
}
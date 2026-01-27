// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using Newtonsoft.Json;

namespace NewRelic.Agent.Core.Utilization;

public class AzureVendorModel : IVendorModel
{

    private readonly string _location;
    private readonly string _name;
    private readonly string _vmId;
    private readonly string _vmSize;

    [JsonProperty("location", NullValueHandling = NullValueHandling.Ignore)]
    public string Location { get { return _location; } }
    [JsonProperty("name", NullValueHandling = NullValueHandling.Ignore)]
    public string Name { get { return _name; } }
    [JsonProperty("vmId", NullValueHandling = NullValueHandling.Ignore)]
    public string VmId { get { return _vmId; } }
    [JsonProperty("vmSize", NullValueHandling = NullValueHandling.Ignore)]
    public string VmSize { get { return _vmSize; } }

    public string VendorName { get { return "azure"; } }

    public AzureVendorModel(string location, string name, string vmId, string vmSize)
    {
        _location = location;
        _name = name;
        _vmId = vmId;
        _vmSize = vmSize;
    }
}
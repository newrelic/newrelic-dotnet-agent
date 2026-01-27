// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using Newtonsoft.Json;

namespace NewRelic.Agent.Core.Utilization;

public class GcpVendorModel : IVendorModel
{
    private readonly string _id;
    private readonly string _machineType;
    private readonly string _name;
    private readonly string _zone;

    public string VendorName { get { return "gcp"; } }

    [JsonProperty("id", NullValueHandling = NullValueHandling.Ignore)]
    public string Id { get { return _id; } }
    [JsonProperty("machineType", NullValueHandling = NullValueHandling.Ignore)]
    public string MachineType { get { return _machineType; } }
    [JsonProperty("name", NullValueHandling = NullValueHandling.Ignore)]
    public string Name { get { return _name; } }
    [JsonProperty("zone", NullValueHandling = NullValueHandling.Ignore)]
    public string Zone { get { return _zone; } }

    public GcpVendorModel(string id, string machineType, string name, string zone)
    {
        _id = id;
        _machineType = machineType;
        _name = name;
        _zone = zone;
    }
}

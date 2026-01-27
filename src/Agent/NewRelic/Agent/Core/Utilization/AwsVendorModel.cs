// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using Newtonsoft.Json;

namespace NewRelic.Agent.Core.Utilization;

public class AwsVendorModel : IVendorModel
{
    private readonly string _availabilityZone;
    private readonly string _instanceId;
    private readonly string _instanceType;

    public AwsVendorModel(string availabilityZone, string instanceId, string instanceType)
    {
        _availabilityZone = availabilityZone;
        _instanceId = instanceId;
        _instanceType = instanceType;
    }

    public string VendorName { get { return "aws"; } }

    [JsonProperty("availabilityZone", NullValueHandling = NullValueHandling.Ignore)]
    public string AvailabilityZone { get { return _availabilityZone; } }

    [JsonProperty("instanceId", NullValueHandling = NullValueHandling.Ignore)]
    public string InstanceId { get { return _instanceId; } }

    [JsonProperty("instanceType", NullValueHandling = NullValueHandling.Ignore)]
    public string InstanceType { get { return _instanceType; } }
}

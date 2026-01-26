// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using Newtonsoft.Json;

namespace NewRelic.Agent.Core.Utilization;

public class PcfVendorModel : IVendorModel
{
    private readonly string _cfInstanceGuid;
    private readonly string _cfInstanceIp;
    private readonly string _memoryLimit;

    public string VendorName { get { return "pcf"; } }

    [JsonProperty("cf_instance_guid", NullValueHandling = NullValueHandling.Ignore)]
    public string CfInstanceGuid { get { return _cfInstanceGuid; } }
    [JsonProperty("cf_instance_ip", NullValueHandling = NullValueHandling.Ignore)]
    public string CfInstanceIp { get { return _cfInstanceIp; } }
    [JsonProperty("memory_limit", NullValueHandling = NullValueHandling.Ignore)]
    public string MemoryLimit { get { return _memoryLimit; } }

    public PcfVendorModel(string cfInstanceGuid, string cfInstanceIp, string memoryLimit)
    {
        _cfInstanceGuid = cfInstanceGuid;
        _cfInstanceIp = cfInstanceIp;
        _memoryLimit = memoryLimit;
    }
}
// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using Newtonsoft.Json;

namespace NewRelic.Agent.Core.Utilization;

public class UtilitizationConfig
{
    private readonly string _billingHost;
    private readonly int? _logicalProcessors;
    private readonly int? _totalRamMib;

    public UtilitizationConfig(string billingHost, int? logicalProcessors, int? totalRamMib)
    {
        _billingHost = billingHost;
        _logicalProcessors = logicalProcessors;
        _totalRamMib = totalRamMib;
    }

    [JsonProperty("hostname", NullValueHandling = NullValueHandling.Ignore)]
    public string BillingHost
    {
        get { return _billingHost; }
    }

    [JsonProperty("logical_processors", NullValueHandling = NullValueHandling.Ignore)]
    public int? LogicalProcessors
    {
        get { return _logicalProcessors; }
    }

    [JsonProperty("total_ram_mib", NullValueHandling = NullValueHandling.Ignore)]
    public int? TotalRamMib
    {
        get { return _totalRamMib; }
    }
}
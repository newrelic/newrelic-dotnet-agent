// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace NewRelic.Agent.Core.Utilization;

[JsonObject(MemberSerialization.OptIn)]
public class UtilizationSettingsModel
{
    public UtilizationSettingsModel(int? logicalProcessors, ulong? totalRamBytes, string hostname, string fullHostName, List<string> ipAddress, string bootId, IDictionary<string, IVendorModel> vendors, UtilitizationConfig utilitizationConfig)
    {
        LogicalProcessors = logicalProcessors;
        TotalRamMebibytes = totalRamBytes.HasValue ? totalRamBytes.Value / (1024 * 1024) : totalRamBytes;
        Hostname = hostname;
        FullHostName = fullHostName;
        IpAddress = ipAddress;
        BootId = bootId;
        Vendors = vendors;
        Config = utilitizationConfig;
    }

    /// <summary>
    /// Utilization spec version number
    /// </summary>
    [JsonProperty("metadata_version")]
    public readonly int MetadataVersion = 5;

    [JsonProperty("logical_processors")]
    public readonly int? LogicalProcessors;

    [JsonProperty("total_ram_mib")]
    public readonly ulong? TotalRamMebibytes;

    [JsonProperty("hostname")]
    public readonly string Hostname;

    [JsonProperty("full_hostname")]
    public readonly string FullHostName;

    [JsonProperty("ip_address")]
    public readonly List<string> IpAddress;

    [JsonProperty("boot_id", NullValueHandling = NullValueHandling.Ignore)]
    public readonly string BootId;

    public readonly IDictionary<string, IVendorModel> Vendors;

    [JsonProperty("vendors", NullValueHandling = NullValueHandling.Ignore)]
    private IDictionary<string, IVendorModel> VendorsForSerialization { get { return Vendors.Any() ? Vendors : null; } }

    [JsonProperty("config", NullValueHandling = NullValueHandling.Ignore)]
    public readonly UtilitizationConfig Config;
}
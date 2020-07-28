using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace NewRelic.Agent.Core.Utilization
{
    [JsonObject(MemberSerialization.OptIn)]
    public class UtilizationSettingsModel
    {
        public UtilizationSettingsModel(int logicalProcessors, ulong totalRamBytes, string hostname, string bootId, IEnumerable<IVendorModel> vendors, UtilitizationConfig utilitizationConfig)
        {
            LogicalProcessors = logicalProcessors;
            TotalRamMebibytes = totalRamBytes / (1024 * 1024);
            Hostname = hostname;
            BootId = bootId;
            Vendors = vendors
                .Where(vendor => vendor != null)
                .ToDictionary(vendor => vendor.VendorName, vendor => vendor);

            Config = utilitizationConfig;
        }

        /// <summary>
        /// Utilization spec version number
        /// </summary>
        [JsonProperty("metadata_version")]
        public readonly int MetadataVersion = 3;

        [JsonProperty("logical_processors")]
        public readonly int LogicalProcessors;

        [JsonProperty("total_ram_mib")]
        public readonly ulong TotalRamMebibytes;
        [JsonProperty("hostname")]
        public readonly string Hostname;

        [JsonProperty("boot_id", NullValueHandling = NullValueHandling.Ignore)]
        public readonly String BootId;
        public readonly IDictionary<string, IVendorModel> Vendors;
        [JsonProperty("vendors", NullValueHandling = NullValueHandling.Ignore)]
        private IDictionary<String, IVendorModel> VendorsForSerialization { get { return Vendors.Any() ? Vendors : null; } }
        [JsonProperty("config", NullValueHandling = NullValueHandling.Ignore)]
        public readonly UtilitizationConfig Config;
    }
}

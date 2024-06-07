// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using Newtonsoft.Json;

namespace NewRelic.Agent.Core.AgentHealth
{
    public class AgentInfo
    {
        [JsonProperty(PropertyName = "install_type", NullValueHandling = NullValueHandling.Ignore)]
        public string InstallType { get; set; }

        [JsonProperty(PropertyName = "azure_site_extension", NullValueHandling = NullValueHandling.Ignore)]
        public bool AzureSiteExtension { get; set; }

        public override string ToString()
        {
            return $"{InstallType ?? "Unknown"}{(AzureSiteExtension ? "SiteExtension" : "")}";
        }
    }
}

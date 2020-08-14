// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using Newtonsoft.Json;

namespace NewRelic.Agent.IntegrationTestHelpers.Models
{
    public class ConnectData
    {
        [JsonProperty("pid")]
        public int ProcessId { get; set; }

        [JsonProperty("language")]
        public string Language { get; set; }

        [JsonProperty("display_host")]
        public string DisplayHost { get; set; }

        [JsonProperty("host")]
        public string HostName { get; set; }

        [JsonProperty("app_name")]
        public IEnumerable<string> AppNames { get; set; }

        [JsonProperty("agent_version")]
        public string AgentVersion { get; set; }

        [JsonProperty("security_settings")]
        public object SecuritySettings { get; set; } //implement as needed

        [JsonProperty("high_security")]
        public bool HighSecurityModeEnabled { get; set; }

        [JsonProperty("identifier")]
        public string Identifier { get; set; }

        [JsonProperty("labels")]
        public IEnumerable<object> Labels { get; set; } //implement as needed

        [JsonProperty("settings")]
        public object JavascriptAgentSettings { get; set; } //implement as needed

        [JsonProperty("metadata")]
        public Dictionary<string, string> Metadata { get; set; }

        [JsonProperty("utilization")]
        public UtilizationData UtilizationSettings { get; set; }

        [JsonProperty("environment", NullValueHandling = NullValueHandling.Ignore)]
        public Environment Environment { get; set; }
    }
}

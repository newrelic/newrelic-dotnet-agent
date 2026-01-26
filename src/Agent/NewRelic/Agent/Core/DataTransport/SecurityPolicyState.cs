// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using Newtonsoft.Json;

namespace NewRelic.Agent.Core.DataTransport;

public class SecurityPolicyState
{
    [JsonProperty("enabled")]
    public bool Enabled { get; set; }

    [JsonProperty("required")]
    public bool Required { get; set; }

    public SecurityPolicyState(bool enabled, bool required)
    {
        Enabled = enabled;
        Required = required;
    }

    public override string ToString()
    {
        return string.Format("{{enabled: {0}, required: {1} }}", Enabled, Required);
    }
}
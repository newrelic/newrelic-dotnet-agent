// Copyright 2023 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using Newtonsoft.Json;

namespace NugetVersionDeprecator;

internal class AgentRelease
{
    [JsonProperty("eolDate")]
    public DateTime EolDate { get; set; }
    [JsonProperty("version")]
    public string Version { get; set; }
}

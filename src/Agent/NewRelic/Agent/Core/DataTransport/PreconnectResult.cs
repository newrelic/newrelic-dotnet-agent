// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using Newtonsoft.Json;

namespace NewRelic.Agent.Core.DataTransport;

public class PreconnectResult
{
    [JsonProperty("redirect_host")]
    public string RedirectHost { get; set; }
}
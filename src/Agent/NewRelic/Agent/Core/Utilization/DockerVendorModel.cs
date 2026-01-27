// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using Newtonsoft.Json;

namespace NewRelic.Agent.Core.Utilization;

public class DockerVendorModel : IVendorModel
{
    private readonly string _id;

    public string VendorName { get { return "docker"; } }

    [JsonProperty("id", NullValueHandling = NullValueHandling.Ignore)]
    public string Id { get { return _id; } }

    public DockerVendorModel(string id)
    {
        _id = id;
    }

}
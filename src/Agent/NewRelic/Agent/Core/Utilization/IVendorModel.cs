/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using Newtonsoft.Json;

namespace NewRelic.Agent.Core.Utilization
{
    public interface IVendorModel
    {
        [JsonIgnore]
        string VendorName { get; }
    }
}

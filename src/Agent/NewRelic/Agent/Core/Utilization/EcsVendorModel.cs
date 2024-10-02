// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using Newtonsoft.Json;

namespace NewRelic.Agent.Core.Utilization
{
    public class EcsVendorModel : IVendorModel
    {
        private readonly string _ecsDockerId;

        public string VendorName { get { return "ecs"; } }

        [JsonProperty("ecsDockerId", NullValueHandling = NullValueHandling.Ignore)]
        public string EcsDockerId { get { return _ecsDockerId; } }

        public EcsVendorModel(string ecsDockerId)
        {
            _ecsDockerId = ecsDockerId;
        }
    }
}

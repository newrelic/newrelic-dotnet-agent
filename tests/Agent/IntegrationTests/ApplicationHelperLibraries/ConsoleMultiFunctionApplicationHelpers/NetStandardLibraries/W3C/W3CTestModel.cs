// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using Newtonsoft.Json;
using System.Collections.Generic;

namespace MultiFunctionApplicationHelpers.NetStandardLibraries.W3C
{
    public class W3CTestModel
    {
        [JsonProperty("url")]
        public string Url { get; set; }

        [JsonProperty("arguments")]
        public List<W3CTestModel> Arguments { get; set; }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
}

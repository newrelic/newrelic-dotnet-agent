// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using Newtonsoft.Json;

namespace NewRelic.Agent.Extensions.Helpers
{
    public static class DictionaryHelpers
    {
        public static IReadOnlyDictionary<string, object> FromJson(string json)
        {
            return JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
        }
    }
}

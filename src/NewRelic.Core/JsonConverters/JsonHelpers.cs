// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using Newtonsoft.Json;

namespace NewRelic.Core.JsonConverters
{
    public static class JsonHelpers
    {
        // This method is intended to be used from wrapper code so that the ILRepacked version of Newtonsoft.Json gets used
        // rather than relying on the customer application to provide it
        public static T DeserializeObject<T>(string payload)
        {
            return JsonConvert.DeserializeObject<T>(payload);
        }
    }
}

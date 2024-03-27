// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using Newtonsoft.Json;

namespace NewRelic.Core.JsonConverters
{
    public static class BedrockHelpers
    {
        // This method is used from the Bedrock wrapper code in order to avoid the wrapper
        // having a dependency on Newtonsoft.Json being available
        public static T DeserializeObject<T>(string payload)
        {
            return JsonConvert.DeserializeObject<T>(payload);
        }
    }
}

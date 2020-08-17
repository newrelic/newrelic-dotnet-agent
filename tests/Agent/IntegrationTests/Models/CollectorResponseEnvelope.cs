// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using Newtonsoft.Json;

namespace NewRelic.IntegrationTests.Models
{
    public class CollectorResponseEnvelope<T>
    {
        [JsonProperty("exception", NullValueHandling = NullValueHandling.Ignore)]
        public readonly CollectorExceptionEnvelope CollectorExceptionEnvelope;

        [JsonProperty("return_value")]
        public readonly T ReturnValue;

        public CollectorResponseEnvelope(CollectorExceptionEnvelope collectorExceptionEnvelope, T returnValue)
        {
            CollectorExceptionEnvelope = collectorExceptionEnvelope;
            ReturnValue = returnValue;
        }
    }
}

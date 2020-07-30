﻿/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using Newtonsoft.Json;

namespace NewRelic.Agent.Core.DataTransport
{
    public class CollectorResponseEnvelope<T>
    {
        [JsonProperty("exception")]
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

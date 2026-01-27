// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using Newtonsoft.Json;

namespace NewRelic.Agent.Core.DataTransport;

public class CollectorResponseEnvelope<T>
{
    [JsonProperty("return_value")]
    public readonly T ReturnValue;

    public CollectorResponseEnvelope(T returnValue)
    {
        ReturnValue = returnValue;
    }
}
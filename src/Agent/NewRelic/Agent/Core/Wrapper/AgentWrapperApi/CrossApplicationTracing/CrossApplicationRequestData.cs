// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Core.JsonConverters;
using Newtonsoft.Json;

namespace NewRelic.Agent.Core.Wrapper.AgentWrapperApi.CrossApplicationTracing;

// Note: this data is referred to as "TransactionData" in the CAT spec.
[JsonConverter(typeof(CrossApplicationRequestDataJsonConverter))]
public class CrossApplicationRequestData
{
    public readonly string TransactionGuid;
    public readonly bool Unused;
    public readonly string TripId;
    public readonly string PathHash;

    public CrossApplicationRequestData(string transactionGuid, bool unused, string tripId, string pathHash)
    {
        TransactionGuid = transactionGuid;
        Unused = unused;
        TripId = tripId;
        PathHash = pathHash;
    }
}

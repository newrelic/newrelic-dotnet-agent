// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Core.JsonConverters;
using Newtonsoft.Json;

namespace NewRelic.Agent.Core.Wrapper.AgentWrapperApi.CrossApplicationTracing
{
    // Note: this data is referred to as "TransactionData" in the CAT spec.
    [JsonConverter(typeof(JsonArrayConverter))]
    public class CrossApplicationRequestData
    {
        [JsonArrayIndex(Index = 0)]
        public readonly string TransactionGuid;
        [JsonArrayIndex(Index = 1)]
        public readonly bool Unused;
        [JsonArrayIndex(Index = 2)]
        public readonly string TripId;
        [JsonArrayIndex(Index = 3)]
        public readonly string PathHash;

        // For backwards compatibility we need to support deserializing transactionData that may be missing any number of fields
        public CrossApplicationRequestData()
        {

        }

        public CrossApplicationRequestData(string transactionGuid)
        {
            TransactionGuid = transactionGuid;
        }

        public CrossApplicationRequestData(string transactionGuid, bool unused)
        {
            TransactionGuid = transactionGuid;
            Unused = unused;
        }

        public CrossApplicationRequestData(string transactionGuid, bool unused, string tripId)
        {
            TransactionGuid = transactionGuid;
            Unused = unused;
            TripId = tripId;
        }

        public CrossApplicationRequestData(string transactionGuid, bool unused, string tripId, string pathHash)
        {
            TransactionGuid = transactionGuid;
            Unused = unused;
            TripId = tripId;
            PathHash = pathHash;
        }
    }
}

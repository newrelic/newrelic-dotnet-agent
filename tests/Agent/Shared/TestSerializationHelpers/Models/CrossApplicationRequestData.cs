// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NewRelic.Agent.Tests.TestSerializationHelpers.Models
{
    // Note: this data is referred to as "TransactionData" in the CAT spec.
    [JsonConverter(typeof(CrossApplicationRequestDataConverter))]
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
        public class CrossApplicationRequestDataConverter : JsonConverter
        {
            public override bool CanConvert(Type objectType)
            {
                return true;
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                var jArray = JArray.Load(reader);
                if (jArray == null)
                    throw new JsonSerializationException("Unable to create a jObject from reader.");

                var transactionGuid = jArray[0].ToObject<string>();
                var unused = (jArray[1] ?? new JObject()).ToObject<bool>();
                var tripId = (jArray[2] ?? new JObject()).ToObject<string>();
                var pathHash = (jArray[3] ?? new JObject()).ToObject<string>();

                return new CrossApplicationRequestData(transactionGuid, unused, tripId, pathHash);
            }

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                if (writer == null)
                    throw new NullReferenceException(nameof(writer));
                if (value == null)
                    throw new NullReferenceException(nameof(value));

                var requestData = value as CrossApplicationRequestData;
                if (requestData == null)
                    throw new NullReferenceException(nameof(requestData));

                writer.WriteStartArray();
                writer.WriteValue(requestData.TransactionGuid);
                writer.WriteValue(requestData.Unused);
                writer.WriteValue(requestData.TripId);
                writer.WriteValue(requestData.PathHash);
                writer.WriteEndArray();
            }
        }
    }
}

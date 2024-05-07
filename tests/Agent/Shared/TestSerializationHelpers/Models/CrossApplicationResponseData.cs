// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NewRelic.Agent.Tests.TestSerializationHelpers.Models
{
    // Note: this data is referred to as "AppData" in the CAT spec.
    [JsonConverter(typeof(CrossApplicationResponseDataConverter))]
    public class CrossApplicationResponseData
    {
        public readonly string CrossProcessId;
        public readonly string TransactionName;
        public readonly float QueueTimeInSeconds;
        public readonly float ResponseTimeInSeconds;
        public readonly int ContentLength;
        public readonly string TransactionGuid;
        public readonly bool Unused;

        // For backwards compatibility we need to support deserializing AppData that is missing fields 5 and 6
        public CrossApplicationResponseData(string crossProcessId, string transactionName, float queueTimeInSeconds, float responseTimeInSeconds, int contentLength)
        {
            CrossProcessId = crossProcessId;
            TransactionName = transactionName;
            QueueTimeInSeconds = queueTimeInSeconds;
            ResponseTimeInSeconds = responseTimeInSeconds;
            ContentLength = contentLength;
        }

        // For backwards compatibility we need to support deserializing AppData that is missing field 6
        public CrossApplicationResponseData(string crossProcessId, string transactionName, float queueTimeInSeconds, float responseTimeInSeconds, int contentLength, string transactionGuid)
        {
            CrossProcessId = crossProcessId;
            TransactionName = transactionName;
            QueueTimeInSeconds = queueTimeInSeconds;
            ResponseTimeInSeconds = responseTimeInSeconds;
            ContentLength = contentLength;
            TransactionGuid = transactionGuid;
        }

        public CrossApplicationResponseData(string crossProcessId, string transactionName, float queueTimeInSeconds, float responseTimeInSeconds, int contentLength, string transactionGuid, bool unused)
        {
            CrossProcessId = crossProcessId;
            TransactionName = transactionName;
            QueueTimeInSeconds = queueTimeInSeconds;
            ResponseTimeInSeconds = responseTimeInSeconds;
            ContentLength = contentLength;
            TransactionGuid = transactionGuid;
            Unused = unused;
        }

        public class CrossApplicationResponseDataConverter : JsonConverter
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

                var crossProcessId = jArray[0].ToObject<string>();
                var transactionname = jArray[1].ToObject<string>();
                var queueTimeInSeconds = jArray[2].ToObject<float>();
                var responseTimeInSeconds = jArray[3].ToObject<float>();
                var contentLength = jArray[4].ToObject<int>();
                var transactionGuid = (jArray[5] ?? new JObject()).ToObject<string>();
                var unused = (jArray[6] ?? new JObject()).ToObject<bool>();

                return new CrossApplicationResponseData(crossProcessId, transactionname, queueTimeInSeconds, responseTimeInSeconds, contentLength, transactionGuid, unused);
            }

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                throw new NotImplementedException();
            }
        }
    }
}

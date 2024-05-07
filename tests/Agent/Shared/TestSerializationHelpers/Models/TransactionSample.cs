// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NewRelic.Agent.Tests.TestSerializationHelpers.Models
{
    [JsonConverter(typeof(TransactionSampleConverter))]
    public class TransactionSample
    {
        // index 0
        public readonly DateTime Timestamp;

        // index 1
        public readonly TimeSpan Duration;

        // index 2
        public readonly string Path;

        // index 3
        public readonly string Uri;

        // index 4
        public readonly TransactionTrace TraceData;

        // index 5
        public readonly string Guid;

        // index 6
        public readonly object Unused1 = null;

        // index 7
        public readonly bool ForcePersist;

        // index 8
        public readonly ulong? XRaySessionId;

        public TransactionSample(DateTime timestamp, TimeSpan duration, string path, string uri, TransactionTrace traceData, string guid, bool forcePersist, ulong? xRaySessionId)
        {
            Timestamp = timestamp;
            Duration = duration;
            Path = path;
            Uri = uri;
            TraceData = traceData;
            Guid = guid;
            ForcePersist = forcePersist;
            XRaySessionId = xRaySessionId;
        }

        public class TransactionSampleConverter : JsonConverter
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

                var timestamp = new DateTime(1970, 01, 01) + TimeSpan.FromMilliseconds((double)(jArray[0] ?? 0));
                var duration = TimeSpan.FromMilliseconds((double)(jArray[1] ?? 0));
                var path = (jArray[2] ?? new JObject()).ToObject<string>();
                var uri = (jArray[3] ?? new JObject()).ToObject<string>();
                var traceData = (jArray[4] ?? new JObject()).ToObject<TransactionTrace>();
                var guid = (jArray[5] ?? new JObject()).ToObject<string>();
                var forcePersist = (jArray[7] ?? new JObject()).ToObject<bool>();
                var xRaySessionId = (jArray[8] ?? new JObject()).ToObject<ulong?>();

                return new TransactionSample(timestamp, duration, path, uri, traceData, guid, forcePersist, xRaySessionId);
            }

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                throw new NotImplementedException();
            }
        }
    }

}

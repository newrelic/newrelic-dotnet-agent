// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NewRelic.Agent.Tests.TestSerializationHelpers.Models
{
    [JsonConverter(typeof(CustomEventConverter))]
    public class CustomEventData
    {
        // index 0
        public readonly CustomEventHeader Header;

        // index 1
        public IDictionary<string, object> Attributes;

        public CustomEventData(CustomEventHeader header, IDictionary<string, object> attributes)
        {
            Header = header;
            Attributes = attributes;
        }

    }

    public class CustomEventConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return true;
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var jArray = JArray.Load(reader);
            if (jArray == null)
                throw new JsonSerializationException("Unable to create a jArray from reader.");
            if (jArray.Count != 3)
                throw new JsonSerializationException("jArray contains fewer elements than expected.");

            var header = (jArray[0] ?? new JObject()).ToObject<CustomEventHeader>(serializer);
            var attributes = (jArray[1] ?? new JObject()).ToObject<IDictionary<string, object>>(serializer);

            return new CustomEventData(header, attributes);
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }

    public class CustomEventHeader
    {
        [JsonProperty("timestamp")]
        public long Timestamp { get; set; }

        [JsonProperty("type", NullValueHandling = NullValueHandling.Ignore)]
        public string Type { get; set; }
    }
}

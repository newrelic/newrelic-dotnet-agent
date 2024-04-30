// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using NewRelic.Agent.Tests.TestSerializationHelpers.JsonConverters;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NewRelic.Agent.Tests.TestSerializationHelpers.Models
{
    [JsonConverter(typeof(SpanEventConverter))]
    public class SpanEvent
    {
        // index 0
        [JsonArrayIndex(Index = 0)]
        public readonly IDictionary<string, object> IntrinsicAttributes;

        // index 1
        [JsonArrayIndex(Index = 1)]
        public readonly IDictionary<string, object> UserAttributes;

        // index 2
        [JsonArrayIndex(Index = 2)]
        public readonly IDictionary<string, object> AgentAttributes;

        public SpanEvent(IDictionary<string, object> intrinsicAttributes, IDictionary<string, object> userAttributes, IDictionary<string, object> agentAttributes)
        {
            IntrinsicAttributes = intrinsicAttributes;
            UserAttributes = userAttributes;
            AgentAttributes = agentAttributes;
        }

        public IDictionary<string, object> GetByType(SpanEventAttributeType attributeType)
        {
            IDictionary<string, object> attributes;
            switch (attributeType)
            {
                case SpanEventAttributeType.Intrinsic:
                    attributes = IntrinsicAttributes;
                    break;
                case SpanEventAttributeType.Agent:
                    attributes = AgentAttributes;
                    break;
                case SpanEventAttributeType.User:
                    attributes = UserAttributes;
                    break;
                default:
                    throw new NotImplementedException();
            }

            return attributes ?? new Dictionary<string, object>();
        }
    }

    public class SpanEventConverter : JsonConverter
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

            var intrinsicAttributes = (jArray[0] ?? new JObject()).ToObject<IDictionary<string, object>>(serializer);
            var userAttributes = (jArray[1] ?? new JObject()).ToObject<IDictionary<string, object>>(serializer);
            var agentAttributes = (jArray[2] ?? new JObject()).ToObject<IDictionary<string, object>>(serializer);

            return new SpanEvent(intrinsicAttributes, userAttributes, agentAttributes);
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }

    public enum SpanEventAttributeType
    {
        Intrinsic,
        Agent,
        User,
    }
}

// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NewRelic.Agent.Tests.TestSerializationHelpers.Models
{
    [JsonConverter(typeof(TransactionEventConverter))]
    public class TransactionEvent
    {
        // index 0
        public readonly IDictionary<string, object> IntrinsicAttributes;

        // index 1
        public readonly IDictionary<string, object> UserAttributes;

        // index 2
        public readonly IDictionary<string, object> AgentAttributes;

        public TransactionEvent(IDictionary<string, object> intrinsicAttributes, IDictionary<string, object> userAttributes, IDictionary<string, object> agentAttributes)
        {
            IntrinsicAttributes = intrinsicAttributes;
            UserAttributes = userAttributes;
            AgentAttributes = agentAttributes;
        }

        public IDictionary<string, object> GetByType(TransactionEventAttributeType attributeType)
        {
            IDictionary<string, object> attributes;
            switch (attributeType)
            {
                case TransactionEventAttributeType.Intrinsic:
                    attributes = IntrinsicAttributes;
                    break;
                case TransactionEventAttributeType.Agent:
                    attributes = AgentAttributes;
                    break;
                case TransactionEventAttributeType.User:
                    attributes = UserAttributes;
                    break;
                default:
                    throw new NotImplementedException();
            }

            return attributes ?? new Dictionary<string, object>();
        }
    }

    public class TransactionEventConverter : JsonConverter
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

            return new TransactionEvent(intrinsicAttributes, userAttributes, agentAttributes);
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }

    public enum TransactionEventAttributeType
    {
        Intrinsic,
        Agent,
        User,
    }
}

using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NewRelic.Agent.IntegrationTestHelpers.Models
{
    [JsonConverter(typeof(TransactionEventConverter))]
    public class TransactionEvent
    {
        // index 0
        public readonly IDictionary<String, Object> IntrinsicAttributes;

        // index 1
        public readonly IDictionary<String, Object> UserAttributes;

        // index 2
        public readonly IDictionary<String, Object> AgentAttributes;

        public TransactionEvent(IDictionary<String, Object> intrinsicAttributes, IDictionary<String, Object> userAttributes, IDictionary<String, Object> agentAttributes)
        {
            IntrinsicAttributes = intrinsicAttributes;
            UserAttributes = userAttributes;
            AgentAttributes = agentAttributes;
        }

        [NotNull]
        public IDictionary<String, Object> GetByType(TransactionEventAttributeType attributeType)
        {
            IDictionary<String, Object> attributes;
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

            return attributes ?? new Dictionary<String, Object>();
        }
    }

    public class TransactionEventConverter : JsonConverter
    {
        public override Boolean CanConvert(Type objectType)
        {
            return true;
        }

        public override Object ReadJson(JsonReader reader, Type objectType, Object existingValue, JsonSerializer serializer)
        {
            var jArray = JArray.Load(reader);
            if (jArray == null)
                throw new JsonSerializationException("Unable to create a jArray from reader.");
            if (jArray.Count != 3)
                throw new JsonSerializationException("jArray contains fewer elements than expected.");

            var intrinsicAttributes = (jArray[0] ?? new JObject()).ToObject<IDictionary<String, Object>>(serializer);
            var userAttributes = (jArray[1] ?? new JObject()).ToObject<IDictionary<String, Object>>(serializer);
            var agentAttributes = (jArray[2] ?? new JObject()).ToObject<IDictionary<String, Object>>(serializer);

            return new TransactionEvent(intrinsicAttributes, userAttributes, agentAttributes);
        }

        public override void WriteJson(JsonWriter writer, Object value, JsonSerializer serializer)
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

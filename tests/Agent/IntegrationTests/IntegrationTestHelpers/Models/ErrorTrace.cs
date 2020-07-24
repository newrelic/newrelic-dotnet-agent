using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NewRelic.Agent.IntegrationTestHelpers.Models
{
    [JsonConverter(typeof(ErrorTraceConverter))]
    public class ErrorTrace
    {
        // index 0
        public readonly DateTime Timestamp;

        // index 1
        public readonly String Path;

        // index 2
        public readonly String Message;

        // index 3
        public readonly String ExceptionClassName;

        // index 4
        public readonly ErrorTraceAttributes Attributes;

        // index 5
        public readonly String Guid;

        public ErrorTrace(DateTime timestamp, String path, String message, String exceptionClassName, ErrorTraceAttributes attributes, String guid)
        {
            Timestamp = timestamp;
            Path = path;
            ExceptionClassName = exceptionClassName;
            Message = message;
            Attributes = attributes;
            Guid = guid;
        }

        public class ErrorTraceConverter : JsonConverter
        {
            public override Boolean CanConvert(Type objectType)
            {
                return true;
            }

            public override Object ReadJson(JsonReader reader, Type objectType, Object existingValue, JsonSerializer serializer)
            {
                var jArray = JArray.Load(reader);
                if (jArray == null)
                    throw new JsonSerializationException("Unable to create a jObject from reader.");

                var timestamp = new DateTime(1970, 01, 01) + TimeSpan.FromSeconds((Double)(jArray[0] ?? 0));
                var path = (jArray[1] ?? new JObject()).ToString();
                var message = (jArray[2] ?? new JObject()).ToString();
                var exceptionClassName = (jArray[3] ?? new JObject()).ToString();
                var attributes = (jArray[4] ?? new JObject()).ToObject<ErrorTraceAttributes>(serializer);
                var guid = (jArray[5] ?? new JObject()).ToString();

                return new ErrorTrace(timestamp, path, message, exceptionClassName, attributes, guid);
            }

            public override void WriteJson(JsonWriter writer, Object value, JsonSerializer serializer)
            {
                throw new NotImplementedException();
            }
        }
    }

    public class ErrorTraceAttributes
    {
        [JsonProperty(PropertyName = "agentAttributes")]
        public readonly IDictionary<String, Object> AgentAttributes;

        [JsonProperty(PropertyName = "intrinsics")]
        public readonly IDictionary<String, Object> IntrinsicAttributes;

        [JsonProperty(PropertyName = "userAttributes")]
        public readonly IDictionary<String, Object> UserAttributes;

        [JsonProperty(PropertyName = "stack_trace")]
        public readonly IEnumerable<String> StackTrace;

        [JsonProperty(PropertyName = "request_uri")]
        public readonly String RequestUri;

        public ErrorTraceAttributes(IDictionary<String, Object> agentAttributes, IDictionary<String, Object> intrinsicAttributes, IDictionary<String, Object> userAttributes, IEnumerable<String> stackTrace, String requestUri)
        {
            AgentAttributes = agentAttributes;
            IntrinsicAttributes = intrinsicAttributes;
            UserAttributes = userAttributes;
            StackTrace = stackTrace;
            RequestUri = requestUri;
        }

        [NotNull]
        public IDictionary<String, Object> GetByType(ErrorTraceAttributeType attributeType)
        {
            IDictionary<String, Object> attributes;
            switch (attributeType)
            {
                case ErrorTraceAttributeType.Intrinsic:
                    attributes = IntrinsicAttributes;
                    break;
                case ErrorTraceAttributeType.Agent:
                    attributes = AgentAttributes;
                    break;
                case ErrorTraceAttributeType.User:
                    attributes = UserAttributes;
                    break;
                default:
                    throw new NotImplementedException();
            }

            return attributes ?? new Dictionary<String, Object>();
        }
    }

    public enum ErrorTraceAttributeType
    {
        Intrinsic,
        Agent,
        User,
    }
}

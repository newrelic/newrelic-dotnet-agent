// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NewRelic.Agent.Tests.TestSerializationHelpers.Models
{
    [JsonConverter(typeof(ErrorTraceConverter))]
    public class ErrorTrace
    {
        // index 0
        public readonly DateTime Timestamp;

        // index 1
        public readonly string Path;

        // index 2
        public readonly string Message;

        // index 3
        public readonly string ExceptionClassName;

        // index 4
        public readonly ErrorTraceAttributes Attributes;

        // index 5
        public readonly string Guid;

        public ErrorTrace(DateTime timestamp, string path, string message, string exceptionClassName, ErrorTraceAttributes attributes, string guid)
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
                var path = (jArray[1] ?? new JObject()).ToString();
                var message = (jArray[2] ?? new JObject()).ToString();
                var exceptionClassName = (jArray[3] ?? new JObject()).ToString();
                var attributes = (jArray[4] ?? new JObject()).ToObject<ErrorTraceAttributes>(serializer);
                var guid = (jArray[5] ?? new JObject()).ToString();

                return new ErrorTrace(timestamp, path, message, exceptionClassName, attributes, guid);
            }

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                throw new NotImplementedException();
            }
        }
    }

    public class ErrorTraceAttributes
    {
        [JsonProperty(PropertyName = "agentAttributes")]
        public readonly IDictionary<string, object> AgentAttributes;

        [JsonProperty(PropertyName = "intrinsics")]
        public readonly IDictionary<string, object> IntrinsicAttributes;

        [JsonProperty(PropertyName = "userAttributes")]
        public readonly IDictionary<string, object> UserAttributes;

        [JsonProperty(PropertyName = "stack_trace")]
        public readonly IEnumerable<string> StackTrace;

        public ErrorTraceAttributes(IDictionary<string, object> agentAttributes, IDictionary<string, object> intrinsicAttributes, IDictionary<string, object> userAttributes, IEnumerable<string> stackTrace)
        {
            AgentAttributes = agentAttributes;
            IntrinsicAttributes = intrinsicAttributes;
            UserAttributes = userAttributes;
            StackTrace = stackTrace;
        }


        public IDictionary<string, object> GetByType(ErrorTraceAttributeType attributeType)
        {
            IDictionary<string, object> attributes;
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

            return attributes ?? new Dictionary<string, object>();
        }
    }

    public enum ErrorTraceAttributeType
    {
        Intrinsic,
        Agent,
        User,
    }
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using NewRelic.Agent.Core.JsonConverters;
using NewRelic.Agent.Core.Utilities;
using Newtonsoft.Json;

namespace NewRelic.Agent.Core.WireModels
{
    [JsonConverter(typeof(JsonArrayConverter))]
    public class ErrorTraceWireModel
    {
        /// <summary>
        /// The UTC timestamp indicating when the error occurred. 
        /// </summary>
        [JsonArrayIndex(Index = 0)]
        [DateTimeSerializesAsUnixTime]
        public virtual DateTime TimeStamp { get; }

        /// <summary>
        /// ex. WebTransaction/ASP/post.aspx
        /// </summary>
        [JsonArrayIndex(Index = 1)]
        public virtual String Path { get; }

        /// <summary>
        /// The error message.
        /// </summary>
        [JsonArrayIndex(Index = 2)]
        public virtual String Message { get; }

        /// <summary>
        /// The class name of the exception thrown.
        /// </summary>
        [JsonArrayIndex(Index = 3)]
        public virtual String ExceptionClassName { get; }

        /// <summary>
        /// Parameters associated with this error.
        /// </summary>
        [JsonArrayIndex(Index = 4)]
        public virtual ErrorTraceAttributesWireModel Attributes { get; }

        /// <summary>
        /// Guid of this error.
        /// </summary>
        [JsonArrayIndex(Index = 5)]
        public virtual String Guid { get; }

        public ErrorTraceWireModel(DateTime timestamp, String path, String message, String exceptionClassName, ErrorTraceAttributesWireModel attributes, String guid)
        {
            TimeStamp = timestamp;
            Path = path;
            Message = message;
            ExceptionClassName = exceptionClassName;
            Attributes = attributes;
            Guid = guid;
        }

        [JsonObject(MemberSerialization.OptIn)]
        public class ErrorTraceAttributesWireModel
        {
            [JsonProperty("stack_trace")]
            public virtual IEnumerable<String> StackTrace { get; }

            [JsonProperty("agentAttributes")]
            public virtual IEnumerable<KeyValuePair<String, Object>> AgentAttributes { get; }

            [JsonProperty("userAttributes")]
            public virtual IEnumerable<KeyValuePair<String, Object>> UserAttributes { get; }

            [JsonProperty("intrinsics")]
            public virtual IEnumerable<KeyValuePair<String, Object>> Intrinsics { get; }

            [JsonProperty("request_uri")]
            public virtual String RequestUri { get; }

            public ErrorTraceAttributesWireModel(String requestUri, IEnumerable<KeyValuePair<String, Object>> agentAttributes, IEnumerable<KeyValuePair<String, Object>> intrinsicAttributes, IEnumerable<KeyValuePair<String, Object>> userAttributes, IEnumerable<String> stackTrace = null)
            {
                AgentAttributes = agentAttributes.ToReadOnlyDictionary();
                Intrinsics = intrinsicAttributes.ToReadOnlyDictionary();
                UserAttributes = userAttributes.ToReadOnlyDictionary();

                RequestUri = requestUri;

                if (stackTrace != null)
                    StackTrace = new ReadOnlyCollection<String>(new List<String>(stackTrace));
            }
        }
    }
}

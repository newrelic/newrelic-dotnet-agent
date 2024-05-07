// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using NewRelic.Agent.Core.Attributes;
using NewRelic.Agent.Core.JsonConverters;
using Newtonsoft.Json;

namespace NewRelic.Agent.Core.WireModels
{
    [JsonConverter(typeof(JsonArrayConverter))]
    public class ErrorTraceWireModel : IWireModel
    {
        /// <summary>
        /// The UTC timestamp indicating when the error occurred. 
        /// </summary>
        [JsonArrayIndex(Index = 0)]
        [DateTimeSerializesAsUnixTimeMilliseconds]
        public virtual DateTime TimeStamp { get; }

        /// <summary>
        /// ex. WebTransaction/ASP/post.aspx or NoticeError API Call if outside a transaction
        /// UI calls this field 'URL' in Errors page; 'Transaction Name' in Error Analytics page
        /// </summary>
        [JsonArrayIndex(Index = 1)]
        public virtual string Path { get; }

        /// <summary>
        /// The error message.
        /// </summary>
        [JsonArrayIndex(Index = 2)]
        public virtual string Message { get; }

        /// <summary>
        /// The class name of the exception thrown.
        /// </summary>
        [JsonArrayIndex(Index = 3)]
        public virtual string ExceptionClassName { get; }

        /// <summary>
        /// Parameters associated with this error.
        /// </summary>
        [JsonArrayIndex(Index = 4)]
        public virtual ErrorTraceAttributesWireModel Attributes { get; }

        /// <summary>
        /// Guid of this error.
        /// </summary>
        [JsonArrayIndex(Index = 5)]
        public virtual string Guid { get; }

        public ErrorTraceWireModel(DateTime timestamp, string path, string message, string exceptionClassName, ErrorTraceAttributesWireModel attributes, string guid)
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
            public virtual IEnumerable<string> StackTrace { get; }

            [JsonProperty("agentAttributes")]
            [JsonConverter(typeof(EventAttributesJsonConverter))]
            public virtual ReadOnlyDictionary<string, object> AgentAttributes { get; }

            [JsonProperty("userAttributes")]
            [JsonConverter(typeof(EventAttributesJsonConverter))]
            public virtual ReadOnlyDictionary<string, object> UserAttributes { get; }

            [JsonProperty("intrinsics")]
            [JsonConverter(typeof(EventAttributesJsonConverter))]
            public virtual ReadOnlyDictionary<string, object> Intrinsics { get; }

            public ErrorTraceAttributesWireModel(IAttributeValueCollection attribValues, IList<string> stackTrace = null)
            {
                var filteredAttribValues = new AttributeValueCollection(attribValues, AttributeDestinations.ErrorTrace);

                filteredAttribValues.MakeImmutable();

                AgentAttributes = new ReadOnlyDictionary<string,object>(filteredAttribValues.GetAttributeValuesDic(AttributeClassification.AgentAttributes));
                Intrinsics = new ReadOnlyDictionary<string, object>(filteredAttribValues.GetAttributeValuesDic(AttributeClassification.Intrinsics));
                UserAttributes = new ReadOnlyDictionary<string, object>(filteredAttribValues.GetAttributeValuesDic(AttributeClassification.UserAttributes));

                if (stackTrace != null)
                {
                    StackTrace = new ReadOnlyCollection<string>(stackTrace);
                }
            }
        }
    }
}

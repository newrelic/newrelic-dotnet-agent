using System;
using System.Collections.Generic;
using NewRelic.Agent.Core.JsonConverters;
using NewRelic.Agent.Core.Utilities;
using NewRelic.SystemExtensions.Collections.Generic;
using Newtonsoft.Json;

namespace NewRelic.Agent.Core.WireModels
{
    [JsonConverter(typeof(JsonArrayConverter))]
    public class CustomEventWireModel
    {
        [JsonArrayIndex(Index = 0)]
        public readonly IEnumerable<KeyValuePair<string, object>> IntrinsicAttributes;

        [JsonArrayIndex(Index = 1)]
        public readonly IEnumerable<KeyValuePair<string, object>> UserAttributes;

        private const string EventTypeKey = "type";
        private const string TimeStampKey = "timestamp";

        /// <param name="eventType">The event type.</param>
        /// <param name="eventTimeStamp">The start time of the event.</param>
        /// <param name="userAttributes">Additional attributes supplied by the user.</param>
        public CustomEventWireModel(string eventType, DateTime eventTimeStamp, IEnumerable<KeyValuePair<string, object>> userAttributes)
        {
            IntrinsicAttributes = new Dictionary<string, object>
            {
                {EventTypeKey, eventType},
                {TimeStampKey, eventTimeStamp.ToUnixTimeSeconds()},
            };

            UserAttributes = userAttributes.ToDictionary();
        }
    }
}

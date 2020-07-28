using System;
using System.Collections.Generic;
using NewRelic.Agent.Core.JsonConverters;
using NewRelic.SystemExtensions.Collections.Generic;
using Newtonsoft.Json;

namespace NewRelic.Agent.Core.WireModels
{
    [JsonConverter(typeof(JsonArrayConverter))]
    [JsonObject(MemberSerialization.OptIn)]
    public class ErrorEventWireModel
    {
        [JsonArrayIndex(Index = 0)]
        public readonly ReadOnlyDictionary<String, Object> IntrinsicAttributes;

        [JsonArrayIndex(Index = 1)]
        public readonly ReadOnlyDictionary<String, Object> UserAttributes;

        [JsonArrayIndex(Index = 2)]
        public readonly ReadOnlyDictionary<String, Object> AgentAttributes;

        private readonly bool _isSynthetics;

        public ErrorEventWireModel(IEnumerable<KeyValuePair<String, Object>> agentAttributes, IEnumerable<KeyValuePair<String, Object>> intrinsicAttributes, IEnumerable<KeyValuePair<String, Object>> userAttributes, bool isSynthetics)
        {
            IntrinsicAttributes = new ReadOnlyDictionary<String, Object>(intrinsicAttributes.ToDictionary<String, Object>());
            UserAttributes = new ReadOnlyDictionary<String, Object>(userAttributes.ToDictionary<String, Object>());
            AgentAttributes = new ReadOnlyDictionary<String, Object>(agentAttributes.ToDictionary<String, Object>());
            _isSynthetics = isSynthetics;
        }

        public Boolean IsSynthetics()
        {
            // An event will always contain either all of the synthetics keys or none of them.
            // There is no need to check for the presence of each synthetics key.
            return _isSynthetics;
        }


    }
}

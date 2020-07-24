using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using NewRelic.Agent.Core.JsonConverters;
using Newtonsoft.Json;

namespace NewRelic.Agent.Core.Commands
{
    [JsonConverter(typeof(JsonArrayConverter))]
    public class CommandModel
    {
        [JsonArrayIndex(Index = 0)]
        public readonly Int32 CommandId;

        [JsonArrayIndex(Index = 1)]
        public readonly CommandDetails Details;

        public CommandModel(Int32 commandId, [CanBeNull] CommandDetails details)
        {
            CommandId = commandId;
            Details = details;
        }
    }

    public class CommandDetails
    {
        [JsonProperty("name")]
        public readonly String Name;

        [JsonProperty("arguments")]
        public readonly IDictionary<String, Object> Arguments;

        public CommandDetails(String name, IDictionary<String, Object> arguments)
        {
            Name = name;
            Arguments = arguments;
        }
    }
}

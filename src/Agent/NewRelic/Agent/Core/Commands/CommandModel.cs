// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using NewRelic.Agent.Core.JsonConverters;
using Newtonsoft.Json;

namespace NewRelic.Agent.Core.Commands
{
    [JsonConverter(typeof(JsonArrayConverter))]
    public class CommandModel
    {
        [JsonArrayIndex(Index = 0)]
        public readonly int CommandId;

        [JsonArrayIndex(Index = 1)]
        public readonly CommandDetails Details;

        public CommandModel(int commandId, CommandDetails details)
        {
            CommandId = commandId;
            Details = details;
        }
    }

    public class CommandDetails
    {
        [JsonProperty("name")]
        public readonly string Name;

        [JsonProperty("arguments")]
        public readonly IDictionary<string, object> Arguments;

        public CommandDetails(string name, IDictionary<string, object> arguments)
        {
            Name = name;
            Arguments = arguments;
        }
    }
}

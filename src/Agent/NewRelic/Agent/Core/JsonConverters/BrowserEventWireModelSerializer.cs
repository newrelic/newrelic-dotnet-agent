// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Linq;
using NewRelic.Agent.Core.Attributes;
using Newtonsoft.Json;

namespace NewRelic.Agent.Core.JsonConverters;

public class BrowserEventWireModelSerializer : JsonConverter<IAttributeValueCollection>
{
    public override IAttributeValueCollection ReadJson(JsonReader reader, Type objectType, IAttributeValueCollection existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        throw new NotImplementedException();
    }

    public override void WriteJson(JsonWriter writer, IAttributeValueCollection value, JsonSerializer serializer)
    {
        var agentAttribs = value.GetAttributeValues(AttributeClassification.AgentAttributes).ToArray();
        var userAttribs = value.GetAttributeValues(AttributeClassification.UserAttributes).ToArray();

        if(agentAttribs.Length == 0 && userAttribs.Length == 0)
        {
            return;
        }

        writer.WriteStartObject();

        if (agentAttribs.Length > 0)
        {
            writer.WritePropertyName("a");
            JsonSerializerHelpers.WriteCollection(writer, agentAttribs);
        }


        if (userAttribs.Length > 0)
        {
            writer.WritePropertyName("u");
            JsonSerializerHelpers.WriteCollection(writer, userAttribs);
        }

        writer.WriteEndObject();
    }
}

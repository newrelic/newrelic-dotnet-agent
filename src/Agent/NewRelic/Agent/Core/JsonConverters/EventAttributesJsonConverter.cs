// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace NewRelic.Agent.Core.JsonConverters;

public class EventAttributesJsonConverter : JsonConverter<IEnumerable<KeyValuePair<string, object>>>
{
    public override IEnumerable<KeyValuePair<string, object>> ReadJson(JsonReader reader, Type objectType, IEnumerable<KeyValuePair<string, object>> existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        throw new NotImplementedException("Deserialization of IDictionary<string,object> is not supported");
    }

    public override void WriteJson(JsonWriter writer, IEnumerable<KeyValuePair<string, object>> value, JsonSerializer serializer)
    {
        JsonSerializerHelpers.WriteObjectCollection(writer, value);
    }
}
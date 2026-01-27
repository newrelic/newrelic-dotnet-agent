// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using NewRelic.Agent.Core.WireModels;
using Newtonsoft.Json;

namespace NewRelic.Agent.Core.JsonConverters;

public class MetricNameWireModelJsonConverter : JsonConverter<MetricNameWireModel>
{
    private const string PropertyName = "name";
    private const string PropertyScope = "scope";

    public override MetricNameWireModel ReadJson(JsonReader reader, Type objectType, MetricNameWireModel existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        throw new NotImplementedException();
    }

    public static void WriteJsonImpl(JsonWriter jsonWriter, MetricNameWireModel value, JsonSerializer serializer)
    {
        jsonWriter.WriteStartObject();
        jsonWriter.WritePropertyName(PropertyName);
        jsonWriter.WriteValue(value.Name);

        if (!string.IsNullOrEmpty(value.Scope))
        {
            jsonWriter.WritePropertyName(PropertyScope);
            jsonWriter.WriteValue(value.Scope);
        }

        jsonWriter.WriteEndObject();
    }

    public override void WriteJson(JsonWriter jsonWriter, MetricNameWireModel value, JsonSerializer serializer)
    {
        WriteJsonImpl(jsonWriter, value, serializer);
    }
}
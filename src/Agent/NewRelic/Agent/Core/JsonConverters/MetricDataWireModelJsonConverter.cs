// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using NewRelic.Agent.Core.WireModels;
using Newtonsoft.Json;

namespace NewRelic.Agent.Core.JsonConverters;

public class MetricDataWireModelJsonConverter : JsonConverter<MetricDataWireModel>
{
    public override MetricDataWireModel ReadJson(JsonReader reader, Type objectType, MetricDataWireModel existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        throw new NotImplementedException();
    }

    public override void WriteJson(JsonWriter jsonWriter, MetricDataWireModel value, JsonSerializer serializer)
    {
        WriteJsonImpl(jsonWriter, value, serializer);
    }

    public static void WriteJsonImpl(JsonWriter jsonWriter, MetricDataWireModel value, JsonSerializer serializer)
    {
        jsonWriter.WriteStartArray();
        jsonWriter.WriteValue(value.Value0);
        jsonWriter.WriteValue(value.Value1);
        jsonWriter.WriteValue(value.Value2);
        jsonWriter.WriteValue(value.Value3);
        jsonWriter.WriteValue(value.Value4);
        jsonWriter.WriteValue(value.Value5);
        jsonWriter.WriteEndArray();
    }
}
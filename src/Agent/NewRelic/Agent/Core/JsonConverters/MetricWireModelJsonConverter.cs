// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using NewRelic.Agent.Core.WireModels;
using Newtonsoft.Json;

namespace NewRelic.Agent.Core.JsonConverters
{
    public class MetricWireModelJsonConverter : JsonConverter<MetricWireModel>
    {
        public override MetricWireModel ReadJson(JsonReader reader, Type objectType, MetricWireModel existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public static void WriteJsonImpl(JsonWriter jsonWriter, MetricWireModel value, JsonSerializer serializer)
        {
            jsonWriter.WriteStartArray();

            MetricNameWireModelJsonConverter.WriteJsonImpl(jsonWriter, value.MetricNameModel, serializer);

            MetricDataWireModelJsonConverter.WriteJsonImpl(jsonWriter, value.DataModel, serializer);

            jsonWriter.WriteEndArray();
        }

        public override void WriteJson(JsonWriter jsonWriter, MetricWireModel value, JsonSerializer serializer)
        {
            WriteJsonImpl(jsonWriter, value, serializer);
        }
    }
}

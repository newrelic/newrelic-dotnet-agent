// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using NewRelic.Agent.Core.WireModels;
using Newtonsoft.Json;

namespace NewRelic.Agent.Core.JsonConverters;

public class MetricWireModelCollectionJsonConverter : JsonConverter<MetricWireModelCollection>
{
    public override MetricWireModelCollection ReadJson(JsonReader reader, Type objectType, MetricWireModelCollection existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        throw new NotImplementedException();
    }

    public override void WriteJson(JsonWriter jsonWriter, MetricWireModelCollection value, JsonSerializer serializer)
    {
        jsonWriter.WriteValue(value.AgentRunID); // agent id
        jsonWriter.WriteValue(value.StartEpochTime); // start epoch time
        jsonWriter.WriteValue(value.EndEpochTime); // end epoch time

        jsonWriter.WriteStartArray();

        foreach (var metric in value.Metrics)
        {
            MetricWireModelJsonConverter.WriteJsonImpl(jsonWriter, metric, serializer);
        }

        jsonWriter.WriteEndArray();
    }
}
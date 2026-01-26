// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.CrossApplicationTracing;
using Newtonsoft.Json;

namespace NewRelic.Agent.Core.JsonConverters;

public class CrossApplicationRequestDataJsonConverter : JsonConverter<CrossApplicationRequestData>
{
    // The required amount for a valid object is 4, but this object should be returned with less.
    // Checking the validation is handle upstream in CatHeaderHandler.
    private const int TotalProperties = 4;
    private const int MinimumProperties = 0;

    private const int TransactionGuidIndex = 0;
    private const int UnusedIndex = 1;
    private const int TripIdIndex = 2;
    private const int PathHashIndex = 3;

    public override CrossApplicationRequestData ReadJson(JsonReader reader, Type objectType, CrossApplicationRequestData existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        var stringArray = CrossApplicationTracingJsonHelper.ConvertJsonToStringArrayForCat(reader, MinimumProperties, TotalProperties);

        if (stringArray == null)
        {
            return null;
        }

        return new CrossApplicationRequestData(
            stringArray[TransactionGuidIndex],
            bool.Parse(stringArray[UnusedIndex]),
            stringArray[TripIdIndex],
            stringArray[PathHashIndex]
        );
    }

    public override void WriteJson(JsonWriter jsonWriter, CrossApplicationRequestData value, JsonSerializer serializer)
    {
        jsonWriter.WriteStartArray();
        jsonWriter.WriteValue(value.TransactionGuid);
        jsonWriter.WriteValue(value.Unused);
        jsonWriter.WriteValue(value.TripId);
        jsonWriter.WriteValue(value.PathHash);
        jsonWriter.WriteEndArray();
    }
}
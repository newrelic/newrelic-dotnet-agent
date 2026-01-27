// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.CrossApplicationTracing;
using Newtonsoft.Json;

namespace NewRelic.Agent.Core.JsonConverters;

public class CrossApplicationResponseDataJsonConverter : JsonConverter<CrossApplicationResponseData>
{
    // This object doesn't need an IsValid due to the contructors always producing a valid object.
    private const int TotalProperties = 7;
    private const int MinimumProperties = 5;
    private const int CrossProcessIdIndex = 0;
    private const int TransactionNameIndex = 1;
    private const int QueueTimeInSecondsIndex = 2;
    private const int ResponseTimeInSecondsIndex = 3;
    private const int ContentLengthIndex = 4;
    private const int TransactionGuidIndex = 5;
    private const int UnusedIndex = 6;


    public override CrossApplicationResponseData ReadJson(JsonReader reader, Type objectType, CrossApplicationResponseData existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        var stringArray = CrossApplicationTracingJsonHelper.ConvertJsonToStringArrayForCat(reader, MinimumProperties, TotalProperties);

        if (stringArray == null)
        {
            return null;
        }

        return new CrossApplicationResponseData(
            stringArray[CrossProcessIdIndex],
            stringArray[TransactionNameIndex],
            float.Parse(stringArray[QueueTimeInSecondsIndex]),
            float.Parse(stringArray[ResponseTimeInSecondsIndex]),
            long.Parse(stringArray[ContentLengthIndex]),
            stringArray[TransactionGuidIndex],
            bool.Parse(stringArray[UnusedIndex] ?? "false")
        );
    }

    public override void WriteJson(JsonWriter jsonWriter, CrossApplicationResponseData value, JsonSerializer serializer)
    {
        jsonWriter.WriteStartArray();
        jsonWriter.WriteValue(value.CrossProcessId);
        jsonWriter.WriteValue(value.TransactionName);
        jsonWriter.WriteValue(value.QueueTimeInSeconds);
        jsonWriter.WriteValue(value.ResponseTimeInSeconds);
        jsonWriter.WriteValue(value.ContentLength);
        jsonWriter.WriteValue(value.TransactionGuid);
        jsonWriter.WriteValue(value.Unused);
        jsonWriter.WriteEndArray();
    }
}
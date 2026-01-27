// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using NewRelic.Agent.Core.WireModels;
using Newtonsoft.Json;

namespace NewRelic.Agent.Core.JsonConverters;

public class LogEventWireModelCollectionJsonConverter : JsonConverter<LogEventWireModelCollection>
{
    private const string Common = "common";
    private const string Attributes = "attributes";
    private const string EntityName = "entity.name";
    private const string EntityGuid = "entity.guid";
    private const string Hostname = "hostname";
    private const string Logs = "logs";
    private const string TimeStamp = "timestamp";
    private const string Message = "message";
    private const string Level = "level";
    private const string SpanId = "span.id";
    private const string TraceId = "trace.id";
    private const string ErrorStack = "error.stack";
    private const string ErrorMessage = "error.message";
    private const string ErrorClass = "error.class";
    private const string Context = "context";
    private const string LabelPrefix = "tags.";

    public override LogEventWireModelCollection ReadJson(JsonReader reader, Type objectType, LogEventWireModelCollection existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        throw new NotImplementedException();
    }

    public override void WriteJson(JsonWriter jsonWriter, LogEventWireModelCollection value, JsonSerializer serializer)
    {
        WriteJsonImpl(jsonWriter, value);
    }

    private static void WriteJsonImpl(JsonWriter jsonWriter, LogEventWireModelCollection value)
    {
        jsonWriter.WriteStartObject();

        jsonWriter.WritePropertyName(Common);
        jsonWriter.WriteStartObject();
        jsonWriter.WritePropertyName(Attributes);
        jsonWriter.WriteStartObject();
        jsonWriter.WritePropertyName(EntityName);
        jsonWriter.WriteValue(value.EntityName);
        jsonWriter.WritePropertyName(EntityGuid);
        jsonWriter.WriteValue(value.EntityGuid);
        jsonWriter.WritePropertyName(Hostname);
        jsonWriter.WriteValue(value.Hostname);

        if (value.Labels != null)
        {
            foreach (var label in value.Labels)
            {
                jsonWriter.WritePropertyName(LabelPrefix + label.Type);
                jsonWriter.WriteValue(label.Value);
            }
        }

        jsonWriter.WriteEndObject();
        jsonWriter.WriteEndObject();

        jsonWriter.WritePropertyName(Logs);
        jsonWriter.WriteStartArray();
        for (int i = 0; i < value.LoggingEvents.Count; i++)
        {
            var logEvent = value.LoggingEvents[i];
            jsonWriter.WriteStartObject();
            jsonWriter.WritePropertyName(TimeStamp);
            jsonWriter.WriteValue(logEvent.TimeStamp);

            if (!string.IsNullOrEmpty(logEvent.Message))
            {
                jsonWriter.WritePropertyName(Message);
                jsonWriter.WriteValue(logEvent.Message);
            }

            jsonWriter.WritePropertyName(Level);
            jsonWriter.WriteValue(logEvent.Level);

            if (!string.IsNullOrWhiteSpace(logEvent.ErrorStack))
            {
                jsonWriter.WritePropertyName(ErrorStack);
                jsonWriter.WriteValue(logEvent.ErrorStack);
            }

            if (!string.IsNullOrWhiteSpace(logEvent.ErrorMessage))
            {
                jsonWriter.WritePropertyName(ErrorMessage);
                jsonWriter.WriteValue(logEvent.ErrorMessage);
            }

            if (!string.IsNullOrWhiteSpace(logEvent.ErrorClass))
            {
                jsonWriter.WritePropertyName(ErrorClass);
                jsonWriter.WriteValue(logEvent.ErrorClass);
            }

            if (!string.IsNullOrWhiteSpace(logEvent.SpanId))
            {
                jsonWriter.WritePropertyName(SpanId);
                jsonWriter.WriteValue(logEvent.SpanId);
            }

            if (!string.IsNullOrWhiteSpace(logEvent.TraceId))
            {
                jsonWriter.WritePropertyName(TraceId);
                jsonWriter.WriteValue(logEvent.TraceId);
            }

            if (logEvent.ContextData?.Count > 0)
            {
                jsonWriter.WritePropertyName(Attributes);
                jsonWriter.WriteStartObject();

                foreach (var item in logEvent.ContextData)
                {
                    jsonWriter.WritePropertyName(Context + "." + item.Key);
                    string contextValueJson;
                    try
                    {
                        contextValueJson = JsonConvert.SerializeObject(item.Value);
                    }
                    catch
                    {
                        // If JsonConvert can't serialize it, maybe it has a ToString()
                        try
                        {
                            contextValueJson = item.Value.ToString();
                        }
                        catch
                        {
                            // If that didn't work, just use the type name
                            contextValueJson = item.Value.GetType().ToString();
                        }
                    }
                    jsonWriter.WriteRawValue(contextValueJson);
                }

                jsonWriter.WriteEndObject();
            }

            jsonWriter.WriteEndObject();
        }

        jsonWriter.WriteEndArray();
        jsonWriter.WriteEndObject();
    }
}
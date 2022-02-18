// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using NewRelic.Agent.Core.WireModels;
using Newtonsoft.Json;

namespace NewRelic.Agent.Core.JsonConverters
{
    public class LogEventWireModelCollectionJsonConverter : JsonConverter<LogEventWireModelCollection>
    {
        private const string Common = "common";
        private const string Attributes = "attributes";
        private const string EntityGuid = "entity.guid";
        private const string Hostname = "hostname";
        private const string Logs = "logs";
        private const string TimeStamp = "timestamp";
        private const string Message = "message";
        private const string Level = "level";
        private const string SpanId = "spanid";
        private const string TraceId = "traceid";

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
            jsonWriter.WritePropertyName(EntityGuid);
            jsonWriter.WriteValue(value.EntityGuid);
            jsonWriter.WritePropertyName(Hostname);
            jsonWriter.WriteValue(value.Hostname);
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
                jsonWriter.WritePropertyName(Message);
                jsonWriter.WriteValue(logEvent.Message);
                jsonWriter.WritePropertyName(Level);
                jsonWriter.WriteValue(logEvent.Level);

                jsonWriter.WritePropertyName(Attributes);
                jsonWriter.WriteStartObject();

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

                jsonWriter.WriteEndObject();
                jsonWriter.WriteEndObject();
            }

            jsonWriter.WriteEndArray();
            jsonWriter.WriteEndObject();
        }
    }
}

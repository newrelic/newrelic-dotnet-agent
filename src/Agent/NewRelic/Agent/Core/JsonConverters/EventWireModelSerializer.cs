// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using Newtonsoft.Json;
using NewRelic.Agent.Core.Attributes;
using System;
using NewRelic.Agent.Core.WireModels;
using NewRelic.Agent.Core.Segments;

namespace NewRelic.Agent.Core.JsonConverters
{
    public class EventWireModelSerializer : JsonConverter<IEventWireModel>
    {
        public override IEventWireModel ReadJson(JsonReader reader, Type objectType, IEventWireModel existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override void WriteJson(JsonWriter writer, IEventWireModel value, JsonSerializer serializer)
        {
            writer.WriteStartArray();
            JsonSerializerHelpers.WriteCollection(writer, value.AttributeValues.GetAttributeValues(AttributeClassification.Intrinsics));
            JsonSerializerHelpers.WriteCollection(writer, value.AttributeValues.GetAttributeValues(AttributeClassification.UserAttributes));
            JsonSerializerHelpers.WriteCollection(writer, value.AttributeValues.GetAttributeValues(AttributeClassification.AgentAttributes));
            writer.WriteEndArray();
        }
    }

    public class SpanEventWireModelSerializer : JsonConverter<ISpanEventWireModel>
    {
        public override ISpanEventWireModel ReadJson(JsonReader reader, Type objectType, ISpanEventWireModel existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override void WriteJson(JsonWriter writer, ISpanEventWireModel value, JsonSerializer serializer)
        {
            writer.WriteStartArray();
            JsonSerializerHelpers.WriteCollection(writer, value.GetAttributeValues(AttributeClassification.Intrinsics));
            JsonSerializerHelpers.WriteCollection(writer, value.GetAttributeValues(AttributeClassification.UserAttributes));
            JsonSerializerHelpers.WriteCollection(writer, value.GetAttributeValues(AttributeClassification.AgentAttributes));
            writer.WriteEndArray();

            foreach (var link in value.Span.Links)
            {
                writer.WriteStartArray();
                JsonSerializerHelpers.WriteCollection(writer, link.AttributeValues.GetAttributeValues(AttributeClassification.Intrinsics));
                JsonSerializerHelpers.WriteCollection(writer, link.AttributeValues.GetAttributeValues(AttributeClassification.UserAttributes));
                JsonSerializerHelpers.WriteCollection(writer, link.AttributeValues.GetAttributeValues(AttributeClassification.AgentAttributes));
                writer.WriteEndArray();
            }

            foreach (var evt in value.Span.Events)
            {
                writer.WriteStartArray();
                JsonSerializerHelpers.WriteCollection(writer, evt.AttributeValues.GetAttributeValues(AttributeClassification.Intrinsics));
                JsonSerializerHelpers.WriteCollection(writer, evt.AttributeValues.GetAttributeValues(AttributeClassification.UserAttributes));
                JsonSerializerHelpers.WriteCollection(writer, evt.AttributeValues.GetAttributeValues(AttributeClassification.AgentAttributes));
                writer.WriteEndArray();
            }
        }
    }
}

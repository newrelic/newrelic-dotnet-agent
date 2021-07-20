// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using System.Diagnostics;
using NewRelic.Agent.Core.Configuration;
using NewRelic.Agent.Core.Utils;
using Newtonsoft.Json;

namespace NewRelic.Agent.Core.JsonConverters
{
    public class EventAttributesJsonConverter : JsonConverter<IEnumerable<KeyValuePair<string, object>>>
    {
        private static ConfigurationSubscriber _configurationSubscriber = new ConfigurationSubscriber();

        public override IEnumerable<KeyValuePair<string, object>> ReadJson(JsonReader reader, Type objectType, IEnumerable<KeyValuePair<string, object>> existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException("Deserialization of IDictionary<string,object> is not supported");
        }

        public override void WriteJson(JsonWriter writer, IEnumerable<KeyValuePair<string, object>> value, JsonSerializer serializer)
        {
            writer.WriteStartObject();
            foreach (var kvp in value)
            {
                if (kvp.Value == null)
                {
                    continue;
                }

                writer.WritePropertyName(kvp.Key);

                if (kvp.Value is string)
                {
                    writer.WriteValue((string)kvp.Value);
                }
                else if (kvp.Value is long)
                {
                    writer.WriteValue((long)kvp.Value);
                }
                else if (kvp.Value is int)
                {
                    writer.WriteValue((int)kvp.Value);
                }
                else if (kvp.Value is bool)
                {
                    writer.WriteValue((bool)kvp.Value);
                }
                else if (kvp.Value is double)
                {
                    writer.WriteValue((double)kvp.Value);
                }
                else if (kvp.Value is float)
                {
                    writer.WriteValue((float)kvp.Value);
                }
                else if (kvp.Value is decimal)
                {
                    writer.WriteValue((decimal)kvp.Value);
                }
                else if (kvp.Value is char)
                {
                    writer.WriteValue((char)kvp.Value);
                }
                else if (kvp.Value is ushort)
                {
                    writer.WriteValue((ushort)kvp.Value);
                }
                else if (kvp.Value is uint)
                {
                    writer.WriteValue((uint)kvp.Value);
                }
                else if (kvp.Value is ulong)
                {
                    writer.WriteValue((ulong)kvp.Value);
                }
                else if (kvp.Value is short)
                {
                    writer.WriteValue((short)kvp.Value);
                }
                else if (kvp.Value is sbyte)
                {
                    writer.WriteValue((sbyte)kvp.Value);
                }
                else if (kvp.Value is byte)
                {
                    writer.WriteValue((byte)kvp.Value);
                }
                else
                {
                    writer.WriteValue(kvp.Value);
                }
            }

            writer.WriteEndObject();
        }
    }
}

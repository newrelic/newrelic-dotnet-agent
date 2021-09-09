// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using Newtonsoft.Json;
using NewRelic.Agent.Core.Attributes;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Core.Logging;

namespace NewRelic.Agent.Core.JsonConverters
{
    public static class JsonSerializerHelpers
    {
        public static void WriteCollection(JsonWriter writer, IEnumerable<IAttributeValue> attribValues)
        {
            writer.WriteStartObject();
            if (attribValues != null)
            {
                foreach (var attribVal in attribValues.OrderBy(x => x.AttributeDefinition.Name))
                {
                    //this performs the lazy function (if necessary)
                    //which can result in a null value
                    var outputValue = attribVal.Value;

                    if (outputValue == null)
                    {
                        continue;
                    }

                    writer.WritePropertyName(attribVal.AttributeDefinition.Name);

                    // Causes an exception since this type is unsupported by the JsonConverter
                    if (outputValue.GetType().ToString() == "Microsoft.Extensions.Primitives.StringValues")
                    {
                        writer.WriteValue(outputValue.ToString());
                    }
                    else
                    {
                        WriteValueSafe(writer, attribVal.AttributeDefinition.Name, outputValue);
                    }
                }
            }

            writer.WriteEndObject();
        }

        public static void WriteObjectCollection(JsonWriter writer, IEnumerable<KeyValuePair<string, object>> collection)
        {
            writer.WriteStartObject();
            foreach (var kvp in collection)
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
                    WriteValueSafe(writer, kvp.Key, kvp.Value);
                }
            }

            writer.WriteEndObject();
        }

        public static void WriteValueSafe(JsonWriter writer, string key, object value)
        {
            try
            {
                writer.WriteValue(value);
            }
            catch (JsonWriterException exception)
            {
                var type = value.GetType().FullName;

                writer.WriteValue($"Unable to serialize type {type}");
                Log.Warn($"Failed to serialize property {key} of type {type}: {exception.Message}");
            }
        }
    }
}

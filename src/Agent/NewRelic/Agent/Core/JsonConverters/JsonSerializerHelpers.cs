// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.Core.Attributes;
using NewRelic.Agent.Extensions.Logging;
using Newtonsoft.Json;

namespace NewRelic.Agent.Core.JsonConverters;

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

                WriteJsonKeyAndValue(writer, attribVal.AttributeDefinition.Name, outputValue);                    }
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

            WriteJsonKeyAndValue(writer, kvp.Key, kvp.Value);
        }

        writer.WriteEndObject();
    }

    private static void WriteJsonKeyAndValue(JsonWriter writer, string key, object value)
    {
        writer.WritePropertyName(key);

        switch (value)
        {
            case string _:
                writer.WriteValue((string)value);
                break;
            case long _:
                writer.WriteValue((long)value);
                break;
            case int _:
                writer.WriteValue((int)value);
                break;
            case bool _:
                writer.WriteValue((bool)value);
                break;
            case double _:
                writer.WriteValue((double)value);
                break;
            case float _:
                writer.WriteValue((float)value);
                break;
            case decimal _:
                writer.WriteValue((decimal)value);
                break;
            case char _:
                writer.WriteValue((char)value);
                break;
            case ushort _:
                writer.WriteValue((ushort)value);
                break;
            case uint _:
                writer.WriteValue((uint)value);
                break;
            case ulong _:
                writer.WriteValue((ulong)value);
                break;
            case short _:
                writer.WriteValue((short)value);
                break;
            case sbyte _:
                writer.WriteValue((sbyte)value);
                break;
            case byte _:
                writer.WriteValue((byte)value);
                break;
            default:
                try
                {
                    writer.WriteValue(value);
                }
                catch (JsonWriterException)
                {
                    writer.WriteValue(value.ToString());

                    var type = value.GetType().FullName;

                    Log.Debug($"Unable to properly serialize property {key} of type {type}. The agent will use the value from calling ToString() on the object of {type} type.");
                }
                break;
        }
    }
}

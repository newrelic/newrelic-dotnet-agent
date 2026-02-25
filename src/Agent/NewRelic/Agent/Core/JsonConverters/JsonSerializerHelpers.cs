// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections;
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

                if (ShouldSkipValue(outputValue))
                {
                    continue;
                }

                WriteJsonKeyAndValue(writer, attribVal.AttributeDefinition.Name, outputValue);
            }
        }

        writer.WriteEndObject();
    }

    public static void WriteObjectCollection(JsonWriter writer, IEnumerable<KeyValuePair<string, object>> collection)
    {
        writer.WriteStartObject();
        foreach (var kvp in collection)
        {
            if (ShouldSkipValue(kvp.Value))
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
        WriteValue(writer, value, key);
    }

    private static bool ShouldSkipValue(object value)
    {
        // Skip null values and collections that only contain null values. This prevents us from sending empty arrays to New Relic,
        // which can cause issues with some of our backend processing.
        return value == null || value is IEnumerable enumerable and not string &&
            enumerable.Cast<object>().All(element => element == null);
    }

    private static void WriteValue(JsonWriter writer, object value, string contextKey = null)
    {
        switch (value)
        {
            case string _:
                writer.WriteValue(value);
                break;
            case long _:
                writer.WriteValue(value);
                break;
            case int _:
                writer.WriteValue(value);
                break;
            case bool _:
                writer.WriteValue(value);
                break;
            case double _:
                writer.WriteValue(value);
                break;
            case float _:
                writer.WriteValue(value);
                break;
            case decimal _:
                writer.WriteValue(value);
                break;
            case char _:
                writer.WriteValue(value);
                break;
            case ushort _:
                writer.WriteValue(value);
                break;
            case uint _:
                writer.WriteValue(value);
                break;
            case ulong _:
                writer.WriteValue(value);
                break;
            case short _:
                writer.WriteValue(value);
                break;
            case sbyte _:
                writer.WriteValue(value);
                break;
            case byte _:
                writer.WriteValue(value);
                break;
            case IEnumerable enumerable:
                writer.WriteStartArray();
                foreach (var element in enumerable)
                {
                    if (element != null)
                    {
                        WriteValue(writer, element);
                    }
                }
                writer.WriteEndArray();
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
                    var context = contextKey != null ? $"property {contextKey}" : "array element";

                    Log.Debug($"Unable to properly serialize {context} of type {type}. The agent will use the value from calling ToString() on the object of {type} type.");
                }
                break;
        }
    }
}

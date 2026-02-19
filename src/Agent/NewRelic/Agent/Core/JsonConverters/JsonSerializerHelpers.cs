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
        if (value == null)
        {
            return true;
        }

        if (value is IEnumerable enumerable && !(value is string))
        {
            // Check if the enumerable is empty or contains only nulls
            foreach (var element in enumerable)
            {
                if (element != null)
                {
                    return false; // Found a non-null element, don't skip
                }
            }
            return true; // Empty or all nulls, skip it
        }

        return false;
    }

    private static void WriteValue(JsonWriter writer, object value, string contextKey = null)
    {
        switch (value)
        {
            case string s:
                writer.WriteValue(s);
                break;
            case long l:
                writer.WriteValue(l);
                break;
            case int i:
                writer.WriteValue(i);
                break;
            case bool b:
                writer.WriteValue(b);
                break;
            case double d:
                writer.WriteValue(d);
                break;
            case float f:
                writer.WriteValue(f);
                break;
            case decimal value1:
                writer.WriteValue(value1);
                break;
            case char c:
                writer.WriteValue(c);
                break;
            case ushort value1:
                writer.WriteValue(value1);
                break;
            case uint u:
                writer.WriteValue(u);
                break;
            case ulong value1:
                writer.WriteValue(value1);
                break;
            case short s:
                writer.WriteValue(s);
                break;
            case sbyte value1:
                writer.WriteValue(value1);
                break;
            case byte b:
                writer.WriteValue(b);
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

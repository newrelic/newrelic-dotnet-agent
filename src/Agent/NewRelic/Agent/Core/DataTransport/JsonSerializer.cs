// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Linq;
using System.Runtime.Serialization;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.Collections.Generic;

namespace NewRelic.Agent.Core.DataTransport
{
    public class JsonSerializer : ISerializer
    {
        static JsonSerializer()
        {
            // Set global defaults to use our custom settings, which include the
            // EnumMemberValueStringEnumConverter and ignore null values.
            //
            // This ensures that ALL serialization and deserialization operations
            // (whether using JsonSerializer or JsonConvert.SerializeObject, etc.)
            // in the application will use these settings unless explicitly overridden.
            JsonConvert.DefaultSettings = () => _settings;
        }

        private static readonly JsonSerializerSettings _settings = new()
        {
            Converters = new List<JsonConverter>
            {
                new EnumMemberValueStringEnumConverter() // custom converter
            },
            NullValueHandling = NullValueHandling.Ignore
        };

        public string Serialize(object[] parameters)
        {
            return JsonConvert.SerializeObject(parameters);
        }

        public T Deserialize<T>(string responseBody)
        {
            return JsonConvert.DeserializeObject<T>(responseBody);
        }
    }

#nullable enable

    /// <summary>
    /// Provides a JSON converter for enum types that serializes and deserializes values using the EnumMemberAttribute
    /// value when present, or falls back to the enum name or underlying numeric value as appropriate.
    /// </summary>
    /// <remarks>This converter enables compatibility with enums decorated with EnumMemberAttribute, commonly
    /// used for customizing serialized values in APIs and data contracts. When serializing, if an EnumMemberAttribute
    /// is present and has a non-empty value, that value is written; otherwise, the enum name or numeric value is used.
    /// When deserializing, the converter matches incoming JSON strings to EnumMemberAttribute values or enum names, and
    /// supports numeric values as well. This is useful for scenarios where interoperability with external systems or
    /// contracts requires specific string representations for enum values.</remarks>
    public class EnumMemberValueStringEnumConverter : Newtonsoft.Json.JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            var t = Nullable.GetUnderlyingType(objectType) ?? objectType;
            return t.IsEnum;
        }

        // Match base class nullability (object?) for value parameter
        public override void WriteJson(Newtonsoft.Json.JsonWriter writer, object? value, Newtonsoft.Json.JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteNull();
                return;
            }

            var enumType = value.GetType();
            var name = Enum.GetName(enumType, value);
            if (name != null)
            {
                var field = enumType.GetField(name);
                if (field != null)
                {
                    var enumMemberAttr = field.GetCustomAttributes(typeof(EnumMemberAttribute), false)
                      .Cast<EnumMemberAttribute>()
                      .FirstOrDefault();
                    if (enumMemberAttr != null && !string.IsNullOrEmpty(enumMemberAttr.Value))
                    {
                        writer.WriteValue(enumMemberAttr.Value);
                        return;
                    }
                }
            }
            // Default behavior: serialize numeric underlying value
            var underlying = Convert.ChangeType(value, Enum.GetUnderlyingType(enumType));
            writer.WriteValue(underlying);
        }

        // Match base class nullability (object?) for existingValue parameter and return object?
        public override object? ReadJson(Newtonsoft.Json.JsonReader reader, Type objectType, object? existingValue, Newtonsoft.Json.JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
            {
                if (!IsNullableType(objectType))
                    throw new JsonSerializationException("Cannot convert null value to enum.");
                return null;
            }

            var enumType = Nullable.GetUnderlyingType(objectType) ?? objectType;

            if (reader.TokenType == JsonToken.String)
            {
                var stringValue = reader.Value?.ToString() ?? string.Empty;
                // Match EnumMemberAttribute values first
                foreach (var field in enumType.GetFields(BindingFlags.Public | BindingFlags.Static))
                {
                    var enumMemberAttr = field.GetCustomAttributes(typeof(EnumMemberAttribute), false)
                      .Cast<EnumMemberAttribute>()
                      .FirstOrDefault();
                    if (enumMemberAttr != null && !string.IsNullOrEmpty(enumMemberAttr.Value) &&
                        string.Equals(enumMemberAttr.Value, stringValue, StringComparison.OrdinalIgnoreCase))
                    {
                        return Enum.Parse(enumType, field.Name);
                    }
                }
                // Fallback to parsing by name
                return Enum.Parse(enumType, stringValue, ignoreCase: true);
            }
            if (reader.TokenType == JsonToken.Integer)
            {
                return Enum.ToObject(enumType, reader.Value!);
            }

            throw new JsonSerializationException($"Unexpected token {reader.TokenType} when parsing enum.");
        }

        private static bool IsNullableType(Type t) => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Nullable<>);
    }
#nullable restore
}

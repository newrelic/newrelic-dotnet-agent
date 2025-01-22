// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections;
using System.Reflection;
using System.Text;

namespace NewRelic.Agent.Extensions.Helpers
{
    public static class ObjectHelper
    {
        public static string GetObjectAsString(object obj)
        {
            if (obj == null) return "null";

            var stringBuilder = new StringBuilder();
            TraverseObject(obj, stringBuilder, string.Empty, 0);
            return stringBuilder.ToString();
        }

        private static void TraverseObject(object obj, StringBuilder stringBuilder, string prefix, int indentLevel)
        {
            if (obj == null)
            {
                stringBuilder.AppendLine("null");
                return;
            }

            try
            {
                if (obj is object[] objArray)
                {
                    int index = 0;
                    foreach (var item in objArray)
                    {
                        var itemIndent = new string(' ', indentLevel * 2);
                        if (IsSimpleType(item.GetType()))
                        {
                            stringBuilder.AppendLine($"{itemIndent}{prefix}[{index}]: {item}");
                        }
                        else
                        {
                            stringBuilder.AppendLine($"{itemIndent}{prefix}[{index}]:");
                            TraverseObject(item, stringBuilder, $"{prefix}[{index}]", indentLevel + 1);
                        }
                        index++;
                    }
                    return;
                }

                var type = obj.GetType();
                var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);

                foreach (var property in properties)
                {
                    var propertyName = string.IsNullOrEmpty(prefix) ? property.Name : $"{prefix}.{property.Name}";
                    var indent = new string(' ', indentLevel * 2);

                    if (property.GetIndexParameters().Length > 0)
                    {
                        // Skip properties with index parameters
                        continue;
                    }

                    var value = property.GetValue(obj, null) ?? "null";

                    if (value is string stringValue && string.IsNullOrWhiteSpace(stringValue))
                    {
                        value = "<empty>";
                    }

                    if (propertyName.ToLower().Contains("license") || propertyName.ToLower().Contains("key") ||
                        propertyName.ToLower().Contains("accountid"))
                    {
                        value = ObfuscateValue(value?.ToString());
                    }

                    if (value == null || IsSimpleType(property.PropertyType))
                    {
                        stringBuilder.AppendLine($"{indent}{propertyName}: {value ?? "null"}");
                    }
                    else if (typeof(IEnumerable).IsAssignableFrom(property.PropertyType) &&
                             property.PropertyType != typeof(string))
                    {
                        if (value is IEnumerable enumerable)
                        {
                            int index = 0;
                            foreach (var item in enumerable)
                            {
                                var itemVal = item ?? "null";
                                if (itemVal is string itemString && string.IsNullOrWhiteSpace(itemString))
                                {
                                    itemVal = "<empty>";
                                }

                                var itemIndent = new string(' ', (indentLevel + 1) * 2);
                                if (IsSimpleType(item.GetType()))
                                {
                                    stringBuilder.AppendLine($"{itemIndent}{propertyName}[{index}]: {itemVal}");
                                }
                                else
                                {
                                    stringBuilder.AppendLine($"{itemIndent}{propertyName}[{index}]:");
                                    TraverseObject(item, stringBuilder, $"{propertyName}[{index}]", indentLevel + 2);
                                }
                                index++;
                            }
                        }
                    }
                    else
                    {
                        stringBuilder.AppendLine($"{indent}{propertyName}:");
                        TraverseObject(value, stringBuilder, propertyName, indentLevel + 1);
                    }
                }

                foreach (var field in fields)
                {
                    var fieldName = string.IsNullOrEmpty(prefix) ? field.Name : $"{prefix}.{field.Name}";
                    var indent = new string(' ', indentLevel * 2);

                    var value = field.GetValue(obj) ?? "null";

                    if (value is string stringValue && string.IsNullOrWhiteSpace(stringValue))
                    {
                        value = "<empty>";
                    }

                    if (fieldName.ToLower().Contains("license") || fieldName.ToLower().Contains("key") ||
                        fieldName.ToLower().Contains("accountid"))
                    {
                        value = ObfuscateValue(value.ToString());
                    }

                    if (value == null || IsSimpleType(field.FieldType))
                    {
                        stringBuilder.AppendLine($"{indent}{fieldName}: {value ?? "null"}");
                    }
                    else if (typeof(IEnumerable).IsAssignableFrom(field.FieldType) &&
                             field.FieldType != typeof(string))
                    {
                        if (value is IEnumerable enumerable)
                        {
                            int index = 0;
                            foreach (var item in enumerable)
                            {
                                var itemVal = item ?? "null";
                                if (itemVal is string itemString && string.IsNullOrWhiteSpace(itemString))
                                {
                                    itemVal = "<empty>";
                                }

                                var itemIndent = new string(' ', (indentLevel + 1) * 2);
                                if (IsSimpleType(item.GetType()))
                                {
                                    stringBuilder.AppendLine($"{itemIndent}{fieldName}[{index}]: {itemVal}");
                                }
                                else
                                {
                                    stringBuilder.AppendLine($"{itemIndent}{fieldName}[{index}]:");
                                    TraverseObject(item, stringBuilder, $"{fieldName}[{index}]", indentLevel + 2);
                                }
                                index++;
                            }
                        }
                    }
                    else
                    {
                        stringBuilder.AppendLine($"{indent}{fieldName}:");
                        TraverseObject(value, stringBuilder, fieldName, indentLevel + 1);
                    }
                }
            }
            catch (Exception e)
            {
                stringBuilder.AppendLine($"Error: {e.Message} {e.StackTrace}");
            }
        }

        private static string ObfuscateValue(string value)
        {
            if (string.IsNullOrEmpty(value) || value == "null") return value; // no need to obfuscate null values
            return new string('*', value.Length);
        }

        private static bool IsSimpleType(Type type)
        {
            return type.IsPrimitive
                   || type.IsEnum
                   || type == typeof(string)
                   || type == typeof(decimal)
                   || type == typeof(DateTime)
                   || type == typeof(DateTimeOffset)
                   || type == typeof(TimeSpan)
                   || type == typeof(Guid);
        }
    }
}

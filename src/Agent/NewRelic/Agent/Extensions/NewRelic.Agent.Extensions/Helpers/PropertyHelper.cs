// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections;
using System.Reflection;
using System.Text;

namespace NewRelic.Agent.Extensions.Helpers
{
    public static class PropertyHelper
    {
        public static string GetPropertiesAsString(object obj)
        {
            if (obj == null) throw new ArgumentNullException(nameof(obj));

            var stringBuilder = new StringBuilder();
            AppendProperties(obj, stringBuilder, string.Empty, 0);
            return stringBuilder.ToString();
        }

        private static void AppendProperties(object obj, StringBuilder stringBuilder, string prefix, int indentLevel)
        {
            if (obj == null) return;

            var type = obj.GetType();
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            foreach (var property in properties)
            {
                var value = property.GetValue(obj, null);
                var propertyName = string.IsNullOrEmpty(prefix) ? property.Name : $"{prefix}.{property.Name}";
                var indent = new string(' ', indentLevel * 2);

                if (value == null || IsSimpleType(property.PropertyType))
                {
                    stringBuilder.AppendLine($"{indent}{propertyName}: {value}");
                }
                else if (typeof(IEnumerable).IsAssignableFrom(property.PropertyType) && property.PropertyType != typeof(string))
                {
                    if (value is IEnumerable enumerable)
                    {
                        int index = 0;
                        foreach (var item in enumerable)
                        {
                            AppendProperties(item, stringBuilder, $"{propertyName}[{index}]", indentLevel + 1);
                            index++;
                        }
                    }
                }
                else
                {
                    stringBuilder.AppendLine($"{indent}{propertyName}:");
                    AppendProperties(value, stringBuilder, propertyName, indentLevel + 1);
                }
            }
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

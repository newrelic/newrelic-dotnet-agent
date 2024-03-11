// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Core.Utilities;
using NewRelic.Core;
using NewRelic.Core.Caching;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace NewRelic.Agent.Core.JsonConverters
{
    public class JsonArrayConverter : JsonConverter
    {
        private static SimpleCache<Type, MemberInfo[]> _memberInfoCache = new SimpleCache<Type, MemberInfo[]>(500);

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value == null)
                return;

            // Write out the type as an array of values ordered by JSON index
            var values = GetJsonMemberValuesOrderedByIndex(value);
            serializer.Serialize(writer, values);
        }

        public override object ReadJson(JsonReader reader, Type type, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return null;

            // Load JSON
            var jArray = JArray.Load(reader);
            if (jArray == null)
                throw new JsonSerializationException("Failed to load JSON into JArray");

            // Find all JsonArrayIndex members in object
            var jsonInfos = GetJsonMemberInfosOrderedByIndex(type, null);

            // Return an object that represents the deserialized JSON
            return TryParameterizedConstruct(type, jArray, jsonInfos) ?? DefaultConstruct(type, jArray, jsonInfos);
        }

        private static object TryParameterizedConstruct(Type type, IList<JToken> jArray, IList<JsonMemberInfo> orderedJsonMemberInfos)
        {
            // If the source JSON array has a larger number of values than the number of members with JsonArrayIndexAttribute then it is unlikely that we'll be able to find an appropriate constructor
            if (jArray.Count > orderedJsonMemberInfos.Count)
                return null;

            orderedJsonMemberInfos = orderedJsonMemberInfos.Take(jArray.Count).ToList();

            // Try to find a constructor that has the same number and type of parameters as the members 
            var jsonArrayMemberTypes = orderedJsonMemberInfos.Select(info => info.MemberType).ToArray();
            var constructor = type.GetConstructor(jsonArrayMemberTypes);
            if (constructor == null)
                return null;

            // Try to construct an object with the parameterized constructor that we found
            var jArrayValues = orderedJsonMemberInfos.Select(info => MemberJsonInfoToObject(jArray, info)).ToArray();
            return TryConstruct(constructor, jArrayValues);
        }

        private static object MemberJsonInfoToObject(IList<JToken> jArray, JsonMemberInfo info)
        {
            if (info.Index >= jArray.Count)
                return null;

            var jToken = jArray[(int)info.Index];
            if (jToken == null)
                throw new JsonSerializationException(string.Format("No JToken found at index {0}", info.Index));

            return jToken.ToObject(info.MemberType);
        }

        private static object DefaultConstruct(Type type, IList<JToken> jArray, IList<JsonMemberInfo> orderedJsonMemberInfos)
        {
            // Find a default constructor
            var constructor = type.GetConstructor(Type.EmptyTypes);
            if (constructor == null)
                throw new JsonSerializationException(string.Format("Failed to deserialize object of type {0} -- type does not have public parameterless constructor", type.Name));

            // Invoke the default constructor
            object deserializedObject = TryConstruct(constructor, null);
            if (deserializedObject == null)
                throw new JsonSerializationException(string.Format("Failed to deserialize object of type {0} -- invoking public parameterless constructor returned null", type.Name));

            // Manually set the values of each JSON member
            for (var index = 0; index < jArray.Count; index++)
            {
                TrySetValue(deserializedObject, orderedJsonMemberInfos, jArray, index);
            }

            return deserializedObject;
        }

        private static object TryConstruct(ConstructorInfo constructor, object[] parameterValues)
        {
            try
            {
                return constructor.Invoke(parameterValues);
            }
            catch
            {
                return null;
            }
        }

        public override bool CanConvert(Type objectType) { throw new NotImplementedException(); }

        private static IList<object> GetJsonMemberValuesOrderedByIndex(object value)
        {
            // Find all JsonArrayIndex members in object
            var jsonInfos = GetJsonMemberInfosOrderedByIndex(value.GetType(), value);

            return jsonInfos.Select(info => info.ExistingValue).ToList();
        }

        private static IList<JsonMemberInfo> GetJsonMemberInfosOrderedByIndex(Type type, object instance)
        {
            var memberInfos = _memberInfoCache.GetOrAdd(type, () =>
            {
                var members = type.GetMembers(BindingFlags.Public | BindingFlags.Instance)
                .Where(x => x is FieldInfo || x is PropertyInfo)
                .Where(x => x.IsDefined(typeof(JsonArrayIndexAttribute), true))
                .Select(x => new KeyValuePair<uint, MemberInfo>(x.GetCustomAttribute<JsonArrayIndexAttribute>(true).Index, x))
                .OrderBy(x => x.Key)
                .ToArray();

                //Check that there are members to serialize
                if (members.Length == 0)
                {
                    throw new JsonSerializationException($"Failed to serialize object of type {type.FullName} -- object has no members marked with JsonArrayIndexAttribute");
                }

                //Indexing starts at 0
                if (members[0].Key != 0)
                {
                    throw new JsonSerializationException($"Failed to serialize object of type {type.FullName} -- no field or property found for index 0");
                }

                //Indexes are sequential
                if (!members.IsSequential(x => x.Key))
                {
                    throw new JsonSerializationException($"Failed to serialize object of type {type.FullName} -- JsonArrayIndex sequence is missing a value in the middle of the sequence");
                }

                //Check for duplicate index values
                var duplicateIndexGroup = members
                .GroupBy(x => x.Key)
                .FirstOrDefault(group => group != null && group.Count() > 1);

                if (duplicateIndexGroup != null)
                {
                    throw new JsonSerializationException($"Failed to read serialization info for object of type {type.FullName} -- index {duplicateIndexGroup.First().Key} is specified multiple times");
                }

                return members.Select(x => x.Value).ToArray();
            });

            var jsonMemberInfos = memberInfos.Select(x =>
            {
                return x is PropertyInfo
                ? TryGetJsonMemberInfo(x as PropertyInfo, instance)
                : TryGetJsonMemberInfo(x as FieldInfo, instance);
            })
            .ToList();

            return jsonMemberInfos;
        }

        public static object SwapInDateTimeAsUnixTimeSeconds(MemberInfo member, object value)
        {
            var dateTime = (DateTime)value;
            return dateTime.ToUnixTimeSeconds();
        }

        public static object SwapInDateTimeAsUnixTimeMilliseconds(MemberInfo member, object value)
        {
            var dateTime = (DateTime)value;
            return dateTime.ToUnixTimeMilliseconds();
        }

        public static object SwapInTimeSpanAsMilliseconds(MemberInfo member, object value)
        {
            var timeSpan = (TimeSpan)value;
            return timeSpan.TotalMilliseconds;
        }

        public static object SwapInTimeSpanAsSeconds(MemberInfo member, object value)
        {
            var timeSpan = (TimeSpan)value;
            return timeSpan.TotalSeconds;
        }

        private static JsonMemberInfo TryGetJsonMemberInfo(FieldInfo fieldInfo, object instance)
        {
            if (fieldInfo == null)
                return null;

            var valueGetter = instance == null ? (Func<object>)null : () => fieldInfo.GetValue(instance);
            Action<object, object> valueSetter = fieldInfo.SetValue;

            return TryGetJsonMemberInfo(fieldInfo, valueGetter, fieldInfo.FieldType, valueSetter);
        }

        private static JsonMemberInfo TryGetJsonMemberInfo(PropertyInfo propertyInfo, object instance)
        {
            if (propertyInfo == null)
                return null;

            var valueGetter = instance == null ? (Func<object>)null : () => propertyInfo.GetValue(instance, null);
            Action<object, object> valueSetter = (targetObject, value) => propertyInfo.SetValue(targetObject, value, null);

            return TryGetJsonMemberInfo(propertyInfo, valueGetter, propertyInfo.PropertyType, valueSetter);
        }

        private static JsonMemberInfo TryGetJsonMemberInfo(MemberInfo memberInfo, Func<object> getValue, Type type, Action<object, object> valueSetter)
        {
            if (memberInfo == null)
                return null;
            if (type == null)
                return null;
            if (valueSetter == null)
                return null;


            var attribute = memberInfo.GetCustomAttribute<JsonArrayIndexAttribute>(true);

            if (attribute == null)
            {
                return null;
            }

            var index = attribute.Index;

            var existingValue = getValue == null ? null : getValue();

            if (existingValue is DateTime)
            {
                if (Attribute.IsDefined(memberInfo, typeof(DateTimeSerializesAsUnixTimeSecondsAttribute)))
                {
                    existingValue = SwapInDateTimeAsUnixTimeSeconds(memberInfo, existingValue);
                }
                else if (Attribute.IsDefined(memberInfo, typeof(DateTimeSerializesAsUnixTimeMillisecondsAttribute)))
                {
                    existingValue = SwapInDateTimeAsUnixTimeMilliseconds(memberInfo, existingValue);
                }
            }
            else if (existingValue is TimeSpan)
            {
                if (Attribute.IsDefined(memberInfo, typeof(TimeSpanSerializesAsMillisecondsAttribute)))
                {
                    existingValue = SwapInTimeSpanAsMilliseconds(memberInfo, existingValue);
                }
                else if (Attribute.IsDefined(memberInfo, typeof(TimeSpanSerializesAsSecondsAttribute)))
                {
                    existingValue = SwapInTimeSpanAsSeconds(memberInfo, existingValue);
                }
            }

            return new JsonMemberInfo(index, existingValue, memberInfo, type, valueSetter);
        }

        private static void TrySetValue(object targetObject, IEnumerable<JsonMemberInfo> jsonInfos, IList<JToken> jTokens, int tokenIndex)
        {
            if (targetObject == null)
                return;
            if (jsonInfos == null)
                return;
            if (jTokens == null)
                return;

            // If a member isn't found with a matching JsonArrayIndexAttribute then just skip the token
            var relevantJsonMemberInfo = jsonInfos.SingleOrDefault(info => info != null && info.Index == tokenIndex);
            if (relevantJsonMemberInfo == null)
                return;

            if (tokenIndex > jTokens.Count)
                return;

            var jToken = jTokens[tokenIndex];
            if (jToken == null)
                return;

            var value = GetTokenValue(relevantJsonMemberInfo.MemberInfo, relevantJsonMemberInfo.MemberType, jToken);
            relevantJsonMemberInfo.SetObjectValue(targetObject, value);
        }

        private static object GetTokenValue(MemberInfo memberInfo, Type type, JToken jToken)
        {
            // Non-numeric values don't need to be specially transformed
            if (jToken.Type != JTokenType.Float && jToken.Type != JTokenType.Integer)
                return jToken.ToObject(type);

            // Numeric values might have to be specially transformed depending on the serialization attributes in play
            if (Attribute.IsDefined(memberInfo, typeof(DateTimeSerializesAsUnixTimeSecondsAttribute)))
                return jToken.ToObject<double>().ToDateTime();
            if (Attribute.IsDefined(memberInfo, typeof(TimeSpanSerializesAsMillisecondsAttribute)))
                return TimeSpan.FromMilliseconds(jToken.ToObject<double>());
            if (Attribute.IsDefined(memberInfo, typeof(TimeSpanSerializesAsSecondsAttribute)))
                return TimeSpan.FromSeconds(jToken.ToObject<double>());

            // If none of the special serialization attributes were used then we can just return the untransformed value
            return jToken.ToObject(type);
        }

        private class JsonMemberInfo
        {
            public readonly uint Index;
            public readonly object ExistingValue;
            public readonly MemberInfo MemberInfo;
            public readonly Type MemberType;

            private readonly Action<object, object> _valueSetter;

            public JsonMemberInfo(uint index, object existingValue, MemberInfo memberInfo, Type memberType, Action<object, object> valueSetter)
            {
                Index = index;
                ExistingValue = existingValue;
                MemberInfo = memberInfo;
                MemberType = memberType;
                _valueSetter = valueSetter;
            }

            /// <summary>
            /// Sets the value of one of the members of <code>targetObject</code> to <code>value</code>.
            /// </summary>
            /// <param name="targetObject">The object whose member will have its value set</param>
            /// <param name="value">The value that will be set</param>
            public void SetObjectValue(object targetObject, object value)
            {
                _valueSetter(targetObject, value);
            }
        }
    }
}

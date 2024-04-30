// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NewRelic.Agent.Tests.TestSerializationHelpers.JsonConverters
{
    public class JsonArrayConverter : JsonConverter
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value == null)
                return;
            if (serializer == null)
                return;
            if (writer == null)
                return;

            // Write out the type as an array of values ordered by JSON index
            var values = GetJsonMemberValuesOrderedByIndex(value, true);
            serializer.Serialize(writer, values);
        }

        public override object ReadJson(JsonReader reader, Type type, object existingValue, JsonSerializer serializer)
        {
            if (reader == null)
                return null;
            if (type == null)
                return null;
            if (serializer == null)
                return null;
            if (reader.TokenType == JsonToken.Null)
                return null;

            // Load JSON
            var jArray = JArray.Load(reader);
            if (jArray == null)
                throw new JsonSerializationException("Failed to load JSON into JArray");

            // Find all JsonArrayIndex members in object
            var jsonInfos = GetJsonMemberInfosOrderedByIndex(type);

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


        private static IList<object> GetJsonMemberValuesOrderedByIndex(object value, bool validateJsonProperties)
        {
            // Find all JsonArrayIndex members in object
            var postProcessed = GetPostProcessed(value);
            var jsonInfos = GetJsonMemberInfosOrderedByIndex(postProcessed.GetType(), postProcessed);

            if (validateJsonProperties)
            {
                // Verify type has members with valid JSON indexes
                if (!jsonInfos.Any())
                    throw new JsonSerializationException(string.Format("Failed to serialize object of type {0} -- object has no members marked with JsonArrayIndexAttribute", value.GetType()));
                if (jsonInfos.First().Index != 0)
                    throw new JsonSerializationException(string.Format("Failed to serialize object of type {0} -- no field or property found for index 0", value.GetType()));
                if (!jsonInfos.IsSequential(info => info.Index))
                    throw new JsonSerializationException(string.Format("Failed to serialize object of type {0} -- JsonArrayIndex sequence is missing a value in the middle of the sequence", value.GetType()));
            }

            return jsonInfos.Select(info => info.ExistingValue).ToList();
        }


        private static IList<JsonMemberInfo> GetJsonMemberInfosOrderedByIndex(Type type, object instance = null)
        {
            var propertyJsonInfo = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(property => property != null)
                .Select(property => TryGetJsonMemberInfo(property, instance));
            var fieldJsonInfo = type.GetFields(BindingFlags.Public | BindingFlags.Instance)
                .Where(field => field != null)
                .Select(field => TryGetJsonMemberInfo(field, instance));

            var jsonMemberInfos = propertyJsonInfo
                .Concat(fieldJsonInfo)
                .Where(info => info != null)
                .OrderBy(info => info.Index)
                .ToList();

            var duplicateIndexGroup = jsonMemberInfos.GroupBy(info => info.Index).FirstOrDefault(group => group != null && group.Count() > 1);
            if (duplicateIndexGroup != null)
                throw new JsonSerializationException(string.Format("Failed to read serialization info for object of type {0} -- index {1} is specified multiple times", type.Name, duplicateIndexGroup.First().Index));

            return jsonMemberInfos;
        }


        private static object GetPostProcessed(object value)
        {
            var properties = value.GetType().GetProperties();
            foreach (var property in properties)
            {
                if (property == null)
                    continue;
                if (!Attribute.IsDefined(property, typeof(SerializationStandInAttribute)))
                    continue;

                var postProcessed = property.GetValue(value, null);
                if (postProcessed == null)
                    continue;

                return postProcessed;
            }

            return value;
        }


        public static object SwapInDateTimeAsUnixTimeIfNecessary(MemberInfo member, object value)
        {
            if (!(value is DateTime))
                return value;

            if (!Attribute.IsDefined(member, typeof(DateTimeSerializesAsUnixTimeAttribute)))
                return value;

            var dateTime = (DateTime)value;
            return dateTime.ToUnixTime();
        }


        public static object SwapInTimeSpanAsMillisecondsIfNecessary(MemberInfo member, object value)
        {
            if (!(value is TimeSpan))
                return value;

            if (!Attribute.IsDefined(member, typeof(TimeSpanSerializesAsMillisecondsAttribute)))
                return value;

            var timeSpan = (TimeSpan)value;
            return timeSpan.TotalMilliseconds;
        }


        public static object SwapInTimeSpanAsSecondsIfNecessary(MemberInfo member, object value)
        {
            if (!(value is TimeSpan))
                return value;

            if (!Attribute.IsDefined(member, typeof(TimeSpanSerializesAsSecondsAttribute)))
                return value;

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

            if (!Attribute.IsDefined(memberInfo, typeof(JsonArrayIndexAttribute)))
                return null;

            var attributes = memberInfo.GetCustomAttributes(typeof(JsonArrayIndexAttribute), true);
            if (attributes.Length <= 0)
                return null;

            var attribute = attributes[0] as JsonArrayIndexAttribute;
            if (attribute == null)
                return null;

            var index = attribute.Index;
            var existingValue = getValue == null ? null : getValue();
            existingValue = SwapInDateTimeAsUnixTimeIfNecessary(memberInfo, existingValue);
            existingValue = SwapInTimeSpanAsMillisecondsIfNecessary(memberInfo, existingValue);
            existingValue = SwapInTimeSpanAsSecondsIfNecessary(memberInfo, existingValue);

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
            if (Attribute.IsDefined(memberInfo, typeof(DateTimeSerializesAsUnixTimeAttribute)))
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

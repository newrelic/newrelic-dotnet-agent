// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Testing.Assertions;
using Newtonsoft.Json;
using NUnit.Framework;

namespace NewRelic.Agent.Core.JsonConverters
{
    [TestFixture]
    public class JsonArrayConverterTests
    {
        [JsonConverter(typeof(JsonArrayConverter))]
        private class SimpleProperties
        {
            [JsonArrayIndex(Index = 0)]
            public bool MyBoolean { get; set; }
            [JsonArrayIndex(Index = 1)]
            public uint MyUInt32 { get; set; }
        }

        [Test]
        public void when_serializing_simple_properties_as_array()
        {
            var simpleObject = new SimpleProperties();
            var serialized = JsonConvert.SerializeObject(simpleObject);
            Assert.That(serialized, Is.EqualTo("[false,0]"));
        }

        [Test]
        public void when_deserializing_simple_properties_as_array()
        {
            var json = "[true,1]";
            var deserialized = JsonConvert.DeserializeObject<SimpleProperties>(json);
            NrAssert.Multiple(
                () => Assert.That(deserialized.MyBoolean, Is.EqualTo(true)),
                () => Assert.That(deserialized.MyUInt32, Is.EqualTo(1))
                );
        }

        [JsonConverter(typeof(JsonArrayConverter))]
        private class OutOfOrderProperties
        {
            [JsonArrayIndex(Index = 1)]
            public bool MyBoolean { get; set; }
            [JsonArrayIndex(Index = 0)]
            public uint MyUInt32 { get; set; }
        }

        [Test]
        public void when_serializing_out_of_order_properties_as_array()
        {
            var outOfOrderObject = new OutOfOrderProperties();
            var serialized = JsonConvert.SerializeObject(outOfOrderObject);
            Assert.That(serialized, Is.EqualTo("[0,false]"));
        }

        [Test]
        public void when_deserializing_out_of_order_properties_as_array()
        {
            var json = "[1,true]";
            var deserialized = JsonConvert.DeserializeObject<OutOfOrderProperties>(json);
            NrAssert.Multiple(
                () => Assert.That(deserialized.MyBoolean, Is.EqualTo(true)),
                () => Assert.That(deserialized.MyUInt32, Is.EqualTo(1))
                );
        }

        [JsonConverter(typeof(JsonArrayConverter))]
        private class SparseProperties
        {
            [JsonArrayIndex(Index = 3)]
            public bool MyBoolean { get; set; }
            [JsonArrayIndex(Index = 0)]
            public uint MyUInt32 { get; set; }
        }

        [Test]
        public void when_serializing_sparse_properties_as_array()
        {
            var sparseObject = new SparseProperties();
            var exception = NrAssert.Throws<JsonSerializationException>(() => JsonConvert.SerializeObject(sparseObject));
            Assert.That(exception, Is.Not.Null);
        }

        [JsonConverter(typeof(JsonArrayConverter))]
        private class PartiallySerializedProperties
        {
            [JsonArrayIndex(Index = 1)]
            public bool MyBoolean { get; set; }
            public string MyString { get { return "Foo"; } }
            [JsonArrayIndex(Index = 0)]
            public uint MyUInt32 { get; set; }
        }

        [Test]
        public void when_serializing_partially_serialized_properties_as_array()
        {
            var partiallySerializedObject = new PartiallySerializedProperties();
            var serialized = JsonConvert.SerializeObject(partiallySerializedObject);
            Assert.That(serialized, Is.EqualTo("[0,false]"));
        }

        [Test]
        public void when_deserializing_partially_serialized_properties_as_array()
        {
            var json = "[1,true]";
            var deserialized = JsonConvert.DeserializeObject<PartiallySerializedProperties>(json);
            NrAssert.Multiple(
                () => Assert.That(deserialized.MyBoolean, Is.EqualTo(true)),
                () => Assert.That(deserialized.MyUInt32, Is.EqualTo(1)),
                () => Assert.That(deserialized.MyString, Is.EqualTo("Foo"))
                );
        }

        [JsonConverter(typeof(JsonArrayConverter))]
        private class TimesObject
        {
            private DateTime _timestampAsUnixTime = new DateTime(1212, 12, 12, 12, 12, 12);
            [JsonArrayIndex(Index = 0)]
            [DateTimeSerializesAsUnixTimeSeconds]
            public DateTime TimestampAsUnixTime { get { return _timestampAsUnixTime; } set { _timestampAsUnixTime = value; } }

            private DateTime _timestampAsDateTime = new DateTime(1111, 11, 11, 11, 11, 11);
            [JsonArrayIndex(Index = 1)]
            public DateTime TimestampAsDateTime { get { return _timestampAsDateTime; } set { _timestampAsDateTime = value; } }

            private TimeSpan _timeSpanAsMilliseconds = TimeSpan.FromMilliseconds(1234);
            [JsonArrayIndex(Index = 2)]
            [TimeSpanSerializesAsMilliseconds]
            public TimeSpan TimeSpanAsMilliseconds { get { return _timeSpanAsMilliseconds; } set { _timeSpanAsMilliseconds = value; } }

            private TimeSpan _timeSpanAsSeconds = TimeSpan.FromSeconds(4321);
            [JsonArrayIndex(Index = 3)]
            [TimeSpanSerializesAsSeconds]
            public TimeSpan TimeSpanAsSeconds { get { return _timeSpanAsSeconds; } set { _timeSpanAsSeconds = value; } }

            private TimeSpan _timeSpanAsTimeSpan = TimeSpan.FromSeconds(4321);
            [JsonArrayIndex(Index = 4)]
            public TimeSpan TimeSpanAsTimeSpan { get { return _timeSpanAsTimeSpan; } set { _timeSpanAsTimeSpan = value; } }

        }

        [Test]
        public void when_time_span_serializes_as_milliseconds()
        {
            var timesObject = new TimesObject();
            var serialized = JsonConvert.SerializeObject(timesObject);
            Assert.That(serialized, Is.EqualTo(@"[-23890247268.0,""1111-11-11T11:11:11"",1234.0,4321.0,""01:12:01""]"));
        }

        [Test]
        public void when_time_span_deserializes_as_milliseconds()
        {
            var json = @"[-23890247267.0,""1111-11-11T11:11:12"",1235.0,4322.0,""01:12:02""]";
            var deserialized = JsonConvert.DeserializeObject<TimesObject>(json);
            NrAssert.Multiple(
                () => Assert.That(deserialized.TimestampAsUnixTime, Is.EqualTo(new DateTime(1212, 12, 12, 12, 12, 13))),
                () => Assert.That(deserialized.TimestampAsDateTime, Is.EqualTo(new DateTime(1111, 11, 11, 11, 11, 12))),
                () => Assert.That(deserialized.TimeSpanAsMilliseconds, Is.EqualTo(TimeSpan.FromMilliseconds(1235))),
                () => Assert.That(deserialized.TimeSpanAsSeconds, Is.EqualTo(TimeSpan.FromSeconds(4322))),
                () => Assert.That(deserialized.TimeSpanAsTimeSpan, Is.EqualTo(TimeSpan.FromSeconds(4322)))
                );
        }

        [JsonConverter(typeof(JsonArrayConverter))]
        private class SimpleFields
        {
            [JsonArrayIndex(Index = 0)]
            public bool MyBoolean = false;

            [JsonArrayIndex(Index = 1)]
            public uint MyUInt32 = 0;
        }

        [Test]
        public void when_serializing_simple_fields_as_array()
        {
            var simpleFields = new SimpleFields();
            var serialized = JsonConvert.SerializeObject(simpleFields);
            Assert.That(serialized, Is.EqualTo("[false,0]"));
        }

        [Test]
        public void when_deserializing_simple_fields_as_array()
        {
            var json = "[true,1]";
            var deserialized = JsonConvert.DeserializeObject<SimpleFields>(json);
            NrAssert.Multiple(
                () => Assert.That(deserialized.MyBoolean, Is.EqualTo(true)),
                () => Assert.That(deserialized.MyUInt32, Is.EqualTo(1))
                );
        }

        [JsonConverter(typeof(JsonArrayConverter))]
        public class OutOfOrderFields
        {
            [JsonArrayIndex(Index = 1)]
            public bool MyBoolean = false;

            [JsonArrayIndex(Index = 0)]
            public uint MyUInt32 = 0;
        }

        [Test]
        public void when_serializing_out_of_order_fields_as_array()
        {
            var outOfOrderFields = new OutOfOrderFields();
            var serialized = JsonConvert.SerializeObject(outOfOrderFields);
            Assert.That(serialized, Is.EqualTo("[0,false]"));
        }

        [Test]
        public void when_deserializing_out_of_order_fields_as_array()
        {
            var json = "[1,true]";
            var deserialized = JsonConvert.DeserializeObject<OutOfOrderFields>(json);
            NrAssert.Multiple(
                () => Assert.That(deserialized.MyBoolean, Is.EqualTo(true)),
                () => Assert.That(deserialized.MyUInt32, Is.EqualTo(1))
                );
        }

        [JsonConverter(typeof(JsonArrayConverter))]
        private class TestClass_Bad_NonContiguous
        {
            [JsonArrayIndex(Index = 3)]
            public bool MyBoolean = false;

            [JsonArrayIndex(Index = 0)]
            public uint MyUInt32 = 0;
        }

        [JsonConverter(typeof(JsonArrayConverter))]
        private class TestClass_Bad_DuplicateIndex
        {
            [JsonArrayIndex(Index = 1)]
            public bool MyBoolean = false;

            [JsonArrayIndex(Index = 1)]
            public bool MyBoolean2 = true;

            [JsonArrayIndex(Index = 0)]
            public uint MyUInt32 = 0;
        }


        [JsonConverter(typeof(JsonArrayConverter))]
        private class TestClass_Bad_NotStartAtZero
        {
            [JsonArrayIndex(Index = 1)]
            public bool MyBoolean = false;

            [JsonArrayIndex(Index = 2)]
            public bool MyBoolean2 = true;

            [JsonArrayIndex(Index = 3)]
            public uint MyUInt32 = 0;
        }


        [JsonConverter(typeof(JsonArrayConverter))]
        private class TestClass_Bad_NoMembersWithAttribute
        {
            public bool MyBoolean = false;

            public bool MyBoolean2 = true;

            public uint MyUInt32 = 0;
        }

        [Test]
        public void ErrorThrownWhenDeserializingClassWithNonContiguousIndexes()
        {
            var sparseFields = new TestClass_Bad_NonContiguous();
            var exception = NrAssert.Throws<JsonSerializationException>(() => JsonConvert.SerializeObject(sparseFields));
            Assert.That(exception, Is.Not.Null);
        }

        [Test]
        public void ErrorThrownWhenDeserializingClassWithDuplicateIndexes()
        {
            var sparseFields = new TestClass_Bad_DuplicateIndex();
            var exception = NrAssert.Throws<JsonSerializationException>(() => JsonConvert.SerializeObject(sparseFields));
            Assert.That(exception, Is.Not.Null);
        }

        [Test]
        public void ErrorThrownWhenDeserializingClassWithIndexNotStartAtZero()
        {
            var sparseFields = new TestClass_Bad_NotStartAtZero();
            var exception = NrAssert.Throws<JsonSerializationException>(() => JsonConvert.SerializeObject(sparseFields));
            Assert.That(exception, Is.Not.Null);
        }

        [Test]
        public void ErrorThrownWhenDeserializingClassWithNoMembersWithIndexes()
        {
            var sparseFields = new TestClass_Bad_NoMembersWithAttribute();
            var exception = NrAssert.Throws<JsonSerializationException>(() => JsonConvert.SerializeObject(sparseFields));
            Assert.That(exception, Is.Not.Null);
        }

        [JsonConverter(typeof(JsonArrayConverter))]
        private class PartiallySerializedFields
        {
            [JsonArrayIndex(Index = 1)]
            public bool MyBoolean = false;

            public string MyString = "Foo";

            [JsonArrayIndex(Index = 0)]
            public uint MyUInt32 = 0;
        }

        [Test]
        public void when_serializing_partially_serialized_fields_as_array()
        {
            var partiallySerializedFields = new PartiallySerializedFields();
            var serialized = JsonConvert.SerializeObject(partiallySerializedFields);
            Assert.That(serialized, Is.EqualTo("[0,false]"));
        }

        [Test]
        public void when_deserializing_partially_serialized_fields_as_array()
        {
            var json = "[1,true]";
            var deserialized = JsonConvert.DeserializeObject<PartiallySerializedFields>(json);
            NrAssert.Multiple(
                () => Assert.That(deserialized.MyBoolean, Is.EqualTo(true)),
                () => Assert.That(deserialized.MyUInt32, Is.EqualTo(1))
                );
        }

        [JsonConverter(typeof(JsonArrayConverter))]
        private class PartiallySerializedOutOfOrderFieldsAndProperties
        {
            [JsonArrayIndex(Index = 2)]
            public bool MyBooleanField = false;

            private string _myStringProperty = "Bar";
            [JsonArrayIndex(Index = 3)]
            public string MyStringProperty { get { return _myStringProperty; } set { _myStringProperty = value; } }

            public string MyStringField = "Foo";

            [JsonArrayIndex(Index = 0)]
            public uint MyUInt32Field = 0;

            [JsonArrayIndex(Index = 1)]
            public uint MyUInt32Property { get; set; }
        }

        [Test]
        public void when_serializing_partially_serialized_fields_and_properties_as_array()
        {
            var PartiallySerializedOutOfOrderFieldsAndProperties = new PartiallySerializedOutOfOrderFieldsAndProperties();
            var serialized = JsonConvert.SerializeObject(PartiallySerializedOutOfOrderFieldsAndProperties);
            Assert.That(serialized, Is.EqualTo(@"[0,0,false,""Bar""]"));
        }

        [Test]
        public void when_deserializing_partially_serialized_fields_and_properties_as_array()
        {
            var json = @"[1,2,true,""Baz""]";
            var deserialized = JsonConvert.DeserializeObject<PartiallySerializedOutOfOrderFieldsAndProperties>(json);
            NrAssert.Multiple(
                () => Assert.That(deserialized.MyUInt32Field, Is.EqualTo(1)),
                () => Assert.That(deserialized.MyUInt32Property, Is.EqualTo(2)),
                () => Assert.That(deserialized.MyBooleanField, Is.EqualTo(true)),
                () => Assert.That(deserialized.MyStringProperty, Is.EqualTo("Baz"))
                );
        }


        [Test]
        public void TestTypesHaveCorrectJsonArrayIndexAttributes()
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(x => x.FullName.ToLower().Contains("newrelic"))
                .Where(x => !x.FullName.ToLower().Contains("test"))
                .ToList();

            foreach (var assembly in assemblies)
            {
                Console.WriteLine($"Assembly: {assembly.FullName}");

                var types = assembly.GetTypes()
                    .Where(t => t.GetCustomAttributes<JsonConverterAttribute>().Any())
                    .Where(t => t.GetCustomAttribute<JsonConverterAttribute>().ConverterType == typeof(JsonArrayConverter))
                    .ToList();

                foreach (var type in types)
                {

                    Console.WriteLine($"\tType: {type.FullName}");

                    var members = type.GetMembers(BindingFlags.Public | BindingFlags.Instance)
                        .Where(x => x is FieldInfo || x is PropertyInfo)
                        .Where(x => x.GetCustomAttributes(typeof(JsonArrayIndexAttribute)).Any())
                        .Select(x => new KeyValuePair<uint, MemberInfo>(x.GetCustomAttribute<JsonArrayIndexAttribute>().Index, x))
                        .OrderBy(x => x.Key)
                        .ToArray();

                    NrAssert.Multiple(
                        () => Assert.That(members, Is.Not.Empty, $"Type {type.FullName} is marked with JsonArrayConverter but does not have any JsonArrayIndex attributes"),
                        () => Assert.That(members[0].Key, Is.EqualTo(0), $"Type {type.FullName} is marked with JsonArrayConverter but does not have any JsonArrayIndex with Index of 0"),
                        () => Assert.That(members.IsSequential(x => x.Key), Is.True, $"Type {type.FullName} has noncontiguous members"),
                        () => Assert.That(members.Select(x => x.Key).Distinct().Count(), Is.EqualTo(members.Select(x => x.Key).Count()), $"Type {type.FullName} has duplicated index values"));
                }
            }
        }
    }
}

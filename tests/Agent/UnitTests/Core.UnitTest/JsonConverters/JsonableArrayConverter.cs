using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.Core.JsonConverters;
using NewRelic.Testing.Assertions;
using Newtonsoft.Json;
using NUnit.Framework;

namespace NewRelic.Agent.Core.Utilities
{
    [TestFixture]
    public class Class_JsonableAsArray
    {
        [JsonConverter(typeof(JsonArrayConverter))]
        private class SimpleProperties
        {
            [JsonArrayIndex(Index = 0)]
            public Boolean MyBoolean { get; set; }
            [JsonArrayIndex(Index = 1)]
            public UInt32 MyUInt32 { get; set; }
        }

        [Test]
        public void when_serializing_simple_properties_as_array()
        {
            var simpleObject = new SimpleProperties();
            var serialized = JsonConvert.SerializeObject(simpleObject);
            Assert.AreEqual("[false,0]", serialized);
        }

        [Test]
        public void when_deserializing_simple_properties_as_array()
        {
            var json = "[true,1]";
            var deserialized = JsonConvert.DeserializeObject<SimpleProperties>(json);
            NrAssert.Multiple(
                () => Assert.AreEqual(true, deserialized.MyBoolean),
                () => Assert.AreEqual(1, deserialized.MyUInt32)
                );
        }

        [JsonConverter(typeof(JsonArrayConverter))]
        private class OutOfOrderProperties
        {
            [JsonArrayIndex(Index = 1)]
            public Boolean MyBoolean { get; set; }
            [JsonArrayIndex(Index = 0)]
            public UInt32 MyUInt32 { get; set; }
        }

        [Test]
        public void when_serializing_out_of_order_properties_as_array()
        {
            var outOfOrderObject = new OutOfOrderProperties();
            var serialized = JsonConvert.SerializeObject(outOfOrderObject);
            Assert.AreEqual("[0,false]", serialized);
        }

        [Test]
        public void when_deserializing_out_of_order_properties_as_array()
        {
            var json = "[1,true]";
            var deserialized = JsonConvert.DeserializeObject<OutOfOrderProperties>(json);
            NrAssert.Multiple(
                () => Assert.AreEqual(true, deserialized.MyBoolean),
                () => Assert.AreEqual(1, deserialized.MyUInt32)
                );
        }

        [JsonConverter(typeof(JsonArrayConverter))]
        private class SparseProperties
        {
            [JsonArrayIndex(Index = 3)]
            public Boolean MyBoolean { get; set; }
            [JsonArrayIndex(Index = 0)]
            public UInt32 MyUInt32 { get; set; }
        }

        [Test]
        public void when_serializing_sparse_properties_as_array()
        {
            var sparseObject = new SparseProperties();
            var exception = NrAssert.Throws<JsonSerializationException>(() => JsonConvert.SerializeObject(sparseObject));
            Assert.NotNull(exception);
        }

        [Test]
        public void when_deserializing_sparse_properties_as_array()
        {
            var json = "[10,11,12,true]";
            var deserialized = JsonConvert.DeserializeObject<SparseProperties>(json);
            NrAssert.Multiple(
                () => Assert.AreEqual(true, deserialized.MyBoolean),
                () => Assert.AreEqual(10, deserialized.MyUInt32)
                );
        }

        [JsonConverter(typeof(JsonArrayConverter))]
        private class PartiallySerializedProperties
        {
            [JsonArrayIndex(Index = 1)]
            public Boolean MyBoolean { get; set; }
            public String MyString { get { return "Foo"; } }
            [JsonArrayIndex(Index = 0)]
            public UInt32 MyUInt32 { get; set; }
        }

        [Test]
        public void when_serializing_partially_serialized_properties_as_array()
        {
            var partiallySerializedObject = new PartiallySerializedProperties();
            var serialized = JsonConvert.SerializeObject(partiallySerializedObject);
            Assert.AreEqual("[0,false]", serialized);
        }

        [Test]
        public void when_deserializing_partially_serialized_properties_as_array()
        {
            var json = "[1,true]";
            var deserialized = JsonConvert.DeserializeObject<PartiallySerializedProperties>(json);
            NrAssert.Multiple(
                () => Assert.AreEqual(true, deserialized.MyBoolean),
                () => Assert.AreEqual(1, deserialized.MyUInt32),
                () => Assert.AreEqual("Foo", deserialized.MyString)
                );
        }

        [JsonConverter(typeof(JsonArrayConverter))]
        private class PostProcessedProperties
        {
            [JsonArrayIndex(Index = 0)]
            public Boolean MyBoolean { get; set; }
            [SerializationStandIn]
            public PostProcessedProperties PostProcessedThis { get { return new PostProcessedProperties { MyBoolean = true }; } }
        }

        [Test]
        public void when_stand_in_is_present_then_it_is_used_instead()
        {
            var postProcessedClass = new PostProcessedProperties();
            var serialized = JsonConvert.SerializeObject(postProcessedClass);
            Assert.AreEqual("[true]", serialized);
        }

        [JsonConverter(typeof(JsonArrayConverter))]
        private class TimesObject
        {
            private DateTime _timestampAsUnixTime = new DateTime(1212, 12, 12, 12, 12, 12);
            [JsonArrayIndex(Index = 0)]
            [DateTimeSerializesAsUnixTime]
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
            Assert.AreEqual(@"[-23890247268.0,""1111-11-11T11:11:11"",1234.0,4321.0,""01:12:01""]", serialized);
        }

        [Test]
        public void when_time_span_deserializes_as_milliseconds()
        {
            var json = @"[-23890247267.0,""1111-11-11T11:11:12"",1235.0,4322.0,""01:12:02""]";
            var deserialized = JsonConvert.DeserializeObject<TimesObject>(json);
            NrAssert.Multiple(
                () => Assert.AreEqual(new DateTime(1212, 12, 12, 12, 12, 13), deserialized.TimestampAsUnixTime),
                () => Assert.AreEqual(new DateTime(1111, 11, 11, 11, 11, 12), deserialized.TimestampAsDateTime),
                () => Assert.AreEqual(TimeSpan.FromMilliseconds(1235), deserialized.TimeSpanAsMilliseconds),
                () => Assert.AreEqual(TimeSpan.FromSeconds(4322), deserialized.TimeSpanAsSeconds),
                () => Assert.AreEqual(TimeSpan.FromSeconds(4322), deserialized.TimeSpanAsTimeSpan)
                );
        }

        [JsonConverter(typeof(JsonArrayConverter))]
        private class SimpleFields
        {
            [JsonArrayIndex(Index = 0)]
            public Boolean MyBoolean = false;

            [JsonArrayIndex(Index = 1)]
            public UInt32 MyUInt32 = 0;
        }

        [Test]
        public void when_serializing_simple_fields_as_array()
        {
            var simpleFields = new SimpleFields();
            var serialized = JsonConvert.SerializeObject(simpleFields);
            Assert.AreEqual("[false,0]", serialized);
        }

        [Test]
        public void when_deserializing_simple_fields_as_array()
        {
            var json = "[true,1]";
            var deserialized = JsonConvert.DeserializeObject<SimpleFields>(json);
            NrAssert.Multiple(
                () => Assert.AreEqual(true, deserialized.MyBoolean),
                () => Assert.AreEqual(1, deserialized.MyUInt32)
                );
        }

        [JsonConverter(typeof(JsonArrayConverter))]
        public class OutOfOrderFields
        {
            [JsonArrayIndex(Index = 1)]
            public Boolean MyBoolean = false;

            [JsonArrayIndex(Index = 0)]
            public UInt32 MyUInt32 = 0;
        }

        [Test]
        public void when_serializing_out_of_order_fields_as_array()
        {
            var outOfOrderFields = new OutOfOrderFields();
            var serialized = JsonConvert.SerializeObject(outOfOrderFields);
            Assert.AreEqual("[0,false]", serialized);
        }

        [Test]
        public void when_deserializing_out_of_order_fields_as_array()
        {
            var json = "[1,true]";
            var deserialized = JsonConvert.DeserializeObject<OutOfOrderFields>(json);
            NrAssert.Multiple(
                () => Assert.AreEqual(true, deserialized.MyBoolean),
                () => Assert.AreEqual(1, deserialized.MyUInt32)
                );
        }

        [JsonConverter(typeof(JsonArrayConverter))]
        private class SparseFields
        {
            [JsonArrayIndex(Index = 3)]
            public Boolean MyBoolean = false;

            [JsonArrayIndex(Index = 0)]
            public UInt32 MyUInt32 = 0;
        }

        [Test]
        public void when_serializing_sparse_Fields_as_array()
        {
            var sparseFields = new SparseFields();
            var exception = NrAssert.Throws<JsonSerializationException>(() => JsonConvert.SerializeObject(sparseFields));
            Assert.NotNull(exception);
        }

        [Test]
        public void when_deserializing_sparse_Fields_as_array()
        {
            var json = "[10,11,12,true]";
            var deserialized = JsonConvert.DeserializeObject<SparseFields>(json);
            NrAssert.Multiple(
                () => Assert.AreEqual(true, deserialized.MyBoolean),
                () => Assert.AreEqual(10, deserialized.MyUInt32)
                );
        }

        [JsonConverter(typeof(JsonArrayConverter))]
        private class PartiallySerializedFields
        {
            [JsonArrayIndex(Index = 1)]
            public Boolean MyBoolean = false;

            public String MyString = "Foo";

            [JsonArrayIndex(Index = 0)]
            public UInt32 MyUInt32 = 0;
        }

        [Test]
        public void when_serializing_partially_serialized_fields_as_array()
        {
            var partiallySerializedFields = new PartiallySerializedFields();
            var serialized = JsonConvert.SerializeObject(partiallySerializedFields);
            Assert.AreEqual("[0,false]", serialized);
        }

        [Test]
        public void when_deserializing_partially_serialized_fields_as_array()
        {
            var json = "[1,true]";
            var deserialized = JsonConvert.DeserializeObject<PartiallySerializedFields>(json);
            NrAssert.Multiple(
                () => Assert.AreEqual(true, deserialized.MyBoolean),
                () => Assert.AreEqual(1, deserialized.MyUInt32)
                );
        }

        [JsonConverter(typeof(JsonArrayConverter))]
        private class PartiallySerializedOutOfOrderFieldsAndProperties
        {
            [JsonArrayIndex(Index = 2)]
            public Boolean MyBooleanField = false;

            private string _myStringProperty = "Bar";
            [JsonArrayIndex(Index = 3)]
            public String MyStringProperty { get { return _myStringProperty; } set { _myStringProperty = value; } }

            public String MyStringField = "Foo";

            [JsonArrayIndex(Index = 0)]
            public UInt32 MyUInt32Field = 0;

            [JsonArrayIndex(Index = 1)]
            public UInt32 MyUInt32Property { get; set; }
        }

        [Test]
        public void when_serializing_partially_serialized_fields_and_properties_as_array()
        {
            var PartiallySerializedOutOfOrderFieldsAndProperties = new PartiallySerializedOutOfOrderFieldsAndProperties();
            var serialized = JsonConvert.SerializeObject(PartiallySerializedOutOfOrderFieldsAndProperties);
            Assert.AreEqual(@"[0,0,false,""Bar""]", serialized);
        }

        [Test]
        public void when_deserializing_partially_serialized_fields_and_properties_as_array()
        {
            var json = @"[1,2,true,""Baz""]";
            var deserialized = JsonConvert.DeserializeObject<PartiallySerializedOutOfOrderFieldsAndProperties>(json);
            NrAssert.Multiple(
                () => Assert.AreEqual(1, deserialized.MyUInt32Field),
                () => Assert.AreEqual(2, deserialized.MyUInt32Property),
                () => Assert.AreEqual(true, deserialized.MyBooleanField),
                () => Assert.AreEqual("Baz", deserialized.MyStringProperty)
                );
        }
    }
}

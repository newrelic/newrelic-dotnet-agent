// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Core.DistributedTracing;
using NewRelic.Core.JsonConverters;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using System;


namespace NewRelic.Agent.Core.JsonConverters
{
    public class TestSerialization
    {
        public int[] Version;           //v
        public string StringField;      //s
        public bool BoolField;          //b
        public DateTime DateTimeField;  //d
        public float FloatField;        //f

        public TestSerialization() : this(string.Empty, false, DateTime.MinValue, -1.0f)
        {
        }

        public TestSerialization(string s, bool b, DateTime d, float f)
        {
            Version = new[] { 1, 0 };
            StringField = s;
            BoolField = b;
            DateTimeField = d;
            FloatField = f;
        }
    }

    [TestFixture]
    public class ValidationConstraintTests
    {
        public readonly JObject JsonObject = new JObject(
            new JProperty("v", new JArray(new[] { 1, 0 })),
            new JProperty("s", new JValue("string value")),
            new JProperty("b", new JValue(false)),
            new JProperty("d", new JValue(new DateTime(1970, 1, 1, 0, 0, 1, DateTimeKind.Utc))),
            new JProperty("f", new JValue(0.666f))
        );

        [Test]
        public void ValidationConstraintTests_ParseRequiredVersion()
        {
            var testInstance = new TestSerialization();

            var constraint = new ValidationConstraint<TestSerialization>(path: "v", type: JTokenType.Array, isRequired: true,
                miniumChildren: 2, maximumChildren: 2, parse: (s, p) => p.Version = s.ToObject<int[]>());

            testInstance.Version = new[] { 42, 42 };
            constraint.ParseAndThrowOnFailure(JsonObject, testInstance);
            Assert.That(testInstance.Version, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(testInstance.Version[0], Is.EqualTo(1));
                Assert.That(testInstance.Version[1], Is.EqualTo(0));
            });
        }

        [Test]
        public void ValidationConstraintTests_ParseOptionalVersion()
        {
            var testInstance = new TestSerialization();
            var constraint = new ValidationConstraint<TestSerialization>("v", JTokenType.Array, false, 2, 2,
                (s, p) => p.Version = s.ToObject<int[]>());
            testInstance.Version = new[] { 42, 42 };

            constraint.ParseAndThrowOnFailure(JsonObject, testInstance);
            Assert.That(testInstance.Version, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(testInstance.Version[0], Is.EqualTo(1));
                Assert.That(testInstance.Version[1], Is.EqualTo(0));
            });
        }

        [Test]
        public void ValidationConstraintTests_ParseRequiredString()
        {
            var testInstance = new TestSerialization();
            var constraint = new ValidationConstraint<TestSerialization>("s", JTokenType.String, true, 0, 0,
                (s, p) => p.StringField = s.ToObject<string>());
            testInstance.StringField = null;
            constraint.ParseAndThrowOnFailure(JsonObject, testInstance);
            Assert.Multiple(() =>
            {
                Assert.That(testInstance.Version, Is.Not.Null);
                Assert.That(testInstance.StringField, Is.EqualTo("string value"));
            });
        }

        [Test]
        public void ValidationConstraintTests_ParseOptionalString()
        {
            var testInstance = new TestSerialization();
            var constraint = new ValidationConstraint<TestSerialization>("s", JTokenType.String, false, 0, 0,
                (s, p) => p.StringField = s.ToObject<string>());
            testInstance.StringField = null;
            constraint.ParseAndThrowOnFailure(JsonObject, testInstance);
            Assert.That(testInstance.StringField, Is.Not.Null);
            Assert.That(testInstance.StringField, Is.EqualTo("string value"));
        }

        [Test]
        public void ValidationConstraintTests_ParseRequiredBoolean()
        {
            var testInstance = new TestSerialization();
            var constraint = new ValidationConstraint<TestSerialization>("b", JTokenType.Boolean, true, 0, 0,
                (s, p) => p.BoolField = s.ToObject<bool>());
            testInstance.BoolField = true;
            constraint.ParseAndThrowOnFailure(JsonObject, testInstance);
            Assert.That(testInstance.BoolField, Is.EqualTo(false));
        }

        [Test]
        public void ValidationConstraintTests_ParseOptionalBoolean()
        {
            var testInstance = new TestSerialization();
            var constraint = new ValidationConstraint<TestSerialization>("b", JTokenType.Boolean, false, 0, 0,
                (s, p) => p.BoolField = s.ToObject<bool>());
            testInstance.BoolField = true;
            constraint.ParseAndThrowOnFailure(JsonObject, testInstance);
            Assert.That(testInstance.BoolField, Is.EqualTo(false));
        }

        [Test]
        public void ValidationConstraintTests_ParseRequiredFloat()
        {
            var testInstance = new TestSerialization();
            var constraint = new ValidationConstraint<TestSerialization>("f", JTokenType.Float, true, 0, 0,
                (s, p) => p.FloatField = s.ToObject<float>());
            testInstance.FloatField = 42.42f;
            constraint.ParseAndThrowOnFailure(JsonObject, testInstance);
            Assert.That(testInstance.FloatField, Is.EqualTo(0.666f));
        }

        [Test]
        public void ValidationConstraintTests_ParseOptionalFloat()
        {
            var testInstance = new TestSerialization();
            var constraint = new ValidationConstraint<TestSerialization>("f", JTokenType.Float, false, 0, 0,
                (s, p) => p.FloatField = s.ToObject<float>());
            testInstance.FloatField = 42.42f;
            constraint.ParseAndThrowOnFailure(JsonObject, testInstance);
            Assert.That(testInstance.FloatField, Is.EqualTo(0.666f));
        }

        [Test]
        public void ValidationConstraintTests_FindMoreChildrenThanExpectedPrecise()
        {
            //expect exactly one, find 2
            var constraint = new ValidationConstraint<TestSerialization>("v", JTokenType.Array, true, 1, 1,
                (s, p) => p.Version = s.ToObject<int[]>());
            Assert.Throws<DistributedTraceAcceptPayloadParseException>(() => constraint.ParseAndThrowOnFailure(JsonObject, new TestSerialization()));
        }

        [Test]
        public void ValidationConstraintTests_FindMoreChildrenThanExpected()
        {
            //find two, only except max of 1
            var constraint = new ValidationConstraint<TestSerialization>("v", JTokenType.Array, true, 2, 1,
                (s, p) => p.Version = s.ToObject<int[]>());
            Assert.Throws<DistributedTraceAcceptPayloadParseException>(() => constraint.ParseAndThrowOnFailure(JsonObject, new TestSerialization()));
        }

        [Test]
        public void ValidationConstraintTests_FindFewerChildrenThanExpected()
        {
            //expect 3, find 2
            var constraint = new ValidationConstraint<TestSerialization>("v", JTokenType.Array, true, 3, 2,
                (s, p) => p.Version = s.ToObject<int[]>());
            Assert.Throws<DistributedTraceAcceptPayloadParseException>(
                () => constraint.ParseAndThrowOnFailure(JsonObject, new TestSerialization())
            );
        }
    }
}

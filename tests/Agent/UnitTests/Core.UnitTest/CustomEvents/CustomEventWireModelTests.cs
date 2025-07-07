// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Core.Attributes;
using Newtonsoft.Json;
using NUnit.Framework;
using System.Collections.Generic;
using NewRelic.Agent.Core.WireModels;
using NewRelic.Agent.Core.Transformers;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.TestUtilities;
using System;
using NewRelic.Agent.Core.JsonConverters;

namespace NewRelic.Agent.Core.CustomEvents.Tests
{
    [TestFixture]
    public class CustomEventWireModelTests
    {
        private IAttributeDefinitionService _attribDefSvc;
        private IAttributeDefinitions _attribDefs => _attribDefSvc?.AttributeDefs;

        [SetUp]
        public void Setup()
        {
            _attribDefSvc = new AttributeDefinitionService((f) => new AttributeDefinitions(f));
        }

        [TearDown]
        public void TearDown()
        {
            _attribDefSvc.Dispose();
        }

        [Test]
        public void CustomEvents_MultipleEvents_Serialization()
        {
            const int countEvents = 2;

            var customEvents = new CustomEventWireModel[countEvents];
            var expectedSerializations = new List<Dictionary<string, object>[]>();

            for (var i = 0; i < countEvents; i++)
            {
                var timestampVal = DateTime.UtcNow;
                var typeVal = $"CustomEvent{i}";
                var userAttribKey = $"foo{i}";
                var userAttribVal = $"bar{i}";


                var expectedSerialization = new Dictionary<string, object>[]
                {
                    new Dictionary<string, object>()
                    {
                        {_attribDefs.CustomEventType.Name, typeVal },
                        {_attribDefs.Timestamp.Name, timestampVal.ToUnixTimeMilliseconds() }
                    },

                    new Dictionary<string, object>()
                    {
                        { userAttribKey, userAttribVal }
                    },

                    new Dictionary<string, object>()
                };

                var attribVals = new AttributeValueCollection(AttributeDestinations.CustomEvent);

                _attribDefs.Timestamp.TrySetValue(attribVals, timestampVal);
                _attribDefs.CustomEventType.TrySetValue(attribVals, typeVal);
                _attribDefs.GetCustomAttributeForCustomEvent(userAttribKey).TrySetValue(attribVals, userAttribVal);

                var customEvent = new CustomEventWireModel(.5f, attribVals);

                customEvents[i] = customEvent;
                expectedSerializations.Add(expectedSerialization);
            }

            var serialized = JsonConvert.SerializeObject(customEvents);

            Assert.That(serialized, Is.Not.Null);

            var deserialized = JsonConvert.DeserializeObject<List<Dictionary<string, object>[]>>(serialized);

            Assert.That(deserialized, Is.Not.Null);

            Assert.That(deserialized, Has.Count.EqualTo(customEvents.Length));
            AttributeComparer.CompareDictionaries(expectedSerializations[0], deserialized[0]);
            AttributeComparer.CompareDictionaries(expectedSerializations[1], deserialized[1]);
        }

        [Test]
        public void CustomEvents_UserAttributes_AllAttributeTypesSerializeCorrectly()
        {
            var dateTime = DateTime.Parse("2025-02-13T20:15:14.4214979Z");
            var timestampVal = dateTime;
            var typeVal = $"CustomEvent";

            var guid = Guid.NewGuid();
            var expectedSerialization = new Dictionary<string, object>[]
            {
                    new Dictionary<string, object>()
                    {
                        {_attribDefs.Timestamp.Name, timestampVal.ToUnixTimeMilliseconds() },
                        {_attribDefs.CustomEventType.Name, typeVal }
                    },

                    new Dictionary<string, object>()
                    {
                        { "boolVal", true},
                        { "dateTimeVal", dateTime},
                        { "decimalVal", 0.2M},
                        { "doubleVal", 0.2D},
                        { "enumVal", AttributeDestinations.CustomEvent},
                        { "floatVal", 0.2f},
                        { "guidVal", guid},
                        { "intVal", 2},
                        { "longVal", 2L},
                        { "stringVal", "string"},
                    },

                    new Dictionary<string, object>()
            };

            var attribVals = new AttributeValueCollection(AttributeDestinations.CustomEvent);

            _attribDefs.Timestamp.TrySetValue(attribVals, timestampVal);
            _attribDefs.CustomEventType.TrySetValue(attribVals, typeVal);

            _attribDefs.GetCustomAttributeForCustomEvent("boolVal").TrySetValue(attribVals, true);
            _attribDefs.GetCustomAttributeForCustomEvent("dateTimeVal").TrySetValue(attribVals, dateTime);
            _attribDefs.GetCustomAttributeForCustomEvent("decimalVal").TrySetValue(attribVals, 0.2M);
            _attribDefs.GetCustomAttributeForCustomEvent("doubleVal").TrySetValue(attribVals, 0.2D);
            _attribDefs.GetCustomAttributeForCustomEvent("enumVal").TrySetValue(attribVals, AttributeDestinations.CustomEvent);
            _attribDefs.GetCustomAttributeForCustomEvent("floatVal").TrySetValue(attribVals, 0.2f);
            _attribDefs.GetCustomAttributeForCustomEvent("guidVal").TrySetValue(attribVals, guid);
            _attribDefs.GetCustomAttributeForCustomEvent("intVal").TrySetValue(attribVals, 2);
            _attribDefs.GetCustomAttributeForCustomEvent("longVal").TrySetValue(attribVals, 2L);
            _attribDefs.GetCustomAttributeForCustomEvent("stringVal").TrySetValue(attribVals, "string");

            var customEvent = new CustomEventWireModel(.5f, attribVals);

            var serialized = JsonConvert.SerializeObject(customEvent);
            var expectedSerialized = JsonConvert.SerializeObject(expectedSerialization, converters: [new EventAttributesJsonConverter()]);

            Assert.That(serialized, Is.Not.Null);
            Assert.That(serialized, Is.EqualTo(expectedSerialized));
        }
    }
}


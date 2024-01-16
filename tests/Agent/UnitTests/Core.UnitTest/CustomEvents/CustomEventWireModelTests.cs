// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Core.Attributes;
using Newtonsoft.Json;
using NewRelic.Agent.Core.WireModels;
using NewRelic.Agent.Core.Transformers;
using NewRelic.Agent.TestUtilities;
using NewRelic.Core;
using AttributeComparer = NewRelic.Agent.TestUtilities.AttributeComparer;

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


        [Test]
        public void CustomEvents_MultipleEvents_Serialization()
        {
            const int countEvents = 2;

            var customEvents = new CustomEventWireModel[countEvents];
            var expectedSerializations = new List<Dictionary<string, object>[]>();

            for(var i = 0; i < countEvents; i++)
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

            ClassicAssert.AreEqual(customEvents.Length, deserialized.Count);
            AttributeComparer.CompareDictionaries(expectedSerializations[0], deserialized[0]);
            AttributeComparer.CompareDictionaries(expectedSerializations[1], deserialized[1]);
        }
    }

}


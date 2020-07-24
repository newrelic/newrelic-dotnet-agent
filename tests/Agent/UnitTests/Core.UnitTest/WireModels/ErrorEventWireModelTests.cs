using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using NUnit.Framework;

namespace NewRelic.Agent.Core.WireModels
{
    [TestFixture, Category("ErrorEvents")]
    public class ErrorEventWireModelTests
    {
        [Test]
        public void all_attribute_value_types_in_an_event_do_serialize_correctly()
        {
            // ARRANGE
            var userAttributes = new ReadOnlyDictionary<String, Object>(new Dictionary<String, Object>
                {
                    {"identity.user", "samw"},
                    {"identity.product", "product"}
                });
            var agentAttributes = new ReadOnlyDictionary<String, Object>(new Dictionary<String, Object>
                {
                    {"Foo", "Bar"},
                    {"Baz", 42},
                });
            var intrinsicAttributes = new ReadOnlyDictionary<String, Object>(new Dictionary<String, Object>
                {
                    {"databaseCallCount", 10 },
                    {"errormessage", "This is the error message"},
                    {"nr.pathHash", "DCBA4321"},
                    {"nr.referringPathHash", "1234ABCD"},
                    {"nr.referringTransactionGuid", "DCBA43211234ABCD"},
                    {"nr.alternatePathHashes", "55f97a7f,6fc8d18f,72827114,9a3ed934,a1744603,a7d2798f,be1039f5,ccadfd2c,da7edf2e,eaca716b"},
                });

            var isSyntheticsEvent = false;

            // ACT
            var errorEventWireModel = new ErrorEventWireModel(agentAttributes, intrinsicAttributes, userAttributes, isSyntheticsEvent);
            var actualResult = JsonConvert.SerializeObject(errorEventWireModel);

            // ASSERT
            const string expected = @"[{""databaseCallCount"":10,""errormessage"":""This is the error message"",""nr.pathHash"":""DCBA4321"",""nr.referringPathHash"":""1234ABCD"",""nr.referringTransactionGuid"":""DCBA43211234ABCD"",""nr.alternatePathHashes"":""55f97a7f,6fc8d18f,72827114,9a3ed934,a1744603,a7d2798f,be1039f5,ccadfd2c,da7edf2e,eaca716b""},{""identity.user"":""samw"",""identity.product"":""product""},{""Foo"":""Bar"",""Baz"":42}]";
            Assert.AreEqual(expected, actualResult);
        }

        [Test]
        public void is_synthetics_set_correctly()
        {
            // Arrange
            var userAttributes = new ReadOnlyDictionary<String, Object>(new Dictionary<String, Object>());
            var agentAttributes = new ReadOnlyDictionary<String, Object>(new Dictionary<String, Object>());
            var intrinsicAttributes = new ReadOnlyDictionary<String, Object>(new Dictionary<String, Object>());
            var isSyntheticsEvent = true;

            // Act
            var errorEventWireModel = new ErrorEventWireModel(agentAttributes, intrinsicAttributes, userAttributes, isSyntheticsEvent);

            // Assert
            Assert.IsTrue(errorEventWireModel.IsSynthetics());
        }
    }
}

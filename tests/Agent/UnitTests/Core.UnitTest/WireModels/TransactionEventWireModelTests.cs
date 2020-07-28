using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using NUnit.Framework;

namespace NewRelic.Agent.Core.WireModels.UnitTest
{
    public class TransactionEventWireModelTests
    {
        [TestFixture, Category("Analytics")]
        public class Method_ToJsonObject
        {
            [Test]
            public void all_fields_serializes_correctly()
            {
                // ARRANGE
                var userAttributes = new ReadOnlyDictionary<String, Object>(new Dictionary<String, Object>
                {
                    {"identity.user", "user"},
                    {"identity.product", "product"},
                });
                var agentAttributes = new ReadOnlyDictionary<String, Object>(new Dictionary<String, Object>
                {
                    {"Foo", "Bar"},
                    {"Baz", 42},
                });
                var intrinsicAttributes = new ReadOnlyDictionary<String, Object>(new Dictionary<String, Object>
                {
                    {"nr.tripId", "1234ABCD1234ABCD"},
                    {"nr.pathHash", "DCBA4321"},
                    {"nr.referringPathHash", "1234ABCD"},
                    {"nr.referringTransactionGuid", "DCBA43211234ABCD"},
                    {"nr.alternatePathHashes", "55f97a7f,6fc8d18f,72827114,9a3ed934,a1744603,a7d2798f,be1039f5,ccadfd2c,da7edf2e,eaca716b"},
                });
                var isSytheticsEvent = false;

                // ACT
                var transactionEventWireModel = new TransactionEventWireModel(userAttributes, agentAttributes, intrinsicAttributes, isSytheticsEvent);
                var actualResult = JsonConvert.SerializeObject(transactionEventWireModel);

                // ASSERT
                const string expected = @"[{""nr.tripId"":""1234ABCD1234ABCD"",""nr.pathHash"":""DCBA4321"",""nr.referringPathHash"":""1234ABCD"",""nr.referringTransactionGuid"":""DCBA43211234ABCD"",""nr.alternatePathHashes"":""55f97a7f,6fc8d18f,72827114,9a3ed934,a1744603,a7d2798f,be1039f5,ccadfd2c,da7edf2e,eaca716b""},{""identity.user"":""user"",""identity.product"":""product""},{""Foo"":""Bar"",""Baz"":42}]";
                Assert.AreEqual(expected, actualResult);
            }

            [Test]
            public void only_required_fields_serialize_correctly()
            {
                // Arrange
                var userAttributes = new ReadOnlyDictionary<String, Object>(new Dictionary<String, Object>());
                var agentAttributes = new ReadOnlyDictionary<String, Object>(new Dictionary<String, Object>());
                var intrinsicAttributes = new ReadOnlyDictionary<String, Object>(new Dictionary<String, Object>());
                var isSytheticsEvent = false;

                // Act
                var transactionEventWireModel = new TransactionEventWireModel(userAttributes, agentAttributes, intrinsicAttributes, isSytheticsEvent);
                var actualResult = JsonConvert.SerializeObject(transactionEventWireModel);

                // Assert
                const string expected = @"[{},{},{}]";
                Assert.AreEqual(expected, actualResult);
            }
        }
    }
}

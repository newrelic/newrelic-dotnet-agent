// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Attributes;
using NewRelic.Agent.Core.Fixtures;
using NewRelic.Agent.TestUtilities;
using Newtonsoft.Json;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.WireModels.UnitTest
{
    public class TransactionEventWireModelTests
    {
        [TestFixture, Category("Analytics")]
        public class Method_ToJsonObject
        {

            private static IConfiguration CreateMockConfiguration()
            {
                var configuration = Mock.Create<IConfiguration>();
                Mock.Arrange(() => configuration.ConfigurationVersion).Returns(int.MaxValue);
                Mock.Arrange(() => configuration.CaptureCustomParameters).Returns(true);
                Mock.Arrange(() => configuration.TransactionEventsEnabled).Returns(true);
                Mock.Arrange(() => configuration.TransactionEventsAttributesEnabled).Returns(true);
                Mock.Arrange(() => configuration.CaptureAttributes).Returns(true);
                Mock.Arrange(() => configuration.CrossApplicationTracingEnabled).Returns(true);
                //Mock.Arrange(() => configuration.CaptureAttributesExcludes)
                //    .Returns(new List<string>() { "identity.*", "request.headers.*", "response.headers.*" });
                Mock.Arrange(() => configuration.CaptureAttributesIncludes).Returns(new string[] { "request.parameters.*" });

                return configuration;
            }
            private ConfigurationAutoResponder _configAutoResponder;

            private IAttributeDefinitionService _attribDefSvc;
            private IAttributeDefinitions _attribDefs => _attribDefSvc?.AttributeDefs;

            [SetUp]
            public void Setup()
            {
                var config = CreateMockConfiguration();
                _configAutoResponder = new ConfigurationAutoResponder(config);
                _attribDefSvc = new AttributeDefinitionService((f) => new AttributeDefinitions(f));
            }

            [TearDown]
            public void TearDown()
            {
                _attribDefSvc.Dispose();
                _configAutoResponder.Dispose();
            }

            [Test]
            public void all_fields_serializes_correctly()
            {
                var attribValues = new AttributeValueCollection(AttributeDestinations.TransactionEvent);

                // ARRANGE
                var userAttributes = new Dictionary<string, object>
                {
                    {"identity.user", "user"},
                    {"identity.product", "product"},
                };

                _attribDefs.GetCustomAttributeForTransaction("identity.user").TrySetValue(attribValues, "user");
                _attribDefs.GetCustomAttributeForTransaction("identity.product").TrySetValue(attribValues, "product");

                var agentAttributes = new Dictionary<string, object>
                {
                    {"request.uri", "www.test.com" },
                };

                _attribDefs.RequestUri.TrySetValue(attribValues, "www.test.com");

                var intrinsicAttributes = new Dictionary<string, object>
                {
                    {"nr.tripId", "1234ABCD1234ABCD"},
                    {"nr.pathHash", "DCBA4321"},
                    {"nr.referringPathHash", "1234ABCD"},
                    {"nr.referringTransactionGuid", "DCBA43211234ABCD"},
                    {"nr.alternatePathHashes", "55f97a7f,6fc8d18f,72827114,9a3ed934,a1744603,a7d2798f,be1039f5,ccadfd2c,da7edf2e,eaca716b"},
                };

                _attribDefs.CatNrTripId.TrySetValue(attribValues, "1234ABCD1234ABCD");
                _attribDefs.CatNrPathHash.TrySetValue(attribValues, "DCBA4321");
                _attribDefs.CatReferringPathHash.TrySetValue(attribValues, "1234ABCD");
                _attribDefs.CatReferringTransactionGuidForEvents.TrySetValue(attribValues, "DCBA43211234ABCD");
                _attribDefs.CatAlternativePathHashes.TrySetValue(attribValues, new[] { "55f97a7f", "6fc8d18f", "72827114", "9a3ed934", "a1744603", "a7d2798f", "be1039f5", "ccadfd2c", "da7edf2e", "eaca716b" });

                var isSytheticsEvent = false;

                var expectedDictionaries = new Dictionary<string, object>[]
                {
                    intrinsicAttributes,
                    userAttributes,
                    agentAttributes
                };

                // ACT
                float priority = 0.5f;
                var transactionEventWireModel = new TransactionEventWireModel(attribValues, isSytheticsEvent, priority);
                var actualResult = JsonConvert.SerializeObject(transactionEventWireModel);
                var deserialized = JsonConvert.DeserializeObject<Dictionary<string, object>[]>(actualResult);

                // ASSERT
                AttributeComparer.CompareDictionaries(expectedDictionaries, deserialized);
            }

            [Test]
            public void only_required_fields_serialize_correctly()
            {
                // Arrange
                var attribValues = new AttributeValueCollection(AttributeDestinations.ErrorEvent);

                var isSytheticsEvent = false;

                var expectedDictionaries = new IDictionary<string, object>[]
                {
                    new Dictionary<string,object>(),
                    new Dictionary<string,object>(),
                    new Dictionary<string,object>()
                };

                // Act
                float priority = 0.5f;
                var transactionEventWireModel = new TransactionEventWireModel(attribValues, isSytheticsEvent, priority);
                var actualResult = JsonConvert.SerializeObject(transactionEventWireModel);
                var deserialized = JsonConvert.DeserializeObject<Dictionary<string, object>[]>(actualResult);

                // Assert
                AttributeComparer.CompareDictionaries(expectedDictionaries, deserialized);
            }

            [Test]
            public void Verify_setting_priority()
            {
                var attribValues = new AttributeValueCollection(AttributeDestinations.TransactionEvent);

                _attribDefs.Timestamp.TrySetValue(attribValues, DateTime.UtcNow);

                float priority = 0.5f;
                var emptyDictionary = new Dictionary<string, object>();
                var object1 = new TransactionEventWireModel(attribValues, false, priority);

                Assert.That(priority, Is.EqualTo(object1.Priority));

                priority = 0.0f;
                object1.Priority = priority;
                Assert.That(priority, Is.EqualTo(object1.Priority));

                priority = 1.0f;
                object1.Priority = priority;
                Assert.That(priority, Is.EqualTo(object1.Priority));

                priority = 1.1f;
                object1.Priority = priority;
                Assert.That(priority, Is.EqualTo(object1.Priority));

                priority = -0.00001f;
                Assert.Throws<ArgumentException>(() => object1.Priority = priority);
                priority = float.NaN;
                Assert.Throws<ArgumentException>(() => object1.Priority = priority);
                priority = float.NegativeInfinity;
                Assert.Throws<ArgumentException>(() => object1.Priority = priority);
                priority = float.PositiveInfinity;
                Assert.Throws<ArgumentException>(() => object1.Priority = priority);
                priority = float.MinValue;
                Assert.Throws<ArgumentException>(() => object1.Priority = priority);
            }
        }
    }
}

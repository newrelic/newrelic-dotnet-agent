// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Core;
using Newtonsoft.Json;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using NewRelic.Agent.TestUtilities;
using Telerik.JustMock;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Attributes;

namespace NewRelic.Agent.Core.WireModels
{
    [TestFixture, Category("ErrorEvents"), TestOf(typeof(ErrorEventWireModel))]
    public class ErrorEventWireModelTests
    {
        private const string TimeStampKey = "timestamp";

        private static IConfiguration CreateMockConfiguration()
        {
            var configuration = Mock.Create<IConfiguration>();
            Mock.Arrange(() => configuration.CaptureCustomParameters).Returns(true);
            Mock.Arrange(() => configuration.CaptureAttributes).Returns(true);
            Mock.Arrange(() => configuration.CaptureAttributesExcludes)
                .Returns(new List<string>() { "identity.*", "request.headers.*", "response.headers.*" });
            Mock.Arrange(() => configuration.CaptureAttributesIncludes).Returns(new string[] { "request.parameters.*" });

            return configuration;
        }

        private IAttributeDefinitionService _attribDefSvc;
        private IAttributeDefinitions _attribDefs => _attribDefSvc?.AttributeDefs;

        [SetUp]
        public void Setup()
        {
            var config = CreateMockConfiguration();

            _attribDefSvc = new AttributeDefinitionService((f) => new AttributeDefinitions(f));
        }

        [TearDown]
        public void TearDown()
        {
            _attribDefSvc.Dispose();
        }

        [Test]
        public void All_attribute_value_types_in_an_event_do_serialize_correctly()
        {
            var attribValues = new AttributeValueCollection(AttributeDestinations.ErrorEvent);


            // ARRANGE
            var userAttributes = new ReadOnlyDictionary<string, object>(new Dictionary<string, object>
                {
                    {"identity.user", "samw"},
                    {"identity.product", "product"}
                });

            _attribDefs.GetCustomAttributeForError("identity.user").TrySetValue(attribValues, "samw");
            _attribDefs.GetCustomAttributeForError("identity.product").TrySetValue(attribValues, "product");

            var agentAttributes = new ReadOnlyDictionary<string, object>(new Dictionary<string, object>
                {
                    {"queue_wait_time_ms", "2000"},
                    {"original_url", "www.test.com"},
                });

            _attribDefs.QueueWaitTime.TrySetValue(attribValues, TimeSpan.FromSeconds(2));
            _attribDefs.OriginalUrl.TrySetValue(attribValues, "www.test.com");

            var intrinsicAttributes = new ReadOnlyDictionary<string, object>(new Dictionary<string, object>
                {
                    {"databaseCallCount", 10d },
                    {"error.message", "This is the error message"},
                    {"nr.referringTransactionGuid", "DCBA43211234ABCD"},
                });

            _attribDefs.DatabaseCallCount.TrySetValue(attribValues, 10);
            _attribDefs.ErrorDotMessage.TrySetValue(attribValues, "This is the error message");
            _attribDefs.CatNrPathHash.TrySetValue(attribValues, "DCBA4321");
            _attribDefs.CatReferringPathHash.TrySetValue(attribValues, "1234ABCD");
            _attribDefs.CatReferringTransactionGuidForEvents.TrySetValue(attribValues, "DCBA43211234ABCD");
            _attribDefs.CatAlternativePathHashes.TrySetValue(attribValues, new[] { "55f97a7f", "6fc8d18f", "72827114", "9a3ed934", "a1744603", "a7d2798f", "be1039f5", "ccadfd2c", "da7edf2e", "eaca716b" });

            var isSyntheticsEvent = false;

            // ACT
            float priority = 0.5f;
            var errorEventWireModel = new ErrorEventWireModel(attribValues, isSyntheticsEvent, priority);
            var serialized = JsonConvert.SerializeObject(errorEventWireModel);
            var deserialized = JsonConvert.DeserializeObject<IDictionary<string, object>[]>(serialized);

            // ASSERT
            var expected = new IDictionary<string, object>[3]{
                intrinsicAttributes,
                userAttributes,
                agentAttributes
            };

            AttributeComparer.CompareDictionaries(expected, deserialized);
        }

        [Test]
        public void Is_synthetics_set_correctly()
        {
            // Arrange
            var attribValues = new AttributeValueCollection(AttributeDestinations.ErrorEvent);
            var isSyntheticsEvent = true;

            // Act
            float priority = 0.5f;
            var errorEventWireModel = new ErrorEventWireModel(attribValues, isSyntheticsEvent, priority);

            // Assert
            Assert.That(errorEventWireModel.IsSynthetics, Is.True);
        }

        [Test]
        public void Verify_setting_priority()
        {
            var priority = 0.5f;

            var attribValues = new AttributeValueCollection(AttributeDestinations.ErrorEvent);
            _attribDefs.TimestampForError.TrySetValue(attribValues, DateTime.UtcNow);

            var wireModel = new ErrorEventWireModel(attribValues, false, priority);

            Assert.That(priority, Is.EqualTo(wireModel.Priority));

            priority = 0.0f;
            wireModel.Priority = priority;
            Assert.That(priority, Is.EqualTo(wireModel.Priority));

            priority = 1.0f;
            wireModel.Priority = priority;
            Assert.That(priority, Is.EqualTo(wireModel.Priority));

            priority = 1.1f;
            wireModel.Priority = priority;
            Assert.That(priority, Is.EqualTo(wireModel.Priority));

            priority = -0.00001f;
            Assert.Throws<ArgumentException>(() => wireModel.Priority = priority);
            priority = float.NaN;
            Assert.Throws<ArgumentException>(() => wireModel.Priority = priority);
            priority = float.NegativeInfinity;
            Assert.Throws<ArgumentException>(() => wireModel.Priority = priority);
            priority = float.PositiveInfinity;
            Assert.Throws<ArgumentException>(() => wireModel.Priority = priority);
            priority = float.MinValue;
            Assert.Throws<ArgumentException>(() => wireModel.Priority = priority);
        }
    }
}

// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Aggregators;
using NewRelic.Agent.Core.WireModels;
using NewRelic.SystemExtensions.Collections.Generic;
using NewRelic.Testing.Assertions;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.Transformers
{
    [TestFixture]
    public class CustomEventTransformerTests
    {
        private CustomEventTransformer _customEventTransformer;
        private IConfigurationService _configurationService;
        private ICustomEventAggregator _customEventAggregator;
        private CustomEventWireModel _lastPublishedCustomEvent;

        [SetUp]
        public void SetUp()
        {
            _lastPublishedCustomEvent = null;

            _configurationService = Mock.Create<IConfigurationService>();
            Mock.Arrange(() => _configurationService.Configuration.CustomEventsEnabled).Returns(true);

            _customEventAggregator = Mock.Create<ICustomEventAggregator>();
            Mock.Arrange(() => _customEventAggregator.Collect(Arg.IsAny<CustomEventWireModel>()))
                .DoInstead<CustomEventWireModel>(customEvent => _lastPublishedCustomEvent = customEvent);

            _customEventTransformer = new CustomEventTransformer(_configurationService, _customEventAggregator);
        }

        [Test]
        public void Transform_CreatesCustomEvents_IfInputIsValid()
        {
            const string expectedEventType = "MyEventType";
            var expectedAttributes = new Dictionary<string, object>
            {
                {"key1", "value1"},
                {"key2", "key2"}
            };

            _customEventTransformer.Transform(expectedEventType, expectedAttributes);

            Assert.NotNull(_lastPublishedCustomEvent);

            var intrinsicAttributes = _lastPublishedCustomEvent.IntrinsicAttributes.ToDictionary();
            var userAttributes = _lastPublishedCustomEvent.UserAttributes.ToDictionary();
            NrAssert.Multiple(
                () => Assert.AreEqual(2, intrinsicAttributes.Count),
                () => Assert.AreEqual(expectedEventType, intrinsicAttributes["type"]),
                () => Assert.IsTrue(intrinsicAttributes.ContainsKey("type")),

                () => Assert.AreEqual(2, userAttributes.Count),
                () => Assert.AreEqual("value1", userAttributes["key1"]),
                () => Assert.AreEqual("key2", userAttributes["key2"])
                );
        }

        [Test]
        public void Transform_OnlyRetainsAttributes_IfValuesAreStringOrSingle()
        {
            const string expectedEventType = "MyEventType";
            var expectedAttributes = new Dictionary<string, object>
            {
                {"key1", "value1"},
                {"key2", 2.0f},
                {"key3", 2.0d},
                {"key4", 2},
                {"key5", 2u}
            };

            _customEventTransformer.Transform(expectedEventType, expectedAttributes);

            Assert.NotNull(_lastPublishedCustomEvent);

            var userAttributes = _lastPublishedCustomEvent.UserAttributes.ToDictionary();
            NrAssert.Multiple(
                () => Assert.AreEqual(2, userAttributes.Count),
                () => Assert.AreEqual("value1", userAttributes["key1"]),
                () => Assert.AreEqual(2.0f, userAttributes["key2"])
                );
        }

        [Test]
        public void Transform_DoesNotCreateCustomEvents_IfCustomEventsAreDisabled()
        {
            Mock.Arrange(() => _configurationService.Configuration.CustomEventsEnabled)
                .Returns(false);

            const string expectedEventType = "MyEventType";
            var expectedAttributes = new Dictionary<string, object>
            {
                {"key1", "value1"},
                {"key2", "key2"}
            };

            _customEventTransformer.Transform(expectedEventType, expectedAttributes);

            Assert.IsNull(_lastPublishedCustomEvent);
        }

        [Test]
        public void Transform_Throws_IfEventTypeIsTooLarge()
        {
            var expectedEventType = new string('a', 257);
            var expectedAttributes = new Dictionary<string, object>
            {
                {"key1", "value1"},
                {"key2", "key2"}
            };

            Assert.Throws<Exception>(() => _customEventTransformer.Transform(expectedEventType, expectedAttributes));
        }

        [Test]
        public void Transform_Throws_IfEventTypeIsNotAlphanumeric()
        {
            const string expectedEventType = "This has symbols!!";
            var expectedAttributes = new Dictionary<string, object>
            {
                {"key1", "value1"},
                {"key2", "key2"}
            };

            Assert.Throws<Exception>(() => _customEventTransformer.Transform(expectedEventType, expectedAttributes));
        }
    }
}

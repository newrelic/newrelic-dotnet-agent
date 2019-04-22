using System;
using System.Collections.Generic;
using JetBrains.Annotations;
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
		[NotNull]
		private CustomEventTransformer _customEventTransformer;

		[NotNull]
		private IConfigurationService _configurationService;

		[NotNull]
		private ICustomEventAggregator _customEventAggregator;

		[CanBeNull]
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
			const String expectedEventType = "MyEventType";
			var expectedAttributes = new Dictionary<String, Object>
			{
				{"key1", "value1"},
				{"key2", "key2"}
			};

			var priority = 0.5f;
			_customEventTransformer.Transform(expectedEventType, expectedAttributes, priority);

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
			const String expectedEventType = "MyEventType";
			var expectedAttributes = new Dictionary<String, Object>
			{
				{"key1", "value1"},
				{"key2", 2.0f},
				{"key3", 2.0d},
				{"key4", 2},
				{"key5", 2u}
			};

			var priority = 0.5f;
			_customEventTransformer.Transform(expectedEventType, expectedAttributes, priority);

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

			const String expectedEventType = "MyEventType";
			var expectedAttributes = new Dictionary<String, Object>
			{
				{"key1", "value1"},
				{"key2", "key2"}
			};

			var priority = 0.5f;
			_customEventTransformer.Transform(expectedEventType, expectedAttributes, priority);

			Assert.IsNull(_lastPublishedCustomEvent);
		}

		[Test]
		public void Transform_Throws_IfEventTypeIsTooLarge()
		{
			var expectedEventType = new String('a', 257);
			var expectedAttributes = new Dictionary<String, Object>
			{
				{"key1", "value1"},
				{"key2", "key2"}
			};
			var priority = 0.5f;
			Assert.Throws<Exception>(() => _customEventTransformer.Transform(expectedEventType, expectedAttributes, priority));
		}

		[Test]
		public void Transform_Throws_IfEventTypeIsNotAlphanumeric()
		{
			const String expectedEventType = "This has symbols!!";
			var expectedAttributes = new Dictionary<String, Object>
			{
				{"key1", "value1"},
				{"key2", "key2"}
			};

			var priority = 0.5f;
			Assert.Throws<Exception>(() => _customEventTransformer.Transform(expectedEventType, expectedAttributes, priority));
		}
	}
}

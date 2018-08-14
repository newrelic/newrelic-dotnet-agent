using NewRelic.Agent.Core.Utilities;
using Newtonsoft.Json;
using NUnit.Framework;
using System;
using System.Collections.Generic;

namespace NewRelic.Agent.Core.WireModels
{
	[TestFixture, Category("ErrorEvents"), TestOf(typeof(ErrorEventWireModel))]
	public class ErrorEventWireModelTests
	{
		private const string TimeStampKey = "timestamp";

		[Test]
		public void All_attribute_value_types_in_an_event_do_serialize_correctly()
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
			float priority = 0.5f;
			var errorEventWireModel = new ErrorEventWireModel(agentAttributes, intrinsicAttributes, userAttributes, isSyntheticsEvent, priority);
			var actualResult = JsonConvert.SerializeObject(errorEventWireModel);

			// ASSERT
			const string expected = @"[{""databaseCallCount"":10,""errormessage"":""This is the error message"",""nr.pathHash"":""DCBA4321"",""nr.referringPathHash"":""1234ABCD"",""nr.referringTransactionGuid"":""DCBA43211234ABCD"",""nr.alternatePathHashes"":""55f97a7f,6fc8d18f,72827114,9a3ed934,a1744603,a7d2798f,be1039f5,ccadfd2c,da7edf2e,eaca716b""},{""identity.user"":""samw"",""identity.product"":""product""},{""Foo"":""Bar"",""Baz"":42}]";
			Assert.AreEqual(expected, actualResult);
		}

		[Test]
		public void Is_synthetics_set_correctly()
		{
			// Arrange
			var userAttributes = new ReadOnlyDictionary<String, Object>(new Dictionary<String, Object>());
			var agentAttributes = new ReadOnlyDictionary<String, Object>(new Dictionary<String, Object>());
			var intrinsicAttributes = new ReadOnlyDictionary<String, Object>(new Dictionary<String, Object>());
			var isSyntheticsEvent = true;

			// Act
			float priority = 0.5f;
			var errorEventWireModel = new ErrorEventWireModel(agentAttributes, intrinsicAttributes, userAttributes, isSyntheticsEvent, priority);

			// Assert
			Assert.IsTrue(errorEventWireModel.IsSynthetics());
		}

		[Test]
		public void Verify_setting_priority()
		{
			var priority = 0.5f;
			var emptyDictionary = new Dictionary<string, object>();
			var intrinsicAttributes = new Dictionary<String, Object> { { TimeStampKey, DateTime.UtcNow.ToUnixTimeMilliseconds() } };
			var object1 = new ErrorEventWireModel(emptyDictionary, intrinsicAttributes, emptyDictionary, false, priority);

			Assert.That(priority == object1.Priority);

			priority = 0.0f;
			object1.Priority = priority;
			Assert.That(priority == object1.Priority);

			priority = 1.0f;
			object1.Priority = priority;
			Assert.That(priority == object1.Priority);

			priority = 1.1f;
			object1.Priority = priority;
			Assert.That(priority == object1.Priority);

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

using NewRelic.Agent.TestUtilities;
using NewRelic.Core;
using Newtonsoft.Json;
using NUnit.Framework;
using System;
using System.Collections.Generic;

// ReSharper disable InconsistentNaming
// ReSharper disable CheckNamespace
namespace NewRelic.Agent.Core.WireModels.UnitTest
{
	public class TransactionEventWireModelTests
	{
		[TestFixture, Category("Analytics")]
		public class Method_ToJsonObject
		{
			private const string TimeStampKey = "timestamp";

			[Test]
			public void all_fields_serializes_correctly()
			{
				// ARRANGE
				var userAttributes = new Dictionary<string, object>
				{
					{"identity.user", "user"},
					{"identity.product", "product"},
				};
				var agentAttributes = new Dictionary<string, object>
				{
					{"Foo", "Bar"},
					{"Baz", 42},
				};
				var intrinsicAttributes = new Dictionary<string, object>
				{
					{"nr.tripId", "1234ABCD1234ABCD"},
					{"nr.pathHash", "DCBA4321"},
					{"nr.referringPathHash", "1234ABCD"},
					{"nr.referringTransactionGuid", "DCBA43211234ABCD"},
					{"nr.alternatePathHashes", "55f97a7f,6fc8d18f,72827114,9a3ed934,a1744603,a7d2798f,be1039f5,ccadfd2c,da7edf2e,eaca716b"},
				};
				var isSytheticsEvent = false;

				var expectedDictionaries = new Dictionary<string, object>[]
				{
					intrinsicAttributes,
					userAttributes,
					agentAttributes
				};

				// ACT
				float priority = 0.5f;
				var transactionEventWireModel = new TransactionEventWireModel(userAttributes, agentAttributes, intrinsicAttributes, isSytheticsEvent, priority);
				var actualResult = JsonConvert.SerializeObject(transactionEventWireModel);
				var deserialized = JsonConvert.DeserializeObject<Dictionary<string,object>[]>(actualResult);

				// ASSERT
				AttributeComparer.CompareDictionaries(expectedDictionaries, deserialized);
			}

			[Test]
			public void only_required_fields_serialize_correctly()
			{
				// Arrange
				var userAttributes = new ReadOnlyDictionary<string, object>(new Dictionary<string, object>());
				var agentAttributes = new ReadOnlyDictionary<string, object>(new Dictionary<string, object>());
				var intrinsicAttributes = new ReadOnlyDictionary<string, object>(new Dictionary<string, object>());
				var isSytheticsEvent = false;

				var expectedDictionaries = new IDictionary<string, object>[]
				{
					intrinsicAttributes,
					userAttributes,
					agentAttributes
				};

				// Act
				float priority = 0.5f;
				var transactionEventWireModel = new TransactionEventWireModel(userAttributes, agentAttributes, intrinsicAttributes, isSytheticsEvent, priority);
				var actualResult = JsonConvert.SerializeObject(transactionEventWireModel);
				var deserialized = JsonConvert.DeserializeObject<Dictionary<string, object>[]>(actualResult);

				// Assert
				AttributeComparer.CompareDictionaries(expectedDictionaries, deserialized);
			}

			[Test]
			public void Verify_setting_priority()
			{
				float priority = 0.5f;
				var emptyDictionary = new Dictionary<string, object>();
				var intrinsicAttributes = new Dictionary<string, object> { { TimeStampKey, DateTime.UtcNow.ToUnixTimeMilliseconds() } };
				var object1 = new TransactionEventWireModel(emptyDictionary, emptyDictionary, intrinsicAttributes, false, priority);

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
}

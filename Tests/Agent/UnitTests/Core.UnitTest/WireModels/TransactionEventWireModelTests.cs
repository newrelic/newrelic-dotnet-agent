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
				var userAttributes = new ReadOnlyDictionary<string, object>(new Dictionary<string, object>
				{
					{"identity.user", "user"},
					{"identity.product", "product"},
				});
				var agentAttributes = new ReadOnlyDictionary<string, object>(new Dictionary<string, object>
				{
					{"Foo", "Bar"},
					{"Baz", 42},
				});
				var intrinsicAttributes = new ReadOnlyDictionary<string, object>(new Dictionary<string, object>
				{
					{"nr.tripId", "1234ABCD1234ABCD"},
					{"nr.pathHash", "DCBA4321"},
					{"nr.referringPathHash", "1234ABCD"},
					{"nr.referringTransactionGuid", "DCBA43211234ABCD"},
					{"nr.alternatePathHashes", "55f97a7f,6fc8d18f,72827114,9a3ed934,a1744603,a7d2798f,be1039f5,ccadfd2c,da7edf2e,eaca716b"},
				});
				var isSytheticsEvent = false;

				// ACT
				float priority = 0.5f;
				var transactionEventWireModel = new TransactionEventWireModel(userAttributes, agentAttributes, intrinsicAttributes, isSytheticsEvent, priority, false, false);
				var actualResult = JsonConvert.SerializeObject(transactionEventWireModel);

				// ASSERT
				const string expected = @"[{""nr.tripId"":""1234ABCD1234ABCD"",""nr.pathHash"":""DCBA4321"",""nr.referringPathHash"":""1234ABCD"",""nr.referringTransactionGuid"":""DCBA43211234ABCD"",""nr.alternatePathHashes"":""55f97a7f,6fc8d18f,72827114,9a3ed934,a1744603,a7d2798f,be1039f5,ccadfd2c,da7edf2e,eaca716b""},{""identity.user"":""user"",""identity.product"":""product""},{""Foo"":""Bar"",""Baz"":42}]";
				Assert.AreEqual(expected, actualResult);
			}

			[Test]
			public void only_required_fields_serialize_correctly()
			{
				// Arrange
				var userAttributes = new ReadOnlyDictionary<string, object>(new Dictionary<string, object>());
				var agentAttributes = new ReadOnlyDictionary<string, object>(new Dictionary<string, object>());
				var intrinsicAttributes = new ReadOnlyDictionary<string, object>(new Dictionary<string, object>());
				var isSytheticsEvent = false;

				// Act
				float priority = 0.5f;
				var transactionEventWireModel = new TransactionEventWireModel(userAttributes, agentAttributes, intrinsicAttributes, isSytheticsEvent, priority, false, false);
				var actualResult = JsonConvert.SerializeObject(transactionEventWireModel);

				// Assert
				const string expected = @"[{},{},{}]";
				Assert.AreEqual(expected, actualResult);
			}

			[Test]
			public void Verify_setting_priority()
			{
				float priority = 0.5f;
				var emptyDictionary = new Dictionary<string, object>();
				var intrinsicAttributes = new Dictionary<string, object> { { TimeStampKey, DateTime.UtcNow.ToUnixTimeMilliseconds() } };
				var object1 = new TransactionEventWireModel(emptyDictionary, emptyDictionary, intrinsicAttributes, false, priority, false, false);

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

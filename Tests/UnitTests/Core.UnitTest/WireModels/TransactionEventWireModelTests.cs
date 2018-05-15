using System;
using System.Collections.Generic;
using System.Threading;
using Newtonsoft.Json;
using NewRelic.Agent.Core.Utilities;
using NUnit.Framework;

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
				float priority = 0.5f;
				var transactionEventWireModel = new TransactionEventWireModel(userAttributes, agentAttributes, intrinsicAttributes, isSytheticsEvent, priority);
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
				float priority = 0.5f;
				var transactionEventWireModel = new TransactionEventWireModel(userAttributes, agentAttributes, intrinsicAttributes, isSytheticsEvent, priority);
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
				var intrinsicAttributes = new Dictionary<String, Object> { { TimeStampKey, DateTime.UtcNow.ToUnixTime() } };
				var object1 = new TransactionEventWireModel(emptyDictionary, emptyDictionary, intrinsicAttributes, false, priority);

				Assert.That(priority == object1.Priority);

				priority = 0.0f;
				object1.Priority = priority;
				Assert.That(priority == object1.Priority);

				priority = 1.0f;
				object1.Priority = priority;
				Assert.That(priority == object1.Priority);

				priority = 1.1f;
				Assert.Throws<ArgumentException>(() => object1.Priority = priority);
				priority = -0.00001f;
				Assert.Throws<ArgumentException>(() => object1.Priority = priority);
				priority = float.NaN;
				Assert.Throws<ArgumentException>(() => object1.Priority = priority);
				priority = float.NegativeInfinity;
				Assert.Throws<ArgumentException>(() => object1.Priority = priority);
				priority = float.PositiveInfinity;
				Assert.Throws<ArgumentException>(() => object1.Priority = priority);
				priority = float.MaxValue;
				Assert.Throws<ArgumentException>(() => object1.Priority = priority);
				priority = float.MinValue;
				Assert.Throws<ArgumentException>(() => object1.Priority = priority);
			}

			[Test]
			public void Verify_comparer_operations()
			{
				var comparer = new TransactionEventWireModel.PriorityTimestampComparer();

				float priority = 0.5f;
				var emptyDictionary = new Dictionary<string, object>();
				var intrinsicAttributes1 = new Dictionary<String, Object> { { TimeStampKey, DateTime.UtcNow.ToUnixTime() } };
				Thread.Sleep(1);
				var intrinsicAttributes2 = new Dictionary<String, Object> { { TimeStampKey, DateTime.UtcNow.ToUnixTime() } };

				//same priority, same timestamp
				var object1 = new TransactionEventWireModel(emptyDictionary, emptyDictionary, intrinsicAttributes1, false, priority);
				var object2 = new TransactionEventWireModel(emptyDictionary, emptyDictionary, intrinsicAttributes1, false, priority);
				Assert.True(0 == comparer.Compare(object1, object2));
				//same priority, timestamp later
				var object3 = new TransactionEventWireModel(emptyDictionary, emptyDictionary, intrinsicAttributes2, false, priority);
				//same priority, object1.timestamp < object2.timestamp
				Assert.True(-1 == comparer.Compare(object1, object3));
				//same priority, object3.timestamp > object1.timestamp
				Assert.True(1 == comparer.Compare(object3, object1));

				var object4 = new TransactionEventWireModel(emptyDictionary, emptyDictionary, emptyDictionary, false, priority);
				//x param does not have a timestamp
				var ex = Assert.Throws<ArgumentException>(() => comparer.Compare(object4, object1));
				Assert.That(ex.ParamName == "x");

				//y param does not have a timestamp
				ex = Assert.Throws<ArgumentException>(() => comparer.Compare(object1, object4));
				Assert.That(ex.ParamName == "y");

				Assert.True(1 == comparer.Compare(object1, null));
				Assert.True(-1 == comparer.Compare(null, object1));
				Assert.True(0 == comparer.Compare(null, null));
			}

		}
	}
}

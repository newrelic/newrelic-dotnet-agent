using System;
using NewRelic.Testing.Assertions;
using Newtonsoft.Json;
using NUnit.Framework;

namespace NewRelic.Agent.Core.Wrapper.AgentWrapperApi.CrossApplicationTracing
{
	[TestFixture]
	public class CrossApplicationResponseDataTests
	{
		[Test]
		public void SerializesCorrectly()
		{
			var data = new CrossApplicationResponseData("crossProcessId", "transactionName", 1.1f, 2.2f, 3, "guid");
			var serialized = data.ToJson();

			Assert.AreEqual("[\"crossProcessId\",\"transactionName\",1.1,2.2,3,\"guid\",false]", serialized);
		}

		[Test]
		public void DeserializesCorrectly()
		{
			const String json = "[\"crossProcessId\",\"transactionName\",1.1,2.2,3,\"guid\",false]";
			var deserialized = CrossApplicationResponseData.TryBuildIncomingDataFromJson(json);

			Assert.NotNull(deserialized);
			NrAssert.Multiple(
				() => Assert.AreEqual("crossProcessId", deserialized.CrossProcessId),
				() => Assert.AreEqual("transactionName", deserialized.TransactionName),
				() => Assert.AreEqual(1.1f, deserialized.QueueTimeInSeconds),
				() => Assert.AreEqual(2.2f, deserialized.ResponseTimeInSeconds),
				() => Assert.AreEqual(3, deserialized.ContentLength),
				() => Assert.AreEqual("guid", deserialized.TransactionGuid),
				() => Assert.AreEqual(false, deserialized.Unused)
				);
		}

		[Test]
		public void DeserializesCorrectly_IfOnly6Elements()
		{
			const String json = "[\"crossProcessId\",\"transactionName\",1.1,2.2,3,\"guid\"]";
			var deserialized = CrossApplicationResponseData.TryBuildIncomingDataFromJson(json);

			Assert.NotNull(deserialized);
			NrAssert.Multiple(
				() => Assert.AreEqual("crossProcessId", deserialized.CrossProcessId),
				() => Assert.AreEqual("transactionName", deserialized.TransactionName),
				() => Assert.AreEqual(1.1f, deserialized.QueueTimeInSeconds),
				() => Assert.AreEqual(2.2f, deserialized.ResponseTimeInSeconds),
				() => Assert.AreEqual(3, deserialized.ContentLength),
				() => Assert.AreEqual("guid", deserialized.TransactionGuid),
				() => Assert.AreEqual(false, deserialized.Unused)
				);
		}

		[Test]
		public void DeserializesCorrectly_IfOnly5Elements()
		{
			const String json = "[\"crossProcessId\",\"transactionName\",1.1,2.2,3]";
			var deserialized = CrossApplicationResponseData.TryBuildIncomingDataFromJson(json);

			Assert.NotNull(deserialized);
			NrAssert.Multiple(
				() => Assert.AreEqual("crossProcessId", deserialized.CrossProcessId),
				() => Assert.AreEqual("transactionName", deserialized.TransactionName),
				() => Assert.AreEqual(1.1f, deserialized.QueueTimeInSeconds),
				() => Assert.AreEqual(2.2f, deserialized.ResponseTimeInSeconds),
				() => Assert.AreEqual(3, deserialized.ContentLength),
				() => Assert.AreEqual(null, deserialized.TransactionGuid),
				() => Assert.AreEqual(false, deserialized.Unused)
				);
		}

		[Test]
		public void CannotDeserialize_IfOnly4Elements()
		{
			const String json = "[\"crossProcessId\",\"transactionName\",1.1,2.2]";
			Assert.IsNull(CrossApplicationResponseData.TryBuildIncomingDataFromJson(json));
		}
	}
}

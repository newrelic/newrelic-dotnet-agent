using NewRelic.Testing.Assertions;
using Newtonsoft.Json;
using NUnit.Framework;

namespace NewRelic.Agent.Core.Wrapper.AgentWrapperApi.CrossApplicationTracing
{
	[TestFixture]
	public class CrossApplicationRequestDataTests
	{
		[Test]
		public void SerializesCorrectly()
		{
			var data = new CrossApplicationRequestData("guid", true, "tripId", "pathHash");

			var serialized = data.ToJson();

			Assert.AreEqual("[\"guid\",true,\"tripId\",\"pathHash\"]", serialized);
		}

		[Test]
		public void DeserializesCorrectly()
		{
			var json = "[\"guid\",true,\"tripId\",\"pathHash\"]";
			var deserialized = CrossApplicationRequestData.TryBuildIncomingDataFromJson(json);

			Assert.NotNull(deserialized);
			NrAssert.Multiple(
				() => Assert.AreEqual("guid", deserialized.TransactionGuid),
				() => Assert.AreEqual(true, deserialized.Unused),
				() => Assert.AreEqual("tripId", deserialized.TripId),
				() => Assert.AreEqual("pathHash", deserialized.PathHash)
				);
		}
	}
}

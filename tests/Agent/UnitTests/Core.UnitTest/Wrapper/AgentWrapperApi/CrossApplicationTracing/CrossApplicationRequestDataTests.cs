// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using Newtonsoft.Json;

namespace NewRelic.Agent.Core.Wrapper.AgentWrapperApi.CrossApplicationTracing
{
    [TestFixture]
    public class CrossApplicationRequestDataTests
    {
        [Test]
        public void SerializesCorrectly()
        {
            var data = new CrossApplicationRequestData("guid", true, "tripId", "pathHash");

            var serialized = JsonConvert.SerializeObject(data);

            ClassicAssert.AreEqual("[\"guid\",true,\"tripId\",\"pathHash\"]", serialized);
        }

        [Test]
        public void DeserializesCorrectly()
        {
            var json = "[\"guid\",true,\"tripId\",\"pathHash\"]";
            var deserialized = JsonConvert.DeserializeObject<CrossApplicationRequestData>(json);

            ClassicAssert.NotNull(deserialized);
            NrAssert.Multiple(
                () => ClassicAssert.AreEqual("guid", deserialized.TransactionGuid),
                () => ClassicAssert.AreEqual(true, deserialized.Unused),
                () => ClassicAssert.AreEqual("tripId", deserialized.TripId),
                () => ClassicAssert.AreEqual("pathHash", deserialized.PathHash)
                );
        }
    }
}

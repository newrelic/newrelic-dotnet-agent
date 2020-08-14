// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

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

            var serialized = JsonConvert.SerializeObject(data);

            Assert.AreEqual("[\"guid\",true,\"tripId\",\"pathHash\"]", serialized);
        }

        [Test]
        public void DeserializesCorrectly()
        {
            var json = "[\"guid\",true,\"tripId\",\"pathHash\"]";
            var deserialized = JsonConvert.DeserializeObject<CrossApplicationRequestData>(json);

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

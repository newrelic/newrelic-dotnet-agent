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

            Assert.That(serialized, Is.EqualTo("[\"guid\",true,\"tripId\",\"pathHash\"]"));
        }

        [Test]
        public void DeserializesCorrectly()
        {
            var json = "[\"guid\",true,\"tripId\",\"pathHash\"]";
            var deserialized = JsonConvert.DeserializeObject<CrossApplicationRequestData>(json);

            Assert.That(deserialized, Is.Not.Null);
            NrAssert.Multiple(
                () => Assert.That(deserialized.TransactionGuid, Is.EqualTo("guid")),
                () => Assert.That(deserialized.Unused, Is.EqualTo(true)),
                () => Assert.That(deserialized.TripId, Is.EqualTo("tripId")),
                () => Assert.That(deserialized.PathHash, Is.EqualTo("pathHash"))
                );
        }
    }
}

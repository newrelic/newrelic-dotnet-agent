// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

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
            var serialized = JsonConvert.SerializeObject(data);

            Assert.That(serialized, Is.EqualTo("[\"crossProcessId\",\"transactionName\",1.1,2.2,3,\"guid\",false]"));
        }

        [Test]
        public void DeserializesCorrectly()
        {
            const string json = "[\"crossProcessId\",\"transactionName\",1.1,2.2,3,\"guid\",false]";
            var deserialized = JsonConvert.DeserializeObject<CrossApplicationResponseData>(json);

            Assert.That(deserialized, Is.Not.Null);
            NrAssert.Multiple(
                () => Assert.That(deserialized.CrossProcessId, Is.EqualTo("crossProcessId")),
                () => Assert.That(deserialized.TransactionName, Is.EqualTo("transactionName")),
                () => Assert.That(deserialized.QueueTimeInSeconds, Is.EqualTo(1.1f)),
                () => Assert.That(deserialized.ResponseTimeInSeconds, Is.EqualTo(2.2f)),
                () => Assert.That(deserialized.ContentLength, Is.EqualTo(3)),
                () => Assert.That(deserialized.TransactionGuid, Is.EqualTo("guid")),
                () => Assert.That(deserialized.Unused, Is.EqualTo(false))
                );
        }

        [Test]
        public void DeserializesCorrectly_IfOnly6Elements()
        {
            const string json = "[\"crossProcessId\",\"transactionName\",1.1,2.2,3,\"guid\"]";
            var deserialized = JsonConvert.DeserializeObject<CrossApplicationResponseData>(json);

            Assert.That(deserialized, Is.Not.Null);
            NrAssert.Multiple(
                () => Assert.That(deserialized.CrossProcessId, Is.EqualTo("crossProcessId")),
                () => Assert.That(deserialized.TransactionName, Is.EqualTo("transactionName")),
                () => Assert.That(deserialized.QueueTimeInSeconds, Is.EqualTo(1.1f)),
                () => Assert.That(deserialized.ResponseTimeInSeconds, Is.EqualTo(2.2f)),
                () => Assert.That(deserialized.ContentLength, Is.EqualTo(3)),
                () => Assert.That(deserialized.TransactionGuid, Is.EqualTo("guid")),
                () => Assert.That(deserialized.Unused, Is.EqualTo(false))
                );
        }

        [Test]
        public void DeserializesCorrectly_IfOnly5Elements()
        {
            const string json = "[\"crossProcessId\",\"transactionName\",1.1,2.2,3]";
            var deserialized = JsonConvert.DeserializeObject<CrossApplicationResponseData>(json);

            Assert.That(deserialized, Is.Not.Null);
            NrAssert.Multiple(
                () => Assert.That(deserialized.CrossProcessId, Is.EqualTo("crossProcessId")),
                () => Assert.That(deserialized.TransactionName, Is.EqualTo("transactionName")),
                () => Assert.That(deserialized.QueueTimeInSeconds, Is.EqualTo(1.1f)),
                () => Assert.That(deserialized.ResponseTimeInSeconds, Is.EqualTo(2.2f)),
                () => Assert.That(deserialized.ContentLength, Is.EqualTo(3)),
                () => Assert.That(deserialized.TransactionGuid, Is.EqualTo(null)),
                () => Assert.That(deserialized.Unused, Is.EqualTo(false))
                );
        }

        [Test]
        public void CannotDeserialize_IfOnly4Elements()
        {
            const string json = "[\"crossProcessId\",\"transactionName\",1.1,2.2]";
            Assert.That(JsonConvert.DeserializeObject<CrossApplicationResponseData>(json), Is.Null);
        }
    }
}

// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using Newtonsoft.Json;

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

            ClassicAssert.AreEqual("[\"crossProcessId\",\"transactionName\",1.1,2.2,3,\"guid\",false]", serialized);
        }

        [Test]
        public void DeserializesCorrectly()
        {
            const string json = "[\"crossProcessId\",\"transactionName\",1.1,2.2,3,\"guid\",false]";
            var deserialized = JsonConvert.DeserializeObject<CrossApplicationResponseData>(json);

            ClassicAssert.NotNull(deserialized);
            NrAssert.Multiple(
                () => ClassicAssert.AreEqual("crossProcessId", deserialized.CrossProcessId),
                () => ClassicAssert.AreEqual("transactionName", deserialized.TransactionName),
                () => ClassicAssert.AreEqual(1.1f, deserialized.QueueTimeInSeconds),
                () => ClassicAssert.AreEqual(2.2f, deserialized.ResponseTimeInSeconds),
                () => ClassicAssert.AreEqual(3, deserialized.ContentLength),
                () => ClassicAssert.AreEqual("guid", deserialized.TransactionGuid),
                () => ClassicAssert.AreEqual(false, deserialized.Unused)
                );
        }

        [Test]
        public void DeserializesCorrectly_IfOnly6Elements()
        {
            const string json = "[\"crossProcessId\",\"transactionName\",1.1,2.2,3,\"guid\"]";
            var deserialized = JsonConvert.DeserializeObject<CrossApplicationResponseData>(json);

            ClassicAssert.NotNull(deserialized);
            NrAssert.Multiple(
                () => ClassicAssert.AreEqual("crossProcessId", deserialized.CrossProcessId),
                () => ClassicAssert.AreEqual("transactionName", deserialized.TransactionName),
                () => ClassicAssert.AreEqual(1.1f, deserialized.QueueTimeInSeconds),
                () => ClassicAssert.AreEqual(2.2f, deserialized.ResponseTimeInSeconds),
                () => ClassicAssert.AreEqual(3, deserialized.ContentLength),
                () => ClassicAssert.AreEqual("guid", deserialized.TransactionGuid),
                () => ClassicAssert.AreEqual(false, deserialized.Unused)
                );
        }

        [Test]
        public void DeserializesCorrectly_IfOnly5Elements()
        {
            const string json = "[\"crossProcessId\",\"transactionName\",1.1,2.2,3]";
            var deserialized = JsonConvert.DeserializeObject<CrossApplicationResponseData>(json);

            ClassicAssert.NotNull(deserialized);
            NrAssert.Multiple(
                () => ClassicAssert.AreEqual("crossProcessId", deserialized.CrossProcessId),
                () => ClassicAssert.AreEqual("transactionName", deserialized.TransactionName),
                () => ClassicAssert.AreEqual(1.1f, deserialized.QueueTimeInSeconds),
                () => ClassicAssert.AreEqual(2.2f, deserialized.ResponseTimeInSeconds),
                () => ClassicAssert.AreEqual(3, deserialized.ContentLength),
                () => ClassicAssert.AreEqual(null, deserialized.TransactionGuid),
                () => ClassicAssert.AreEqual(false, deserialized.Unused)
                );
        }

        [Test]
        public void CannotDeserialize_IfOnly4Elements()
        {
            const string json = "[\"crossProcessId\",\"transactionName\",1.1,2.2]";
            ClassicAssert.IsNull(JsonConvert.DeserializeObject<CrossApplicationResponseData>(json));
        }
    }
}

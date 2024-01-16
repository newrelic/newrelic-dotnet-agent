// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Core.WireModels;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.CrossApplicationTracing;
using Newtonsoft.Json;

namespace NewRelic.Agent.Core.Utilities
{
    [TestFixture]
    public class HeaderEncoderTests
    {
        [Test]
        public void SerializeAndEncode_CreatesCorrectEncodedString_IfNullEncodingKey()
        {
            var data = new CrossApplicationRequestData("guid", false, "tripId", "pathHash");

            var encoded = HeaderEncoder.EncodeSerializedData(JsonConvert.SerializeObject(data), null);

            ClassicAssert.AreEqual("WyJndWlkIixmYWxzZSwidHJpcElkIiwicGF0aEhhc2giXQ==", encoded);
        }

        [Test]
        public void SerializeAndEncode_CreatesCorrectEncodedString_IfNonNullEncodingKey()
        {
            var data = new CrossApplicationRequestData("guid", false, "tripId", "pathHash");

            var encoded = HeaderEncoder.EncodeSerializedData(JsonConvert.SerializeObject(data), "encodingKey");

            ClassicAssert.AreEqual("PkwEGg0NTEstBBUWC09NEBsHFwIBW0lMEw4QASYGOA1bOA==", encoded);
        }

        [Test]
        public void TryDecodeAndDeserialize_ReturnsCorrectDeserializedObject_IfNullEncodingKey()
        {
            const string encoded = "WyJndWlkIixmYWxzZSwidHJpcElkIiwicGF0aEhhc2giXQ==";

            var deserialized = HeaderEncoder.TryDecodeAndDeserialize<CrossApplicationRequestData>(encoded, null);
            ClassicAssert.NotNull(deserialized);

            NrAssert.Multiple(
                () => ClassicAssert.AreEqual("guid", deserialized.TransactionGuid),
                () => ClassicAssert.AreEqual(false, deserialized.Unused),
                () => ClassicAssert.AreEqual("tripId", deserialized.TripId),
                () => ClassicAssert.AreEqual("pathHash", deserialized.PathHash)
            );
        }

        [Test]
        public void TryDecodeAndDeserialize_ReturnsCorrectDeserializedObject_IfNonNullEncodingKey()
        {
            const string encoded = "PkwEGg0NTEstBBUWC09NEBsHFwIBW0lMEw4QASYGOA1bOA==";

            var deserialized = HeaderEncoder.TryDecodeAndDeserialize<CrossApplicationRequestData>(encoded, "encodingKey");
            ClassicAssert.NotNull(deserialized);

            NrAssert.Multiple(
                () => ClassicAssert.AreEqual("guid", deserialized.TransactionGuid),
                () => ClassicAssert.AreEqual(false, deserialized.Unused),
                () => ClassicAssert.AreEqual("tripId", deserialized.TripId),
                () => ClassicAssert.AreEqual("pathHash", deserialized.PathHash)
            );
        }

        [Test]
        public void TryDecodeAndDeserialize_ReturnsNull_IfIncorrectEncodingKey()
        {
            const string encoded = "PkwEGg0NTEstBBUWC09NEBsHFwIBW0lMEw4QASYGOA1bOA==";

            var deserialized = HeaderEncoder.TryDecodeAndDeserialize<CrossApplicationRequestData>(encoded, "wrong!");

            ClassicAssert.Null(deserialized);
        }

        [Test]
        public void TryDecodeAndDeserialize_ReturnsNull_IfInvalidString()
        {
            const string encoded = "not a valid base64 encoded string";

            var deserialized = HeaderEncoder.TryDecodeAndDeserialize<CrossApplicationRequestData>(encoded, "encodingKey");

            ClassicAssert.Null(deserialized);
        }

        [Test]
        public void TryDecodeAndDeserialize_ReturnsNull_IfObjectCannotBeDeserializedAsExpectedType()
        {
            const string encoded = "PkwEGg0NTEstBBUWC09NEBsHFwIBW0lMEw4QASYGOA1bOA==";

            var deserialized = HeaderEncoder.TryDecodeAndDeserialize<MetricWireModel>(encoded, "encodingKey");
            ClassicAssert.Null(deserialized);
        }
    }
}

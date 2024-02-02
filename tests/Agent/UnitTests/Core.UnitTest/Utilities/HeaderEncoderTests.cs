// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Core.WireModels;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.CrossApplicationTracing;
using NewRelic.Testing.Assertions;
using Newtonsoft.Json;
using NUnit.Framework;

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

            Assert.That(encoded, Is.EqualTo("WyJndWlkIixmYWxzZSwidHJpcElkIiwicGF0aEhhc2giXQ=="));
        }

        [Test]
        public void SerializeAndEncode_CreatesCorrectEncodedString_IfNonNullEncodingKey()
        {
            var data = new CrossApplicationRequestData("guid", false, "tripId", "pathHash");

            var encoded = HeaderEncoder.EncodeSerializedData(JsonConvert.SerializeObject(data), "encodingKey");

            Assert.That(encoded, Is.EqualTo("PkwEGg0NTEstBBUWC09NEBsHFwIBW0lMEw4QASYGOA1bOA=="));
        }

        [Test]
        public void TryDecodeAndDeserialize_ReturnsCorrectDeserializedObject_IfNullEncodingKey()
        {
            const string encoded = "WyJndWlkIixmYWxzZSwidHJpcElkIiwicGF0aEhhc2giXQ==";

            var deserialized = HeaderEncoder.TryDecodeAndDeserialize<CrossApplicationRequestData>(encoded, null);
            Assert.That(deserialized, Is.Not.Null);

            NrAssert.Multiple(
                () => Assert.That(deserialized.TransactionGuid, Is.EqualTo("guid")),
                () => Assert.That(deserialized.Unused, Is.EqualTo(false)),
                () => Assert.That(deserialized.TripId, Is.EqualTo("tripId")),
                () => Assert.That(deserialized.PathHash, Is.EqualTo("pathHash"))
            );
        }

        [Test]
        public void TryDecodeAndDeserialize_ReturnsCorrectDeserializedObject_IfNonNullEncodingKey()
        {
            const string encoded = "PkwEGg0NTEstBBUWC09NEBsHFwIBW0lMEw4QASYGOA1bOA==";

            var deserialized = HeaderEncoder.TryDecodeAndDeserialize<CrossApplicationRequestData>(encoded, "encodingKey");
            Assert.That(deserialized, Is.Not.Null);

            NrAssert.Multiple(
                () => Assert.That(deserialized.TransactionGuid, Is.EqualTo("guid")),
                () => Assert.That(deserialized.Unused, Is.EqualTo(false)),
                () => Assert.That(deserialized.TripId, Is.EqualTo("tripId")),
                () => Assert.That(deserialized.PathHash, Is.EqualTo("pathHash"))
            );
        }

        [Test]
        public void TryDecodeAndDeserialize_ReturnsNull_IfIncorrectEncodingKey()
        {
            const string encoded = "PkwEGg0NTEstBBUWC09NEBsHFwIBW0lMEw4QASYGOA1bOA==";

            var deserialized = HeaderEncoder.TryDecodeAndDeserialize<CrossApplicationRequestData>(encoded, "wrong!");

            Assert.That(deserialized, Is.Null);
        }

        [Test]
        public void TryDecodeAndDeserialize_ReturnsNull_IfInvalidString()
        {
            const string encoded = "not a valid base64 encoded string";

            var deserialized = HeaderEncoder.TryDecodeAndDeserialize<CrossApplicationRequestData>(encoded, "encodingKey");

            Assert.That(deserialized, Is.Null);
        }

        [Test]
        public void TryDecodeAndDeserialize_ReturnsNull_IfObjectCannotBeDeserializedAsExpectedType()
        {
            const string encoded = "PkwEGg0NTEstBBUWC09NEBsHFwIBW0lMEw4QASYGOA1bOA==";

            var deserialized = HeaderEncoder.TryDecodeAndDeserialize<MetricWireModel>(encoded, "encodingKey");
            Assert.That(deserialized, Is.Null);
        }
    }
}

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

            Assert.AreEqual("WyJndWlkIixmYWxzZSwidHJpcElkIiwicGF0aEhhc2giXQ==", encoded);
        }

        [Test]
        public void SerializeAndEncode_CreatesCorrectEncodedString_IfNonNullEncodingKey()
        {
            var data = new CrossApplicationRequestData("guid", false, "tripId", "pathHash");

            var encoded = HeaderEncoder.EncodeSerializedData(JsonConvert.SerializeObject(data), "encodingKey");

            Assert.AreEqual("PkwEGg0NTEstBBUWC09NEBsHFwIBW0lMEw4QASYGOA1bOA==", encoded);
        }

        [Test]
        public void TryDecodeAndDeserialize_ReturnsCorrectDeserializedObject_IfNullEncodingKey()
        {
            const string encoded = "WyJndWlkIixmYWxzZSwidHJpcElkIiwicGF0aEhhc2giXQ==";

            var deserialized = HeaderEncoder.TryDecodeAndDeserialize<CrossApplicationRequestData>(encoded, null);
            Assert.NotNull(deserialized);

            NrAssert.Multiple(
                () => Assert.AreEqual("guid", deserialized.TransactionGuid),
                () => Assert.AreEqual(false, deserialized.Unused),
                () => Assert.AreEqual("tripId", deserialized.TripId),
                () => Assert.AreEqual("pathHash", deserialized.PathHash)
            );
        }

        [Test]
        public void TryDecodeAndDeserialize_ReturnsCorrectDeserializedObject_IfNonNullEncodingKey()
        {
            const string encoded = "PkwEGg0NTEstBBUWC09NEBsHFwIBW0lMEw4QASYGOA1bOA==";

            var deserialized = HeaderEncoder.TryDecodeAndDeserialize<CrossApplicationRequestData>(encoded, "encodingKey");
            Assert.NotNull(deserialized);

            NrAssert.Multiple(
                () => Assert.AreEqual("guid", deserialized.TransactionGuid),
                () => Assert.AreEqual(false, deserialized.Unused),
                () => Assert.AreEqual("tripId", deserialized.TripId),
                () => Assert.AreEqual("pathHash", deserialized.PathHash)
            );
        }

        [Test]
        public void TryDecodeAndDeserialize_ReturnsNull_IfIncorrectEncodingKey()
        {
            const string encoded = "PkwEGg0NTEstBBUWC09NEBsHFwIBW0lMEw4QASYGOA1bOA==";

            var deserialized = HeaderEncoder.TryDecodeAndDeserialize<CrossApplicationRequestData>(encoded, "wrong!");

            Assert.Null(deserialized);
        }

        [Test]
        public void TryDecodeAndDeserialize_ReturnsNull_IfInvalidString()
        {
            const string encoded = "not a valid base64 encoded string";

            var deserialized = HeaderEncoder.TryDecodeAndDeserialize<CrossApplicationRequestData>(encoded, "encodingKey");

            Assert.Null(deserialized);
        }

        [Test]
        public void TryDecodeAndDeserialize_ReturnsNull_IfObjectCannotBeDeserializedAsExpectedType()
        {
            const string encoded = "PkwEGg0NTEstBBUWC09NEBsHFwIBW0lMEw4QASYGOA1bOA==";

            var deserialized = HeaderEncoder.TryDecodeAndDeserialize<MetricWireModel>(encoded, "encodingKey");
            Assert.Null(deserialized);
        }
    }
}

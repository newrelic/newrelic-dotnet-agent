using NewRelic.Agent.Core.DistributedTracing;
using NewRelic.Agent.Core.Utils;
using NewRelic.Agent.Core.WireModels;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.CrossApplicationTracing;
using NewRelic.Testing.Assertions;
using NUnit.Framework;
using System;

namespace NewRelic.Agent.Core.Utilities
{
	[TestFixture]
	public class HeaderEncoderTests
	{
		#region Default

		[Test]
		public void SerializeAndEncode_CreatesCorrectEncodedString_IfNullEncodingKey()
		{
			var data = new CrossApplicationRequestData("guid", false, "tripId", "pathHash");

			var encoded = HeaderEncoder.EncodeSerializedData(data.ToJson(), null);

			Assert.AreEqual("WyJndWlkIixmYWxzZSwidHJpcElkIiwicGF0aEhhc2giXQ==", encoded);
		}

		[Test]
		public void SerializeAndEncode_CreatesCorrectEncodedString_IfNonNullEncodingKey()
		{
			var data = new CrossApplicationRequestData("guid", false, "tripId", "pathHash");

			var encoded = HeaderEncoder.EncodeSerializedData(data.ToJson(), "encodingKey");

			Assert.AreEqual("PkwEGg0NTEstBBUWC09NEBsHFwIBW0lMEw4QASYGOA1bOA==", encoded);
		}

		[Test]
		public void TryDecodeAndDeserialize_ReturnsCorrectDeserializedObject_IfNullEncodingKey()
		{
			const String encoded = "WyJndWlkIixmYWxzZSwidHJpcElkIiwicGF0aEhhc2giXQ==";

			var deserialized = CrossApplicationRequestData.TryBuildIncomingDataFromJson(HeaderEncoder.DecodeSerializedData(encoded, null));
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
			const String encoded = "PkwEGg0NTEstBBUWC09NEBsHFwIBW0lMEw4QASYGOA1bOA==";

			var deserialized = CrossApplicationRequestData.TryBuildIncomingDataFromJson(HeaderEncoder.DecodeSerializedData(encoded, "encodingKey"));
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
			const String encoded = "PkwEGg0NTEstBBUWC09NEBsHFwIBW0lMEw4QASYGOA1bOA==";

			var deserialized = CrossApplicationRequestData.TryBuildIncomingDataFromJson(HeaderEncoder.DecodeSerializedData(encoded, "wrong!"));
			Assert.Null(deserialized);
		}

		[Test]
		public void TryDecodeAndDeserialize_ReturnsNull_IfInvalidString()
		{
			const String encoded = "not a valid base64 encoded string";

			var deserialized = CrossApplicationRequestData.TryBuildIncomingDataFromJson(HeaderEncoder.DecodeSerializedData(encoded, "encodingKey"));
			Assert.Null(deserialized);
		}

		[Test]
		public void TryDecodeAndDeserialize_ReturnsNull_IfObjectCannotBeDeserializedAsExpectedType()
		{
			const String encoded = "PkwEGg0NTEstBBUWC09NEBsHFwIBW0lMEw4QASYGOA1bOA==";

			var deserialized = HeaderEncoder.TryDecodeAndDeserialize<MetricWireModel>(encoded, "encodingKey");
			Assert.Null(deserialized);
		}

#endregion

		#region Distributed Trace

		[Test]
		public void SerializeAndEncodeDistributedTracePayload_CreatesCorrectEncodedString()
		{
			var payload = GetDistributedTracePayload();
			var jsonString = DistributedTracePayload.ToJson(payload);
			var encodedJsonString = Strings.Base64Encode(jsonString);
			var serializedPayload = HeaderEncoder.SerializeAndEncodeDistributedTracePayload(payload);

			Assert.AreEqual(encodedJsonString, serializedPayload);
		}

		[Test]
		public void TryDecodeAndDeserializeDistributedTracePayload_ReturnsCorrectDeserializedObject()
		{
			var payload = GetDistributedTracePayload();
			var encodedString = HeaderEncoder.SerializeAndEncodeDistributedTracePayload(payload);
			var deserializedObject = HeaderEncoder.TryDecodeAndDeserializeDistributedTracePayload(encodedString);
			var expectedObject = GetDistributedTracePayload();

			NrAssert.Multiple(
				() => Assert.NotNull(deserializedObject),
				() => Assert.AreEqual(typeof(DistributedTracePayload), deserializedObject.GetType()),

				() => Assert.AreEqual(expectedObject.AccountId, deserializedObject.AccountId),
				() => Assert.AreEqual(expectedObject.AppId, deserializedObject.AppId),
				() => Assert.AreEqual(expectedObject.Guid, deserializedObject.Guid),
				() => Assert.AreEqual(expectedObject.Priority, deserializedObject.Priority),
				() => Assert.AreEqual(expectedObject.Sampled, deserializedObject.Sampled),
				() => Assert.AreEqual(expectedObject.Timestamp.ToUnixTimeMilliseconds(), deserializedObject.Timestamp.ToUnixTimeMilliseconds()),
				() => Assert.AreEqual(expectedObject.TraceId, deserializedObject.TraceId),
				() => Assert.AreEqual(expectedObject.TrustKey, deserializedObject.TrustKey),
				() => Assert.AreEqual(expectedObject.Type, deserializedObject.Type),
				() => Assert.AreEqual(expectedObject.Version, deserializedObject.Version),
				() => Assert.AreEqual(expectedObject.TransactionId, deserializedObject.TransactionId)
			);
		}

		[Test]
		public void TryDecodeAndDeserializeDistributedTracePayload_UnencodedObject_ReturnsCorrectDeserializedObject()
		{
			var payload = DistributedTracePayload.ToJson(GetDistributedTracePayload());
			var deserializedObject = HeaderEncoder.TryDecodeAndDeserializeDistributedTracePayload(payload);
			var expectedObject = GetDistributedTracePayload();

			NrAssert.Multiple(
				() => Assert.NotNull(deserializedObject),
				() => Assert.AreEqual(typeof(DistributedTracePayload), deserializedObject.GetType()),

				() => Assert.AreEqual(expectedObject.AccountId, deserializedObject.AccountId),
				() => Assert.AreEqual(expectedObject.AppId, deserializedObject.AppId),
				() => Assert.AreEqual(expectedObject.Guid, deserializedObject.Guid),
				() => Assert.AreEqual(expectedObject.Priority, deserializedObject.Priority),
				() => Assert.AreEqual(expectedObject.Sampled, deserializedObject.Sampled),
				() => Assert.AreEqual(expectedObject.Timestamp.ToUnixTimeMilliseconds(), deserializedObject.Timestamp.ToUnixTimeMilliseconds()),
				() => Assert.AreEqual(expectedObject.TraceId, deserializedObject.TraceId),
				() => Assert.AreEqual(expectedObject.TrustKey, deserializedObject.TrustKey),
				() => Assert.AreEqual(expectedObject.Type, deserializedObject.Type),
				() => Assert.AreEqual(expectedObject.Version, deserializedObject.Version),
				() => Assert.AreEqual(expectedObject.TransactionId, deserializedObject.TransactionId)
			);
		}

		[Test]
		public void TryDecodeAndDeserializeDistributedTracePayload_ReturnsNull_IfInvalidBase64()
		{
			var payload = GetDistributedTracePayload();
			var encodedString = HeaderEncoder.SerializeAndEncodeDistributedTracePayload(payload);
			var badEncodedString = "badbasd64string" + encodedString; 
			var deserializedObject = HeaderEncoder.TryDecodeAndDeserializeDistributedTracePayload(badEncodedString);

			Assert.IsNull(deserializedObject);
		}
	

		[Test]
		public void TryDecodeAndDeserializeDistributedTracePayload_ThrowsException_IfEncodedAsInvalidType()
		{
			// The following base64 string isn't an encoding of a DistributedTracePayload but it is valid base64.
			var encodedString = "eyJ2IjpbMCwxXSwiZCI6eyJEaWZmZXJlbnQiOiJUeXBlIiwidHkiOiJBcHAiLCJhYyI6IjkxMjMiLCJhcCI6IjUxNDI0IiwiaWQiOiI1ZjQ3NGQ2NGI5Y2M5YjJhIiwidHIiOiIzMjIxYmYwOWFhMGJjZjBkIiwidGsiOiIxMjM0NSIsInByIjowLjEyMzQsInNhIjpmYWxzZSwidGkiOjE1Mjk0MjQxMzA2MDMsInR4IjoiMjc4NTZmNzBkM2QzMTRiNyJ9fQ==";
			Assert.Throws<DistributedTraceAcceptPayloadParseException>(() => HeaderEncoder.TryDecodeAndDeserializeDistributedTracePayload(encodedString));
		}

		[Test]
		public void TryDecodeAndDeserializeDistributedTracePayload_ThrowsException_IfInvalidVersion()
		{
			var payload = GetDistributedTracePayload();
			payload.Version = new int[] { 9999, 1};
			var encodedString = HeaderEncoder.SerializeAndEncodeDistributedTracePayload(payload);

			Assert.Throws<DistributedTraceAcceptPayloadVersionException>(() => HeaderEncoder.TryDecodeAndDeserializeDistributedTracePayload(encodedString));
			
		}

		private DistributedTracePayload GetDistributedTracePayload()
		{
			return DistributedTracePayload.TryBuildOutgoingPayload(

				"App",
				"9123",
				"51424",
				"5f474d64b9cc9b2a",
				"3221bf09aa0bcf0d",
				"12345",
				0.1234f,
				false,
				new DateTime(636650209306034197, DateTimeKind.Utc), // UnixTimeInMilliseconds: 1529424130603 // 1482959525577
				"27856f70d3d314b7"
			);
		}

		#endregion Distributed Trace
	}
}

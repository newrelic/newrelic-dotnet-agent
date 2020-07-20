using System;
using System.Text;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace NewRelic.Agent.IntegrationTestHelpers
{
	public class HeaderEncoder
	{
		[NotNull]
		public const String IntegrationTestEncodingKey = "d67afc830dab717fd163bfcb0b8b88423e9a1a3b";

		/// <summary>
		/// Serializes <paramref name="data"/> to JSON and Base64 encodes it with <paramref name="encodingKey"/>
		/// </summary>
		/// <param name="data">The data to encode. Must not be null.</param>
		/// <param name="encodingKey">The encoding key. Can be null.</param>
		/// <returns>The serialized and encoded data.</returns>
		[NotNull, Pure]
		public static String SerializeAndEncode([NotNull] Object data, [CanBeNull] String encodingKey)
		{
			var serializedData = JsonConvert.SerializeObject(data);
			if (serializedData == null)
				throw new NullReferenceException("serializedData");

			return Base64Encode(serializedData, encodingKey);
		}

		/// <summary>
		/// Tries to Base64 decode <paramref name="encodedString"/> using <paramref name="encodingKey"/> and deserialize it to type <typeparamref name="T"/>. Returns null if any step fails.
		/// </summary>
		/// <param name="encodedString">The encoded string to decode. Can be null.</param>
		/// <param name="encodingKey">The encoding key. Can be null.</param>
		/// <param name="maxKeyLength">supplied key will be truncated to this length, for compatibility with agent code which stops at 13</param>
		/// <returns>The decoded and deserialized data if possible, else null.</returns>
		[NotNull, Pure]
		public static T DecodeAndDeserialize<T>([NotNull] String encodedString, [CanBeNull] String encodingKey, [CanBeNull] Int32? maxKeyLength = null) where T : class
		{
			var decodedString = Base64Decode(encodedString, encodingKey != null && maxKeyLength.HasValue ? encodingKey.Substring(0, maxKeyLength.Value) : encodingKey);
			var deserializedData = JsonConvert.DeserializeObject<T>(decodedString);
			if (deserializedData == null)
				throw new NullReferenceException(nameof(deserializedData));

			return deserializedData;
		}

		[NotNull]
		public static String Base64Encode([NotNull] String val, String encodingKey = null)
		{
			var encodedBytes = Encoding.UTF8.GetBytes(val);

			if (!String.IsNullOrEmpty(encodingKey))
				encodedBytes = EncodeWithKey(encodedBytes, encodingKey);

			return Convert.ToBase64String(encodedBytes);
		}

		[NotNull]
		public static String Base64Decode([NotNull] String val, String encodingKey = null)
		{
			var bytes = Convert.FromBase64String(val);

			if (!String.IsNullOrEmpty(encodingKey))
				bytes = EncodeWithKey(bytes, encodingKey);

			return Encoding.UTF8.GetString(bytes);
		}

		[NotNull]
		public static Byte[] EncodeWithKey([NotNull] Byte[] bytes, [NotNull] String key)
		{
			var keyBytes = Encoding.UTF8.GetBytes(key);

			for (var i = 0; i < bytes.Length; i++)
			{
				bytes[i] = (Byte)(bytes[i] ^ keyBytes[i % keyBytes.Length]);
			}

			return bytes;
		}
	}
}

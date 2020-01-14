using NewRelic.Core;
using NewRelic.Core.DistributedTracing;
using NewRelic.Core.Logging;
using Newtonsoft.Json;
using System;

namespace NewRelic.Agent.Core.Utilities
{
	public static class HeaderEncoder
	{
		/// <summary>
		/// Serializes <paramref name="data"/> to JSON and Base64 encodes it with <paramref name="encodingKey"/>
		/// </summary>
		/// <param name="data">The data to encode. Must not be null.</param>
		/// <param name="encodingKey">The encoding key. Can be null.</param>
		/// <returns>The serialized and encoded data.</returns>
		public static string SerializeAndEncode(object data, string encodingKey)
		{
			var serializedData = JsonConvert.SerializeObject(data);
			if (serializedData == null)
			{
				throw new NullReferenceException("serializedData");
			}

			return EncodeSerializedData(serializedData, encodingKey);
		}

		/// <summary>
		/// Tries to Base64 decode <paramref name="encodedString"/> using <paramref name="encodingKey"/> and deserialize it to type <typeparamref name="T"/>. Returns null if any step fails.
		/// </summary>
		/// <param name="encodedString">The encoded string to decode. Can be null.</param>
		/// <param name="encodingKey">The encoding key. Can be null.</param>
		/// <returns>The decoded and deserialized data if possible, else null.</returns>
		public static T TryDecodeAndDeserialize<T>(string encodedString, string encodingKey) where T : class
		{
			var decodedString = DecodeSerializedData(encodedString, encodingKey);

			if (decodedString == null)
			{
				return null;		//Condition already logged already Logged in DecodeSerialiedData
			}

			try
			{
				return JsonConvert.DeserializeObject<T>(decodedString);
			}
			catch
			{
				Log.Debug($"Could not deserialize JSON into {typeof(T).FullName}.");
				return null;
			}
		}

		/// <summary>
		/// Serializes <paramref name="data"/> to JSON and Base64 encodes it with <paramref name="encodingKey"/>
		/// </summary>
		/// <param name="data">The data to encode. Must not be null.</param>
		/// <returns>The serialized and encoded data.</returns>
		public static string SerializeAndEncodeDistributedTracePayload(DistributedTracePayload data)
		{
			var serializedData = data.ToJson();
			if (serializedData == null)
			{
				throw new NullReferenceException("serializedData");
			}

			return Strings.Base64Encode(serializedData);
		}

		public static DistributedTracePayload TryDecodeAndDeserializeDistributedTracePayload(string encodedString)
		{
			var stringToConvert = encodedString?.Trim();
			if (!string.IsNullOrEmpty(stringToConvert))
			{
				var firstChar = stringToConvert[0];
				if (firstChar != '{' && firstChar != '[')
				{
					stringToConvert = Strings.TryBase64Decode(stringToConvert);
					if (stringToConvert == null)
					{
						Log.Debug("Could not decode distributed trace payload string: " + encodedString);
						return null;
					}
				}
			}

			return DistributedTracePayload.TryBuildIncomingPayloadFromJson(stringToConvert);
		}

		public static string EncodeSerializedData(string serializedData, string encodingKey)
		{
			return Strings.Base64Encode(serializedData, encodingKey);
		}

		public static string DecodeSerializedData(string encodedString, string encodingKey)
		{
			if (encodedString == null)
			{
				return null;
			}

			var decodedString = Strings.TryBase64Decode(encodedString, encodingKey);
			if (decodedString == null)
			{
				Log.Debug("Could not decode encoded string.");
				return null;
			}

			return decodedString;
		}
	}
}

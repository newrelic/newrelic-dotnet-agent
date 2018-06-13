using System;
using JetBrains.Annotations;
using NewRelic.Agent.Core.JsonConverters;
using NewRelic.Agent.Core.Logging;
using NewRelic.Agent.Core.Utils;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.DistributedTracing;
using Newtonsoft.Json;

namespace NewRelic.Agent.Core.Utilities
{
	// TODO: It would make unit testing a bit easier if this class were DI'd
	public static class HeaderEncoder
	{
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
			{
				throw new NullReferenceException("serializedData");
			}

			return Strings.Base64Encode(serializedData, encodingKey);
		}

		/// <summary>
		/// Tries to Base64 decode <paramref name="encodedString"/> using <paramref name="encodingKey"/> and deserialize it to type <typeparamref name="T"/>. Returns null if any step fails.
		/// </summary>
		/// <param name="encodedString">The encoded string to decode. Can be null.</param>
		/// <param name="encodingKey">The encoding key. Can be null.</param>
		/// <returns>The decoded and deserialized data if possible, else null.</returns>
		[CanBeNull, Pure]
		public static T TryDecodeAndDeserialize<T>([CanBeNull] String encodedString, [CanBeNull] String encodingKey) where T : class
		{
			if (encodedString == null)
				return null;

			var decodedString = Strings.TryBase64Decode(encodedString, encodingKey);
			if (decodedString == null)
			{
				Log.Debug("Could not decode encoded string.");
				return null;
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

		// TODO: put this in dtheaderhandler? parameterize existing trydecodeanddeserialize to work for cat or dt? leave as separate method?
		public static T TryDecodeAndDeserializeDistributedTracePayload<T>([CanBeNull] String encodedString, [CanBeNull] String encodingKey) where T : class
		{
			if (encodedString == null)
				return null;

			var decodedString = Strings.TryBase64Decode(encodedString, encodingKey);
			if (decodedString == null)
			{
				Log.Debug("Could not decode encoded string.");
				return null;
			}

			try
			{
				return JsonConvert.DeserializeObject<T>(decodedString, new DistributedTracePayloadJsonConverter());
			}
			catch
			{
				Log.Debug($"Could not deserialize JSON into {typeof(T).FullName}.");
				return null;
			}
		}
	}
}

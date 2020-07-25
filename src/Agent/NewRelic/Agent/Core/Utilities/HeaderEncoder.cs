using System;
using NewRelic.Agent.Core.Logging;
using NewRelic.Agent.Core.Utils;
using Newtonsoft.Json;

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
        public static String SerializeAndEncode(Object data, String encodingKey)
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
        public static T TryDecodeAndDeserialize<T>(String encodedString, String encodingKey) where T : class
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
    }
}

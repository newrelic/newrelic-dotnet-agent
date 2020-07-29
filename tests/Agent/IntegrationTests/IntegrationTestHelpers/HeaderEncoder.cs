/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using System;
using System.Text;
using Newtonsoft.Json;

namespace NewRelic.Agent.IntegrationTestHelpers
{
    public class HeaderEncoder
    {
        public const string IntegrationTestEncodingKey = "d67afc830dab717fd163bfcb0b8b88423e9a1a3b";

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
        public static T DecodeAndDeserialize<T>(string encodedString, string encodingKey, int? maxKeyLength = null) where T : class
        {
            var decodedString = Base64Decode(encodedString, encodingKey != null && maxKeyLength.HasValue ? encodingKey.Substring(0, maxKeyLength.Value) : encodingKey);
            var deserializedData = JsonConvert.DeserializeObject<T>(decodedString);
            if (deserializedData == null)
                throw new NullReferenceException(nameof(deserializedData));

            return deserializedData;
        }

        public static string Base64Encode(string val, string encodingKey = null)
        {
            var encodedBytes = Encoding.UTF8.GetBytes(val);

            if (!string.IsNullOrEmpty(encodingKey))
                encodedBytes = EncodeWithKey(encodedBytes, encodingKey);

            return Convert.ToBase64String(encodedBytes);
        }

        public static string Base64Decode(string val, string encodingKey = null)
        {
            var bytes = Convert.FromBase64String(val);

            if (!string.IsNullOrEmpty(encodingKey))
                bytes = EncodeWithKey(bytes, encodingKey);

            return Encoding.UTF8.GetString(bytes);
        }

        public static byte[] EncodeWithKey(byte[] bytes, string key)
        {
            var keyBytes = Encoding.UTF8.GetBytes(key);

            for (var i = 0; i < bytes.Length; i++)
            {
                bytes[i] = (byte)(bytes[i] ^ keyBytes[i % keyBytes.Length]);
            }

            return bytes;
        }
    }
}

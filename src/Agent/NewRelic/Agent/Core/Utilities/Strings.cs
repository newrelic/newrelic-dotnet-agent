// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.IO;
using System.Text;

namespace NewRelic.Agent.Core.Utilities
{
    public static class Strings
    {
        private static readonly UTF8Encoding _encoding = new UTF8Encoding();

        /// <summary>
        /// Sanitize the given file name, replacing illegal characters with _.
        /// </summary>
        /// <param name="name">The file name to sanitize.</param>
        /// <returns>The sanitized file name.</returns>
        public static string SafeFileName(string name)
        {
            var invalidPathChars = Path.GetInvalidPathChars();

            if (name.IndexOfAny(invalidPathChars) != -1)
            {
                foreach (var c in invalidPathChars)
                {
                    name = name.Replace(c, '_');
                }
            }

            var invalidFileNameChars = Path.GetInvalidFileNameChars();
            if (name.IndexOfAny(invalidFileNameChars) != -1)
            {
                foreach (var c in invalidFileNameChars)
                {
                    name = name.Replace(c, '_');
                }
            }

            return name;
        }

        public static string TryBase64Decode(string val, string encodingKey = null)
        {
            if (val == null)
                return null;

            try
            {
                return Base64Decode(val, encodingKey);
            }
            catch
            {
                return null;
            }
        }

        public static string Base64Decode(string val, string encodingKey = null)
        {
            var bytes = Convert.FromBase64String(val);

            if (!string.IsNullOrEmpty(encodingKey))
                bytes = EncodeWithKey(bytes, encodingKey);

            return Encoding.UTF8.GetString(bytes);
        }

        public static string Base64Encode(string val, string encodingKey = null)
        {
            var encodedBytes = Encoding.UTF8.GetBytes(val);

            if (!string.IsNullOrEmpty(encodingKey))
                encodedBytes = EncodeWithKey(encodedBytes, encodingKey);

            return Convert.ToBase64String(encodedBytes);
        }

        private static byte[] EncodeWithKey(byte[] bytes, string key)
        {
            var keyBytes = Encoding.UTF8.GetBytes(key);

            var keyIdx = 0;
            for (var i = 0; i < bytes.Length; i++)
            {
                if (keyIdx == keyBytes.Length)
                    keyIdx = 0;

                bytes[i] = (byte)(bytes[i] ^ keyBytes[keyIdx]);
                keyIdx++;
            }

            return bytes;
        }

        public static string ObfuscateStringWithKey(string val, string key)
        {
            if (val == null || string.IsNullOrEmpty(key))
            {
                return null;
            }

            var bytes = _encoding.GetBytes(val);

            var maxKeyLength = Math.Min(13, key.Length); // we don't really need this, but it helps for unit testing

            var keyIdx = 0;
            for (var i = 0; i < bytes.Length; i++)
            {

                if (keyIdx == maxKeyLength)
                {
                    keyIdx = 0;
                }

                bytes[i] = (byte)(bytes[i] ^ key[keyIdx]);
                keyIdx++;
            }

            return Convert.ToBase64String(bytes);
        }

        public static string ToString(System.Collections.IEnumerable enumerable, char separator = ',')
        {
            var builder = new StringBuilder();
            var first = true;
            foreach (object obj in enumerable)
            {
                if (first)
                {
                    first = false;
                }
                else
                {
                    builder.Append(separator);
                }
                builder.Append(obj.ToString());
            }
            return builder.ToString();
        }

        public static string Replace(this string originalString, string oldValue, string newValue, StringComparison comparisonType, int count)
        {
            int startIndex = 0;
            int numberReplaced = 0;

            while (numberReplaced < count)
            {
                startIndex = originalString.IndexOf(oldValue, startIndex, comparisonType);
                if (startIndex == -1)
                    break;

                originalString = originalString.Substring(0, startIndex) + newValue + originalString.Substring(startIndex + oldValue.Length);
                numberReplaced++;

                startIndex += newValue.Length;
            }

            return originalString;
        }

        // Must use Encoder/Decoder and not Encoding.GetString.  See: http://msdn.microsoft.com/en-us/library/ms404377(v=vs.110).aspx
        public static string GetStringBufferFromBytes(Decoder decoder, byte[] buffer, int offset, int count)
        {
            var length = decoder.GetCharCount(buffer, offset, count);
            var chars = new char[length];
            decoder.GetChars(buffer, offset, count, chars, 0);
            return new string(chars);
        }

        public static string ObfuscateLicenseKeyInAuditLog(string text, string licenseKeyParameterName)
        {
            var licenseKeyParameterIndex = text.IndexOf(licenseKeyParameterName + "=");
            if (licenseKeyParameterIndex == -1)
            {
                return text;
            }
            try
            {
                var licenseKeyStartPos = licenseKeyParameterIndex + licenseKeyParameterName.Length + 1; // +1 to account for the =
                var licenseKeyEndPos = text.IndexOf('&', licenseKeyStartPos);
                var licenseKeyLength = licenseKeyEndPos - licenseKeyStartPos;
                var licenseKey = text.Substring(licenseKeyStartPos, licenseKeyLength);
                var obfuscatedLicenseKey = ObfuscateLicenseKey(licenseKey);
                var sb = new StringBuilder(text);
                sb.Remove(licenseKeyStartPos, licenseKeyLength);
                sb.Insert(licenseKeyStartPos, obfuscatedLicenseKey);
                return sb.ToString();
            }
            catch
            {
                return text;
            }
        }

        public static string ObfuscateLicenseKey(string licenseKey)
        {
            // We can log up to 8 characters of a 40-character license key, the rest must be obfuscated
            // For our agent, the license key should always be 40 characters. For safety, if it isn't, just obfuscate the whole thing
            if (licenseKey.Length == 40)
            {
                return licenseKey.Substring(0, 8) + new string('*', 32);
            }
            else
            {
                return new string('*', licenseKey.Length);
            }
        }



    }
}

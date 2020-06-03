using System;
using System.IO;
using System.Text;

namespace NewRelic.Core
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
            foreach (var c in Path.GetInvalidPathChars())
            {
                name = name.Replace(c, '_');
            }
            foreach (var c in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c, '_');
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

    }
}

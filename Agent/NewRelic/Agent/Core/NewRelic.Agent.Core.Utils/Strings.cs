using System;
using System.Text;
using JetBrains.Annotations;

namespace NewRelic.Agent.Core.Utils
{
	public static class Strings
	{
		/// <summary>
		/// Sanitize the given file name, replacing illegal characters with _.
		/// </summary>
		/// <param name="name">The file name to sanitize.</param>
		/// <returns>The sanitized file name.</returns>
		public static String SafeFileName(String name)
		{
			foreach (char c in System.IO.Path.GetInvalidPathChars())
			{
				name = name.Replace(c, '_');
			}
			foreach (char c in System.IO.Path.GetInvalidFileNameChars())
			{
				name = name.Replace(c, '_');
			}
			return name;
		}

		[CanBeNull]
		public static String TryBase64Decode([CanBeNull] String val, String encodingKey = null)
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

		[NotNull]
		public static String Base64Decode([NotNull] String val, String encodingKey = null)
		{
			var bytes = Convert.FromBase64String(val);

			if (!String.IsNullOrEmpty(encodingKey))
				bytes = EncodeWithKey(bytes, encodingKey);

			return Encoding.UTF8.GetString(bytes);
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
		private static Byte[] EncodeWithKey([NotNull] Byte[] bytes, [NotNull] String key)
		{
			var keyBytes = Encoding.UTF8.GetBytes(key);

			var keyIdx = 0;
			for (var i = 0; i < bytes.Length; i++)
			{
				if (keyIdx == keyBytes.Length)
					keyIdx = 0;

				bytes[i] = (Byte)(bytes[i] ^ keyBytes[keyIdx]);
				keyIdx++;
			}

			return bytes;

		}

		private static readonly UTF8Encoding _encoding = new UTF8Encoding();

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

				bytes[i] = (Byte)(bytes[i] ^ key[keyIdx]);
				keyIdx++;
			}

			return Convert.ToBase64String(bytes);
		}

		public static string ToString(System.Collections.IEnumerable enumerable, char separator = ',')
		{
			StringBuilder builder = new StringBuilder();
			bool first = true;
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
		public static string GetStringBufferFromBytes([NotNull] Decoder decoder, [NotNull] byte[] buffer, int offset, int count)
		{
			var length = decoder.GetCharCount(buffer, offset, count);
			var chars = new char[length];
			decoder.GetChars(buffer, offset, count, chars, 0);
			return new string(chars);
		}

	}
}

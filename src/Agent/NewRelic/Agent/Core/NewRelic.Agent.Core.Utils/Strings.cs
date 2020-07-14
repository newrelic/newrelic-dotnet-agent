using System;
using System.Text;
using System.Text.RegularExpressions;
using JetBrains.Annotations;

namespace NewRelic.Agent.Core.Utils
{
	public static class Strings
	{
		public static String FixDatabaseObjectName(String s) {
			int index = s.IndexOf('.');
			if (index > 0)
			{
				return new StringBuilder(s.Length)
					.Append(FixDatabaseName(s.Substring(0, index)))
					.Append('.')
					.Append(FixDatabaseName(s.Substring(index+1)))
					.ToString();
			}
			else
			{
				return FixDatabaseName(s);
			}
		}

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

		/// <summary>
		/// Unbracket and unquote a database object name.
		/// </summary>
		/// <param name="s">
		/// A <see cref="String"/>
		/// </param>
		/// <returns>
		/// A <see cref="String"/>
		/// </returns>
		private static String FixDatabaseName(String s) {
			StringBuilder sb = new StringBuilder(s.Length);
			bool first = true;
			foreach (String segment in s.Split('.')) {
				if (!first)
				{
					sb.Append('.');
				}
				else
				{
					first = false;
				}
				sb.Append(Unbracket(Unquote(Unparenthesize(segment.ToLower()))));
			}
			return sb.ToString();
		}

		public static String SafeMethodName(String name)
		{
			if (name[0] < '\u0021')
			{
				return "Obfuscated";
			}
			return name;
		}

	    public static String ToRubyName(String name)
		{
			StringBuilder stringBuilder = new StringBuilder((int)(name.Length * 1.2));
			foreach (char c in name.ToCharArray())
			{
				if (c >= 'A' && c <= 'Z')
				{
					stringBuilder.Append('_').Append(char.ToLower(c));
				}
				else
				{
					stringBuilder.Append(c);
				}
			}

			return stringBuilder.ToString() ;
		}
	
		/// <summary>
		/// Unquote a string.
		/// </summary>
		/// <param name="value">
		/// A <see cref="String"/>
		/// </param>
		/// <returns>
		/// A <see cref="String"/>
		/// </returns>
		[NotNull]
		public static String Unquote([NotNull] String value)
	    {
			if (value.Length < 3)
				return value;
	
	        var first = value[0];
	        var last = value[value.Length-1];
			if (first != last || (first != '"' && first != '\'' && first != '`'))
				return value;

	        return value.Substring(1, value.Length - 2);
	    }

		[NotNull]
		public static String Unbracket([NotNull] String value)
	    {
			if (value.Length < 3)
				return value;
	
	        var first = value[0];
	        var last = value[value.Length-1];
			while (first == '[' || last == ']')
			{
				value = value.Substring(1, value.Length - 2);
				first = value[0];
				last = value[value.Length - 1];
			}

			return value;
	    }

		[NotNull] 
		public static String Unparenthesize([NotNull] String value)
		{
			if (value.Length < 3)
				return value;

			var first = value[0];
			var last = value[value.Length - 1];
			while (first == '(' || last == ')')
			{
				value = value.Substring(1, value.Length - 2);
				first = value[0];
				last = value[value.Length - 1];
			}

			return value;
		}

		[NotNull]
		public static String CleanUri(String uri)
		{
			if (uri == null)
				return String.Empty;

			var index = uri.IndexOf('?');
			return (index > 0)
				? uri.Substring(0, index)
				: uri;
		}

		public static String CleanUri(Uri uri)
		{
			if (uri == null)
				return String.Empty;

			// Can't clean up relative URIs (Uri.GetComponents will throw an exception for relative URIs)
			if (!uri.IsAbsoluteUri)
				return uri.ToString();

			return uri.GetComponents(
					UriComponents.Scheme |
					UriComponents.HostAndPort |
					UriComponents.Path,
					UriFormat.UriEscaped);
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

		[CanBeNull]
		public static String TryBase64Encode([CanBeNull] String val, String encodingKey = null)
		{
			if (val == null)
				return null;

			try
			{
				return Base64Encode(val, encodingKey);
			}
			catch
			{
				return null;
			}
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

		[NotNull]
		public static String ObfuscateStringWithKey([NotNull] String val, [NotNull] String key, String defaultValue = null)
		{
			var returnVal = defaultValue;
			if (val == null)
				return returnVal;
			if (key.Length <= 0)
				return returnVal;

			var utf8 = new UTF8Encoding();
			var bytes = utf8.GetBytes(val);
			if (key != null) // only needed for unit tests
			{
				var maxKeyLength = Math.Min(13, key.Length); // we don't really need this, but it helps for unit testing
				var keyIdx = 0;
				for (var i = 0; i < bytes.Length; i++)
				{
					if (keyIdx == maxKeyLength)
						keyIdx = 0;

					var c = bytes[i] ^ key[keyIdx];
					keyIdx++;

					bytes[i] = (Byte)c;
				}
			}
			returnVal = Convert.ToBase64String(bytes);
			return returnVal;
		}

		/// <summary>
		/// Will take in a string and the origional formatter used to construct it and parse out a string array - this should really be an extension method off of string
		/// pulled from  http://stackoverflow.com/questions/1410012/parsing-formatted-string then modified
		/// </summary>
		/// <param name="data"></param>
		/// <param name="format"></param>
		/// <returns></returns>
		public static string[] ParseExact(
		string data,
		string format)
		{
			return ParseExact(data, format, false);
		}

		public static string[] ParseExact(
			string data,
			string format,
			bool ignoreCase)
		{
			string[] values;

			if (TryParseExact(data, format, out values, ignoreCase))
			{
				return values;
			}

			else
			{
				throw new ArgumentException("Format not compatible with value.");
			}
		}

		public static bool TryExtract(
			string data,
			string format,
			out string[] values)
		{
			return TryParseExact(data, format, out values, false);
		}

		public static bool TryParseExact(
			string data,
			string format,
			out string[] values,
			bool ignoreCase)
		{
			int tokenCount = 0;
			format = Regex.Escape(format).Replace("\\{", "{");

			for (tokenCount = 0; ; tokenCount++)
			{
				string token = string.Format("{{{0}}}", tokenCount);
				if (!format.Contains(token)) break;
				format = format.Replace(token,
					string.Format("(?'group{0}'.*)", tokenCount));
			}

			RegexOptions options =
				ignoreCase ? RegexOptions.IgnoreCase : RegexOptions.None;

			Match match = new Regex(format, options).Match(data);

			if (tokenCount != (match.Groups.Count - 1))
			{
				values = new string[] { };
				return false;
			}
			else
			{
				values = new string[tokenCount];
				for (int index = 0; index < tokenCount; index++)
					values[index] =
						match.Groups[string.Format("group{0}", index)].Value;
				return true;
			}
		}

		/// <summary>
		/// Returns the application virtual path without the leading / character.
		/// </summary>
		/// <returns></returns>
		/*
		public static String GetAppVirtualPath()
		{
			return System.Web.HttpRuntime.AppDomainAppVirtualPath == null
				? ""
				: System.Web.HttpRuntime.AppDomainAppVirtualPath.Substring(1);
		}
		*/

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

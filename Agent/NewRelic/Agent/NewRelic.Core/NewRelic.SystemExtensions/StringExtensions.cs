using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using JetBrains.Annotations;

namespace NewRelic.SystemExtensions
{
	public static class StringExtensions
	{
		[NotNull]
		public static String TruncateUnicode([NotNull] this String value, Int32 maxLength)
		{
			if (value == null)
				throw new ArgumentNullException("value");
			if (maxLength < 0)
				throw new ArgumentOutOfRangeException(String.Format("maxLength must be positive.  value: {0}  maxLength: {1}", value, maxLength));

			var textElements = new StringInfo(value);
			if (textElements.LengthInTextElements <= maxLength)
				return value;

			return textElements.SubstringByTextElements(0, maxLength);
		}

		public static Boolean ContainsAny(this String source, IEnumerable<String> searchTargets, StringComparison comparison = StringComparison.InvariantCultureIgnoreCase)
		{
			if (source == null)
				return false;
			if (searchTargets == null)
				return false;

			return searchTargets.Any(target => target != null && source.IndexOf(target, comparison) > -1);
		}

		[NotNull]
		public static String TrimAfter([NotNull] this String source, [NotNull] String token)
		{
			if (source == null)
				throw new ArgumentNullException("source");
			if (token == null)
				throw new ArgumentNullException("token");

			var result = source.Split(new[]{token}, 2, StringSplitOptions.None)[0];
			return result ?? source;
		}

		[NotNull]
		public static String TrimEnd([NotNull] this String source, Char trimChar, Int32 maxCharactersToTrim)
		{
			// Traverse backward through string skipping trimChars until maxCharactersToTrim is hit
			var index = source.Length - 1;
			while (maxCharactersToTrim > 0 && source[index] == trimChar)
			{
				maxCharactersToTrim--;
				index--;
			}

			return source.Substring(0, index + 1);
		}

		[NotNull]
		public static String EnsureLeading([NotNull] this String source, String leading)
		{
			if (leading == null)
				return source;

			if (source.StartsWith(leading))
				return source;

			return leading + source;
		}

		[NotNull]
		public static String EnsureTrailing([NotNull] this String source, String trailing)
		{
			if (trailing == null)
				return source;

			if (source.EndsWith(trailing))
				return source;

			return source + trailing;
		}
	}
}

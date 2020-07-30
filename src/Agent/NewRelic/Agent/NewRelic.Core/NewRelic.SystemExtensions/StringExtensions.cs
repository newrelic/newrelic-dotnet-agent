/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace NewRelic.SystemExtensions
{
    public static class StringExtensions
    {
        public static string TruncateUnicode(this string value, int maxLength)
        {
            if (value == null)
                throw new ArgumentNullException("value");
            if (maxLength < 0)
                throw new ArgumentOutOfRangeException(string.Format("maxLength must be positive.  value: {0}  maxLength: {1}", value, maxLength));

            var textElements = new StringInfo(value);
            if (textElements.LengthInTextElements <= maxLength)
                return value;

            return textElements.SubstringByTextElements(0, maxLength);
        }

        public static bool ContainsAny(this string source, IEnumerable<string> searchTargets, StringComparison comparison = StringComparison.InvariantCultureIgnoreCase)
        {
            if (source == null)
                return false;
            if (searchTargets == null)
                return false;

            return searchTargets.Any(target => target != null && source.IndexOf(target, comparison) > -1);
        }
        public static string TrimAfter(this string source, string token)
        {
            if (source == null)
                throw new ArgumentNullException("source");
            if (token == null)
                throw new ArgumentNullException("token");

            var result = source.Split(new[] { token }, 2, StringSplitOptions.None)[0];
            return result ?? source;
        }
        public static string TrimEnd(this string source, char trimChar, int maxCharactersToTrim)
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
        public static string EnsureLeading(this string source, string leading)
        {
            if (leading == null)
                return source;

            if (source.StartsWith(leading))
                return source;

            return leading + source;
        }
        public static string EnsureTrailing(this string source, string trailing)
        {
            if (trailing == null)
                return source;

            if (source.EndsWith(trailing))
                return source;

            return source + trailing;
        }
    }
}

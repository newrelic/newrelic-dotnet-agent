// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace NewRelic.Agent.Extensions.SystemExtensions
{
    public static class StringExtensions
    {
        public static string TruncateUnicodeStringByLength(this string value, int maxLength)
        {
            if (value == null)
            {
                throw new ArgumentNullException("value");
            }

            if (maxLength < 0)
            {
                throw new ArgumentOutOfRangeException(string.Format("maxLength must be positive.  value: {0}  maxLength: {1}",
                    value, maxLength));
            }

            if (value.Length <= maxLength)
            {
                return value;
            }

            var textElements = new StringInfo(value);
            if (textElements.LengthInTextElements <= maxLength)
                return value;

            return textElements.SubstringByTextElements(0, maxLength);
        }

        public static string TruncateUnicodeStringByBytes(this string value, uint maxBytes)
        {
            TruncateUnicodeStringByBytes(value, maxBytes, out var result);
            return result;
        }

        public static int SizeBytes(this string value)
        {
            if(value == null)
            {
                return 0;
            }

            return Encoding.UTF8.GetByteCount(value);
        }

        public static bool TruncateUnicodeStringByBytes(this string value, uint maxBytes, out string resultValue)
        {
            resultValue = value;

            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            if (maxBytes == 0)
            {
                resultValue = string.Empty;
                return value.Length > 0;
            }

            if (Encoding.UTF8.GetByteCount(value) <= maxBytes)
            {
                return false;
            }

            var bytes = new byte[maxBytes];
            var chars = value.ToCharArray();

            try
            {
                Encoding.UTF8.GetEncoder().Convert(chars, 0, chars.Length,
                    bytes, 0, (int)maxBytes,
                    true, out int charsUsed, out int _, out bool _);
                resultValue = new string(chars, 0, charsUsed);
                return true;
            }
            //In the case when maxBytes is less than the size of the first character in the input string,
            //the Encoder.Convert() method will throw buffer is too small exception. In this case, we want
            //the method to return an empty string instead.
            catch (ArgumentException)
            {
                resultValue = string.Empty;
                return value.Length > 0;
            }
        }

        public static bool ContainsAny(this string source, IEnumerable<string> searchTargets, StringComparison comparison = StringComparison.InvariantCultureIgnoreCase)
        {
            if (source == null)
                return false;
            if (searchTargets == null)
                return false;

            return searchTargets.Any(target => target != null && source.IndexOf(target, comparison) > -1);
        }

        public static string TrimAfterAChar(this string source, char token)
        {
            if (source == null)
                throw new ArgumentNullException("source");

            var stopIndex = source.IndexOf(token);
            var result = stopIndex == -1 ? source : source.Substring(0, stopIndex);

            return result;
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

        public static string CapitalizeWord(this string word)
        {
            if (string.IsNullOrEmpty(word))
            {
                return word;
            }
            string result = char.ToUpper(word[0]).ToString();
            if (word.Length > 1)
            {
                result += word.Substring(1);
            }
            return result;
        }

        public static string CapitalizeEachWord(this string source, char separator = ' ', bool removeSeparator = true)
        {
            var words = source.Split(separator);
            for (int idx = 0; idx < words.Length; idx++)
            {
                words[idx] = CapitalizeWord(words[idx]);
            }
            return removeSeparator ? string.Join("", words) : string.Join(separator.ToString(), words);
        }
    }
}

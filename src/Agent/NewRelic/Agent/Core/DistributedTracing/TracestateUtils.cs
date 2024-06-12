// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;

namespace NewRelic.Agent.Core.DistributedTracing
{
    internal static class TracestateUtils
    {
        private const int ValueMaxSize = 256;
        private const int MaxEntriesCount = 32;

        public static bool ValidateValue(string value)
        {
            // Value is opaque string up to 256 characters printable ASCII RFC0020 characters (i.e., the range
            // 0x20 to 0x7E) except comma , and =.

            if (value.Length > ValueMaxSize || value[value.Length - 1] == ' ' /* '\u0020' */)
            {
                return false;
            }

            foreach (var c in value)
            {
                if (c == ',' || c == '=' || c < ' ' /* '\u0020' */ || c > '~' /* '\u007E' */)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// This method parses a raw tracestate header string into a list of key/value pairs where keys are
        /// value on the left of '=' sign in tracestate entries.
        /// </summary>
        /// <param name="tracestateString">Raw tracestate header string.</param>
        /// <param name="tracestateEntries">A provided list that is used to store parsed tracestate entries.</param>
        /// <returns>Returns true if parses successfully. Returns false if not.</returns>
        public static bool ParseTracestate(string tracestateString, List<KeyValuePair<string, string>> tracestateEntries)
        {
            tracestateString = tracestateString.Trim(' ').Trim(',');

            var isValid = true;
            var keys = new HashSet<string>();

            while (tracestateString.Length > 0)
            {
                var entryEnd = tracestateString.IndexOf(',');
                if (entryEnd < 0)
                {
                    entryEnd = tracestateString.Length;
                }

                if (!TryParseKeyValue(tracestateString.Substring(0, entryEnd), out var key, out var value))
                {
                    // we have reached to the end.
                    if (entryEnd == tracestateString.Length)
                    {
                        break;
                    }

                    // if we can't parse a tracestate entry, ignore this entry and move on with another one.
                    tracestateString = tracestateString.Substring(entryEnd + 1, tracestateString.Length - entryEnd - 1);
                    continue;
                }

                var keyStr = key.ToString();

                // only add non-existing keys
                if (keys.Add(keyStr))
                {
                    tracestateEntries.Add(new KeyValuePair<string, string>(keyStr, value.ToString()));
                }

                if (tracestateEntries.Count > MaxEntriesCount)
                {
                    isValid = false;
                    break;
                }

                if (entryEnd == tracestateString.Length)
                {
                    break;
                }

                tracestateString = tracestateString.Substring(entryEnd + 1, tracestateString.Length - entryEnd - 1);
            }

            if (tracestateEntries.Count == 0)
            {
                return false;
            }

            if (!isValid)
            {
                tracestateEntries.Clear();
                return false;
            }

            return true;
        }

        private static bool TryParseKeyValue(string tracestateEntryString, out string key, out string value)
        {
            tracestateEntryString = tracestateEntryString.Trim(' ');

            key = default;
            value = default;

            var length = tracestateEntryString.Length;

            var keyEndIdx = tracestateEntryString.IndexOf('=');
            if (keyEndIdx <= 0)
            {
                return false;
            }

            var valueStartIdx = keyEndIdx + 1;
            if (valueStartIdx >= tracestateEntryString.Length)
            {
                return false;
            }

            key = tracestateEntryString.Substring(0, keyEndIdx);
            value = tracestateEntryString.Substring(valueStartIdx, length - valueStartIdx);

            return true;
        }
    }
}

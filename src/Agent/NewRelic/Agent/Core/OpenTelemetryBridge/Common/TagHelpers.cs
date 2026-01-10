// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using NewRelic.Agent.Core.Utilities;

namespace NewRelic.Agent.Core.OpenTelemetryBridge.Common
{
    public static class TagHelpers
    {
        /// <summary>
        /// Attempts to retrieve a value of the specified type associated with any of the given keys from the provided
        /// dictionary, and removes the corresponding key-value pair(s) from the dictionary.
        /// </summary>
        /// <remarks>This method iterates through the provided keys and attempts to retrieve the
        /// associated value from the dictionary. If a match is found, the corresponding key-value pair is removed from
        /// the dictionary. All keys in the <paramref name="keys"/> array are removed from the dictionary, regardless of
        /// whether a value is successfully retrieved.</remarks>
        /// <typeparam name="T">The type of the value to retrieve.</typeparam>
        /// <param name="tags">The dictionary containing key-value pairs to search and modify. Cannot be null.</param>
        /// <param name="keys">An array of keys to search for in the dictionary. Cannot be null or empty.</param>
        /// <param name="value">When this method returns, contains the value associated with the first matching key, if found; otherwise,
        /// the default value for the type <typeparamref name="T"/>.</param>
        /// <returns><see langword="true"/> if a value was successfully retrieved and at least one key-value pair was removed;
        /// otherwise, <see langword="false"/>.</returns>
        public static bool TryGetAndRemoveTag<T>(this Dictionary<string, object> tags, string[] keys, out T value)
        {
            value = default;
            var retVal = false;

            foreach (var key in keys)
            {
                if (!retVal && tags.TryGetValue<T, string, object>(key, out value))
                {
                    retVal = true;
                }

                tags.Remove(key);
            }

            return retVal;
        }

        /// <summary>
        /// Attempts to retrieve a value of the specified type from the dictionary using one of the provided keys.
        /// </summary>
        /// <remarks>This method iterates through the provided keys in order and stops at the first key
        /// that exists in the dictionary.  If no matching key is found or the value cannot be cast to the specified
        /// type, the method returns <see langword="false"/>  and the <paramref name="value"/> parameter is set to the
        /// default value of <typeparamref name="T"/>.</remarks>
        /// <typeparam name="T">The type of the value to retrieve.</typeparam>
        /// <param name="tags">The dictionary containing key-value pairs to search.</param>
        /// <param name="keys">An array of keys to search for in the dictionary. The method checks each key in order.</param>
        /// <param name="value">When this method returns, contains the value associated with the first key found in the dictionary,  if the
        /// key exists and the value can be cast to the specified type; otherwise, the default value for the type
        /// <typeparamref name="T"/>.</param>
        /// <returns><see langword="true"/> if a key is found in the dictionary and its associated value can be cast to the
        /// specified type;  otherwise, <see langword="false"/>.</returns>
        public static bool TryGetTag<T>(this Dictionary<string, object> tags, string[] keys, out T value)
        {
            value = default;
            foreach (var key in keys)
            {
                if (tags.TryGetValue<T, string, object>(key, out value))
                {
                    return true;
                }
            }
            return false;
        }
    }
}

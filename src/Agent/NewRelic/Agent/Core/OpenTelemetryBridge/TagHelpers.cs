// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using NewRelic.Agent.Core.Utilities;

namespace NewRelic.Agent.Core.OpenTelemetryBridge
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
    }
}

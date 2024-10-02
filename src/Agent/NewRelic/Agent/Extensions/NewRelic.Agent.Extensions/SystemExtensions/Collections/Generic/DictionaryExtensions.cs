// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;

namespace NewRelic.Agent.Extensions.SystemExtensions.Collections.Generic
{
    public static class DictionaryExtensions
    {
        /// <summary>
        /// Gets the value at <paramref name="key"/> from <paramref name="dictionary"/> or default(<typeparamref name="TValue"/>) if <paramref name="key"/> is not found.
        /// </summary>
        /// <typeparam name="TKey">The type of the <paramref name="dictionary"/>'s key.</typeparam>
        /// <typeparam name="TValue">The type of the <paramref name="dictionary"/>'s value.</typeparam>
        /// <param name="dictionary">The dictionary to lookup the value in.</param>
        /// <param name="key">The key to lookup in the <paramref name="dictionary"/>.</param>
        /// <returns>Either the value found at <paramref name="key"/> or default(<typeparamref name="TValue"/>)</returns>
        public static TValue GetValueOrDefault<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key)
        {
            return dictionary.GetValueOrDefault(key, default(TValue));
        }

        /// <summary>
        /// Gets the value at <paramref name="key"/> from <paramref name="dictionary"/> or <paramref name="default"/> if <paramref name="key"/> is not found.
        /// </summary>
        /// <typeparam name="TKey">The type of the <paramref name="dictionary"/>'s key.</typeparam>
        /// <typeparam name="TValue">The type of the <paramref name="dictionary"/>'s value.</typeparam>
        /// <param name="dictionary">The dictionary to lookup the value in.</param>
        /// <param name="key">The key to lookup in the <paramref name="dictionary"/>.</param>
        /// <param name="default">The value to return if nothing is found in <paramref name="dictionary"/> at <paramref name="key"/>.</param>
        /// <returns>Either the value found at <paramref name="key"/> or <paramref name="default"/> if <paramref name="key"/> is not found.</returns>
        public static TValue GetValueOrDefault<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, TValue @default)
        {
            return dictionary.GetValueOrDefault(key, () => @default);
        }

        /// <summary>
        /// Gets the value at <paramref name="key"/> from <paramref name="dictionary"/> or the result of evaluating <paramref name="defaultEvaluator"/> if <paramref name="key"/> is not found.
        /// </summary>
        /// <typeparam name="TKey">The type of the <paramref name="dictionary"/>'s key.</typeparam>
        /// <typeparam name="TValue">The type of the <paramref name="dictionary"/>'s value.</typeparam>
        /// <param name="dictionary">The dictionary to lookup the value in.</param>
        /// <param name="key">The key to lookup in the <paramref name="dictionary"/>.</param>
        /// <param name="default">The value to return if nothing is found in <paramref name="dictionary"/> at <paramref name="key"/>.</param>
        /// <returns>Either the value found at <paramref name="key"/> or the result of evaluating <paramref name="defaultEvaluator"/> if <paramref name="key"/> is not found.</returns>
        public static TValue GetValueOrDefault<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, Func<TValue> defaultEvaluator)
        {
            if (defaultEvaluator == null)
                throw new ArgumentNullException("defaultEvaluator");

            if (dictionary == null)
                return defaultEvaluator();

            if (key == null)
                return defaultEvaluator();

            TValue value;
            var result = dictionary.TryGetValue(key, out value);
            return (result)
                ? value
                : defaultEvaluator();
        }
    }
}

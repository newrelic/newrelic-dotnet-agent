// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace NewRelic.Agent.Core.Utilities
{
    public static class DictionaryExtensions
    {
        public static void Merge<TKey, TValue>(this IDictionary<TKey, TValue> me, IDictionary<TKey, TValue> merge)
        {
            if (merge == null)
                return;

            foreach (var item in merge)
            {
                if (item.Key == null)
                    continue;
                me[item.Key] = item.Value;
            }
        }

        /// <summary>
        /// Takes a &lt;String, T&gt; enumerable (e.g., &lt;String, T&gt; dictionary) and returns a new &lt;String, Object&gt; dictionary made up off the items that were in source.
        /// </summary>
        /// <typeparam name="T">A type that is castable to Object (i.e., anything).</typeparam>
        /// <param name="source">The enumerable (dictionary) that you want to downcast construct.</param>
        /// <returns>A dictionary containing all of the items in the source dictionary, but cast to Objects.</returns>
        public static IDictionary<string, object> DowncastCopyConstruct<T>(this IEnumerable<KeyValuePair<string, T>> source)
        {
            if (source == null)
                throw new ArgumentNullException("source");

            var destination = new Dictionary<string, object>();
            foreach (var item in source)
            {
                if (item.Key == null)
                    continue;

                destination.Add(item.Key, item.Value);
            }

            return destination;
        }

        public static void AddIfNotNull<T, U>(this IDictionary<T, U> dictionary, T key, U value) where U : class
        {
            if (value != null)
            {
                dictionary.Add(key, value);
            }
        }

        public static void AddStringIfNotNullOrEmpty<T>(this IDictionary<T, object> dictionary, T key, string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                dictionary.Add(key, value);
            }
        }

        public static void AddTimespanIfNotNull<T>(this IDictionary<T, object> dictionary, T key, TimeSpan? value, TimeUnit timeUnit)
        {
            if (value == null)
                return;

            switch (timeUnit)
            {
                case TimeUnit.Ticks:
                    dictionary.Add(key, value.Value.Ticks);
                    break;
                case TimeUnit.Milliseconds:
                    dictionary.Add(key, value.Value.TotalMilliseconds);
                    break;
                case TimeUnit.Seconds:
                    dictionary.Add(key, value.Value.TotalSeconds);
                    break;
                case TimeUnit.Minutes:
                    dictionary.Add(key, value.Value.TotalMinutes);
                    break;
                case TimeUnit.Hours:
                    dictionary.Add(key, value.Value.TotalHours);
                    break;
                case TimeUnit.Days:
                    dictionary.Add(key, value.Value.TotalHours);
                    break;
            }
        }

        public static ReadOnlyDictionary<TKey, TValue> WrapInReadOnly<TKey, TValue>(this IDictionary<TKey, TValue> source)
        {
            return new ReadOnlyDictionary<TKey, TValue>(source);
        }

        public static ReadOnlyDictionary<TKey, TValue> CopyToReadOnly<TKey, TValue>(this IDictionary<TKey, TValue> source)
        {
            return new ReadOnlyDictionary<TKey, TValue>(new Dictionary<TKey, TValue>(source));
        }

        /// <summary>
        /// Attempts to get a value from the dictionary and cast it to the specified type.
        /// </summary>
        public static bool TryGetValue<TResult, TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, out TResult defaultValue)
        {
            if (dictionary.TryGetValue(key, out var value) && value is TResult typedValue)
            {
                defaultValue = typedValue;
                return true;
            }

            defaultValue = default;
            return false;
        }
    }
}

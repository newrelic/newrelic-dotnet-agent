// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace NewRelic.Agent.Tests.TestSerializationHelpers.JsonConverters
{
    public static class IEnumerableExtensions
    {
        public static bool IsEmpty<T>(this IEnumerable<T> enumerable)
        {
            return !enumerable.Any();
        }

        public static IEnumerable<T> NotNull<T>(this IEnumerable<T> @this)
        {
            return @this.Where(x => x != null);
        }

        public static ReadOnlyDictionary<TKey, TValue> ToReadOnlyDictionary<TKey, TValue>(this IEnumerable<KeyValuePair<TKey, TValue>> source)
        {
            return source.ToReadOnlyDictionary(item => item.Key, item => item.Value);
        }

        public static ReadOnlyDictionary<TKey, TValue> ToReadOnlyDictionary<TSource, TKey, TValue>(this IEnumerable<TSource> source, Func<TSource, TKey> keySelector, Func<TSource, TValue> valueSelector)
        {
            var dictionary = new Dictionary<TKey, TValue>();
            foreach (var item in source)
            {
                if (item == null)
                    continue;
                var key = keySelector(item);
                if (key == null)
                    continue;
                var value = valueSelector(item);
                if (value == null)
                    continue;

                dictionary.Add(key, value);
            }

            return new ReadOnlyDictionary<TKey, TValue>(dictionary);
        }

        public static bool IsSequential(this IEnumerable<uint> sequence)
        {
            // Null is inherently sequential... sort of. I mean, it's not NOT sequential, right?
            if (sequence == null)
                return true;

            var enumerator = sequence.GetEnumerator();

            // An empty set is inherently sequential
            if (!enumerator.MoveNext())
                return true;

            var previousValue = enumerator.Current;
            while (enumerator.MoveNext())
            {
                // If any value is not equal to (1 + previousValue) then the set is not sequential
                if (enumerator.Current != ++previousValue)
                    return false;
            }

            return true;
        }

        public static TimeSpan Sum<T>(this IEnumerable<T> source, Func<T, TimeSpan> selector)
        {
            return source.Aggregate(TimeSpan.Zero, (runningTotal, nextItem) => runningTotal + selector(nextItem));
        }

        public static bool IsSequential<T>(this IEnumerable<T> enumerable, Func<T, uint> predicate)
        {
            if (enumerable == null)
                return true;

            return enumerable.Select(predicate).IsSequential();
        }
    }
}

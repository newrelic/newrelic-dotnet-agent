using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using JetBrains.Annotations;

namespace NewRelic.Agent.IntegrationTestHelpers.JsonConverters
{
    public static class IEnumerableExtensions
    {
        [NotNull]
        public static Boolean IsEmpty<T>([NotNull] this IEnumerable<T> enumerable)
        {
            return !enumerable.Any();
        }

        [Pure]
        [NotNull]
        public static IEnumerable<T> NotNull<T>([NotNull] this IEnumerable<T> @this)
        {
            return @this.Where(x => x != null);
        }

        [NotNull]
        public static ReadOnlyDictionary<TKey, TValue> ToReadOnlyDictionary<TKey, TValue>([NotNull] this IEnumerable<KeyValuePair<TKey, TValue>> source)
        {
            return source.ToReadOnlyDictionary(item => item.Key, item => item.Value);
        }

        [NotNull]
        public static ReadOnlyDictionary<TKey, TValue> ToReadOnlyDictionary<TSource, TKey, TValue>([NotNull] this IEnumerable<TSource> source, [NotNull] Func<TSource, TKey> keySelector, [NotNull] Func<TSource, TValue> valueSelector)
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

        [Pure]
        public static Boolean IsSequential(this IEnumerable<UInt32> sequence)
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

        [Pure]
        public static TimeSpan Sum<T>([NotNull] this IEnumerable<T> source, [NotNull] Func<T, TimeSpan> selector)
        {
            return source.Aggregate(TimeSpan.Zero, (runningTotal, nextItem) => runningTotal + selector(nextItem));
        }

        [Pure]
        public static Boolean IsSequential<T>(this IEnumerable<T> enumerable, [NotNull] Func<T, UInt32> predicate)
        {
            if (enumerable == null)
                return true;

            return enumerable.Select(predicate).IsSequential();
        }
    }
}

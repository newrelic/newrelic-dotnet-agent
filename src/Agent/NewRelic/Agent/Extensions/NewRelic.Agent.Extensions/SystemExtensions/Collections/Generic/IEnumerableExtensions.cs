// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace NewRelic.Agent.Extensions.SystemExtensions.Collections.Generic
{
    public static class IEnumerableExtensions
    {
        /// <summary>
        /// Returns a new collection based on source, skipping elements that DO satisfy the given predicate. Basically the opposite of .Where()
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <param name="predicate">A function to test each element for a condition. Elements that DO meet the condition are omitted; elements that DO NOT meet the condition are included.</param>
        /// <returns></returns>
        public static IEnumerable<T> Unless<T>(this IEnumerable<T> source, Func<T, bool> predicate)
        {
            return source.Where(current => !predicate(current));
        }

        /// <summary>
        /// Returns a new collection based on source, skipping elements that DO satisfy the given predicate. Basically the opposite of .Where(), except that you can compare the current element to the last one.
        /// 
        /// Will always return the first element (if any) because there is nothing to compare the first element to.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <param name="predicate">A function that takes the last element and the current element (in that order) to test each element for a condition. Elements that DO meet the condition are omitted; elements that DO NOT meet the condition are included.</param>
        /// <returns></returns>

        public static IEnumerable<T> Unless<T>(this IEnumerable<T> source, Func<T, T, bool> predicate)
        {
            var last = default(T);
            return source.Where((current, index) =>
            {
                // Always return the first item
                if (index == 0)
                {
                    last = current;
                    return true;
                }

                // Check if the current item matches the predicate before moving the "last" pointer
                var isMatch = predicate(last, current);
                last = current;

                // Items that match the predicate should be skipped
                return !isMatch;
            });
        }

        public enum DuplicateKeyBehavior
        {
            /// <summary>
            /// Keep the first value found with the given key
            /// </summary>
            KeepFirst,
            /// <summary>
            /// Keep the last value found with the given key
            /// </summary>
            KeepLast,
            /// <summary>
            /// Throw if duplicate keys are found
            /// </summary>
            Throw
        }

        /// <summary>
        /// Creates a dictionary from a list of KeyValuePairs, using the specified behavior in case of duplicate keys.
        /// </summary>
        /// <typeparam name="TKey"></typeparam>
        /// <typeparam name="TValue"></typeparam>
        /// <param name="source"></param>
        /// <param name="duplicateKeyBehavior"></param>
        /// <param name="equalityComparer"></param>
        /// <returns></returns>

        public static Dictionary<TKey, TValue> ToDictionary<TKey, TValue>(this IEnumerable<KeyValuePair<TKey, TValue>> source, DuplicateKeyBehavior duplicateKeyBehavior = DuplicateKeyBehavior.Throw, IEqualityComparer<TKey> equalityComparer = null)
        {
            var dictionary = new Dictionary<TKey, TValue>(equalityComparer);

            foreach (var kvp in source)
            {
                if (kvp.Key == null)
                    continue;

                if (dictionary.ContainsKey(kvp.Key))
                {
                    if (duplicateKeyBehavior == DuplicateKeyBehavior.Throw)
                        throw new ArgumentException("An item with the same key has already been added");
                    if (duplicateKeyBehavior == DuplicateKeyBehavior.KeepFirst)
                        continue;
                }

                dictionary[kvp.Key] = kvp.Value;
            }

            return dictionary;
        }

        public static IEnumerable<T> NotNull<T>(this IEnumerable<T> @this)
        {
            return @this.Where(x => x != null);
        }

        public static IEnumerable<T> Swallow<T, TException>(this IEnumerable<T> source) where TException : Exception
        {
            using (var enumerator = source.GetEnumerator())
            {
                var next = true;
                while (next)
                {
                    try
                    {
                        next = enumerator.MoveNext();
                    }
                    catch (TException)
                    {
                        continue;
                    }

                    if (next)
                        yield return enumerator.Current;
                }
            }
        }

        public static IEnumerable<T> Swallow<T>(this IEnumerable<T> source)
        {
            return Swallow<T, Exception>(source);
        }

        public static IEnumerable<T> ForEachLazy<T>(this IEnumerable<T> source, Action<T> action)
        {
            return new ForEachEnumerable<T>(source, action);
        }

        public static void ForEachNow<T>(this IEnumerable<T> source, Action<T> action)
        {
            var enumerable = source.ForEachLazy(action);
            foreach (var item in enumerable)
            {
                // do nothing, we just want to force enumeration
            }
        }

        /// <summary>
        /// Flattens a tree of items into an enumerable.
        /// </summary>
        /// <param name="root">The root node of the tree.</param>
        /// <param name="getChildren">A method that will return the children of a given node.</param>
        /// <returns>An enumerable that includes all nodes in the tree.  Does not guarantee any particular order.</returns>
        public static IEnumerable<T> Flatten<T>(this T root, Func<T, IEnumerable<T>> getChildren)
        {
            var stack = new Stack<T>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                var current = stack.Pop();
                yield return current;
                foreach (var child in getChildren(current))
                    stack.Push(child);
            }
        }

        public static bool IsEmpty<T>(this IEnumerable<T> enumerable)
        {
            return !enumerable.Any();
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

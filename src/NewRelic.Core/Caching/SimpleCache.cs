// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading;

namespace NewRelic.Core.Caching
{
    /// <summary>
    /// Simple cache maintains a collection. Periodically, the cache is maintained on a seperate thread.
    /// When it is full, the cache is cleared.
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    public class SimpleCache<TKey, TValue> : ICacheStats, IDisposable where TValue : class
    {
        private readonly ConcurrentDictionary<TKey, TValue> _cacheMap = new ConcurrentDictionary<TKey, TValue>();

        private readonly Timer _maintainCacheTimer;

        /// <summary>
        /// Time in milliseconds. How often the Agent will check cache size and clears off the cache
        /// if its size is greater than its capacity.
        /// </summary>
        public const int CleanUpTimePeriod = 500;

        private int _countHits;
        private int _countMisses;
        private int _countEjections;

        private int _capacity;

        public int Capacity
        {
            get => _capacity;
            set => SetCapacity(value);
        }


        ///// <summary>
        ///// Metric for counting the number of items a Get function hits an existing item in the cache
        ///// </summary>
        public int CountHits => _countHits;

        ///// <summary>
        ///// Metric for counting the number of items a Get function does not hit an existing item in the cache
        ///// </summary>
        public int CountMisses => _countMisses;

        ///// <summary>
        ///// Metric for counting the number of items gets removed from the cache
        ///// </summary>
        public int CountEjections => _countEjections;

        public SimpleCache(int capacity)
        {
            Capacity = capacity;
            _maintainCacheTimer = new Timer(o => MaintainCache(), null, CleanUpTimePeriod, CleanUpTimePeriod);
        }

        /// <summary>
        /// Allows searching of the cache without updating stats or affecting the priority of items in the cache.
        /// </summary>
        public TValue Peek(TKey key)
        {
            var node = PeekInternal(key);
            return node;
        }

        /// <summary>
        /// Allows searching of the cache.  If found, returns the existing item and updates the statistics.
        /// If not found, returns NULL.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public TValue Get(TKey key)
        {

            var node = PeekInternal(key);

            if (node != null)
            {
                Interlocked.Increment(ref _countHits);
            }
            else
            {
                Interlocked.Increment(ref _countMisses);
            }

            return node;
        }

        /// <summary>
        /// Attempts to find an item in the cache.  If found, returns the existing item and updates the statistics.
        /// If not found, will add the item to the cache.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="valueFx">Function to call to obtain the value if the key is not present in the cache.</param>
        /// <returns></returns>
        public TValue GetOrAdd(TKey key, Func<TValue> valueFx)
        {
            var result = Get(key);

            return result ?? _cacheMap.GetOrAdd(key, x => valueFx());
        }

        /// <summary>
        /// Allows resetting of the Hit, Miss, and Ejection counters
        /// </summary>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void ResetStats()
        {
            _countHits = 0;
            _countMisses = 0;
            _countEjections = 0;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private void SetCapacity(int newCapacity)
        {
            if (newCapacity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(newCapacity), newCapacity, "Cache size cannot be less than 1.");
            }

            _capacity = newCapacity;
        }

        /// <summary>
        /// The number of items stored in the cache
        /// </summary>
        public int Size => _cacheMap.Count;

        private TValue PeekInternal(TKey key)
        {
            TValue node;
            if (!_cacheMap.TryGetValue(key, out node))
            {
                return null;
            }

            return node;
        }

        /// <summary>
        /// public only for unit tests. Don't call this method directly!
        /// </summary>        
        public void MaintainCache()
        {
            var count = _cacheMap.Count;
            if (count > _capacity)
            {
                _cacheMap.Clear();
                Interlocked.Add(ref _countEjections, count);
            }
        }

        public void Dispose()
        {
            _cacheMap.Clear();
            _maintainCacheTimer?.Dispose();
        }

        public void Reset()
        {
            _cacheMap.Clear();
            ResetStats();
        }
    }
}

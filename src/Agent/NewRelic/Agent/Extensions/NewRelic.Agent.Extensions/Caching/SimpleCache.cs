// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading;

namespace NewRelic.Agent.Extensions.Caching;

/// <summary>
/// Simple cache maintains a collection. New items are dropped instead of added once the cache is at
/// (approximately) capacity. Periodically, the cache is maintained on a separate thread: if any items
/// were dropped, or the tracked size is over capacity (e.g. after <see cref="Capacity"/> is lowered),
/// the cache is cleared.
/// </summary>
/// <typeparam name="TKey"></typeparam>
/// <typeparam name="TValue"></typeparam>
public class SimpleCache<TKey, TValue> : ICacheStats, IDisposable where TValue : class
{
    private readonly ConcurrentDictionary<TKey, TValue> _cacheMap = new ConcurrentDictionary<TKey, TValue>();

    private readonly Timer _maintainCacheTimer;

    /// <summary>
    /// Default time in milliseconds between <see cref="MaintainCache"/> checks.
    /// </summary>
    public const int CleanUpTimePeriod = 500;

    private int _countHits;
    private int _countMisses;
    private int _countEjections;
    private int _countDropped;

    /// <summary>
    /// Set when an item is dropped and cleared back to 0 by <see cref="MaintainCache"/>. Separate from
    /// <see cref="_countDropped"/> (which is cumulative and only reset via <see cref="ResetStats"/>) so that
    /// a single drop doesn't cause every future maintenance tick to clear the cache forever.
    /// </summary>
    private int _droppedSinceLastMaintenance;

    /// <summary>
    /// The number of items currently in <see cref="_cacheMap"/>. Tracked separately because
    /// ConcurrentDictionary.Count acquires every one of the dictionary's internal locks, which
    /// blocks all concurrent writers, and the count is consulted on every insert attempt.
    /// </summary>
    private int _count;

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

    ///// <summary>
    ///// Metric for counting the number of items that were not added to the cache because it was at capacity
    ///// </summary>
    public int CountDropped => _countDropped;

    /// <param name="capacity"></param>
    /// <param name="maintainCacheIntervalMs">
    /// How often <see cref="MaintainCache"/> runs on its own timer. Exposed (instead of hard-coding
    /// <see cref="CleanUpTimePeriod"/>) so tests can pass <see cref="Timeout.Infinite"/> to disable the
    /// timer and drive maintenance deterministically via explicit <see cref="MaintainCache"/> calls.
    /// </param>
    public SimpleCache(int capacity, int maintainCacheIntervalMs = CleanUpTimePeriod)
    {
        Capacity = capacity;
        _maintainCacheTimer = new Timer(o => MaintainCache(), null, maintainCacheIntervalMs, maintainCacheIntervalMs);
    }

    /// <summary>
    /// Allows searching of the cache without updating stats.
    /// </summary>
    public TValue Peek(TKey key)
    {
        _cacheMap.TryGetValue(key, out var value);
        return value;
    }

    /// <summary>
    /// Checks whether the specified key exists in the cache without updating stats. Unlike <see cref="Peek"/>,
    /// this is correct for a key whose cached value is itself null.
    /// </summary>
    public bool Contains(TKey key) => _cacheMap.ContainsKey(key);

    /// <summary>
    /// Allows searching of the cache. If found, returns the existing item (which may itself be null) and
    /// records a hit. If not found, records a miss and returns null.
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    public TValue Get(TKey key)
    {
        TryGetTrackingStats(key, out var value);
        return value;
    }

    /// <summary>
    /// Attempts to find an item in the cache.  If found (a cached null value counts as found), returns the
    /// existing item and updates the statistics. If not found, will add the item to the cache, unless the
    /// cache is at capacity, in which case the item is dropped (not cached) but is still computed and returned.
    /// </summary>
    /// <param name="key"></param>
    /// <param name="valueFx">Function to call to obtain the value if the key is not present in the cache.</param>
    /// <returns></returns>
    public TValue GetOrAdd(TKey key, Func<TValue> valueFx)
    {
        if (TryGetTrackingStats(key, out var existing))
        {
            return existing;
        }

        if (_count >= _capacity)
        {
            Interlocked.Increment(ref _countDropped);
            Interlocked.Increment(ref _droppedSinceLastMaintenance);
            return valueFx();
        }

        var newValue = valueFx();

        // TryAdd, not GetOrAdd: it returns true on exactly one thread - the one that actually
        // inserted - so it is a safe trigger for incrementing the tracked count. A factory delegate
        // passed to GetOrAdd can run on more than one thread when they race for the same new key,
        // which would over-count.
        if (_cacheMap.TryAdd(key, newValue))
        {
            Interlocked.Increment(ref _count);
            return newValue;
        }

        // Another thread won the race for this key; return the value that is actually cached, which may
        // legitimately be null. If the cache was cleared in between, fall back to the value just computed.
        return _cacheMap.TryGetValue(key, out var raceWinner) ? raceWinner : newValue;
    }

    /// <summary>
    /// Attempts to add an item to the cache.  If the item already exists, returns false. Otherwise, returns true.
    /// If the cache is at capacity, the item is dropped and not added.
    /// </summary>
    /// <param name="key"></param>
    /// <param name="valueFunc">Function to call to obtain the value if the key is not present in the cache.</param>
    /// <returns></returns>
    public bool TryAdd(TKey key, Func<TValue> valueFunc)
    {
        // Fast exit for a key that is already cached: it is not a capacity drop, and there is no
        // need to build a value for it.
        if (_cacheMap.ContainsKey(key))
        {
            return false;
        }

        if (_count >= _capacity)
        {
            Interlocked.Increment(ref _countDropped);
            Interlocked.Increment(ref _droppedSinceLastMaintenance);
            return false;
        }

        if (!_cacheMap.TryAdd(key, valueFunc()))
        {
            // Another thread added this key between the ContainsKey check and here.
            return false;
        }

        Interlocked.Increment(ref _count);
        return true;
    }

    /// <summary>
    /// Allows resetting of the Hit, Miss, Ejection, and Dropped counters
    /// </summary>
    [MethodImpl(MethodImplOptions.Synchronized)]
    public void ResetStats()
    {
        _countHits = 0;
        _countMisses = 0;
        _countEjections = 0;
        _countDropped = 0;
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
    /// The approximate number of items stored in the cache. Concurrent writers racing the capacity
    /// check can transiently push this a little past <see cref="Capacity"/>; it self-corrects at the
    /// next <see cref="MaintainCache"/> tick.
    /// </summary>
    public int Size => _count;

    /// <summary>
    /// Looks up <paramref name="key"/> and records a hit or miss based on whether it is actually present -
    /// not on whether <paramref name="value"/> is null, since a cached null value is a valid hit.
    /// </summary>
    private bool TryGetTrackingStats(TKey key, out TValue value)
    {
        if (_cacheMap.TryGetValue(key, out value))
        {
            Interlocked.Increment(ref _countHits);
            return true;
        }

        Interlocked.Increment(ref _countMisses);
        return false;
    }

    /// <summary>
    /// Clears the cache if anything was dropped since the last check, or if the tracked size is over
    /// capacity (e.g. <see cref="Capacity"/> was just lowered below the current size). Public only for
    /// unit tests. Don't call this method directly!
    /// </summary>
    public void MaintainCache()
    {
        if (_droppedSinceLastMaintenance > 0 || _count > _capacity)
        {
            // Zero the tracked count before clearing the map, not after: once both halves of every
            // in-flight insert have completed, this guarantees map.Count <= _count - drift is over-report
            // only, so the capacity guard can never be silently defeated and the map can't grow past
            // capacity indefinitely. (Transiently, e.g. mid-Clear, the map can briefly hold more real
            // entries than _count reports - that's expected and resolves once Clear finishes.) The
            // opposite order (Clear then Exchange) permits an insert to durably survive the Clear while
            // its Increment is wiped by the Exchange, permanently under-counting and letting the map
            // creep past capacity forever.
            var count = Interlocked.Exchange(ref _count, 0);
            _cacheMap.Clear();
            Interlocked.Add(ref _countEjections, count);
            Interlocked.Exchange(ref _droppedSinceLastMaintenance, 0);
        }
    }

    public void Dispose()
    {
        Interlocked.Exchange(ref _count, 0);
        _cacheMap.Clear();
        _maintainCacheTimer?.Dispose();
    }

    public void Reset()
    {
        Interlocked.Exchange(ref _count, 0);
        _cacheMap.Clear();
        Interlocked.Exchange(ref _droppedSinceLastMaintenance, 0);
        ResetStats();
    }
}

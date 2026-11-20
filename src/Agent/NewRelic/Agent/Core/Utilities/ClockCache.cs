// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace NewRelic.Agent.Core.Utilities;

/// <summary>
/// A bounded, thread-safe cache with CLOCK eviction. A hit takes no lock and allocates nothing.
/// A miss takes a lock and evicts at most one entry.
/// </summary>
public sealed class ClockCache<TKey, TValue> where TValue : class
{
    private const int InitialSlotCount = 16;

    private readonly int _capacity;
    private readonly Func<TKey, TValue> _factory;
    private readonly ConcurrentDictionary<TKey, Entry> _map;
    private readonly object _lock = new object();

    // _slots and _hand are used only while holding _lock. _count is written under _lock and read by Count without it.
    private Entry[] _slots;
    private int _count;
    private int _hand;

    public int Count => Volatile.Read(ref _count);

    public ClockCache(int capacity, Func<TKey, TValue> factory, IEqualityComparer<TKey> comparer)
    {
        if (capacity < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be at least 1.");
        }

        _capacity = capacity;
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _map = new ConcurrentDictionary<TKey, Entry>(comparer ?? throw new ArgumentNullException(nameof(comparer)));
    }

    public TValue GetOrAdd(TKey key)
    {
        if (_map.TryGetValue(key, out var entry))
        {
            // Read first: most hits then skip a write to shared memory.
            if (Volatile.Read(ref entry.Referenced) == 0)
            {
                Volatile.Write(ref entry.Referenced, 1);
            }

            return entry.Value;
        }

        return Add(key);
    }

    private TValue Add(TKey key)
    {
        // The factory runs outside the lock. Two threads can compute the same key; the first one in wins.
        var value = _factory(key);

        lock (_lock)
        {
            if (_map.TryGetValue(key, out var existing))
            {
                return existing.Value;
            }

            int slot;
            if (_count < _capacity)
            {
                if (_slots == null)
                {
                    _slots = new Entry[Math.Min(InitialSlotCount, _capacity)];
                }
                else if (_count == _slots.Length)
                {
                    Array.Resize(ref _slots, Math.Min(_slots.Length * 2, _capacity));
                }

                slot = _count;
                _count++;
            }
            else
            {
                slot = Evict();
            }

            var newEntry = new Entry(key, value);
            _slots[slot] = newEntry;
            _map[key] = newEntry;
            return value;
        }
    }

    // Caller holds _lock and the cache is full. Returns the freed slot; _hand stays on it.
    private int Evict()
    {
        // The bound matters: hits on other threads can set flags again behind the hand.
        for (var step = 0; step < _capacity; step++)
        {
            if (Volatile.Read(ref _slots[_hand].Referenced) == 0)
            {
                break;
            }

            Volatile.Write(ref _slots[_hand].Referenced, 0);
            _hand = (_hand + 1) % _capacity;
        }

        _map.TryRemove(_slots[_hand].Key, out _);
        return _hand;
    }

    private sealed class Entry
    {
        public readonly TKey Key;
        public readonly TValue Value;
        public int Referenced;

        public Entry(TKey key, TValue value)
        {
            Key = key;
            Value = value;
        }
    }
}

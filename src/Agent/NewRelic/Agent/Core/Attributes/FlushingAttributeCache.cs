// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Concurrent;
using System.Threading;
using NewRelic.Agent.Extensions.Logging;

namespace NewRelic.Agent.Core.Attributes;

// A string-keyed cache that clears itself when it goes over its capacity, so names from
// request data cannot grow it without limit (NR-651452). The bound is the capacity plus
// the number of threads that miss at the same moment.
public class FlushingAttributeCache<TValue> where TValue : class
{
    private readonly ConcurrentDictionary<string, TValue> _map = new ConcurrentDictionary<string, TValue>();
    private readonly Func<string, TValue> _valueFactory;
    private readonly Action _onFlush;
    private readonly int _capacity;
    private readonly string _cacheName;
    private int _count;
    private int _flushLogged;

    public FlushingAttributeCache(string cacheName, int capacity, Func<string, TValue> valueFactory, Action onFlush)
    {
        _cacheName = cacheName;
        _capacity = capacity;
        _valueFactory = valueFactory;
        _onFlush = onFlush;
    }

    // For unit tests only: the real number of entries. ConcurrentDictionary.Count takes every lock.
    public int Count => _map.Count;

    public TValue GetOrAdd(string key)
    {
        if (_map.TryGetValue(key, out var existing))
        {
            return existing;
        }

        // Exactly one thread sees capacity + 1, so one thread flushes per cycle.
        var count = Interlocked.Increment(ref _count);
        if (count > _capacity)
        {
            if (count != _capacity + 1)
            {
                // Another thread is flushing: return an uncached value so the map cannot grow.
                return _valueFactory(key);
            }

            Flush();
        }

        return _map.GetOrAdd(key, _valueFactory);
    }

    private void Flush()
    {
        try
        {
            _map.Clear();
            _onFlush();
        }
        finally
        {
            Interlocked.Exchange(ref _count, 1);
        }

        if (Interlocked.Exchange(ref _flushLogged, 1) == 0)
        {
            Log.Info($"The {_cacheName} attribute cache reached its limit of {_capacity} names and was cleared. Names are cached again as requests arrive. To change the limit, set the RequestAttributeCacheCapacity app setting.");
        }
    }
}

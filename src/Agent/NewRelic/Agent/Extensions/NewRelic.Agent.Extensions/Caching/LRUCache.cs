// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Threading;

namespace NewRelic.Agent.Extensions.Caching
{
    /// <summary>
    /// A thread-safe LRU cache implementation.
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    public class LRUCache<TKey, TValue>
    {
        private readonly int _capacity;
        private readonly Dictionary<TKey, LinkedListNode<CacheItem>> _cacheMap;
        private readonly LinkedList<CacheItem> _lruList;
        private readonly ReaderWriterLockSlim _lock = new();

        public LRUCache(int capacity)
        {
            if (capacity <= 0)
            {
                throw new ArgumentException("Capacity must be greater than zero.", nameof(capacity));
            }

            _capacity = capacity;
            _cacheMap = new Dictionary<TKey, LinkedListNode<CacheItem>>(capacity);
            _lruList = new LinkedList<CacheItem>();
        }

        public TValue Get(TKey key)
        {
            _lock.EnterUpgradeableReadLock();
            try
            {
                if (_cacheMap.TryGetValue(key, out var node))
                {
                    // Move the accessed node to the front of the list
                    _lock.EnterWriteLock();
                    try
                    {
                        _lruList.Remove(node);
                        _lruList.AddFirst(node);
                    }
                    finally
                    {
                        _lock.ExitWriteLock();
                    }
                    return node.Value.Value;
                }
                throw new KeyNotFoundException("The given key was not present in the cache.");
            }
            finally
            {
                _lock.ExitUpgradeableReadLock();
            }
        }

        public void Put(TKey key, TValue value)
        {
            _lock.EnterWriteLock();
            try
            {
                if (_cacheMap.TryGetValue(key, out var node))
                {
                    // Update the value and move the node to the front of the list
                    node.Value.Value = value;
                    _lruList.Remove(node);
                    _lruList.AddFirst(node);
                }
                else
                {
                    if (_cacheMap.Count >= _capacity)
                    {
                        // Remove the least recently used item
                        var lruNode = _lruList.Last;
                        _cacheMap.Remove(lruNode.Value.Key);
                        _lruList.RemoveLast();
                    }

                    // Add the new item to the cache
                    var cacheItem = new CacheItem(key, value);
                    var newNode = new LinkedListNode<CacheItem>(cacheItem);
                    _lruList.AddFirst(newNode);
                    _cacheMap[key] = newNode;
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }
        public bool ContainsKey(TKey key)
        {
            _lock.EnterReadLock();
            try
            {
                return _cacheMap.ContainsKey(key);
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        private class CacheItem
        {
            public TKey Key { get; }
            public TValue Value { get; set; }

            public CacheItem(TKey key, TValue value)
            {
                Key = key;
                Value = value;
            }
        }
    }
}

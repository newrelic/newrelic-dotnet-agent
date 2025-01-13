// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Threading;

namespace NewRelic.Agent.Extensions.Caching
{
    /// <summary>
    /// A thread-safe LRU HashSet implementation.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class LRUHashSet<T>
    {
        private readonly int _capacity;
        private readonly HashSet<T> _hashSet;
        private readonly LinkedList<T> _lruList;
        private readonly ReaderWriterLockSlim _lock = new();

        public LRUHashSet(int capacity)
        {
            if (capacity <= 0)
            {
                throw new ArgumentException("Capacity must be greater than zero.", nameof(capacity));
            }

            _capacity = capacity;
            _hashSet = new HashSet<T>();
            _lruList = new LinkedList<T>();
        }

        public bool Add(T item)
        {
            _lock.EnterWriteLock();
            try
            {
                if (_hashSet.Contains(item))
                {
                    // Move the accessed item to the front of the list
                    _lruList.Remove(item);
                    _lruList.AddFirst(item);
                    return false;
                }
                else
                {
                    if (_hashSet.Count >= _capacity)
                    {
                        // Remove the least recently used item
                        var lruItem = _lruList.Last.Value;
                        _hashSet.Remove(lruItem);
                        _lruList.RemoveLast();
                    }

                    // Add the new item to the set and list
                    _hashSet.Add(item);
                    _lruList.AddFirst(item);
                    return true;
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public bool Contains(T item)
        {
            _lock.EnterReadLock();
            try
            {
                return _hashSet.Contains(item);
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        public bool Remove(T item)
        {
            _lock.EnterWriteLock();
            try
            {
                if (_hashSet.Remove(item))
                {
                    _lruList.Remove(item);
                    return true;
                }
                return false;
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public int Count
        {
            get
            {
                _lock.EnterReadLock();
                try
                {
                    return _hashSet.Count;
                }
                finally
                {
                    _lock.ExitReadLock();
                }
            }
        }
    }
}

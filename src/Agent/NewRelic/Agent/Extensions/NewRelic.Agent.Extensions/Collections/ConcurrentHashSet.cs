// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using NewRelic.Agent.Extensions.SystemExtensions.Threading;

namespace NewRelic.Agent.Extensions.Collections
{
    public class ConcurrentHashSet<T> : ICollection<T>
    {
        private readonly HashSet<T> _hashSet = new HashSet<T>();

        private readonly Func<IDisposable> _readLock;

        private readonly Func<IDisposable> _writeLock;

        public ConcurrentHashSet()
        {
            var theLock = new ReaderWriterLockSlim();
            _readLock = theLock.ReusableDisposableReadLock();
            _writeLock = theLock.ReusableDisposableWriteLock();
        }

        public IEnumerator<T> GetEnumerator()
        {
            using (_readLock())
            {
                return new HashSet<T>(_hashSet).GetEnumerator();
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            using (_readLock())
            {
                return new HashSet<T>(_hashSet).GetEnumerator();
            }
        }

        public void Add(T item)
        {
            using (_writeLock())
            {
                _hashSet.Add(item);
            }
        }
        public bool TryAdd(T item)
        {
            using (_writeLock())
            {
                return _hashSet.Add(item);
            }
        }
        public void Clear()
        {
            using (_writeLock())
            {
                _hashSet.Clear();
            }
        }

        public bool Contains(T item)
        {
            using (_readLock())
            {
                return _hashSet.Contains(item);
            }
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            using (_readLock())
            {
                _hashSet.CopyTo(array, arrayIndex);
            }
        }

        public bool Remove(T item)
        {
            using (_writeLock())
            {
                return _hashSet.Remove(item);
            }
        }

        public int Count
        {
            get
            {
                using (_readLock())
                {
                    return _hashSet.Count;
                }
            }
        }

        public bool IsReadOnly
        {
            get
            {
                using (_readLock())
                {
                    return ((ICollection<T>)_hashSet).IsReadOnly;
                }
            }
        }
    }
}

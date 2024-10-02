// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using NewRelic.Agent.Extensions.SystemExtensions.Threading;

namespace NewRelic.Agent.Extensions.Collections
{
    public class ConcurrentList<T> : IList<T>
    {
        private readonly IList<T> _list = new List<T>();

        private readonly Func<IDisposable> _readLock;

        private readonly Func<IDisposable> _writeLock;

        public ConcurrentList()
        {
            var theLock = new ReaderWriterLockSlim();
            _readLock = theLock.ReusableDisposableReadLock();
            _writeLock = theLock.ReusableDisposableWriteLock();
        }

        public ConcurrentList(IEnumerable<T> enumerable)
        {
            if (enumerable == null)
                return;

            _list = new List<T>(enumerable);
        }

        public IEnumerator<T> GetEnumerator()
        {
            using (_readLock())
            {
                return new List<T>(_list).GetEnumerator();
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            using (_readLock())
            {
                return new List<T>(_list).GetEnumerator();
            }
        }

        public void Add(T item)
        {
            using (_writeLock())
            {
                _list.Add(item);
            }
        }

        /// <summary>
        /// Adds an item to the list and returns its index.
        /// </summary>
        /// <param name="item"></param>
        /// <returns>The index of the item</returns>
        public int AddAndReturnIndex(T item)
        {
            using (_writeLock())
            {
                var id = _list.Count;
                _list.Add(item);
                return id;
            }
        }

        public void Clear()
        {
            using (_writeLock())
            {
                _list.Clear();
            }
        }

        public bool Contains(T item)
        {
            using (_readLock())
            {
                return _list.Contains(item);
            }
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            using (_readLock())
            {
                _list.CopyTo(array, arrayIndex);
            }
        }

        public bool Remove(T item)
        {
            using (_writeLock())
            {
                return _list.Remove(item);
            }
        }

        public int Count
        {
            get
            {
                using (_readLock())
                {
                    return _list.Count;
                }
            }
        }

        public bool IsReadOnly
        {
            get
            {
                using (_readLock())
                {
                    return _list.IsReadOnly;
                }
            }
        }

        public int IndexOf(T item)
        {
            using (_readLock())
            {
                return _list.IndexOf(item);
            }
        }

        public void Insert(int index, T item)
        {
            using (_writeLock())
            {
                _list.Insert(index, item);
            }
        }

        public void RemoveAt(int index)
        {
            using (_writeLock())
            {
                _list.RemoveAt(index);
            }
        }

        public T this[int index]
        {
            get
            {
                using (_readLock())
                {
                    return _list[index];
                }
            }
            set
            {
                using (_writeLock())
                {
                    _list[index] = value;
                }
            }
        }
    }
}

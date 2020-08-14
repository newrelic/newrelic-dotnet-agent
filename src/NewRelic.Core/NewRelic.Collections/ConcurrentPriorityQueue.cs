// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace NewRelic.Collections
{
    public class ConcurrentPriorityQueue<T> : IResizableCappedCollection<T>
    {
        private readonly object _syncroot = new object();

        //count of number of item adds that have been attempted since ctor/Clear/Set
        private int _addsAttempted;

        private readonly SortedSet<T> _sortedSet;

        public int Size { get; private set; }

        public int Count { get { lock (_syncroot) return _sortedSet.Count; } }

        public bool IsReadOnly => false;

        #region Constructors
        public ConcurrentPriorityQueue(int capacity)
        {
            _sortedSet = new SortedSet<T>();
            Size = capacity;
        }

        public ConcurrentPriorityQueue(int capacity, IComparer<T> comparer)
        {
            _sortedSet = new SortedSet<T>(comparer);
            Size = capacity;
        }
        #endregion Constructors

        public int Add(IEnumerable<T> items)
        {
            var itemsAdded = 0;
            if (Size > 0)
            {
                lock (_syncroot)
                {
                    foreach (var item in items)
                    {
                        if (AddInternal(item))
                        {
                            ++itemsAdded;
                        }
                    }
                }
            }
            return itemsAdded;
        }

        public bool Add(T item)
        {
            if (Size > 0)
            {
                lock (_syncroot)
                {
                    return AddInternal(item);
                }
            }
            return false;
        }

        private bool AddInternal(T item)
        {
            ++_addsAttempted;

            if (_sortedSet.Add(item))
            {
                RemoveItemsFromBottomOfSortedSet();
                return true;
            }

            return false;
        }

        void ICollection<T>.Add(T item)
        {
            Add(item);
        }

        IEnumerator<T> IEnumerable<T>.GetEnumerator()
        {
            List<T> duplicate;
            //SortedSet.GetEnumerator duplicates the set into the enumerator, so lock to prevent race while they duplicate
            lock (_syncroot)
            {
                duplicate = _sortedSet.ToList();
            }
            return duplicate.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            List<T> duplicate;
            //SortedSet.GetEnumerator duplicates the set into the enumerator, so lock to prevent race while they duplicate
            lock (_syncroot)
            {
                duplicate = _sortedSet.ToList();
            }
            return duplicate.GetEnumerator();
        }

        //Assume lock on the _syncroot is held.
        private void Reset()
        {
            _addsAttempted = 0;
            _sortedSet.Clear();
        }

        //Assume lock on the _syncroot is held.
        private void RemoveItemsFromBottomOfSortedSet()
        {
            if (Size == 0)
            {
                Reset();
                return;
            }

            while (_sortedSet.Count > Size)
            {
                //_sortedSet.Max is the bottom item in the list (lowest priority).
                _sortedSet.Remove(_sortedSet.Max);
            }
        }

        public void Resize(int newSize)
        {
            lock (_syncroot)
            {
                Size = newSize;
                RemoveItemsFromBottomOfSortedSet();
            }
        }

        public int GetAddAttemptsCount()
        {
            return Volatile.Read(ref _addsAttempted);
        }

        public void Clear()
        {
            lock (_syncroot)
            {
                Reset();
            }
        }

        public bool Contains(T item)
        {
            lock (_syncroot)
            {
                return _sortedSet.Contains(item);
            }
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            lock (_syncroot)
            {
                _sortedSet.CopyTo(array, arrayIndex);
            }
        }

        public bool Remove(T item)
        {
            throw new NotSupportedException();
        }
    }
}

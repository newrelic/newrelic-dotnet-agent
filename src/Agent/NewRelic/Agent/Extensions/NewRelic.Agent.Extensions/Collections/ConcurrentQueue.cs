// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using NewRelic.Agent.Extensions.SystemExtensions.Threading;

namespace NewRelic.Agent.Extensions.Collections
{
    public class ConcurrentQueue<T> : IEnumerable<T>, ICollection
    {
        private readonly Queue<T> _queue = new Queue<T>();

        private readonly Func<IDisposable> _readLock;

        private readonly Func<IDisposable> _writeLock;

        public ConcurrentQueue()
        {
            var theLock = new ReaderWriterLockSlim();
            _readLock = theLock.ReusableDisposableReadLock();
            _writeLock = theLock.ReusableDisposableWriteLock();
        }

        public int Count
        {
            get
            {
                using (_readLock())
                {
                    return _queue.Count;
                }
            }
        }

        public void Clear()
        {
            using (_writeLock())
            {
                _queue.Clear();
            }
        }

        public bool Contains(T item)
        {
            using (_readLock())
            {
                return _queue.Contains(item);
            }
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            using (_readLock())
            {
                _queue.CopyTo(array, arrayIndex);
            }
        }

        public T Dequeue()
        {
            using (_writeLock())
            {
                return _queue.Dequeue();
            }
        }

        public T DequeueOrDefault()
        {
            using (_writeLock())
            {
                if (_queue.Count < 1)
                    return default(T);

                return _queue.Dequeue();
            }
        }

        public void Enqueue(T item)
        {
            using (_writeLock())
            {
                _queue.Enqueue(item);
            }
        }

        public T Peek()
        {
            using (_readLock())
            {
                return _queue.Peek();
            }
        }

        public T[] ToArray()
        {
            using (_readLock())
            {
                return _queue.ToArray();
            }
        }

        public void TrimExcess()
        {
            using (_writeLock())
            {
                _queue.TrimExcess();
            }
        }

        public IEnumerator<T> GetEnumerator()
        {
            using (_readLock())
            {
                return new List<T>(_queue).GetEnumerator();
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            using (_readLock())
            {
                return new List<T>(_queue).GetEnumerator();
            }
        }

        #region ICollection

        public void CopyTo(Array array, int index)
        {
            using (_readLock())
            {
                ((ICollection)_queue).CopyTo(array, index);
            }
        }

        public object SyncRoot
        {
            get
            {
                using (_readLock())
                {
                    return ((ICollection)_queue).SyncRoot;
                }
            }
        }

        public bool IsSynchronized
        {
            get
            {
                using (_readLock())
                {
                    return ((ICollection)_queue).IsSynchronized;
                }
            }
        }

        #endregion ICollection
    }
}

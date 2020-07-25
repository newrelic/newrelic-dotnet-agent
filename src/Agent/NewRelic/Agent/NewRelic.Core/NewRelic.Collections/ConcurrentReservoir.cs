using System;
using System.Collections;
using System.Collections.Generic;
using NewRelic.Collections;
using NewRelic.SystemExtensions;

namespace NewRelic.Collections
{
    public class ConcurrentReservoir<T> : IResizableCappedCollection<T>
    {
        private IList<T> _list = new ConcurrentList<T>();
        private readonly Random _random = new Random();
        public UInt32 Size { get; private set; }
        // technically we could run into race conditions with addCount but it is used for probabalistic calculations so if it is off by a bit it doesn't matter
        private UInt64 _addCount;

        public int Count { get { return _list.Count; } }

        public bool IsReadOnly { get { return false; } }

        /// <summary>
        /// Creates a ConcurrentReservoir with the supplied size as the maximum number of items the collection can contain.
        /// </summary>
        /// <param name="size">The maximum numer of items the collection can contain. CONTRACT: size < Int32.Max</param>
        public ConcurrentReservoir(UInt32 size)
        {
            Size = size;
        }

        IEnumerator<T> IEnumerable<T>.GetEnumerator()
        {
            return _list.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _list.GetEnumerator();
        }

        public void Add(T item)
        {
            ++_addCount;

            if (_list.Count < Size)
            {
                _list.Add(item);
            }
            else
            {
                // generate 64 bit random number in range [0, addCount) and insert into list at that location if it is less than _reservoirSize
                var random64 = _random.Next64(_addCount);
                if (random64 >= Size)
                    return;
                var indexToInsertInto = Convert.ToInt32(random64);
                _list[indexToInsertInto] = item;
            }
        }

        public void Clear()
        {
            _addCount = 0;
            _list.Clear();
        }

        public bool Contains(T item)
        {
            return _list.Contains(item);
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            _list.CopyTo(array, arrayIndex);
        }

        // removal from reservoir is not supported
        public bool Remove(T item)
        {
            throw new NotSupportedException();
        }

        public void Resize(UInt32 newSize)
        {
            // There is a small risk of dropping data if an Add occurs on an old reference of _list
            Size = newSize;
            var oldList = _list;
            _list = new ConcurrentList<T>();
            foreach (var item in oldList)
                Add(item);
        }

        public UInt64 GetAddAttemptsCount()
        {
            return _addCount;
        }
    }
}

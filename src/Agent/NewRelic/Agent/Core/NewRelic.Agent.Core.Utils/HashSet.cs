using System;
using System.Collections.Generic;

namespace NewRelic.Agent.Core.Utils
{
    /// <summary>
    /// A HashSet class for .NET 2.0.
    /// </summary>
    [System.SerializableAttribute()]
    public class HashSet<T> : ICollection<T>
    {
        private readonly Object exists = new Object();
        private readonly IDictionary<T, Object> map;

        public HashSet()
        {
            map = new Dictionary<T, Object>();
        }

        public HashSet(ICollection<T> set)
        {
            map = new Dictionary<T, Object>(set.Count);
            foreach (T obj in set)
            {
                Add(obj);
            }
        }

        public HashSet(IEnumerable<T> source)
        {
            map = new Dictionary<T, Object>();
            foreach (var item in source)
            {
                Add(item);
            }
        }

        public HashSet(int initialCapacity)
        {
            map = new Dictionary<T, Object>(initialCapacity);
        }

        public void Add(T item)
        {
            if (!map.ContainsKey(item))
            {
                map.Add(item, exists);
            }
        }

        public void Clear()
        {
            map.Clear();
        }

        public bool Contains(T item)
        {
            return map.ContainsKey(item);
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            this.map.Keys.CopyTo(array, arrayIndex);
        }

        public int Count
        {
            get { return map.Count; }
        }

        public bool IsReadOnly
        {
            get { return map.IsReadOnly; }
        }

        public bool Remove(T item)
        {
            return map.Remove(item);
        }

        public IEnumerator<T> GetEnumerator()
        {
            return map.Keys.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return map.Keys.GetEnumerator();
        }
    }
}

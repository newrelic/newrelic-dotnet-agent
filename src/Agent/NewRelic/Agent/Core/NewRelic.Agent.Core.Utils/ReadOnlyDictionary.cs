/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using System;
using System.Collections.Generic;

namespace NewRelic.Agent.Core
{
    public class ReadOnlyDictionary<TKey, TValue> : IDictionary<TKey, TValue>
    {
        private readonly IDictionary<TKey, TValue> dict;

        public ReadOnlyDictionary(IDictionary<TKey, TValue> dict)
        {
            this.dict = dict;
        }

        public void Add(TKey key, TValue value)
        {
            throw new InvalidOperationException("Read only dictionary");
        }

        public bool ContainsKey(TKey key)
        {
            return dict.ContainsKey(key);
        }

        public bool Remove(TKey key)
        {
            throw new InvalidOperationException("Read only dictionary");
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            return dict.TryGetValue(key, out value);
        }

        public TValue this[TKey key]
        {
            get
            {
                return dict[key];
            }
            set
            {
                throw new InvalidOperationException("Read only dictionary");
            }
        }

        public ICollection<TKey> Keys
        {
            get
            {
                return dict.Keys;
            }
        }

        public ICollection<TValue> Values
        {
            get
            {
                return dict.Values;
            }
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            return dict.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return dict.GetEnumerator();
        }

        public void Add(KeyValuePair<TKey, TValue> item)
        {
            throw new InvalidOperationException("Read only dictionary");
        }

        public void Clear()
        {
            throw new InvalidOperationException("Read only dictionary");
        }

        public bool Contains(KeyValuePair<TKey, TValue> item)
        {
            return dict.Contains(item);
        }

        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            dict.CopyTo(array, arrayIndex);
        }

        public bool Remove(KeyValuePair<TKey, TValue> item)
        {
            throw new InvalidOperationException("Read only dictionary");
        }

        public int Count
        {
            get
            {
                return dict.Count;
            }
        }

        public bool IsReadOnly
        {
            get
            {
                return true;
            }
        }
    }
}


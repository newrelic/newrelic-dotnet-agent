using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using JetBrains.Annotations;
using NewRelic.SystemExtensions.Threading;

namespace NewRelic.Collections
{
    public class ConcurrentDictionary<TKey, TValue> : IDictionary<TKey, TValue>
    {
        [NotNull]
        private readonly IDictionary<TKey, TValue> _dictionary;
        [NotNull]
        private readonly Func<IDisposable> _readLock;
        [NotNull]
        private readonly Func<IDisposable> _writeLock;

        #region Constructors

        public ConcurrentDictionary() : this(new Dictionary<TKey, TValue>()) { }

        public ConcurrentDictionary(int capacity) : this(new Dictionary<TKey, TValue>(capacity)) { }

        protected ConcurrentDictionary([NotNull] IDictionary<TKey, TValue> dictionary)
        {
            _dictionary = dictionary;
            var theLock = new ReaderWriterLockSlim();
            _readLock = theLock.ReusableDisposableReadLock();
            _writeLock = theLock.ReusableDisposableWriteLock();
        }

        #endregion

        /// <summary>
        /// If <paramref name="key"/> is in dictionary and its mapped value is not null, returns the mapped value. Otherwise, maps <paramref name="key"/> to the result of <paramref name="getNewValue"/> and returns it.
        /// 
        /// To be clear: if <paramref name="key"/> is mapped to a null value, this method will re-map <paramref name="key"/> to the result of <paramref name="getNewValue"/>.
        /// 
        /// This method will throw if the result of <paramref name="getNewValue"/> is null.
        /// </summary>
        /// <param name="key">The key of the value to get or set.</param>
        /// <param name="getNewValue">A function that will return a <typeparamref name="TValue"/> if one is not found in the dictionary. Must not return null.</param>
        /// <returns>Returns the non-null value mapped to <paramref name="key"/> if exists, otherwise returns the result of <paramref name="getNewValue"/>.</returns>
        /// <exception cref="NullReferenceException">Thrown if <paramref name="getNewValue"/> returns null.</exception>
        [NotNull]
        public TValue GetOrSetValue([NotNull] TKey key, [NotNull] Func<TValue> getNewValue)
        {
            // In the common case, the given key will already be in the dictionary, and we can increase performance by only taking out a read lock.
            using (_readLock())
            {
                TValue value;
                var exists = _dictionary.TryGetValue(key, out value);
                if (exists && value != null)
                    return value;
            }

            // In the less common case, the given key won't already be in the dictionary and we'll need to take out a write lock to write a new value. We still need to check again for an existing value in case another thread managed to insert one between this lock and the last lock.
            using (_writeLock())
            {
                TValue value;
                var exists = _dictionary.TryGetValue(key, out value);
                if (exists && value != null)
                    return value;

                var newValue = getNewValue();
                if (newValue == null)
                    throw new NullReferenceException("newValue");

                _dictionary[key] = newValue;
                return newValue;
            }
        }

        /// <summary>
        /// Adds <paramref name="value"/> to the dictionary under <paramref name="key"/>. If there is no existing value for <paramref name="key"/> then it will be set to <paramref name="value"/>. If there is already a value for <paramref name="key"/>, the new value will be merged with the existing value using <paramref name="mergeFunction"/>.
        /// 
        /// For safety, be sure to account for null values in <paramref name="mergeFunction"/>
        /// </summary>
        /// <param name="key">The key to merge with.</param>
        /// <param name="value">The value to merge.</param>
        /// <param name="mergeFunction">A function that will merge two values (existingValue, newValue) if an existing value is found.</param>
        public void Merge([NotNull] TKey key, TValue value, [NotNull] Func<TValue, TValue, TValue> mergeFunction)
        {
            using (_writeLock())
            {
                if (!_dictionary.ContainsKey(key))
                {
                    _dictionary.Add(key, value);
                    return;
                }

                _dictionary[key] = mergeFunction(_dictionary[key], value);
            }
        }

        #region IDictionary<TKey, TValue> Implementation

        public virtual void Add(TKey key, TValue value)
        {
            using (_writeLock())
            {
                _dictionary.Add(key, value);
            }
        }

        public virtual Boolean ContainsKey(TKey key)
        {
            using (_readLock())
            {
                return _dictionary.ContainsKey(key);
            }
        }

        public virtual ICollection<TKey> Keys
        {
            get
            {
                using (_readLock())
                {
                    return _dictionary.Keys;
                }
            }
        }

        public virtual Boolean Remove(TKey key)
        {
            using (_writeLock())
            {
                return _dictionary.Remove(key);
            }
        }

        public virtual Boolean TryGetValue(TKey key, out TValue value)
        {
            using (_readLock())
            {
                return _dictionary.TryGetValue(key, out value);
            }
        }

        public virtual ICollection<TValue> Values
        {
            get
            {
                using (_readLock())
                {
                    return _dictionary.Values;
                }
            }
        }

        public virtual TValue this[TKey key]
        {
            get
            {
                using (_readLock())
                {
                    return _dictionary[key];
                }
            }
            set
            {
                using (_writeLock())
                {
                    _dictionary[key] = value;
                }
            }
        }

        #endregion

        #region ICollection<TKey, TValue> Implementation

        public virtual void Add(KeyValuePair<TKey, TValue> item)
        {
            using (_writeLock())
            {
                ((ICollection<KeyValuePair<TKey, TValue>>)_dictionary).Add(item);
            }
        }

        public virtual void Clear()
        {
            using (_writeLock())
            {
                ((ICollection<KeyValuePair<TKey, TValue>>)_dictionary).Clear();
            }
        }

        public virtual Boolean Contains(KeyValuePair<TKey, TValue> item)
        {
            using (_readLock())
            {
                return ((ICollection<KeyValuePair<TKey, TValue>>)_dictionary).Contains(item);
            }
        }

        public virtual void CopyTo(KeyValuePair<TKey, TValue>[] array, Int32 arrayIndex)
        {
            using (_readLock())
            {
                ((ICollection<KeyValuePair<TKey, TValue>>)_dictionary).CopyTo(array, arrayIndex);
            }
        }

        public virtual Boolean Remove(KeyValuePair<TKey, TValue> item)
        {
            using (_writeLock())
            {
                return ((ICollection<KeyValuePair<TKey, TValue>>)_dictionary).Remove(item);
            }
        }

        public virtual Int32 Count
        {
            get
            {
                using (_readLock())
                {
                    return ((ICollection<KeyValuePair<TKey, TValue>>)_dictionary).Count;
                }
            }
        }

        public virtual Boolean IsReadOnly
        {
            get
            {
                using (_readLock())
                {
                    return ((ICollection<KeyValuePair<TKey, TValue>>)_dictionary).IsReadOnly;
                }
            }
        }

        #endregion

        #region IEnumerable<KeyValuePair<TKey, TValue>> Implementation

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            using (_readLock())
            {
                return new Dictionary<TKey, TValue>(_dictionary).GetEnumerator();
            }
        }

        #endregion

        #region IEnumerable Implementation

        IEnumerator IEnumerable.GetEnumerator()
        {
            using (_readLock())
            {
                return new Dictionary<TKey, TValue>(_dictionary).GetEnumerator();
            }
        }

        #endregion
    }
}

// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;

namespace NewRelic.Agent.Extensions.Caching
{
    /// <summary>
    /// Creates an object that can be used as a dictionary key, which holds a WeakReference&lt;T&gt;
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class WeakReferenceKey<T> where T : class
    {
        private readonly int _hashCode;
        private WeakReference<T> WeakReference { get; }

        public WeakReferenceKey(T cacheKey)
        {
            WeakReference = new WeakReference<T>(cacheKey);
            _hashCode = cacheKey.GetHashCode(); // store the hashcode since we use it in the Equals method and the object could have been GC'd by the time we need to look for it
        }

        public override bool Equals(object obj)
        {
            if (obj is WeakReferenceKey<T> otherKey)
            {
                return otherKey.GetHashCode() == _hashCode;
            }

            return false;
        }

        public override int GetHashCode()
        {
            return _hashCode;
        }

        /// <summary>
        /// Gets the value from the WeakReference or null if the target has been garbage collected.
        /// </summary>
        public T Value => WeakReference.TryGetTarget(out var target) ? target : null;
    }
}

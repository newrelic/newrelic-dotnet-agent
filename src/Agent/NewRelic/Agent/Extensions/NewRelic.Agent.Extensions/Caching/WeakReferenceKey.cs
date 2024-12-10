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
        public WeakReference<T> WeakReference { get; }

        public WeakReferenceKey(T cacheKey)
        {
            WeakReference = new WeakReference<T>(cacheKey);
        }

        public override bool Equals(object obj)
        {
            if (obj is WeakReferenceKey<T> otherKey)
            {
                if (WeakReference.TryGetTarget(out var thisTarget) &&
                    otherKey.WeakReference.TryGetTarget(out var otherTarget))
                {
                    return ReferenceEquals(thisTarget, otherTarget);
                }
            }

            return false;
        }

        public override int GetHashCode()
        {
            if (WeakReference.TryGetTarget(out var target))
            {
                return target.GetHashCode();
            }

            return 0;
        }

        /// <summary>
        /// Gets the value from the WeakReference or null if the target has been garbage collected.
        /// </summary>
        public T Value => WeakReference.TryGetTarget(out var target) ? target : null;
    }
}

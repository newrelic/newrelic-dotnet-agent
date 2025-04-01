// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Agent.Extensions.Caching;
using NewRelic.Agent.Extensions.Logging;

namespace NewRelic.Agent.Core.Database
{
    public class CacheByDatastoreVendor<TKey, TValue> where TValue : class
    {
        // The capacity defaults to 1000 but can be configured using the SqlStatementCacheMaxSize setting in the local newrelic.config.
        private int _capacity = 1000;
        private readonly SimpleCache<TKey, TValue>[] _caches;

        public CacheByDatastoreVendor(string name)
        {
            var vendors = Enum.GetValues(typeof(DatastoreVendor));
            _caches = new SimpleCache<TKey, TValue>[vendors.Length];
            for (var i = 0; i < vendors.Length; i++)
            {
                _caches[i] = new SimpleCache<TKey, TValue>(Capacity);
            }
        }

        public TValue GetOrAdd(DatastoreVendor vendor, TKey key, Func<TValue> valueFunc)
        {
            return _caches[(int)vendor].GetOrAdd(key, valueFunc);
        }

        public bool TryAdd(DatastoreVendor vendor, TKey key, Func<TValue> valueFunc)
        {
            return _caches[(int)vendor].TryAdd(key, valueFunc);
        }
        public bool Contains(DatastoreVendor vendor, TKey key)
        {
            return _caches[(int)vendor].Contains(key);
        }

        public void SetCapacity(int capacity)
        {
            if (capacity != Capacity)
            {
                var oldCapacity = Capacity;
                Capacity = capacity;
                Log.Info($"The capacity of cache type ({GetType()}) has been modified from {oldCapacity} to {Capacity}. Agent's memory allocation will be affected by this change so use with caution.");
            }
        }

        private int Capacity
        {
            get => _capacity;
            set
            {
                _capacity = value;
                for (var i = 0; i < Enum.GetValues(typeof(DatastoreVendor)).Length; i++)
                {
                    _caches[i].Capacity = value;
                }
            }
        }

        public void Reset()
        {
            for (var i = 0; i < Enum.GetValues(typeof(DatastoreVendor)).Length; i++)
            {
                _caches[i].Reset();
            }
        }
    }
}

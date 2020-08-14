// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Core.Caching;
using NewRelic.Core.Logging;

namespace NewRelic.Agent.Core.Database
{
    public class CacheByDatastoreVendor<TKey, TValue> where TValue : class
    {
        // The capcity defaults to 1000 but can be configured using the SqlStatementCacheMaxSize setting in the local newrelic.config.
        private int _capacity = 1000;
        private readonly SimpleCache<TKey, TValue>[] _caches;

        public CacheByDatastoreVendor(string name, ICacheStatsReporter cacheStatsReporter)
        {
            var vendors = Enum.GetValues(typeof(DatastoreVendor));
            _caches = new SimpleCache<TKey, TValue>[vendors.Length];
            for (var i = 0; i < vendors.Length; i++)
            {
                _caches[i] = new SimpleCache<TKey, TValue>(Capacity);
                cacheStatsReporter.RegisterCache(_caches[i], name, ((DatastoreVendor)i).ToString());
            }
        }

        public TValue GetOrAdd(DatastoreVendor vendor, TKey key, Func<TValue> valueFunc)
        {
            return _caches[(int)vendor].GetOrAdd(key, valueFunc);
        }

        public void SetCapacity(int capacity)
        {
            if (capacity != Capacity)
            {
                var oldCapacity = Capacity;
                Capacity = capacity;
                Log.Info($"The capcity of cache type ({GetType()}) has been modified from {oldCapacity} to {Capacity}. Agent's memory allocation will be affected by this change so use with precaution.");
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
    }
}

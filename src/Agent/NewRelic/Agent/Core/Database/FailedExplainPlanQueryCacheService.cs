// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Utilities;

namespace NewRelic.Agent.Core.Database
{
    public interface IFailedExplainPlanQueryCacheService : IDisposable
    {
        public CacheByDatastoreVendor<string, string> FailedExplainPlanQueryCache { get; }
    }

    public class FailedExplainPlanQueryCacheService : ConfigurationBasedService, IFailedExplainPlanQueryCacheService
    {
        private readonly CacheByDatastoreVendor<string, string> _cache;
        private readonly int _defaultCapacity = 1000;

        public FailedExplainPlanQueryCacheService()
        {
            _cache = new CacheByDatastoreVendor<string, string>("FailedExplainPlanQueryCache");
            _cache.SetCapacity(_defaultCapacity);
        }

        public CacheByDatastoreVendor<string, string> FailedExplainPlanQueryCache => _cache;

        protected override void OnConfigurationUpdated(ConfigurationUpdateSource configurationUpdateSource)
        {
            _cache.SetCapacity(_configuration.FailedExplainPlanQueryCacheCapacity);
        }
    }
}

/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
#include "stdafx.h"
#include "CppUnitTest.h"

#include <functional>

#include "../ContinuousProfiler/namecache.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;
using NewRelic::Profiler::ContinuousProfiler::BoundedLruCache;

namespace NewRelic { namespace Profiler { namespace ContinuousProfiler
{
    TEST_CLASS(BoundedLruCacheTest)
    {
    private:
        using Cache = BoundedLruCache<int, int, std::hash<int>, 3>;

    public:

        // Filling past capacity evicts the least-recently-used entry, not the most-recent.
        TEST_METHOD(put_past_capacity_evicts_least_recently_used)
        {
            Cache cache;
            cache.Put(1, 10);
            cache.Put(2, 20);
            cache.Put(3, 30); // at capacity; LRU order (oldest->newest): 1,2,3

            cache.Put(4, 40); // evicts the oldest, key 1

            Assert::IsNull(cache.Get(1));
            Assert::IsNotNull(cache.Get(2));
            Assert::IsNotNull(cache.Get(3));
            Assert::IsNotNull(cache.Get(4));
        }

        // Get promotes the hit to most-recently-used, so a later insert evicts the SECOND-oldest.
        TEST_METHOD(get_promotes_entry_to_most_recently_used)
        {
            Cache cache;
            cache.Put(1, 10);
            cache.Put(2, 20);
            cache.Put(3, 30); // oldest->newest: 1,2,3

            Assert::IsNotNull(cache.Get(1)); // promote 1 -> MRU; oldest->newest now 2,3,1

            cache.Put(4, 40); // evicts key 2 (now the oldest), NOT key 1

            Assert::IsNull(cache.Get(2));
            Assert::IsNotNull(cache.Get(1));
            Assert::IsNotNull(cache.Get(3));
            Assert::IsNotNull(cache.Get(4));
        }

        // Put on an already-present key keeps the ORIGINAL value (returns it) and evicts nothing.
        TEST_METHOD(put_existing_key_keeps_original_value_and_does_not_evict)
        {
            Cache cache;
            cache.Put(1, 10);
            cache.Put(2, 20);
            cache.Put(3, 30); // at capacity

            const int* stored = cache.Put(1, 999); // key present -> original kept
            Assert::IsNotNull(stored);
            Assert::AreEqual(10, *stored);

            // Nothing was evicted; all three keys remain.
            Assert::IsNotNull(cache.Get(1));
            Assert::IsNotNull(cache.Get(2));
            Assert::IsNotNull(cache.Get(3));
        }

        // clear() empties the cache.
        TEST_METHOD(clear_empties_the_cache)
        {
            Cache cache;
            cache.Put(1, 10);
            cache.Put(2, 20);
            cache.Put(3, 30);

            cache.clear();

            Assert::IsNull(cache.Get(1));
            Assert::IsNull(cache.Get(2));
            Assert::IsNull(cache.Get(3));
        }
    };
}}}

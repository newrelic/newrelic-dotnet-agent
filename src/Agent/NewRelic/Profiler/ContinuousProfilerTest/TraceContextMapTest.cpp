/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
#include "stdafx.h"
#include "CppUnitTest.h"

#include <atomic>
#include <chrono>
#include <cstdint>
#include <thread>
#include <unordered_map>
#include <vector>

#include "../ContinuousProfiler/TraceContextMap.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace NewRelic { namespace Profiler { namespace ContinuousProfiler
{
    TEST_CLASS(TraceContextMapTest)
    {
    private:
        // Replicates the header's private HashOf so the tests can construct keys that deliberately share
        // a home slot (to exercise probing / tombstone reclaim). Must stay in lock-step with the header.
        static size_t HomeSlot(ThreadID key)
        {
            return static_cast<size_t>((static_cast<uint64_t>(key) * 0x9E3779B97F4A7C15ull) >> (64 - 12));
        }

        // Find `n` distinct, valid (nonzero, not all-ones), pointer-aligned ThreadIDs that all hash to the
        // same home slot. Set into a fresh map in order, they occupy home, home+1, ... via linear probing.
        static std::vector<ThreadID> FindKeysSharingHome(size_t n)
        {
            std::unordered_map<size_t, std::vector<ThreadID>> byHome;
            for (uint64_t i = 1; i < 20000000ull; ++i)
            {
                const ThreadID k = static_cast<ThreadID>(i * 16); // 16-aligned like a real CLR ThreadID
                if (k == 0 || k == static_cast<ThreadID>(~static_cast<uint64_t>(0)))
                {
                    continue;
                }
                auto& bucket = byHome[HomeSlot(k)];
                bucket.push_back(k);
                if (bucket.size() == n)
                {
                    return bucket;
                }
            }
            return {};
        }

    public:

        // Set then TryGet round-trips the exact stored values.
        TEST_METHOD(set_then_try_get_returns_stored_values)
        {
            TraceContextMap map;
            const ThreadID id = static_cast<ThreadID>(0x1000);

            map.Set(id, 111, 222, 333);

            TraceContext ctx;
            Assert::IsTrue(map.TryGet(id, ctx));
            Assert::AreEqual(static_cast<int64_t>(111), ctx.TraceIdHigh);
            Assert::AreEqual(static_cast<int64_t>(222), ctx.TraceIdLow);
            Assert::AreEqual(static_cast<int64_t>(333), ctx.SpanId);
        }

        // TryGet on an unknown thread returns false and zeroes the out param.
        TEST_METHOD(try_get_unknown_thread_returns_false_and_zeroes_out)
        {
            TraceContextMap map;

            TraceContext ctx;
            ctx.TraceIdHigh = 7; ctx.TraceIdLow = 8; ctx.SpanId = 9; // pre-dirty to prove it gets zeroed
            Assert::IsFalse(map.TryGet(static_cast<ThreadID>(0x2000), ctx));
            Assert::AreEqual(static_cast<int64_t>(0), ctx.TraceIdHigh);
            Assert::AreEqual(static_cast<int64_t>(0), ctx.TraceIdLow);
            Assert::AreEqual(static_cast<int64_t>(0), ctx.SpanId);
        }

        // Reset clears a thread's context so a later TryGet reports "no context".
        TEST_METHOD(reset_clears_context)
        {
            TraceContextMap map;
            const ThreadID id = static_cast<ThreadID>(0x3000);

            map.Set(id, 1, 2, 3);
            map.Reset(id);

            TraceContext ctx;
            Assert::IsFalse(map.TryGet(id, ctx));
        }

        // A slot freed by Reset can be reclaimed by a DIFFERENT ThreadID that hashes to the same home,
        // exercising tombstone reclaim in FindOrClaimSlot.
        TEST_METHOD(tombstone_freed_slot_is_reclaimed_by_new_key)
        {
            const auto keys = FindKeysSharingHome(2);
            Assert::AreEqual(static_cast<size_t>(2), keys.size());

            TraceContextMap map;
            map.Set(keys[0], 10, 11, 12); // claims the home slot
            map.Reset(keys[0]);           // tombstones the home slot

            map.Set(keys[1], 20, 21, 22); // must reclaim the tombstoned home slot

            TraceContext ctx;
            Assert::IsTrue(map.TryGet(keys[1], ctx));
            Assert::AreEqual(static_cast<int64_t>(20), ctx.TraceIdHigh);
            Assert::AreEqual(static_cast<int64_t>(21), ctx.TraceIdLow);
            Assert::AreEqual(static_cast<int64_t>(22), ctx.SpanId);

            Assert::IsFalse(map.TryGet(keys[0], ctx)); // original key is gone
        }

        // A live key sitting PAST a tombstone in its probe chain is still found: FindSlot must probe
        // across a tombstone rather than treating it as end-of-chain.
        TEST_METHOD(live_key_is_found_across_a_tombstone)
        {
            const auto keys = FindKeysSharingHome(3);
            Assert::AreEqual(static_cast<size_t>(3), keys.size());

            TraceContextMap map;
            map.Set(keys[0], 1, 1, 1); // home
            map.Set(keys[1], 2, 2, 2); // home+1
            map.Set(keys[2], 3, 3, 3); // home+2

            map.Reset(keys[0]); // tombstone the home slot; keys[1]/keys[2] sit past it

            TraceContext ctx;
            Assert::IsTrue(map.TryGet(keys[1], ctx));
            Assert::AreEqual(static_cast<int64_t>(2), ctx.SpanId);
            Assert::IsTrue(map.TryGet(keys[2], ctx));
            Assert::AreEqual(static_cast<int64_t>(3), ctx.SpanId);
        }

        // The reserved sentinel keys (0 and all-ones) are rejected by every operation without crashing.
        TEST_METHOD(sentinel_keys_are_rejected)
        {
            TraceContextMap map;
            const ThreadID emptyKey = static_cast<ThreadID>(0);
            const ThreadID tombstoneKey = static_cast<ThreadID>(~static_cast<uint64_t>(0));

            map.Set(emptyKey, 1, 2, 3);      // no-op
            map.Set(tombstoneKey, 4, 5, 6);  // no-op
            map.Reset(emptyKey);             // no-op, no crash
            map.Reset(tombstoneKey);         // no-op, no crash

            TraceContext ctx;
            Assert::IsFalse(map.TryGet(emptyKey, ctx));
            Assert::IsFalse(map.TryGet(tombstoneKey, ctx));
        }

        // Several distinct threads keep independent contexts with no cross-talk.
        TEST_METHOD(multiple_threads_coexist_without_collision)
        {
            TraceContextMap map;
            const ThreadID a = static_cast<ThreadID>(0x1000);
            const ThreadID b = static_cast<ThreadID>(0x2000);
            const ThreadID c = static_cast<ThreadID>(0x3000);

            map.Set(a, 1, 2, 3);
            map.Set(b, 4, 5, 6);
            map.Set(c, 7, 8, 9);

            TraceContext ctx;
            Assert::IsTrue(map.TryGet(a, ctx));
            Assert::AreEqual(static_cast<int64_t>(3), ctx.SpanId);
            Assert::IsTrue(map.TryGet(b, ctx));
            Assert::AreEqual(static_cast<int64_t>(6), ctx.SpanId);
            Assert::IsTrue(map.TryGet(c, ctx));
            Assert::AreEqual(static_cast<int64_t>(9), ctx.SpanId);
        }

        // Best-effort concurrency smoke test for the seqlock: several writers churn Set/Reset on distinct
        // threads while a reader loops over all of them for a bounded window. We do NOT pierce
        // encapsulation to force a torn read; the seqlock's torn-read handling is argued in the header's
        // own comments and exercised only indirectly here. The reader asserts the OBSERVABLE contract: a
        // successful TryGet never returns a mix of fields (each writer stores hi==lo==span==marker, so any
        // value the reader accepts must have all three equal), and nothing crashes or hangs.
        TEST_METHOD(concurrent_set_reset_get_stays_consistent)
        {
            TraceContextMap map;
            std::atomic<bool> stop{ false };
            std::atomic<bool> inconsistent{ false };

            std::vector<ThreadID> ids;
            for (int t = 0; t < 4; ++t)
            {
                ids.push_back(static_cast<ThreadID>((t + 1) * 0x1000));
            }

            std::vector<std::thread> threads;
            for (int t = 0; t < 4; ++t)
            {
                const ThreadID id = ids[t];
                const int64_t marker = static_cast<int64_t>((t + 1) * 0x111111);
                threads.emplace_back([&map, &stop, id, marker]
                {
                    while (!stop.load(std::memory_order_relaxed))
                    {
                        map.Set(id, marker, marker, marker);
                        map.Reset(id);
                    }
                });
            }

            threads.emplace_back([&map, &stop, &inconsistent, &ids]
            {
                while (!stop.load(std::memory_order_relaxed))
                {
                    for (const ThreadID id : ids)
                    {
                        TraceContext ctx;
                        if (map.TryGet(id, ctx))
                        {
                            if (ctx.TraceIdHigh != ctx.TraceIdLow || ctx.TraceIdLow != ctx.SpanId)
                            {
                                inconsistent.store(true, std::memory_order_relaxed);
                            }
                        }
                    }
                }
            });

            const auto start = std::chrono::steady_clock::now();
            while (std::chrono::steady_clock::now() - start < std::chrono::milliseconds(80))
            {
                std::this_thread::yield();
            }
            stop.store(true, std::memory_order_relaxed);
            for (auto& th : threads)
            {
                th.join();
            }

            Assert::IsFalse(inconsistent.load(std::memory_order_relaxed));
        }
    };
}}}

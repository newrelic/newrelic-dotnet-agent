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
        // Find `n` distinct, valid (nonzero, not all-ones), pointer-aligned ThreadIDs that all hash to the
        // same home slot. Set into a fresh map in order, they occupy home, home+1, ... via linear probing.
        // Hashes via the production TraceContextMap::HashOf (friend access) so these keys track the real
        // slot-selection logic and its SlotBits width instead of a copy that can drift out of sync.
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
                auto& bucket = byHome[TraceContextMap::HashOf(k)];
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

        // A context stored before a generation bump is no longer readable after it, even though its slot is
        // untouched and its seqlock is perfectly settled -- this is what suppresses a previous profiling
        // session's stale links without writing to slots we do not own.
        TEST_METHOD(context_from_a_previous_generation_is_not_readable)
        {
            TraceContextMap map;
            const ThreadID id = static_cast<ThreadID>(0x4000);

            map.Set(id, 1, 2, 3);
            TraceContext ctx;
            Assert::IsTrue(map.TryGet(id, ctx));

            map.NewGeneration();

            Assert::IsFalse(map.TryGet(id, ctx));
            Assert::AreEqual(static_cast<int64_t>(0), ctx.TraceIdHigh);
            Assert::AreEqual(static_cast<int64_t>(0), ctx.TraceIdLow);
            Assert::AreEqual(static_cast<int64_t>(0), ctx.SpanId);
        }

        // The write path stamps the CURRENT generation, so a Set after a bump reads back normally -- on the
        // same key whose previous-generation value was just invalidated (proving the slot is reused in place
        // with no cleanup at bump time).
        TEST_METHOD(set_after_a_generation_bump_is_readable)
        {
            TraceContextMap map;
            const ThreadID id = static_cast<ThreadID>(0x5000);

            map.Set(id, 1, 2, 3);
            map.NewGeneration();

            map.Set(id, 44, 55, 66);

            TraceContext ctx;
            Assert::IsTrue(map.TryGet(id, ctx));
            Assert::AreEqual(static_cast<int64_t>(44), ctx.TraceIdHigh);
            Assert::AreEqual(static_cast<int64_t>(55), ctx.TraceIdLow);
            Assert::AreEqual(static_cast<int64_t>(66), ctx.SpanId);
        }

        // Repeated bumps keep invalidating: a context is only ever readable in the generation it was
        // written in, not in any later one.
        TEST_METHOD(each_generation_only_reads_its_own_writes)
        {
            TraceContextMap map;
            const ThreadID id = static_cast<ThreadID>(0x6000);

            for (int64_t generation = 1; generation <= 3; ++generation)
            {
                map.Set(id, generation, generation, generation);

                TraceContext ctx;
                Assert::IsTrue(map.TryGet(id, ctx));
                Assert::AreEqual(generation, ctx.SpanId);

                map.NewGeneration();
                Assert::IsFalse(map.TryGet(id, ctx));
            }
        }

        // Reset still zeroes and frees a slot after a generation bump: the freed slot is reclaimable by a
        // different key hashing to the same home, exactly as within a single generation.
        TEST_METHOD(reset_frees_a_slot_written_in_an_earlier_generation)
        {
            const auto keys = FindKeysSharingHome(2);
            Assert::AreEqual(static_cast<size_t>(2), keys.size());

            TraceContextMap map;
            map.Set(keys[0], 10, 11, 12); // claims the home slot in generation 1

            map.NewGeneration();

            map.Reset(keys[0]);           // tombstones it while its stamp is a previous generation
            map.Set(keys[1], 20, 21, 22); // must reclaim the tombstoned home slot

            TraceContext ctx;
            Assert::IsTrue(map.TryGet(keys[1], ctx));
            Assert::AreEqual(static_cast<int64_t>(22), ctx.SpanId);
            Assert::IsFalse(map.TryGet(keys[0], ctx));
        }

        // A slot left behind by a generation bump (its owner never calls Reset -- the orphaned-reset case
        // this mechanism exists for) is silently reused by a new key that probes onto it, with no
        // per-slot cleanup having happened at bump time.
        TEST_METHOD(stale_slot_is_reused_by_a_new_key_after_a_bump)
        {
            const auto keys = FindKeysSharingHome(2);
            Assert::AreEqual(static_cast<size_t>(2), keys.size());

            TraceContextMap map;
            map.Set(keys[0], 10, 11, 12); // occupies home; never Reset -> stale across the bump

            map.NewGeneration();

            map.Set(keys[1], 20, 21, 22); // probes past the still-occupied home slot

            TraceContext ctx;
            Assert::IsTrue(map.TryGet(keys[1], ctx));
            Assert::AreEqual(static_cast<int64_t>(20), ctx.TraceIdHigh);
            Assert::AreEqual(static_cast<int64_t>(21), ctx.TraceIdLow);
            Assert::AreEqual(static_cast<int64_t>(22), ctx.SpanId);

            Assert::IsFalse(map.TryGet(keys[0], ctx)); // stale entry stays unreadable
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

        // A second Set on the SAME key overwrites the first (last-writer-wins): TraceContextMap is a single
        // value per thread, not a stack. The new triple is readable, the old one is gone, and the seqlock
        // settles even after repeated overwrites -- so TryGet keeps succeeding with the latest value.
        TEST_METHOD(set_on_an_existing_key_overwrites_last_writer_wins)
        {
            TraceContextMap map;
            const ThreadID id = static_cast<ThreadID>(0x7000);

            map.Set(id, 1, 2, 3);
            map.Set(id, 44, 55, 66); // overwrite in place, same slot, same generation

            TraceContext ctx;
            Assert::IsTrue(map.TryGet(id, ctx));
            Assert::AreEqual(static_cast<int64_t>(44), ctx.TraceIdHigh);
            Assert::AreEqual(static_cast<int64_t>(55), ctx.TraceIdLow);
            Assert::AreEqual(static_cast<int64_t>(66), ctx.SpanId);

            // Several more overwrites: the seqlock returns to even each time, so TryGet still succeeds and
            // always yields the most recent write with no torn/mixed fields.
            for (int64_t v = 100; v <= 103; ++v)
            {
                map.Set(id, v, v * 2, v * 3);
                TraceContext latest;
                Assert::IsTrue(map.TryGet(id, latest));
                Assert::AreEqual(v, latest.TraceIdHigh);
                Assert::AreEqual(v * 2, latest.TraceIdLow);
                Assert::AreEqual(v * 3, latest.SpanId);
            }
        }

        // Probe budget exhausted -> FindOrClaimSlot returns nullptr -> Set silently drops (no link for that
        // thread). Fill a full MaxProbes-long chain (all keys sharing one home) so the next distinct key
        // hashing to that home can claim no slot within the budget. That key carries no context, while every
        // key already on the chain remains readable and uncorrupted -- the documented "table full -> silently
        // drop; this thread's samples carry no link" failure mode.
        TEST_METHOD(table_full_drops_a_new_key_without_disturbing_existing_slots)
        {
            const size_t chainLength = TraceContextMap::MaxProbes;
            const auto keys = FindKeysSharingHome(chainLength + 1);
            Assert::AreEqual(chainLength + 1, keys.size());

            TraceContextMap map;

            // Fill the whole probe window: keys[0..MaxProbes-1] land in home..home+(MaxProbes-1).
            for (size_t i = 0; i < chainLength; ++i)
            {
                const int64_t v = static_cast<int64_t>(i + 1);
                map.Set(keys[i], v, v, v);
            }

            // One more distinct key hashing to the same home: 64 probes all hit occupied live slots, no
            // tombstone -> FindOrClaimSlot returns nullptr -> Set is a no-op -> TryGet reports no link.
            map.Set(keys[chainLength], 999, 999, 999);
            TraceContext ctx;
            Assert::IsFalse(map.TryGet(keys[chainLength], ctx));

            // The dropped Set must not have clobbered any of the filled slots.
            for (size_t i = 0; i < chainLength; ++i)
            {
                const int64_t v = static_cast<int64_t>(i + 1);
                TraceContext existing;
                Assert::IsTrue(map.TryGet(keys[i], existing));
                Assert::AreEqual(v, existing.SpanId);
            }

            // Resetting a slot frees room on the chain, so the previously-dropped key can now claim it --
            // proving the drop was purely a capacity condition, not a wedged table.
            map.Reset(keys[0]);
            map.Set(keys[chainLength], 12, 13, 14);
            Assert::IsTrue(map.TryGet(keys[chainLength], ctx));
            Assert::AreEqual(static_cast<int64_t>(14), ctx.SpanId);
        }
    };
}}}

/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
#include "stdafx.h"
#include "CppUnitTest.h"

#include <atomic>
#include <cstdint>
#include <thread>
#include <unordered_map>
#include <vector>

#include "../ContinuousProfiler/PendingPushMap.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace NewRelic { namespace Profiler { namespace ContinuousProfiler
{
    TEST_CLASS(PendingPushMapTest)
    {
    private:
        // Find `n` distinct usable ThreadIDs that all hash to the same home slot, so claiming them forces
        // linear probing through home, home+1, ... . Hashes via the production PendingPushMap::HashOf
        // (friend access) so these keys track the real slot-selection logic rather than a hand-copied hash
        // that silently goes stale -- same pattern as AgentWorkMapTest / TraceContextMapTest.
        static std::vector<ThreadID> FindKeysSharingHome(size_t n)
        {
            std::unordered_map<size_t, std::vector<ThreadID>> byHome;
            for (uint64_t i = 1; i < 40000000; ++i)
            {
                const ThreadID key = static_cast<ThreadID>(i);
                auto& bucket = byHome[PendingPushMap::HashOf(key)];
                bucket.push_back(key);
                if (bucket.size() == n)
                {
                    return bucket;
                }
            }
            return {};
        }

        static size_t SnapshotInto(const PendingPushMap& map, std::vector<int64_t>& out)
        {
            size_t count = 0;
            Assert::IsTrue(map.SnapshotPending(out.data(), out.size(), count));
            return count;
        }

    public:

        // The whole contract managed code depends on: ONE call per thread, then cache the pointer. If the
        // address were not stable, a cached pointer would write into some other thread's cell.
        TEST_METHOD(cell_for_returns_a_stable_address_for_the_same_thread)
        {
            PendingPushMap map;
            Assert::IsTrue(map.EnsureAllocated());

            const ThreadID thread = static_cast<ThreadID>(0x1000);
            int64_t* first = map.CellFor(thread);
            Assert::IsTrue(first != nullptr);

            Assert::IsTrue(first == map.CellFor(thread));
            Assert::IsTrue(first == map.CellFor(thread));
            Assert::AreEqual(static_cast<size_t>(1), map.ClaimedCountForTesting());
        }

        TEST_METHOD(cell_for_returns_distinct_addresses_for_distinct_threads)
        {
            PendingPushMap map;
            Assert::IsTrue(map.EnsureAllocated());

            int64_t* a = map.CellFor(static_cast<ThreadID>(0x1000));
            int64_t* b = map.CellFor(static_cast<ThreadID>(0x2000));
            int64_t* c = map.CellFor(static_cast<ThreadID>(0x3000));

            Assert::IsTrue(a != nullptr && b != nullptr && c != nullptr);
            Assert::IsTrue(a != b);
            Assert::IsTrue(b != c);
            Assert::IsTrue(a != c);
            Assert::AreEqual(static_cast<size_t>(3), map.ClaimedCountForTesting());
        }

        // ...including for keys that share a home slot, where the second must probe past the first rather
        // than being handed the same cell.
        TEST_METHOD(colliding_threads_get_distinct_cells)
        {
            const std::vector<ThreadID> keys = FindKeysSharingHome(3);
            Assert::AreEqual(static_cast<size_t>(3), keys.size());

            PendingPushMap map;
            Assert::IsTrue(map.EnsureAllocated());

            int64_t* a = map.CellFor(keys[0]);
            int64_t* b = map.CellFor(keys[1]);
            int64_t* c = map.CellFor(keys[2]);

            Assert::IsTrue(a != nullptr && b != nullptr && c != nullptr);
            Assert::IsTrue(a != b && b != c && a != c);

            // ...and each key keeps resolving to its OWN cell on a repeat lookup, which is what proves the
            // probe chain is walked rather than the home slot being handed out twice.
            Assert::IsTrue(a == map.CellFor(keys[0]));
            Assert::IsTrue(b == map.CellFor(keys[1]));
            Assert::IsTrue(c == map.CellFor(keys[2]));
        }

        // The mechanism itself: a value written through the handed-out address is what a compaction pass
        // sees. This is the whole reason the map exists.
        TEST_METHOD(a_value_written_through_the_cell_is_visible_in_the_snapshot)
        {
            PendingPushMap map;
            Assert::IsTrue(map.EnsureAllocated());

            int64_t* cell = map.CellFor(static_cast<ThreadID>(0x1000));
            Assert::IsTrue(cell != nullptr);
            *cell = 0x1122334455667788LL;

            std::vector<int64_t> out(16, 0);
            Assert::AreEqual(static_cast<size_t>(1), SnapshotInto(map, out));
            Assert::AreEqual(0x1122334455667788LL, out[0]);
        }

        // Zero is "no push in flight", and every claimed-but-idle thread sits at zero. Reporting those
        // would pin an arbitrary registry entry (the entry whose span happens to be 0 is impossible, but a
        // zero in the live set is pure noise) -- and, more importantly, the snapshot buffer is sized for
        // real in-flight pushes, not for every live thread.
        TEST_METHOD(zero_cells_are_omitted_from_the_snapshot)
        {
            PendingPushMap map;
            Assert::IsTrue(map.EnsureAllocated());

            map.CellFor(static_cast<ThreadID>(0x1000));                    // claimed, idle
            int64_t* busy = map.CellFor(static_cast<ThreadID>(0x2000));    // claimed, pushing
            map.CellFor(static_cast<ThreadID>(0x3000));                    // claimed, idle
            Assert::IsTrue(busy != nullptr);
            *busy = 555;

            std::vector<int64_t> out(16, 0);
            Assert::AreEqual(static_cast<size_t>(1), SnapshotInto(map, out));
            Assert::AreEqual(static_cast<int64_t>(555), out[0]);
        }

        TEST_METHOD(every_pending_value_is_reported)
        {
            PendingPushMap map;
            Assert::IsTrue(map.EnsureAllocated());

            for (uint64_t i = 1; i <= 5; ++i)
            {
                int64_t* cell = map.CellFor(static_cast<ThreadID>(i * 0x1000));
                Assert::IsTrue(cell != nullptr);
                *cell = static_cast<int64_t>(i * 100);
            }

            std::vector<int64_t> out(16, 0);
            const size_t count = SnapshotInto(map, out);
            Assert::AreEqual(static_cast<size_t>(5), count);

            int64_t sum = 0;
            for (size_t i = 0; i < count; ++i)
            {
                sum += out[i];
            }
            Assert::AreEqual(static_cast<int64_t>(100 + 200 + 300 + 400 + 500), sum);
        }

        // Forget is the ONLY release path, and it runs from ThreadDestroyed -- after the owning thread has
        // exited. Afterwards the cell contributes nothing to the live set, so any registry entry it was
        // pinning becomes reclaimable again.
        TEST_METHOD(forget_clears_the_cell_and_frees_the_slot)
        {
            PendingPushMap map;
            Assert::IsTrue(map.EnsureAllocated());

            const ThreadID thread = static_cast<ThreadID>(0x1000);
            int64_t* cell = map.CellFor(thread);
            Assert::IsTrue(cell != nullptr);
            *cell = 555;

            map.Forget(thread);

            std::vector<int64_t> out(16, 0);
            Assert::AreEqual(static_cast<size_t>(0), SnapshotInto(map, out));
            Assert::AreEqual(static_cast<size_t>(0), map.ClaimedCountForTesting());
        }

        // A forgotten slot must be REUSABLE, or the fixed table would fill permanently over a long-running
        // process's thread-pool churn. The reused slot must also come back ZEROED: a recycled slot that
        // still held the dead thread's last in-flight span would pin that span's registry entry for the
        // life of the new thread.
        TEST_METHOD(a_forgotten_slot_is_reused_by_a_different_thread_and_comes_back_zeroed)
        {
            const std::vector<ThreadID> keys = FindKeysSharingHome(2);
            Assert::AreEqual(static_cast<size_t>(2), keys.size());

            PendingPushMap map;
            Assert::IsTrue(map.EnsureAllocated());

            int64_t* first = map.CellFor(keys[0]);
            Assert::IsTrue(first != nullptr);
            *first = 555;
            map.Forget(keys[0]);

            // keys[1] shares keys[0]'s home slot, so it reclaims exactly that tombstoned slot.
            int64_t* second = map.CellFor(keys[1]);
            Assert::IsTrue(second == first);
            Assert::AreEqual(static_cast<int64_t>(0), *second);

            std::vector<int64_t> out(16, 0);
            Assert::AreEqual(static_cast<size_t>(0), SnapshotInto(map, out));
        }

        // A repeat CellFor for a thread that already owns its slot must NOT zero the cell: managed code
        // calls this once, but if it ever called it again while a push was in flight, clobbering the cell
        // would erase the very intent the mechanism exists to publish.
        TEST_METHOD(a_repeat_cell_for_does_not_clobber_a_pending_value)
        {
            PendingPushMap map;
            Assert::IsTrue(map.EnsureAllocated());

            const ThreadID thread = static_cast<ThreadID>(0x1000);
            int64_t* cell = map.CellFor(thread);
            Assert::IsTrue(cell != nullptr);
            *cell = 777;

            Assert::IsTrue(cell == map.CellFor(thread));
            Assert::AreEqual(static_cast<int64_t>(777), *cell);
        }

        TEST_METHOD(forgetting_a_thread_that_never_had_a_cell_is_a_no_op)
        {
            PendingPushMap map;
            Assert::IsTrue(map.EnsureAllocated());

            map.CellFor(static_cast<ThreadID>(0x1000));

            map.Forget(static_cast<ThreadID>(0x9999)); // must not crash, must not free someone else's slot

            Assert::AreEqual(static_cast<size_t>(1), map.ClaimedCountForTesting());
        }

        // The read path must be safe in a process that never started continuous profiling, exactly like
        // RetiredSpanRegistry::Contains. An unallocated table is an EMPTY live half, not a failure -- if it
        // reported failure, every compaction pass would abort and the registry would never reclaim.
        TEST_METHOD(an_unallocated_map_hands_out_no_cell_and_snapshots_empty_successfully)
        {
            PendingPushMap map;

            Assert::IsTrue(map.CellFor(static_cast<ThreadID>(0x1000)) == nullptr);

            std::vector<int64_t> out(16, 0);
            size_t count = 123;
            Assert::IsTrue(map.SnapshotPending(out.data(), out.size(), count));
            Assert::AreEqual(static_cast<size_t>(0), count);
        }

        TEST_METHOD(forget_on_an_unallocated_map_is_a_no_op)
        {
            PendingPushMap map;

            map.Forget(static_cast<ThreadID>(0x1000)); // must not crash

            Assert::AreEqual(static_cast<size_t>(0), map.ClaimedCountForTesting());
        }

        TEST_METHOD(ensure_allocated_is_idempotent_and_preserves_cells)
        {
            PendingPushMap map;
            Assert::IsTrue(map.EnsureAllocated());

            const ThreadID thread = static_cast<ThreadID>(0x1000);
            int64_t* cell = map.CellFor(thread);
            Assert::IsTrue(cell != nullptr);
            *cell = 555;

            Assert::IsTrue(map.EnsureAllocated());

            Assert::IsTrue(cell == map.CellFor(thread));
            Assert::AreEqual(static_cast<int64_t>(555), *cell);
        }

        // A buffer that cannot hold every pending value must FAIL the snapshot, not silently truncate: a
        // truncated live set would let compaction reclaim an entry a thread is about to publish.
        TEST_METHOD(a_buffer_too_small_fails_the_snapshot)
        {
            PendingPushMap map;
            Assert::IsTrue(map.EnsureAllocated());

            for (uint64_t i = 1; i <= 3; ++i)
            {
                int64_t* cell = map.CellFor(static_cast<ThreadID>(i * 0x1000));
                Assert::IsTrue(cell != nullptr);
                *cell = static_cast<int64_t>(i * 100);
            }

            std::vector<int64_t> tooSmall(2, 0);
            size_t count = 0;
            Assert::IsFalse(map.SnapshotPending(tooSmall.data(), tooSmall.size(), count));
        }

        // A zero-capacity buffer still succeeds when nothing is pending -- the common steady state, and the
        // one case where "cannot hold everything" and "nothing to hold" must not be confused.
        TEST_METHOD(a_zero_capacity_buffer_succeeds_when_nothing_is_pending)
        {
            PendingPushMap map;
            Assert::IsTrue(map.EnsureAllocated());
            map.CellFor(static_cast<ThreadID>(0x1000)); // claimed but idle

            std::vector<int64_t> buffer(1, 0);
            size_t count = 0;
            Assert::IsTrue(map.SnapshotPending(buffer.data(), 0, count));
            Assert::AreEqual(static_cast<size_t>(0), count);
        }

        TEST_METHOD(a_null_buffer_fails_the_snapshot)
        {
            PendingPushMap map;
            Assert::IsTrue(map.EnsureAllocated());

            size_t count = 123;
            Assert::IsFalse(map.SnapshotPending(nullptr, 16, count));
            Assert::AreEqual(static_cast<size_t>(0), count);
        }

        // 0 and all-ones are slot sentinels, never real CLR ThreadIDs. CurrentManagedThreadId returns 0
        // when the CLR call fails, so this path is genuinely reachable.
        TEST_METHOD(sentinel_thread_ids_get_no_cell)
        {
            PendingPushMap map;
            Assert::IsTrue(map.EnsureAllocated());

            Assert::IsTrue(map.CellFor(static_cast<ThreadID>(0)) == nullptr);
            Assert::IsTrue(map.CellFor(static_cast<ThreadID>(~static_cast<uint64_t>(0))) == nullptr);
            Assert::AreEqual(static_cast<size_t>(0), map.ClaimedCountForTesting());
        }

        // Probe-budget exhaustion degrades to "no cell" -- the caller just pushes without publishing its
        // intent, which is the pre-cell behaviour -- and never to a stall or a shared cell.
        TEST_METHOD(probe_budget_exhaustion_hands_out_no_cell)
        {
            const size_t probes = PendingPushMap::MaxProbes; // copy: never ODR-use an in-class constant.
            const std::vector<ThreadID> keys = FindKeysSharingHome(probes + 1);
            Assert::AreEqual(probes + 1, keys.size());

            PendingPushMap map;
            Assert::IsTrue(map.EnsureAllocated());

            for (size_t i = 0; i < probes; ++i)
            {
                Assert::IsTrue(map.CellFor(keys[i]) != nullptr);
            }

            Assert::IsTrue(map.CellFor(keys[probes]) == nullptr);

            // ...and every cell that was handed out is still that thread's own.
            Assert::AreEqual(probes, map.ClaimedCountForTesting());
        }

        // Concurrent first-time claims must never hand two threads the same cell: a shared cell is a
        // cross-thread write, the exact invariant violation this map is shaped to avoid.
        TEST_METHOD(concurrent_claims_never_share_a_cell)
        {
            const std::vector<ThreadID> keys = FindKeysSharingHome(PendingPushMap::MaxProbes);
            const size_t keyCount = keys.size();
            Assert::IsTrue(keyCount > 0);

            const int threadCount = 4;
            for (int round = 0; round < 25; ++round)
            {
                PendingPushMap map;
                Assert::IsTrue(map.EnsureAllocated());

                std::vector<int64_t*> cells(keyCount, nullptr);
                std::atomic<int> arrived(0);
                std::vector<std::thread> threads;
                for (int t = 0; t < threadCount; ++t)
                {
                    threads.emplace_back([&map, &keys, &cells, &arrived, t, threadCount, keyCount]() {
                        arrived.fetch_add(1, std::memory_order_acq_rel);
                        while (arrived.load(std::memory_order_acquire) < threadCount)
                        {
                            std::this_thread::yield();
                        }

                        for (size_t i = static_cast<size_t>(t); i < keyCount; i += static_cast<size_t>(threadCount))
                        {
                            cells[i] = map.CellFor(keys[i]);
                        }
                    });
                }
                for (auto& thread : threads)
                {
                    thread.join();
                }

                for (size_t i = 0; i < keyCount; ++i)
                {
                    Assert::IsTrue(cells[i] != nullptr);
                    for (size_t j = i + 1; j < keyCount; ++j)
                    {
                        Assert::IsTrue(cells[i] != cells[j]);
                    }
                }
                Assert::AreEqual(keyCount, map.ClaimedCountForTesting());
            }
        }
    };
}}}

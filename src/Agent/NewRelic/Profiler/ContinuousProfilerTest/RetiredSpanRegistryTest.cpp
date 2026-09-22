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

#include "../ContinuousProfiler/RetiredSpanRegistry.h"
#include "../ContinuousProfiler/PendingPushMap.h"
#include "../ContinuousProfiler/TraceContextMap.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace NewRelic { namespace Profiler { namespace ContinuousProfiler
{
    TEST_CLASS(RetiredSpanRegistryTest)
    {
    private:
        // Find `n` distinct nonzero span ids that all hash to the same home slot, so that inserting them
        // into a fresh registry forces linear probing through home, home+1, ... . Hashes via the production
        // RetiredSpanRegistry::HashOf (friend access) so these keys track the real slot-selection logic
        // instead of a hand-copied hash that silently goes stale.
        static std::vector<int64_t> FindKeysSharingHome(size_t n)
        {
            std::unordered_map<size_t, std::vector<int64_t>> byHome;
            for (int64_t i = 1; i < 40000000; ++i)
            {
                auto& bucket = byHome[RetiredSpanRegistry::HashOf(i)];
                bucket.push_back(i);
                if (bucket.size() == n)
                {
                    return bucket;
                }
            }
            return {};
        }

        // Every compaction test needs a pending-push map (it is half the live set), and reclamation now
        // takes TWO consecutive clean passes. These two helpers keep that contract in one place so a future
        // change to the pass count does not have to be chased through every test.
        static void CompactOnce(RetiredSpanRegistry& registry, const TraceContextMap& map, const PendingPushMap& pending)
        {
            registry.CompactAgainst(map, pending);
        }

        // Two consecutive clean passes: the minimum that can reclaim anything.
        static void CompactTwice(RetiredSpanRegistry& registry, const TraceContextMap& map, const PendingPushMap& pending)
        {
            registry.CompactAgainst(map, pending);
            registry.CompactAgainst(map, pending);
        }

    public:

        TEST_METHOD(insert_then_contains_round_trips)
        {
            RetiredSpanRegistry registry;
            Assert::IsTrue(registry.EnsureAllocated());

            Assert::IsTrue(registry.TryInsert(0x1122334455667788LL));

            Assert::IsTrue(registry.Contains(0x1122334455667788LL));
        }

        TEST_METHOD(contains_is_false_for_a_span_never_inserted)
        {
            RetiredSpanRegistry registry;
            Assert::IsTrue(registry.EnsureAllocated());

            registry.TryInsert(111);

            Assert::IsFalse(registry.Contains(222));
        }

        // The whole point of Contains being safe on the suspend-window path in a process that never
        // started CP: an unallocated table must answer false, not dereference null.
        TEST_METHOD(contains_is_false_before_the_table_is_allocated)
        {
            RetiredSpanRegistry registry;

            Assert::IsFalse(registry.Contains(111));
        }

        TEST_METHOD(insert_is_rejected_before_the_table_is_allocated)
        {
            RetiredSpanRegistry registry;

            Assert::IsFalse(registry.TryInsert(111));
        }

        TEST_METHOD(ensure_allocated_is_idempotent_and_preserves_contents)
        {
            RetiredSpanRegistry registry;
            Assert::IsTrue(registry.EnsureAllocated());
            registry.TryInsert(111);

            Assert::IsTrue(registry.EnsureAllocated());

            Assert::IsTrue(registry.Contains(111));
        }

        // Zero is the "no span" sentinel that TraceContextMap already treats as no-link, and it is also
        // the empty-payload value. It must never be insertable or reported as retired, or every
        // no-context slot in the map would read as retired.
        TEST_METHOD(zero_span_id_is_rejected_and_never_contained)
        {
            RetiredSpanRegistry registry;
            Assert::IsTrue(registry.EnsureAllocated());

            Assert::IsFalse(registry.TryInsert(0));

            Assert::IsFalse(registry.Contains(0));
        }

        TEST_METHOD(negative_span_ids_round_trip)
        {
            // Span ids are the bit-exact reinterpretation of 8 random bytes, so the sign bit is live and
            // roughly half of all real span ids are negative.
            RetiredSpanRegistry registry;
            Assert::IsTrue(registry.EnsureAllocated());

            Assert::IsTrue(registry.TryInsert(-1));
            Assert::IsTrue(registry.TryInsert(static_cast<int64_t>(0x8000000000000000ULL)));

            Assert::IsTrue(registry.Contains(-1));
            Assert::IsTrue(registry.Contains(static_cast<int64_t>(0x8000000000000000ULL)));
        }

        TEST_METHOD(inserting_the_same_span_twice_succeeds_and_occupies_one_slot)
        {
            RetiredSpanRegistry registry;
            Assert::IsTrue(registry.EnsureAllocated());

            Assert::IsTrue(registry.TryInsert(111));
            Assert::IsTrue(registry.TryInsert(111));

            Assert::IsTrue(registry.Contains(111));
            Assert::AreEqual(static_cast<size_t>(1), registry.OccupiedCountForTesting());
        }

        // Two keys sharing a home slot must both be independently resolvable: an exact-match compare on
        // the stored span id, never a hash-only match, is what makes a collision harmless.
        TEST_METHOD(colliding_keys_are_both_resolvable_by_exact_match)
        {
            const std::vector<int64_t> keys = FindKeysSharingHome(3);
            Assert::AreEqual(static_cast<size_t>(3), keys.size());

            RetiredSpanRegistry registry;
            Assert::IsTrue(registry.EnsureAllocated());

            for (int64_t key : keys)
            {
                Assert::IsTrue(registry.TryInsert(key));
            }

            for (int64_t key : keys)
            {
                Assert::IsTrue(registry.Contains(key));
            }

            Assert::AreEqual(static_cast<size_t>(3), registry.OccupiedCountForTesting());
        }

        // A key that hashes to an occupied-and-full probe chain must NOT be reported as retired just
        // because a colliding neighbour is.
        TEST_METHOD(a_colliding_neighbour_is_not_reported_as_retired)
        {
            const std::vector<int64_t> keys = FindKeysSharingHome(2);
            Assert::AreEqual(static_cast<size_t>(2), keys.size());

            RetiredSpanRegistry registry;
            Assert::IsTrue(registry.EnsureAllocated());
            Assert::IsTrue(registry.TryInsert(keys[0]));

            Assert::IsFalse(registry.Contains(keys[1]));
        }

        // Overflow must be graceful AND observable: a false return plus a counter bump, never a crash,
        // never a silent success. MaxProbes colliding keys fill one probe window exactly.
        TEST_METHOD(overflow_returns_false_and_is_counted)
        {
            const size_t probes = RetiredSpanRegistry::MaxProbes;
            const std::vector<int64_t> keys = FindKeysSharingHome(probes + 1);
            Assert::AreEqual(probes + 1, keys.size());

            RetiredSpanRegistry registry;
            Assert::IsTrue(registry.EnsureAllocated());

            for (size_t i = 0; i < probes; ++i)
            {
                Assert::IsTrue(registry.TryInsert(keys[i]));
            }

            Assert::AreEqual(static_cast<uint64_t>(0), registry.OverflowCount());

            Assert::IsFalse(registry.TryInsert(keys[probes]));

            Assert::AreEqual(static_cast<uint64_t>(1), registry.OverflowCount());
            // The dropped key must read as NOT retired -- a dropped retirement is a missed rejection,
            // never a wrong acceptance of some other span.
            Assert::IsFalse(registry.Contains(keys[probes]));
            // ...and every key that did get in is untouched.
            for (size_t i = 0; i < probes; ++i)
            {
                Assert::IsTrue(registry.Contains(keys[i]));
            }
        }

        // Regression pin for a lost claim CAS silently dropping a real retirement -- the exact stale-link
        // failure this registry exists to prevent. Every key here shares one home slot, so all inserting
        // threads race for the same probe window and losing a claim CAS is routine rather than incidental.
        // The window is exactly MaxProbes wide and there are exactly MaxProbes distinct keys, so a correct
        // implementation lands all of them with zero overflow: a thread that loses a claim must keep
        // probing inside its budget instead of giving up. All threads are released from a barrier and the
        // burst is repeated, because a single unsynchronized round can serialize and never lose a CAS.
        TEST_METHOD(concurrent_colliding_inserts_all_land_without_overflow)
        {
            const size_t probes = RetiredSpanRegistry::MaxProbes;
            const std::vector<int64_t> keys = FindKeysSharingHome(probes);
            Assert::AreEqual(probes, keys.size());

            const int threadCount = 4;
            for (int round = 0; round < 25; ++round)
            {
                RetiredSpanRegistry registry;
                Assert::IsTrue(registry.EnsureAllocated());

                std::atomic<int> arrived(0);
                std::vector<std::thread> threads;
                for (int t = 0; t < threadCount; ++t)
                {
                    threads.emplace_back([&registry, &keys, &arrived, t, threadCount]() {
                        arrived.fetch_add(1, std::memory_order_acq_rel);
                        while (arrived.load(std::memory_order_acquire) < threadCount)
                        {
                            std::this_thread::yield();
                        }

                        for (size_t i = static_cast<size_t>(t); i < keys.size(); i += static_cast<size_t>(threadCount))
                        {
                            registry.TryInsert(keys[i]);
                        }
                    });
                }
                for (auto& thread : threads)
                {
                    thread.join();
                }

                for (size_t i = 0; i < keys.size(); ++i)
                {
                    Assert::IsTrue(registry.Contains(keys[i]));
                }
                Assert::AreEqual(static_cast<uint64_t>(0), registry.OverflowCount());
                Assert::AreEqual(probes, registry.OccupiedCountForTesting());
            }
        }

        TEST_METHOD(concurrent_inserts_of_distinct_spans_all_land)
        {
            RetiredSpanRegistry registry;
            Assert::IsTrue(registry.EnsureAllocated());

            const int threadCount = 4;
            const int perThread = 500;
            std::vector<std::thread> threads;
            for (int t = 0; t < threadCount; ++t)
            {
                threads.emplace_back([&registry, t, perThread]() {
                    for (int i = 0; i < perThread; ++i)
                    {
                        registry.TryInsert(static_cast<int64_t>(t * 1000000 + i + 1));
                    }
                });
            }
            for (auto& thread : threads)
            {
                thread.join();
            }

            for (int t = 0; t < threadCount; ++t)
            {
                for (int i = 0; i < perThread; ++i)
                {
                    Assert::IsTrue(registry.Contains(static_cast<int64_t>(t * 1000000 + i + 1)));
                }
            }
        }

        // The core guarantee: an entry a live slot still references SURVIVES compaction. This is what
        // makes reclamation proven rather than guessed, and it is the difference between this design and
        // the capacity-pressure eviction the spec rejected.
        TEST_METHOD(compaction_keeps_an_entry_a_live_slot_still_references)
        {
            TraceContextMap map;
            PendingPushMap pending;
            RetiredSpanRegistry registry;
            Assert::IsTrue(registry.EnsureAllocated());

            const ThreadID idleThread = static_cast<ThreadID>(0x1000);
            map.Set(idleThread, 11, 22, 999);   // a thread still parked on span 999
            Assert::IsTrue(registry.TryInsert(999));

            // Two passes, not one: a referenced entry must survive for as many passes as the slot holds it.
            CompactTwice(registry, map, pending);

            Assert::IsTrue(registry.Contains(999));
            Assert::AreEqual(static_cast<size_t>(1), registry.OccupiedCountForTesting());
        }

        // ...and an entry no live slot references is reclaimed, which is what keeps occupancy bounded.
        TEST_METHOD(compaction_reclaims_an_entry_no_live_slot_references)
        {
            TraceContextMap map;
            PendingPushMap pending;
            RetiredSpanRegistry registry;
            Assert::IsTrue(registry.EnsureAllocated());

            Assert::IsTrue(registry.TryInsert(999)); // nobody ever parked on it (or the thread moved on)

            // One pass only MARKS it; the second reclaims. See CompactAgainst's two-pass rule.
            CompactOnce(registry, map, pending);
            Assert::IsTrue(registry.Contains(999));

            CompactOnce(registry, map, pending);

            Assert::IsFalse(registry.Contains(999));
            Assert::AreEqual(static_cast<size_t>(0), registry.OccupiedCountForTesting());
        }

        TEST_METHOD(compaction_keeps_referenced_and_reclaims_unreferenced_across_two_passes)
        {
            TraceContextMap map;
            PendingPushMap pending;
            RetiredSpanRegistry registry;
            Assert::IsTrue(registry.EnsureAllocated());

            map.Set(static_cast<ThreadID>(0x1000), 1, 2, 100);
            map.Set(static_cast<ThreadID>(0x2000), 1, 2, 300);
            Assert::IsTrue(registry.TryInsert(100));
            Assert::IsTrue(registry.TryInsert(200));
            Assert::IsTrue(registry.TryInsert(300));
            Assert::IsTrue(registry.TryInsert(400));

            CompactTwice(registry, map, pending);

            Assert::IsTrue(registry.Contains(100));
            Assert::IsFalse(registry.Contains(200));
            Assert::IsTrue(registry.Contains(300));
            Assert::IsFalse(registry.Contains(400));
            Assert::AreEqual(static_cast<size_t>(2), registry.OccupiedCountForTesting());
        }

        // A slot whose context was Reset no longer references its old span, so that entry becomes
        // reclaimable -- this is the steady-state path that keeps the table from filling.
        TEST_METHOD(compaction_reclaims_an_entry_whose_slot_was_reset)
        {
            TraceContextMap map;
            PendingPushMap pending;
            RetiredSpanRegistry registry;
            Assert::IsTrue(registry.EnsureAllocated());

            const ThreadID thread = static_cast<ThreadID>(0x1000);
            map.Set(thread, 1, 2, 999);
            Assert::IsTrue(registry.TryInsert(999));
            map.Reset(thread);

            CompactTwice(registry, map, pending);

            Assert::IsFalse(registry.Contains(999));
        }

        // An entry inserted between two passes survives the first (it did not exist yet) and the second
        // (a slot references it by then). Renamed from an earlier "..._after_its_horizon": this test does
        // NOT reach the horizon guard, because a single-threaded insert always predates the NEXT pass's
        // horizon, so the entry is eligible at pass 2 and survives purely on the reference check. The
        // guard's actual coverage is compaction_keeps_an_entry_whose_seq_is_not_below_the_horizon below.
        TEST_METHOD(an_entry_inserted_between_passes_survives_until_a_slot_references_it)
        {
            TraceContextMap map;
            PendingPushMap pending;
            RetiredSpanRegistry registry;
            Assert::IsTrue(registry.EnsureAllocated());

            // Pass 1 runs against an empty registry, establishing a horizon.
            CompactOnce(registry, map, pending);

            // This insert is newer than pass 1's horizon and, at pass 2, newer than nothing -- it must
            // survive pass 1 trivially and be eligible only from pass 2 onward.
            Assert::IsTrue(registry.TryInsert(999));
            Assert::IsTrue(registry.Contains(999));

            // A slot picks the value up before the next pass, so pass 2 must keep it too.
            map.Set(static_cast<ThreadID>(0x1000), 1, 2, 999);
            CompactTwice(registry, map, pending);
            Assert::IsTrue(registry.Contains(999));
        }

        // Reclaimed slots must be reusable, or the table would fill permanently despite compaction.
        TEST_METHOD(a_reclaimed_slot_is_reused_by_a_later_insert)
        {
            const std::vector<int64_t> keys = FindKeysSharingHome(RetiredSpanRegistry::MaxProbes + 1);
            Assert::AreEqual(RetiredSpanRegistry::MaxProbes + 1, keys.size());

            TraceContextMap map;
            PendingPushMap pending;
            RetiredSpanRegistry registry;
            Assert::IsTrue(registry.EnsureAllocated());

            const size_t probes = RetiredSpanRegistry::MaxProbes;
            for (size_t i = 0; i < probes; ++i)
            {
                Assert::IsTrue(registry.TryInsert(keys[i]));
            }
            Assert::IsFalse(registry.TryInsert(keys[probes])); // window full

            // No live slot references any of them, so two passes free the whole chain.
            CompactTwice(registry, map, pending);
            Assert::AreEqual(static_cast<size_t>(0), registry.OccupiedCountForTesting());

            Assert::IsTrue(registry.TryInsert(keys[probes]));
            Assert::IsTrue(registry.Contains(keys[probes]));
        }

        // A tombstone must not terminate a probe chain, or reclaiming one entry would hide the live
        // entries behind it and turn a retired span into a missed rejection.
        TEST_METHOD(a_live_entry_is_still_found_past_a_reclaimed_one)
        {
            const std::vector<int64_t> keys = FindKeysSharingHome(2);
            Assert::AreEqual(static_cast<size_t>(2), keys.size());

            TraceContextMap map;
            PendingPushMap pending;
            RetiredSpanRegistry registry;
            Assert::IsTrue(registry.EnsureAllocated());
            Assert::IsTrue(registry.TryInsert(keys[0]));
            Assert::IsTrue(registry.TryInsert(keys[1]));

            // Keep only the SECOND key referenced, so the first (at the home slot) is reclaimed.
            map.Set(static_cast<ThreadID>(0x1000), 1, 2, keys[1]);
            CompactTwice(registry, map, pending);

            Assert::IsFalse(registry.Contains(keys[0]));
            Assert::IsTrue(registry.Contains(keys[1]));
        }

        TEST_METHOD(compaction_on_an_unallocated_registry_is_a_no_op)
        {
            TraceContextMap map;
            PendingPushMap pending;
            RetiredSpanRegistry registry;

            CompactTwice(registry, map, pending); // must not crash

            Assert::AreEqual(static_cast<size_t>(0), registry.OccupiedCountForTesting());
        }

        TEST_METHOD(compaction_ignores_slots_holding_no_span)
        {
            TraceContextMap map;
            PendingPushMap pending;
            RetiredSpanRegistry registry;
            Assert::IsTrue(registry.EnsureAllocated());

            // A slot with a trace but a zero span contributes nothing to the live set.
            map.Set(static_cast<ThreadID>(0x1000), 11, 22, 0);
            Assert::IsTrue(registry.TryInsert(999));

            CompactTwice(registry, map, pending);

            Assert::IsFalse(registry.Contains(999));
        }

        // If the live-slot snapshot cannot complete, the pass must reclaim NOTHING. Verified through the
        // observable counter, since a snapshot failure cannot be forced from outside the map.
        TEST_METHOD(compaction_records_no_aborts_on_a_clean_pass)
        {
            TraceContextMap map;
            PendingPushMap pending;
            RetiredSpanRegistry registry;
            Assert::IsTrue(registry.EnsureAllocated());
            Assert::IsTrue(registry.TryInsert(999));

            // The first pass reclaims nothing (it only marks), so the reclaim total is asserted after two.
            CompactOnce(registry, map, pending);
            Assert::AreEqual(static_cast<uint64_t>(0), registry.ReclaimedCountForTesting());

            CompactOnce(registry, map, pending);

            Assert::AreEqual(static_cast<uint64_t>(0), registry.AbortedCompactionCountForTesting());
            Assert::AreEqual(static_cast<uint64_t>(1), registry.ReclaimedCountForTesting());
        }

        // The insert-between-passes test above cannot actually reach the horizon guard: a single-threaded
        // insert always predates the NEXT pass's horizon, so that test stays green even with the guard
        // deleted. This one constructs the state the guard exists for directly -- an occupied entry whose
        // Seq is not below the pass's horizon, which in production only a retirement racing the snapshot
        // produces -- and asserts it survives even though nothing references it.
        TEST_METHOD(compaction_keeps_an_entry_whose_seq_is_not_below_the_horizon)
        {
            TraceContextMap map;
            PendingPushMap pending;
            RetiredSpanRegistry registry;
            Assert::IsTrue(registry.EnsureAllocated());
            Assert::IsTrue(registry.TryInsert(999));

            // Rewind the insertion counter (friend access) so the pass below captures a horizon at or
            // under the entry's own Seq -- the same relationship a concurrent insert creates.
            registry._insertSeq.store(1, std::memory_order_relaxed);

            CompactTwice(registry, map, pending);

            Assert::IsTrue(registry.Contains(999));
            Assert::AreEqual(static_cast<uint64_t>(0), registry.ReclaimedCountForTesting());
        }

        // ============================ two-pass reclamation ============================
        //
        // An entry is reclaimed only on the SECOND CONSECUTIVE pass that finds it unreferenced and older
        // than that pass's horizon. This narrows -- it does NOT close -- the window in which a thread that
        // has chosen a span id but not yet published it is invisible to a snapshot: the push must now stay
        // unpublished across two whole compaction intervals rather than losing one sub-microsecond race.

        TEST_METHOD(an_unreferenced_entry_survives_one_pass_and_is_reclaimed_on_the_second)
        {
            TraceContextMap map;
            PendingPushMap pending;
            RetiredSpanRegistry registry;
            Assert::IsTrue(registry.EnsureAllocated());
            Assert::IsTrue(registry.TryInsert(999));

            CompactOnce(registry, map, pending);

            // One clean pass MARKS only. Both the observable answer and the occupancy must be unchanged.
            Assert::IsTrue(registry.Contains(999));
            Assert::AreEqual(static_cast<size_t>(1), registry.OccupiedCountForTesting());
            Assert::AreEqual(static_cast<uint64_t>(0), registry.ReclaimedCountForTesting());

            CompactOnce(registry, map, pending);

            Assert::IsFalse(registry.Contains(999));
            Assert::AreEqual(static_cast<size_t>(0), registry.OccupiedCountForTesting());
            Assert::AreEqual(static_cast<uint64_t>(1), registry.ReclaimedCountForTesting());
        }

        // A marked entry must still answer Contains == true. This is the trap the mark is a separate field
        // rather than a fourth State value for: if marking moved the entry out of StateOccupied, a span
        // would stop being reported as retired the moment it was marked -- reinstating exactly the stale
        // link this registry exists to eliminate, one whole compaction interval before reclamation.
        TEST_METHOD(a_marked_entry_is_still_reported_as_retired)
        {
            TraceContextMap map;
            PendingPushMap pending;
            RetiredSpanRegistry registry;
            Assert::IsTrue(registry.EnsureAllocated());
            Assert::IsTrue(registry.TryInsert(999));

            CompactOnce(registry, map, pending); // marks 999

            Assert::IsTrue(registry.Contains(999));
            Assert::AreEqual(static_cast<size_t>(1), registry.OccupiedCountForTesting());
        }

        // ...and a marked entry must not be treated as claimable, or a later insert of a DIFFERENT span
        // would take its slot while it is still live. Same trap, insert side.
        TEST_METHOD(a_marked_entry_does_not_have_its_slot_stolen_by_a_later_insert)
        {
            const std::vector<int64_t> keys = FindKeysSharingHome(2);
            Assert::AreEqual(static_cast<size_t>(2), keys.size());

            TraceContextMap map;
            PendingPushMap pending;
            RetiredSpanRegistry registry;
            Assert::IsTrue(registry.EnsureAllocated());
            Assert::IsTrue(registry.TryInsert(keys[0]));

            CompactOnce(registry, map, pending); // marks keys[0], which sits at the shared home slot

            Assert::IsTrue(registry.TryInsert(keys[1]));

            // Both must be independently resolvable: the marked entry kept its slot and the new one took
            // the next slot in the chain.
            Assert::IsTrue(registry.Contains(keys[0]));
            Assert::IsTrue(registry.Contains(keys[1]));
            Assert::AreEqual(static_cast<size_t>(2), registry.OccupiedCountForTesting());
        }

        // The mark must be cleared the INSTANT a pass sees the entry referenced again, so strikes can never
        // accumulate non-consecutively. Here the entry is marked by pass 1, referenced at pass 2, then
        // unreferenced again at pass 3 -- and pass 3 must only re-MARK it, not reclaim on pass 1's strike.
        TEST_METHOD(an_entry_referenced_again_clears_its_mark_and_is_not_reclaimed_on_the_next_single_pass)
        {
            TraceContextMap map;
            PendingPushMap pending;
            RetiredSpanRegistry registry;
            Assert::IsTrue(registry.EnsureAllocated());
            Assert::IsTrue(registry.TryInsert(999));

            CompactOnce(registry, map, pending); // pass 1: unreferenced -> marked
            Assert::IsTrue(registry.Contains(999));

            const ThreadID thread = static_cast<ThreadID>(0x1000);
            map.Set(thread, 1, 2, 999);
            CompactOnce(registry, map, pending); // pass 2: referenced -> mark cleared
            Assert::IsTrue(registry.Contains(999));

            map.Reset(thread);
            CompactOnce(registry, map, pending); // pass 3: unreferenced -> marked AGAIN, not reclaimed
            Assert::IsTrue(registry.Contains(999));
            Assert::AreEqual(static_cast<uint64_t>(0), registry.ReclaimedCountForTesting());

            CompactOnce(registry, map, pending); // pass 4: second CONSECUTIVE clean pass -> reclaimed
            Assert::IsFalse(registry.Contains(999));
            Assert::AreEqual(static_cast<uint64_t>(1), registry.ReclaimedCountForTesting());
        }

        // A tombstoned slot reclaimed by a NEW span must not inherit the previous occupant's mark, or the
        // new entry would be reclaimed after a single clean pass -- silently reinstating one-pass
        // reclamation for exactly the entries that churn through recycled slots.
        TEST_METHOD(a_reused_slot_does_not_inherit_the_previous_entrys_mark)
        {
            const std::vector<int64_t> keys = FindKeysSharingHome(2);
            Assert::AreEqual(static_cast<size_t>(2), keys.size());

            TraceContextMap map;
            PendingPushMap pending;
            RetiredSpanRegistry registry;
            Assert::IsTrue(registry.EnsureAllocated());

            Assert::IsTrue(registry.TryInsert(keys[0]));
            CompactTwice(registry, map, pending);          // keys[0] marked then reclaimed -> tombstone
            Assert::AreEqual(static_cast<size_t>(0), registry.OccupiedCountForTesting());

            // keys[1] shares keys[0]'s home slot, so it reclaims that tombstone.
            Assert::IsTrue(registry.TryInsert(keys[1]));

            CompactOnce(registry, map, pending);           // must MARK only, not reclaim
            Assert::IsTrue(registry.Contains(keys[1]));
            Assert::AreEqual(static_cast<uint64_t>(1), registry.ReclaimedCountForTesting());

            CompactOnce(registry, map, pending);
            Assert::IsFalse(registry.Contains(keys[1]));
            Assert::AreEqual(static_cast<uint64_t>(2), registry.ReclaimedCountForTesting());
        }

        // ============================ the pending-push cell ============================

        // THE regression test for the mechanism: an entry whose span appears ONLY in a pending cell -- no
        // live TraceContextMap slot holds it, because the pushing thread has not published it yet -- must
        // survive compaction. Without this, the push lands after reclamation and produces a link with no
        // registry entry left to reject it: the original bug, reached through a different door.
        TEST_METHOD(an_entry_referenced_only_by_a_pending_cell_survives_and_is_reclaimed_once_the_cell_clears)
        {
            TraceContextMap map;   // deliberately EMPTY: no slot holds the span
            PendingPushMap pending;
            Assert::IsTrue(pending.EnsureAllocated());
            RetiredSpanRegistry registry;
            Assert::IsTrue(registry.EnsureAllocated());

            Assert::IsTrue(registry.TryInsert(999));

            // A thread announces the push it is about to make, exactly as the managed push path will.
            int64_t* cell = pending.CellFor(static_cast<ThreadID>(0x3000));
            Assert::IsTrue(cell != nullptr);
            *cell = 999;

            CompactTwice(registry, map, pending);

            Assert::IsTrue(registry.Contains(999));
            Assert::AreEqual(static_cast<size_t>(1), registry.OccupiedCountForTesting());
            Assert::AreEqual(static_cast<uint64_t>(0), registry.ReclaimedCountForTesting());

            // The push completed (or was abandoned) and the cell is cleared; now nothing references it.
            *cell = 0;

            CompactTwice(registry, map, pending);

            Assert::IsFalse(registry.Contains(999));
            Assert::AreEqual(static_cast<uint64_t>(1), registry.ReclaimedCountForTesting());
        }

        // The union is ADDITIVE: a pending cell holding one span must not shrink the live set to just that
        // span. If a cell ever REPLACED the slot-based set, the managed side's ReferenceEquals dedupe --
        // which returns before writing any cell -- would leave live slots unprotected.
        TEST_METHOD(a_pending_cell_adds_to_the_live_set_rather_than_replacing_it)
        {
            TraceContextMap map;
            PendingPushMap pending;
            Assert::IsTrue(pending.EnsureAllocated());
            RetiredSpanRegistry registry;
            Assert::IsTrue(registry.EnsureAllocated());

            map.Set(static_cast<ThreadID>(0x1000), 1, 2, 100);  // published
            Assert::IsTrue(registry.TryInsert(100));
            Assert::IsTrue(registry.TryInsert(200));            // pending only
            Assert::IsTrue(registry.TryInsert(300));            // neither

            int64_t* cell = pending.CellFor(static_cast<ThreadID>(0x2000));
            Assert::IsTrue(cell != nullptr);
            *cell = 200;

            CompactTwice(registry, map, pending);

            Assert::IsTrue(registry.Contains(100));
            Assert::IsTrue(registry.Contains(200));
            Assert::IsFalse(registry.Contains(300));
        }

        // An unallocated pending map is not a snapshot failure -- it is an empty live half. Compaction must
        // still reclaim against it, or a process whose pending table failed to allocate would never reclaim
        // anything and would eventually overflow the registry.
        TEST_METHOD(compaction_still_reclaims_when_the_pending_map_was_never_allocated)
        {
            TraceContextMap map;
            PendingPushMap pending; // never EnsureAllocated
            RetiredSpanRegistry registry;
            Assert::IsTrue(registry.EnsureAllocated());
            Assert::IsTrue(registry.TryInsert(999));

            CompactTwice(registry, map, pending);

            Assert::IsFalse(registry.Contains(999));
            Assert::AreEqual(static_cast<uint64_t>(0), registry.AbortedCompactionCountForTesting());
        }
    };
}}}

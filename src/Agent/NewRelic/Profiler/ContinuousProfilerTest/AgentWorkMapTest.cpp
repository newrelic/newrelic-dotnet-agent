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

#include "../ContinuousProfiler/AgentWorkMap.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace NewRelic { namespace Profiler { namespace ContinuousProfiler
{
    TEST_CLASS(AgentWorkMapTest)
    {
    private:
        // Find `n` distinct, valid (nonzero, not all-ones), pointer-aligned ThreadIDs that all hash to the
        // same home slot. Incremented in order into a fresh map they occupy home, home+1, ... via linear
        // probing. Hashes via the production AgentWorkMap::HashOf (friend access) so the keys track the real
        // slot-selection logic and its SlotBits width rather than a copy that can drift out of sync.
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
                auto& bucket = byHome[AgentWorkMap::HashOf(k)];
                bucket.push_back(k);
                if (bucket.size() == n)
                {
                    return bucket;
                }
            }
            return {};
        }

    public:

        // A matched Increment/Decrement pair returns the thread to the untagged (depth 0) state.
        TEST_METHOD(increment_then_decrement_returns_to_untagged)
        {
            AgentWorkMap map;
            const ThreadID id = static_cast<ThreadID>(0x1000);

            Assert::IsFalse(map.IsAgentWork(id)); // never incremented
            map.Increment(id);
            Assert::IsTrue(map.IsAgentWork(id));
            map.Decrement(id);
            Assert::IsFalse(map.IsAgentWork(id));
        }

        // Nesting: two Increments need two Decrements before the thread reads as untagged.
        TEST_METHOD(nested_increments_require_matching_decrements)
        {
            AgentWorkMap map;
            const ThreadID id = static_cast<ThreadID>(0x2000);

            map.Increment(id);
            map.Increment(id);
            map.Decrement(id);
            Assert::IsTrue(map.IsAgentWork(id)); // still nested one level deep
            map.Decrement(id);
            Assert::IsFalse(map.IsAgentWork(id));
        }

        // Decrementing a thread that is already at depth 0 (or was never incremented) is a safe no-op.
        TEST_METHOD(decrement_below_zero_is_a_safe_no_op)
        {
            AgentWorkMap map;
            const ThreadID never = static_cast<ThreadID>(0x3000);
            map.Decrement(never); // never incremented -> no slot -> no-op
            Assert::IsFalse(map.IsAgentWork(never));

            const ThreadID id = static_cast<ThreadID>(0x4000);
            map.Increment(id);
            map.Decrement(id);
            map.Decrement(id); // already at 0 -> must not underflow
            Assert::IsFalse(map.IsAgentWork(id));

            // A slot pinned at 0 must still respond correctly to a fresh Increment.
            map.Increment(id);
            Assert::IsTrue(map.IsAgentWork(id));
        }

        // The reserved empty key (0) is rejected by every operation without crashing.
        TEST_METHOD(empty_key_is_rejected)
        {
            AgentWorkMap map;
            const ThreadID emptyKey = static_cast<ThreadID>(0);

            map.Increment(emptyKey); // no-op
            map.Decrement(emptyKey); // no-op
            Assert::IsFalse(map.IsAgentWork(emptyKey));
        }

        // NewGeneration() un-poisons a slot left stuck at depth >= 1 by an orphaned Increment -- the whole
        // point of calling it from ContinuousProfiler::Start.
        TEST_METHOD(newgeneration_releases_a_slot_stuck_at_nonzero_depth)
        {
            AgentWorkMap map;
            const ThreadID id = static_cast<ThreadID>(0x5000);

            map.Increment(id); // no matching Decrement -> permanently tagged without NewGeneration()
            Assert::IsTrue(map.IsAgentWork(id));

            map.NewGeneration();
            Assert::IsFalse(map.IsAgentWork(id));
        }

        // NewGeneration() retires EVERY slot, not just one, and leaves the map reusable afterwards.
        TEST_METHOD(newgeneration_drops_all_threads_and_leaves_the_map_usable)
        {
            AgentWorkMap map;
            const ThreadID a = static_cast<ThreadID>(0x6000);
            const ThreadID b = static_cast<ThreadID>(0x7000);

            map.Increment(a);
            map.Increment(b);
            map.Increment(b); // nested two deep -> NewGeneration must retire the whole depth, not decrement it
            Assert::IsTrue(map.IsAgentWork(a));
            Assert::IsTrue(map.IsAgentWork(b));

            map.NewGeneration();
            Assert::IsFalse(map.IsAgentWork(a));
            Assert::IsFalse(map.IsAgentWork(b));

            // A retired slot must be reusable, so the next session tracks normally.
            map.Increment(a);
            Assert::IsTrue(map.IsAgentWork(a));
            map.Decrement(a);
            Assert::IsFalse(map.IsAgentWork(a));
        }

        // A Decrement that arrives after NewGeneration() (the orphaned other half of a pre-bump Increment)
        // must clamp at zero rather than underflow, and must not resurrect the retired slot as agent work.
        TEST_METHOD(decrement_after_newgeneration_does_not_underflow)
        {
            AgentWorkMap map;
            const ThreadID id = static_cast<ThreadID>(0x8000);

            map.Increment(id);
            map.NewGeneration();
            map.Decrement(id); // late half of the pre-bump pair
            Assert::IsFalse(map.IsAgentWork(id));

            map.Increment(id);
            Assert::IsTrue(map.IsAgentWork(id));
        }

        // Decrement must gate on the generation stamp the same way Increment/IsAgentWork do: a Decrement
        // that lands on a slot stamped by a RETIRED generation must not touch that slot's raw Depth at all,
        // not merely fail to make the change observable through IsAgentWork (which already gates on
        // generation regardless). Reaches into the slot directly (friend access) because IsAgentWork's own
        // gate would mask an unguarded Decrement here -- this is exactly the latent trap the read-side gate
        // was hiding.
        TEST_METHOD(decrement_on_stale_generation_slot_does_not_mutate_depth)
        {
            AgentWorkMap map;
            const ThreadID id = static_cast<ThreadID>(0xA000);

            map.Increment(id);
            map.Increment(id); // depth 2, stamped generation 1
            map.NewGeneration(); // bumps to generation 2; slot's Gen stamp still reads 1

            AgentWorkMap::Slot* slot = map.FindSlot(id);
            Assert::IsNotNull(slot);
            const uint32_t depthBeforeDecrement = slot->Depth.load(std::memory_order_relaxed);
            Assert::AreEqual(static_cast<uint32_t>(2), depthBeforeDecrement);

            map.Decrement(id); // slot's stamp is stale -> must be a no-op on the raw Depth

            Assert::AreEqual(depthBeforeDecrement, slot->Depth.load(std::memory_order_relaxed));
        }

        // NewGeneration() on a never-used map is a harmless no-op.
        TEST_METHOD(newgeneration_on_an_empty_map_is_a_no_op)
        {
            AgentWorkMap map;
            const ThreadID id = static_cast<ThreadID>(0x9000);

            map.NewGeneration();
            Assert::IsFalse(map.IsAgentWork(id));

            map.Increment(id);
            Assert::IsTrue(map.IsAgentWork(id));
        }

        // A live thread whose slot was retired by NewGeneration re-tags itself on its very next Increment
        // (stale stamp -> adopt path resets Depth to one level in the CURRENT generation), and a single
        // matching Decrement returns it to untagged -- i.e. the adopt reset never double-counts.
        TEST_METHOD(increment_after_newgeneration_restamps_the_current_generation)
        {
            AgentWorkMap map;
            const ThreadID id = static_cast<ThreadID>(0x9500);

            map.Increment(id);
            map.Increment(id); // nested two deep in the previous generation
            map.NewGeneration();
            Assert::IsFalse(map.IsAgentWork(id)); // prior-generation depth retired

            map.Increment(id);                    // adopt: resets to a single fresh level, not 3
            Assert::IsTrue(map.IsAgentWork(id));
            map.Decrement(id);                    // one Decrement is enough -> proves no double-count
            Assert::IsFalse(map.IsAgentWork(id));
        }

        // Distinct threads are tracked independently.
        TEST_METHOD(multiple_threads_tracked_independently)
        {
            AgentWorkMap map;
            const ThreadID a = static_cast<ThreadID>(0x1000);
            const ThreadID b = static_cast<ThreadID>(0x2000);

            map.Increment(a);
            Assert::IsTrue(map.IsAgentWork(a));
            Assert::IsFalse(map.IsAgentWork(b)); // b untouched

            map.Increment(b);
            map.Decrement(a);
            Assert::IsFalse(map.IsAgentWork(a));
            Assert::IsTrue(map.IsAgentWork(b)); // b still tagged
        }

        // Forget() releases a dead thread's slot, exactly as it must when CorProfilerCallbackImpl
        // forwards ThreadDestroyed here: a thread that died holding a nonzero Depth (H4) reads as
        // untagged afterwards instead of poisoning the slot forever.
        TEST_METHOD(forget_releases_a_slot_stuck_at_nonzero_depth)
        {
            AgentWorkMap map;
            const ThreadID id = static_cast<ThreadID>(0xA000);

            map.Increment(id);
            map.Increment(id); // nested two deep -- a dead thread mid-callback
            Assert::IsTrue(map.IsAgentWork(id));

            map.Forget(id);
            Assert::IsFalse(map.IsAgentWork(id));
        }

        // Forget() on a thread with no slot (never incremented) is a safe no-op.
        TEST_METHOD(forget_on_an_unknown_thread_is_a_no_op)
        {
            AgentWorkMap map;
            const ThreadID id = static_cast<ThreadID>(0xB000);

            map.Forget(id); // no-op, no crash
            Assert::IsFalse(map.IsAgentWork(id));
        }

        // The reserved sentinels are rejected by Forget() the same way they are by every other op.
        TEST_METHOD(forget_rejects_reserved_keys)
        {
            AgentWorkMap map;
            map.Forget(static_cast<ThreadID>(0)); // EmptyKey -- no-op, no crash
        }

        // The core H4 scenario: a CLR ThreadID is recycled after Forget() tombstones it. The new
        // thread reusing that address must start fresh (untagged), not inherit the dead thread's
        // depth -- and Increment/Decrement on it must work normally afterwards.
        TEST_METHOD(a_recycled_thread_id_starts_fresh_after_forget)
        {
            AgentWorkMap map;
            const ThreadID recycled = static_cast<ThreadID>(0xC000);

            map.Increment(recycled);
            map.Increment(recycled); // depth 2, then the thread dies without unwinding
            map.Forget(recycled);

            // A different, unrelated thread is allocated at the same recycled address.
            Assert::IsFalse(map.IsAgentWork(recycled));
            map.Increment(recycled);
            Assert::IsTrue(map.IsAgentWork(recycled));
            map.Decrement(recycled);
            Assert::IsFalse(map.IsAgentWork(recycled));
        }

        // Forgetting one thread must not disturb a different, still-live thread that happens to
        // probe through the same slot chain.
        TEST_METHOD(forget_does_not_disturb_other_threads_in_the_same_chain)
        {
            AgentWorkMap map;
            const ThreadID dying = static_cast<ThreadID>(0xD000);
            const ThreadID alive = static_cast<ThreadID>(0xD000 + 1); // deliberately adjacent key

            map.Increment(dying);
            map.Increment(alive);
            Assert::IsTrue(map.IsAgentWork(dying));
            Assert::IsTrue(map.IsAgentWork(alive));

            map.Forget(dying);
            Assert::IsFalse(map.IsAgentWork(dying));
            Assert::IsTrue(map.IsAgentWork(alive)); // must survive a tombstone placed nearby
        }

        // Probe budget exhausted -> FindOrClaimSlot returns nullptr -> Increment silently drops. Fill an
        // entire MaxProbes-long chain (all keys sharing one home) so the next distinct key hashing to that
        // home can find no free slot within the budget. That key must simply go untagged (never a crash,
        // never a stall) AND every already-tracked key on the chain must be undisturbed -- the documented
        // "table full -> this thread's samples go untagged" failure mode, with no corruption of neighbors.
        TEST_METHOD(table_full_drops_a_new_key_without_disturbing_existing_slots)
        {
            const size_t chainLength = AgentWorkMap::MaxProbes;
            const auto keys = FindKeysSharingHome(chainLength + 1);
            Assert::AreEqual(chainLength + 1, keys.size());

            AgentWorkMap map;

            // Fill the whole probe window: keys[0..MaxProbes-1] land in home..home+(MaxProbes-1).
            for (size_t i = 0; i < chainLength; ++i)
            {
                map.Increment(keys[i]);
                Assert::IsTrue(map.IsAgentWork(keys[i]));
            }

            // One more distinct key hashing to the same home: 64 probes all hit occupied live slots, no
            // tombstone -> FindOrClaimSlot returns nullptr -> Increment is a no-op -> untagged.
            map.Increment(keys[chainLength]);
            Assert::IsFalse(map.IsAgentWork(keys[chainLength]));

            // The overflow attempt must not have clobbered any of the filled slots.
            for (size_t i = 0; i < chainLength; ++i)
            {
                Assert::IsTrue(map.IsAgentWork(keys[i]));
            }

            // A slot freed by Forget re-opens room on the chain, so a subsequent claim succeeds again --
            // proving the drop was purely a capacity condition, not a wedged table.
            map.Forget(keys[0]);
            map.Increment(keys[chainLength]);
            Assert::IsTrue(map.IsAgentWork(keys[chainLength]));
        }

        // Cluster G regression: reconstructs the EXACT interleaving the finding describes and asserts the
        // fix neutralizes it. The race, step by step:
        //   * thread A is mid-Increment(A): it has its slot back from FindOrClaimSlot and has read the
        //     live generation, then it is preempted;
        //   * ContinuousProfiler::Start runs the wholesale reset on another thread (NewGeneration);
        //   * A resumes and publishes Depth=1 into that slot, stamped with the generation it captured
        //     BEFORE the bump -- the classic pre-increment-generation writer the NewGeneration comment
        //     calls out;
        //   * an unrelated thread B is later allocated at a recycled address hashing to the same home and
        //     CLAIMS that slot without ever touching its Depth -- exactly the move the finding names
        //     ("B's FindOrClaimSlot CASes that empty-looking slot to itself and never touches Depth"),
        //     inheriting A's stray 1.
        // Pre-fix, IsAgentWork was `Depth > 0` with no generation gate, so B read as agent work FOREVER and
        // every one of its samples was silently filtered out of the profile. The generation stamp is what
        // makes IsAgentWork reject that inherited depth. We drive the single-step ordering deterministically
        // via friend access -- a genuine thread race hits this sub-microsecond window only by luck -- while
        // the load-bearing assertions run against the real public IsAgentWork/Increment. With the generation
        // gate removed (the pre-fix logic), this test fails on the `IsFalse(map.IsAgentWork(b))` below.
        TEST_METHOD(newgeneration_racing_an_in_flight_increment_never_poisons_a_later_claimant)
        {
            // A and B deliberately share a home slot so B reclaims the very slot A's orphaned Increment used.
            const auto keys = FindKeysSharingHome(2);
            Assert::AreEqual(static_cast<size_t>(2), keys.size());
            const ThreadID a = keys[0];
            const ThreadID b = keys[1];

            AgentWorkMap map;

            // --- Thread A enters Increment(a): claim the slot and read the live generation, then it is
            //     preempted before publishing its depth. ---
            AgentWorkMap::Slot* slot = map.FindOrClaimSlot(a);
            Assert::IsNotNull(slot);
            const uint64_t capturedGeneration = map._generation.load(std::memory_order_relaxed);

            // --- ContinuousProfiler::Start runs NewGeneration on another thread while A is preempted. ---
            map.NewGeneration();

            // --- Thread A resumes and completes its Increment, publishing Depth=1 stamped with the
            //     generation it captured BEFORE the bump. ---
            slot->Depth.store(1, std::memory_order_release);
            slot->Gen.store(capturedGeneration, std::memory_order_release);
            Assert::IsFalse(map.IsAgentWork(a)); // A is (acceptably) under-tagged: its slot is a retired gen

            // --- Unrelated thread B claims this slot WITHOUT touching Depth, inheriting A's stray 1. In the
            //     original bug the wholesale reset (Clear) had zeroed the Key, so B's FindOrClaimSlot CAS'd
            //     the empty-looking slot to itself and never cleared the Depth. We reproduce that exact
            //     reclaim-without-touching-Depth end state directly: Key -> b, Depth left at the inherited 1,
            //     stamp left as the previous generation. B has done NO agent work. ---
            slot->Key.store(b, std::memory_order_release);

            // Pre-fix, IsAgentWork was `Depth > 0`, so this returned true FOREVER. The generation gate makes
            // it false: the slot's stamp is a retired generation. THIS is the load-bearing assertion -- it
            // fails against the pre-fix (gate-less) code and passes against the fix.
            Assert::IsFalse(map.IsAgentWork(b));

            // B still tracks correctly once it really does agent work in the current generation: its first
            // Increment hits the adopt path (stale stamp) and RESETS the stray depth rather than counting on
            // top of it -- so a single matching Decrement returns it to untagged (proving no double-count).
            map.Increment(b);
            Assert::IsTrue(map.IsAgentWork(b));
            map.Decrement(b);
            Assert::IsFalse(map.IsAgentWork(b));
        }

        // Best-effort concurrency stress for the NewGeneration-vs-Increment race the Cluster G finding is
        // about. Worker threads churn balanced Increment/Decrement pairs on their OWN ThreadIDs (as the
        // Scheduler's timer callbacks do) while another thread hammers NewGeneration (as the backoff-resume
        // Start path does), all against the one map, for a bounded window. Like TraceContextMapTest's
        // seqlock smoke test we do not pierce timing to force the exact sub-microsecond interleaving; we
        // exercise it indirectly and assert the OBSERVABLE contract the fix guarantees: nothing crashes,
        // hangs, or underflows, and once the churn stops every worker that is no longer inside a dispatch
        // reads as untagged -- i.e. the race can never leave a thread PERMANENTLY tagged.
        TEST_METHOD(concurrent_increment_decrement_racing_newgeneration_never_permanently_tags)
        {
            AgentWorkMap map;
            std::atomic<bool> stop{ false };

            std::vector<ThreadID> ids;
            for (int t = 0; t < 4; ++t)
            {
                ids.push_back(static_cast<ThreadID>((t + 1) * 0x1000));
            }

            std::vector<std::thread> threads;
            for (const ThreadID id : ids)
            {
                threads.emplace_back([&map, &stop, id]
                {
                    while (!stop.load(std::memory_order_relaxed))
                    {
                        map.Increment(id);
                        map.Increment(id); // nest, to exercise the adopt-vs-fetch_add branch across bumps
                        map.Decrement(id);
                        map.Decrement(id);
                    }
                });
            }

            // The generation-bumping thread: stands in for repeated ContinuousProfiler::Start calls racing
            // the live Increment/Decrement churn above.
            threads.emplace_back([&map, &stop]
            {
                while (!stop.load(std::memory_order_relaxed))
                {
                    map.NewGeneration();
                    std::this_thread::yield();
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

            // Every worker exited having done a balanced number of Increment/Decrement pairs, so none is
            // inside a dispatch. A final NewGeneration retires anything left over from the last racy tick.
            // No worker may still read as agent work -- the permanent over-tag this fix prevents.
            map.NewGeneration();
            for (const ThreadID id : ids)
            {
                Assert::IsFalse(map.IsAgentWork(id));
            }

            // The map is still fully usable after all that churn.
            for (const ThreadID id : ids)
            {
                map.Increment(id);
                Assert::IsTrue(map.IsAgentWork(id));
                map.Decrement(id);
                Assert::IsFalse(map.IsAgentWork(id));
            }
        }

        // Genuine high-contention stress for the FindOrClaimSlot CAS races and the tombstone-reclaim path,
        // which the tests above never reach: the Cluster G stress test gives every worker its OWN,
        // well-separated key, so each thread owns a distinct home slot and no two threads ever CAS the same
        // slot.Key -- the lock-free claim protocol is effectively single-threaded there. Here every worker
        // key deliberately shares ONE home slot, so all Increment claims AND all post-Forget re-claims CAS on
        // the SAME probe chain concurrently -- the maximal-contention arrangement for the slot.Key
        // compare_exchange and the tombstone-reclaim CAS. Single-writer-per-key is preserved (each worker
        // owns a distinct key); each worker churns a balanced Increment/Decrement pair then Forget()s its own
        // slot at depth 0, so the next iteration must re-claim a slot on a chain peers are simultaneously
        // reclaiming. A reader thread hammers IsAgentWork throughout (the sampler's role) so reads run against
        // the churn. Iteration-count bounded and barrier-aligned so the interleaving is driven by real
        // contention on the chain rather than a wall-clock window: a claim/reclaim CAS that dropped an update,
        // double-claimed a slot, or duplicated a key would leave a key stuck-tagged or un-reclaimable, which
        // the post-join assertions detect. This is the test that would go flaky if FindOrClaimSlot's
        // acq_rel CAS were weakened on a weak memory model (arm64).
        TEST_METHOD(concurrent_claim_and_reclaim_under_slot_contention_never_corrupts)
        {
            constexpr int workerCount = 8;
            constexpr int iterations = 50000;

            const auto keys = FindKeysSharingHome(workerCount);
            Assert::AreEqual(static_cast<size_t>(workerCount), keys.size());

            AgentWorkMap map;
            std::atomic<int> ready{ 0 };
            std::atomic<bool> go{ false };
            std::atomic<bool> stopReader{ false };

            std::vector<std::thread> threads;
            for (int w = 0; w < workerCount; ++w)
            {
                const ThreadID id = keys[w];
                threads.emplace_back([&map, &ready, &go, id, iterations]
                {
                    ready.fetch_add(1, std::memory_order_relaxed);
                    while (!go.load(std::memory_order_acquire)) { std::this_thread::yield(); }

                    for (int i = 0; i < iterations; ++i)
                    {
                        map.Increment(id);
                        map.Increment(id); // nest -> exercises the adopt-vs-fetch_add branch under contention
                        map.Decrement(id);
                        map.Decrement(id);
                        // Tombstone this worker's slot at depth 0; the next iteration's Increment must
                        // re-claim a slot on the shared chain, racing peers reclaiming their own freed slots.
                        map.Forget(id);
                    }
                });
            }

            // Sampler stand-in: reads across all keys while the runtime would be "suspended" -- must never
            // crash or hang against the concurrent claim/reclaim churn. It asserts nothing (values are in
            // flux); its job is to run the wait-free reader concurrently with writers.
            std::thread reader([&map, &keys, &stopReader]
            {
                while (!stopReader.load(std::memory_order_relaxed))
                {
                    for (const ThreadID id : keys)
                    {
                        (void)map.IsAgentWork(id);
                    }
                }
            });

            while (ready.load(std::memory_order_relaxed) < workerCount) { std::this_thread::yield(); }
            go.store(true, std::memory_order_release);
            for (auto& th : threads) { th.join(); }
            stopReader.store(true, std::memory_order_relaxed);
            reader.join();

            // Every worker exited on a balanced pair count with a final Forget, so none is mid-dispatch and
            // every slot is tombstoned. A fresh generation retires anything left over from the last racy
            // iteration; no key may still read as agent work.
            map.NewGeneration();
            for (const ThreadID id : keys)
            {
                Assert::IsFalse(map.IsAgentWork(id));
            }

            // Every key must still claim and track cleanly -- proves no slot was lost, double-claimed, or
            // wedged by the contention, and that the chain remains fully reusable.
            for (const ThreadID id : keys)
            {
                map.Increment(id);
                Assert::IsTrue(map.IsAgentWork(id));
                map.Decrement(id);
                Assert::IsFalse(map.IsAgentWork(id));
            }
        }
    };
}}}

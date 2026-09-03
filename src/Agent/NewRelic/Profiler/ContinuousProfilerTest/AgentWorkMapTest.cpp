/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
#include "stdafx.h"
#include "CppUnitTest.h"

#include <cstdint>
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

        // Clear() un-poisons a slot left stuck at depth >= 1 by an orphaned Increment -- the whole point
        // of calling it from ContinuousProfiler::Start.
        TEST_METHOD(clear_releases_a_slot_stuck_at_nonzero_depth)
        {
            AgentWorkMap map;
            const ThreadID id = static_cast<ThreadID>(0x5000);

            map.Increment(id); // no matching Decrement -> permanently tagged without Clear()
            Assert::IsTrue(map.IsAgentWork(id));

            map.Clear();
            Assert::IsFalse(map.IsAgentWork(id));
        }

        // Clear() drops EVERY slot, not just one, and leaves the map reusable afterwards.
        TEST_METHOD(clear_drops_all_threads_and_leaves_the_map_usable)
        {
            AgentWorkMap map;
            const ThreadID a = static_cast<ThreadID>(0x6000);
            const ThreadID b = static_cast<ThreadID>(0x7000);

            map.Increment(a);
            map.Increment(b);
            map.Increment(b); // nested two deep -> Clear must drop the whole depth, not decrement it
            Assert::IsTrue(map.IsAgentWork(a));
            Assert::IsTrue(map.IsAgentWork(b));

            map.Clear();
            Assert::IsFalse(map.IsAgentWork(a));
            Assert::IsFalse(map.IsAgentWork(b));

            // A cleared slot must be re-claimable, so the next session tracks normally.
            map.Increment(a);
            Assert::IsTrue(map.IsAgentWork(a));
            map.Decrement(a);
            Assert::IsFalse(map.IsAgentWork(a));
        }

        // A Decrement that arrives after Clear() (the orphaned other half of a pre-Clear Increment) must
        // clamp at zero rather than underflow the freshly cleared slot.
        TEST_METHOD(decrement_after_clear_does_not_underflow)
        {
            AgentWorkMap map;
            const ThreadID id = static_cast<ThreadID>(0x8000);

            map.Increment(id);
            map.Clear();
            map.Decrement(id); // late half of the pre-Clear pair
            Assert::IsFalse(map.IsAgentWork(id));

            map.Increment(id);
            Assert::IsTrue(map.IsAgentWork(id));
        }

        // Clear() on a never-used map is a harmless no-op.
        TEST_METHOD(clear_on_an_empty_map_is_a_no_op)
        {
            AgentWorkMap map;
            const ThreadID id = static_cast<ThreadID>(0x9000);

            map.Clear();
            Assert::IsFalse(map.IsAgentWork(id));

            map.Increment(id);
            Assert::IsTrue(map.IsAgentWork(id));
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
    };
}}}

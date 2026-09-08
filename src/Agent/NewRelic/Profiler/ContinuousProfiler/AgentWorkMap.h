/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
#pragma once
#include <atomic>
#include <array>
#include <cstdint>

#include <cor.h>
#include <corprof.h>

// AgentWorkMap tracks, per managed thread (keyed by CLR ThreadID, same id space as
// TraceContextMap -- see that header for why), whether the thread is CURRENTLY executing
// agent-owned background dispatch (Scheduler's timer callbacks: harvest, samplers, health
// reporter, command polling, CP's own drain). It exists to let the sampler tag a sample as
// "agent work" by thread IDENTITY at the instant of capture, instead of by matching frame text
// -- frame-text matching cannot see a thread parked in System.Threading.Monitor.Wait with no
// agent frame anywhere on its captured stack (see follow-up #16).
//
// Same hard constraint as TraceContextMap: Increment/Decrement are called from arbitrary app
// threads (Scheduler wraps its own dispatch), and the sampler reads this map for every sampled
// thread while the CLR is SUSPENDED. A mutex here would risk the sampler deadlocking behind a
// suspended writer, so this is lock-free with a wait-free, non-spinning reader.
//
// While a thread is alive, a slot's Depth needs no end-of-life signal: the same ThreadPool thread
// runs many Increment/Decrement cycles (once per timer tick) over its life, so once claimed a slot
// is kept (at Depth 0 between ticks) rather than tombstoned/freed on every cycle -- avoiding reclaim
// churn on every tick. A slot is a single atomic counter (not the three-field seqlock TraceContextMap
// needs), so no seqlock is required: a lone atomic load/store never tears. The one wholesale reset is
// Clear(), called on ContinuousProfiler::Start so a depth orphaned by a managed-side lifecycle race
// cannot outlive the session that created it -- see Clear().
//
// A CLR ThreadID IS recycled once its thread dies (it's really a Thread* value), so a slot DOES need
// an end-of-life signal at that point: Forget(), called from CorProfilerCallbackImpl::ThreadDestroyed,
// tombstones the dead thread's slot (same TombstoneKey scheme as TraceContextMap) so neither a later,
// unrelated thread reusing the address inherits a stale nonzero Depth, nor does the table fill
// permanently over a long-running process's thread-pool churn.
namespace NewRelic { namespace Profiler { namespace ContinuousProfiler
{
    class AgentWorkMap
    {
        // The unit test builds keys that deliberately share a home slot to exercise probe-budget
        // exhaustion (the table-full drop path). Friendship lets it call the private HashOf and read
        // MaxProbes rather than keeping a hand-copied hash that silently goes stale -- same rationale
        // and pattern as TraceContextMap's friendship with TraceContextMapTest.
        friend class AgentWorkMapTest;

    public:
        // Mark the calling thread as one level deeper into agent-owned dispatch. Nesting-safe --
        // Decrement must be called once per matching Increment. Silently no-ops if the table's
        // probe budget is exhausted (this thread's samples simply go untagged; never a stall).
        void Increment(ThreadID threadId) noexcept
        {
            if (threadId == EmptyKey)
            {
                return; // a real ThreadID is never 0.
            }

            Slot* slot = FindOrClaimSlot(threadId);
            if (slot == nullptr)
            {
                return; // table full -> silently drop; this thread's samples simply go untagged.
            }

            slot->Depth.fetch_add(1, std::memory_order_acq_rel);
        }

        // Mark the calling thread one level shallower. A no-op if the thread has no slot (should
        // not happen given correctly paired Increment/Decrement) or is already at depth 0.
        void Decrement(ThreadID threadId) noexcept
        {
            if (threadId == EmptyKey)
            {
                return;
            }

            Slot* slot = FindSlot(threadId);
            if (slot == nullptr)
            {
                return;
            }

            uint32_t current = slot->Depth.load(std::memory_order_relaxed);
            while (current > 0)
            {
                if (slot->Depth.compare_exchange_weak(current, current - 1, std::memory_order_acq_rel))
                {
                    return;
                }
            }
        }

        // Drop every slot, returning the map to its freshly-constructed (all-untagged) state.
        //
        // Called only from ContinuousProfiler::Start, as a self-healing floor under the strict 1:1
        // Increment/Decrement pairing this map requires. If a managed-side lifecycle race ever orphans an
        // Increment on a thread that stays alive (so Forget() never fires for it), that thread's slot
        // stays at Depth >= 1 for the whole process, so every later sample on it is tagged agent work and
        // filtered out of the profile: silent, permanent coverage loss. Clearing on Start bounds that
        // damage to one session instead of the process lifetime. Forget() (see below) handles the
        // complementary case -- a thread that dies while still holding a nonzero Depth.
        //
        // Concurrency: same hard constraint as the rest of the class, so this is plain atomic stores --
        // no lock, no allocation -- and therefore safe even if the sampler is reading with the runtime
        // suspended. A reader racing this sees a partially cleared table, which can only make it read
        // FEWER threads as agent work (a slot's Depth is zeroed BEFORE its Key, so a key is never visible
        // with a stale non-zero depth, and a key cleared mid-probe-chain merely terminates a concurrent
        // lookup early -> "absent" -> untagged). Never a stall, never a tear, never over-tagging.
        //
        // Accepted cost: the backoff-resume path also reaches ContinuousProfiler::Start while a session's
        // Scheduler timers are still ticking, so this can zero a legitimately in-flight callback's depth.
        // Its pending Decrement then clamps at 0 (a no-op) and the thread is briefly under-tagged until
        // its next tick re-Increments -- transient and self-healing, versus permanent poisoning.
        void Clear() noexcept
        {
            for (Slot& slot : _slots)
            {
                slot.Depth.store(0, std::memory_order_release);
                slot.Key.store(EmptyKey, std::memory_order_release);
            }
        }

        // True if the given thread is currently inside agent-owned dispatch. Called by the
        // SAMPLER while the runtime is suspended -- wait-free, single load, never spins.
        bool IsAgentWork(ThreadID threadId) const noexcept
        {
            if (threadId == EmptyKey)
            {
                return false;
            }

            const Slot* slot = FindSlot(threadId);
            if (slot == nullptr)
            {
                return false;
            }

            return slot->Depth.load(std::memory_order_acquire) > 0;
        }

        // Release the given (now-dead) thread's slot: zero its Depth and tombstone its Key so a
        // later, unrelated thread reusing the same recycled ThreadID never inherits a stale nonzero
        // depth, and so the table doesn't fill permanently over a process's thread-pool churn.
        // Called from CorProfilerCallbackImpl::ThreadDestroyed -- never from the dying thread itself
        // (it is already dead), so there is no race with its own last Increment/Decrement. A no-op
        // if the thread never had a slot. Same release order as Clear() (Depth before Key) for the
        // same reason: a concurrent suspended-runtime reader can then only under-tag, never
        // over-tag or tear.
        void Forget(ThreadID threadId) noexcept
        {
            if (threadId == EmptyKey || threadId == TombstoneKey)
            {
                return;
            }

            Slot* slot = FindSlot(threadId);
            if (slot == nullptr)
            {
                return;
            }

            slot->Depth.store(0, std::memory_order_release);
            slot->Key.store(TombstoneKey, std::memory_order_release);
        }

    private:
        // 0 is reserved as the empty-slot sentinel. A valid CLR ThreadID is never 0.
        static constexpr ThreadID EmptyKey = 0;

        // All-ones is reserved as the tombstone sentinel -- same scheme and rationale as
        // TraceContextMap::TombstoneKey: a slot freed by Forget() so a different ThreadID can later
        // reclaim it, distinct from EmptyKey because a tombstone must NOT terminate a probe chain (a
        // live key may sit past it), whereas an empty slot does terminate the chain.
        static constexpr ThreadID TombstoneKey = static_cast<ThreadID>(~static_cast<uint64_t>(0));

        // Same sizing rationale as TraceContextMap: bounds memory (a few KB) and keeps every
        // operation allocation-free. Slots are freed (tombstoned) by Forget() when a thread dies, so
        // this bounds the number of DISTINCT threads that can be concurrently tracked, not the total
        // ever seen over the process lifetime -- comfortably above realistic ThreadPool churn.
        static constexpr size_t SlotCount = 4096;
        static constexpr size_t SlotMask = SlotCount - 1;
        static constexpr int SlotBits = 12; // log2(SlotCount); used to take the HIGH hash bits.
        static_assert(SlotCount == (static_cast<size_t>(1) << SlotBits), "SlotBits must equal log2(SlotCount)");

        // Cap on slots probed per lookup/claim -- bounds the SAMPLER's suspend-window cost to
        // O(MaxProbes) regardless of table state. See TraceContextMap for the full rationale.
        static constexpr size_t MaxProbes = 64;

        struct Slot
        {
            std::atomic<ThreadID> Key{ EmptyKey };
            std::atomic<uint32_t> Depth{ 0 };
        };

        // Same multiplicative hash as TraceContextMap -- see that header for why the LOW bits
        // must not be used (CLR ThreadIDs are pointer-aligned, so their low bits are always 0).
        static size_t HashOf(ThreadID key) noexcept
        {
            return static_cast<size_t>((static_cast<uint64_t>(key) * 0x9E3779B97F4A7C15ull) >> (64 - SlotBits));
        }

        const Slot* FindSlot(ThreadID key) const noexcept
        {
            size_t idx = HashOf(key);
            for (size_t probe = 0; probe < MaxProbes; ++probe)
            {
                const Slot& slot = _slots[idx];
                const ThreadID k = slot.Key.load(std::memory_order_acquire);
                if (k == key)
                {
                    return &slot;
                }
                if (k == EmptyKey)
                {
                    return nullptr; // empty slot terminates the chain -> key was never inserted.
                }
                // TombstoneKey or a different live key -> keep probing; a tombstone must not
                // terminate the chain since a live key may sit past it.
                idx = (idx + 1) & SlotMask;
            }
            return nullptr; // probe budget exhausted -> treat as absent, never a stall.
        }

        Slot* FindSlot(ThreadID key) noexcept
        {
            return const_cast<Slot*>(static_cast<const AgentWorkMap*>(this)->FindSlot(key));
        }

        // Locate the slot for `key`, claiming a free or tombstoned slot if not already present.
        // Writer-only (called from the app thread doing Increment). Same tombstone-reclaim shape as
        // TraceContextMap::FindOrClaimSlot: scan the whole chain for the key first -- remembering the
        // earliest tombstone passed -- and only reclaim it once an empty slot confirms the key is
        // absent, so a live slot sitting past an earlier tombstone is never shadowed by a duplicate.
        Slot* FindOrClaimSlot(ThreadID key) noexcept
        {
            size_t idx = HashOf(key);
            Slot* firstTombstone = nullptr;
            for (size_t probe = 0; probe < MaxProbes; ++probe, idx = (idx + 1) & SlotMask)
            {
                Slot& slot = _slots[idx];
                ThreadID k = slot.Key.load(std::memory_order_acquire);

                if (k == key)
                {
                    return &slot; // already present.
                }

                if (k == TombstoneKey)
                {
                    if (firstTombstone == nullptr)
                    {
                        firstTombstone = &slot; // reuse candidate; keep scanning in case the key is live ahead.
                    }
                    continue;
                }

                if (k != EmptyKey)
                {
                    continue; // occupied by a different live key -> keep probing.
                }

                // Empty slot: the chain ends here, so the key is absent. Prefer reclaiming the
                // earliest tombstone seen; otherwise claim this empty slot.
                if (firstTombstone != nullptr)
                {
                    ThreadID expected = TombstoneKey;
                    if (firstTombstone->Key.compare_exchange_strong(expected, key, std::memory_order_acq_rel))
                    {
                        return firstTombstone;
                    }
                    if (expected == key)
                    {
                        return firstTombstone;
                    }
                    firstTombstone = nullptr; // tombstone taken by another key -> fall back to the empty slot.
                }

                ThreadID expected = EmptyKey;
                if (slot.Key.compare_exchange_strong(expected, key, std::memory_order_acq_rel))
                {
                    return &slot;
                }
                if (expected == key)
                {
                    return &slot; // another thread claimed it for the SAME key -> reuse it.
                }
                // A different key won this slot; it is now occupied -> keep probing.
            }

            // Probe budget exhausted. If we passed a tombstone, make one last attempt to reclaim it.
            if (firstTombstone != nullptr)
            {
                ThreadID expected = TombstoneKey;
                if (firstTombstone->Key.compare_exchange_strong(expected, key, std::memory_order_acq_rel))
                {
                    return firstTombstone;
                }
                if (expected == key)
                {
                    return firstTombstone;
                }
            }
            return nullptr; // table effectively full within the probe window -> caller drops.
        }

        std::array<Slot, SlotCount> _slots{};
    };
}}}

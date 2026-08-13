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
// Unlike TraceContextMap, a slot's Depth genuinely never needs an end-of-life signal: the same
// ThreadPool thread runs many Increment/Decrement cycles (once per timer tick) over its life, so
// once claimed a slot is kept (at Depth 0 between ticks) rather than tombstoned/freed -- avoiding
// reclaim churn on every tick. A slot is a single atomic counter (not the three-field seqlock
// TraceContextMap needs), so no seqlock is required: a lone atomic load/store never tears.
namespace NewRelic { namespace Profiler { namespace ContinuousProfiler
{
    class AgentWorkMap
    {
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

    private:
        // 0 is reserved as the empty-slot sentinel. A valid CLR ThreadID is never 0.
        static constexpr ThreadID EmptyKey = 0;

        // Same sizing rationale as TraceContextMap: bounds memory (a few KB) and keeps every
        // operation allocation-free. No tombstoning here (see class comment), so this bounds the
        // number of DISTINCT threads that have EVER run agent-owned dispatch over the process
        // lifetime -- comfortably above realistic ThreadPool churn.
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
                idx = (idx + 1) & SlotMask;
            }
            return nullptr; // probe budget exhausted -> treat as absent, never a stall.
        }

        Slot* FindSlot(ThreadID key) noexcept
        {
            return const_cast<Slot*>(static_cast<const AgentWorkMap*>(this)->FindSlot(key));
        }

        // Locate the slot for `key`, claiming a free slot if not already present. Writer-only
        // (called from the app thread doing Increment). No tombstones exist in this map (see
        // class comment), so this is simpler than TraceContextMap's claim logic: an empty slot
        // always terminates the chain and is always safe to claim.
        Slot* FindOrClaimSlot(ThreadID key) noexcept
        {
            size_t idx = HashOf(key);
            for (size_t probe = 0; probe < MaxProbes; ++probe, idx = (idx + 1) & SlotMask)
            {
                Slot& slot = _slots[idx];
                ThreadID k = slot.Key.load(std::memory_order_acquire);

                if (k == key)
                {
                    return &slot; // already present.
                }

                if (k != EmptyKey)
                {
                    continue; // occupied by a different live key -> keep probing.
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
            return nullptr; // table effectively full within the probe window -> caller drops.
        }

        std::array<Slot, SlotCount> _slots{};
    };
}}}

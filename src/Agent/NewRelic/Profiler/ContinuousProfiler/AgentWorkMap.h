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
// churn on every tick. A slot is two atomics (a Depth counter plus a generation stamp); neither tears,
// and only the slot's OWNING thread ever mutates them (single-writer-per-slot), so no seqlock is
// required -- the reader gates on the stamp, which Increment publishes release-AFTER the Depth it commits.
//
// The one wholesale reset is NewGeneration(), called on ContinuousProfiler::Start so a depth orphaned by
// a managed-side lifecycle race cannot outlive the session that created it. It touches no slot: it bumps
// a session counter that Increment stamps into each slot and IsAgentWork checks, so the previous session's
// depths all read as "not agent work" at once -- see NewGeneration() for why a bulk per-slot clear was
// rejected in favor of this (same fix TraceContextMap::NewGeneration uses for the identical hazard).
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

            // Adopt the slot into the current session if its stamp is stale (previous generation, never
            // stamped, or reclaimed): discard its Depth and start this session's nesting fresh at one level
            // rather than fetch_add-ing onto a stale count. Publish Depth before the stamp, both release --
            // the stamp is the commit flag IsAgentWork gates on, so a suspended-runtime reader caught between
            // the two stores under-tags (sees "absent"), never over-tags.
            const uint64_t generation = _generation.load(std::memory_order_relaxed);
            if (slot->Gen.load(std::memory_order_relaxed) != generation)
            {
                slot->Depth.store(1, std::memory_order_release);
                slot->Gen.store(generation, std::memory_order_release);
                return;
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

            // Same generation gate as Increment/IsAgentWork: a Decrement racing NewGeneration must not
            // touch a Depth stamped by the retired session. That count no longer means anything -- IsAgentWork
            // already ignores it -- so decrementing it here would be silent noise on dead data, and (if this
            // slot is later re-adopted by Increment for the new generation) could underflow a fresh Depth that
            // has nothing to do with this call. Without this gate the trap is latent only because IsAgentWork's
            // own read-side gate happens to make the result unobservable; it must not be dropped independently.
            const uint64_t generation = _generation.load(std::memory_order_relaxed);
            if (slot->Gen.load(std::memory_order_relaxed) != generation)
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

        // Retire every depth recorded by the previous profiling session WITHOUT writing a single slot.
        //
        // Called only from ContinuousProfiler::Start, as a self-healing floor under the strict 1:1
        // Increment/Decrement pairing this map requires. If a managed-side lifecycle race ever orphans an
        // Increment on a thread that stays alive (so Forget() never fires for it), that thread's slot stays
        // at Depth >= 1 and every later sample on it is silently filtered out of the profile forever.
        // Bumping the generation makes every slot stamped by the previous session read as "not agent work"
        // at once, bounding that damage to one session.
        //
        // A bulk per-slot Depth/Key clear was tried and rejected: it writes slots this call does not own,
        // so it can race a live Increment -- clearing a slot's Key while its owner is mid-Increment lets the
        // owner republish Depth >= 1 on an empty-looking slot, which a later unrelated thread then CAS-claims
        // and inherits, over-tagging it forever. The generation counter (one relaxed increment, no lock, no
        // allocation, touches no slot) sidesteps that: a writer racing this reads either the old or new
        // generation and stamps accordingly, so the worst outcome is a transient, self-healing under-tag,
        // never a tear or a permanent over-tag.
        void NewGeneration() noexcept
        {
            _generation.fetch_add(1, std::memory_order_relaxed);
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

            // Gate on the generation stamp FIRST: it is the commit flag Increment publishes release-AFTER
            // the Depth it belongs to. If this acquire-load already shows the current generation, the
            // release/acquire pairing guarantees the matching Depth store is visible on the load below. A
            // slot stamped by a retired session (or never stamped) -- including one caught mid-adopt, or a
            // slot reclaimed by a new thread that has not yet stamped it -- reads as "not agent work", so a
            // stale non-zero Depth can never over-tag an unrelated thread. Favours under-tagging, which
            // self-heals on the thread's next Increment, over over-tagging, which does not.
            if (slot->Gen.load(std::memory_order_acquire) != _generation.load(std::memory_order_relaxed))
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
        // if the thread never had a slot. Depth is zeroed BEFORE the Key is tombstoned (both release) so
        // a concurrent suspended-runtime reader that still matches the old Key reads Depth 0 (and is in
        // any case rejected by the generation gate), never a stale non-zero depth -- only under-tagging.
        //
        // Same single-writer-per-slot invariant as TraceContextMap::Reset: the caller here runs on the
        // CLR's thread-destruction callback thread, not the owning thread, and that cross-thread call is
        // safe only because, per Microsoft's ICorProfilerCallback::ThreadDestroyed documentation, this
        // callback fires only after the thread has already exited -- so it can no longer be concurrently
        // Incrementing/Decrementing its own slot when Forget runs on its behalf.
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

            // Profiling session this slot's Depth was last stamped in, by Increment. 0 means "never
            // stamped", which never matches a live generation (those start at 1), so a default-constructed
            // or freshly-claimed slot is rejected by IsAgentWork on the generation check alone -- its stale
            // Depth (if any) can never be mistaken for current-session agent work. See NewGeneration.
            std::atomic<uint64_t> Gen{ 0 };
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

        // Current profiling session, bumped by NewGeneration. Starts at 1 so it can never equal the 0 a
        // never-stamped slot carries. Mirrors TraceContextMap::_generation.
        std::atomic<uint64_t> _generation{ 1 };
    };
}}}

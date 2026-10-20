/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
#pragma once
#include <atomic>
#include <cstdint>
#include <cstddef>
#include <new>

#include <cor.h>
#include <corprof.h>

// PendingPushMap gives each managed thread ONE stable memory cell in which it can publish the span id it
// is ABOUT TO push into TraceContextMap, before it starts the push. Keyed by CLR ThreadID -- the same id
// space as TraceContextMap and AgentWorkMap (see TraceContextMap.h for why that key).
//
// WHY IT EXISTS
//
// RetiredSpanRegistry::CompactAgainst reclaims an entry only after proving no live TraceContextMap slot
// references it. That proof can only observe PUBLISHED slots. A thread that has chosen a span id but not
// yet written it into its slot is invisible to any snapshot: the slot reads either Span == 0 (claimed but
// not yet written) or the thread's PREVIOUS span, and both look exactly like "this thread does not
// reference the span". So if that span was already retired, compaction can reclaim its entry and the push
// then publishes a link with no entry left to reject it -- the original stale-link bug, bit for bit.
//
// A cell written before the push makes that intent visible to the snapshot, so compaction can count it as
// live. The union is ADDITIVE: a pending cell never replaces the slot-based live set. The managed push
// path returns early on a ReferenceEquals dedupe BEFORE writing any cell, and that is only sound because
// in that case the slot already holds the span from an earlier push in the same epoch -- i.e. the
// slot-based set is still the primary evidence and the cell is extra evidence, never a substitute.
//
// WHAT IT DOES NOT DO -- read this before reasoning about the guarantee
//
// It does NOT close the hole, and it does not close it in combination with two-pass reclamation either.
// It REMOVES the structurally-likely suspension points from the vulnerable window -- the P/Invoke
// transition into GC-preemptive mode, plus the native probe and slot write -- leaving only the
// straight-line managed hex decode between choosing the span id and publishing the intent. The remaining
// window is narrower, not absent: a thread can still be preempted, blocked behind a gen2 GC, or paged out
// inside it, and the tail is unbounded. Provably closing it requires publishing the intent BEFORE the span
// id is chosen or decoded, which means a managed-supplied in-flight set rather than a native cell --
// deliberately out of scope here.
//
// WHY THIS IS A SEPARATE MAP AND NOT A FIELD IN TraceContextMap::Slot
//
// This is the load-bearing design decision. A TraceContextMap slot is TOMBSTONED by Reset on transaction
// end and can then be reclaimed by a DIFFERENT ThreadID. A managed-side cached pointer into such a slot
// would therefore dangle into another thread's slot and turn every later cell write into a FOREIGN write
// -- violating the single-writer-per-slot invariant that is the only reason TraceContextMap's seqlock
// writes are race-free, in the worst possible way.
//
// AgentWorkMap is the correct precedent and this follows it: a slot is keyed by ThreadID, is NEVER
// tombstoned by any transaction-lifecycle event, and is released only by Forget() from
// ContinuousProfiler::ThreadDestroyed -- which, per Microsoft's ICorProfilerCallback::ThreadDestroyed
// documentation, the CLR invokes only AFTER the owning thread has already exited. So a cell address handed
// to a thread stays valid for that thread's entire remaining life, and is only recycled once no code on
// that thread can ever run again.
//
// Unlike AgentWorkMap the table is heap-allocated on demand (EnsureAllocated, from CP Start), matching
// RetiredSpanRegistry: a process that never enables continuous profiling must not carry it at all.
//
// FAILURE MODE, bounded and deliberately biased. A cell is cleared by the OWNING thread after its push, so
// a thread that writes a cell and then never clears it (a managed-side bug, or a thread that is torn down
// abruptly) leaves a non-zero value that keeps ONE registry entry alive until Forget() runs at
// ThreadDestroyed. For a long-lived thread that is an INDEFINITE hold, not a delay. It is bounded -- at
// most one pinned entry per live thread (SlotCount), against a registry an order of magnitude larger -- and
// it is the safe direction: a stuck cell over-reports the live set, which postpones a reclamation, whereas
// a missed cell under-reports it and risks a stale link. TraceContextMap::SnapshotLiveSpanIds documents the
// same trade for the same reason.
namespace NewRelic { namespace Profiler { namespace ContinuousProfiler
{
    class PendingPushMap
    {
        // The unit test builds keys that deliberately share a home slot to exercise probe-budget
        // exhaustion, so it must hash exactly as production does. Same friendship rationale as
        // AgentWorkMap/TraceContextMap: no hand-copied hash that can silently go stale.
        friend class PendingPushMapTest;

    public:
        PendingPushMap() noexcept = default;

        ~PendingPushMap()
        {
            // Only reached at process teardown, when the profiler singleton is destroyed. Deliberately no
            // free on CP stop: a managed thread may hold a cell address indefinitely, and there is no safe
            // point at which a stop can prove otherwise. Same lifetime discipline as RetiredSpanRegistry.
            delete[] _slots.load(std::memory_order_relaxed);
        }

        PendingPushMap(const PendingPushMap&) = delete;
        PendingPushMap& operator=(const PendingPushMap&) = delete;

        // Allocate the slot table. Idempotent and content-preserving, called from
        // ContinuousProfiler::Start -- never from a hot path and never inside a suspend window. Not
        // thread-safe against a concurrent EnsureAllocated; CP start/stop is already serialized under
        // ContinuousProfilingService's lock.
        bool EnsureAllocated() noexcept
        {
            if (_slots.load(std::memory_order_acquire) != nullptr)
            {
                return true;
            }

            Slot* table = new (std::nothrow) Slot[SlotCount]();
            if (table == nullptr)
            {
                return false; // allocation failure -> map stays inert; CellFor answers nullptr.
            }

            _slots.store(table, std::memory_order_release); // publish LAST: it is the readiness signal.
            return true;
        }

        // Claim this thread's cell if it does not have one yet, and return a STABLE address to it. The
        // address remains valid until Forget(threadId), i.e. until after the thread has exited, so managed
        // code is expected to call this ONCE per thread and cache the pointer for that thread's lifetime.
        //
        // Returns nullptr when the table was never allocated (a process that never started CP), when the
        // thread id is a slot sentinel, or when the probe budget is exhausted. A null answer is benign:
        // the caller simply pushes without publishing its intent first, which is exactly the pre-cell
        // behaviour.
        int64_t* CellFor(ThreadID threadId) noexcept
        {
            if (threadId == EmptyKey || threadId == TombstoneKey)
            {
                return nullptr; // a real CLR ThreadID is never 0 or all-ones.
            }

            Slot* table = _slots.load(std::memory_order_acquire);
            if (table == nullptr)
            {
                return nullptr;
            }

            bool claimed = false;
            Slot* slot = FindOrClaimSlot(table, threadId, claimed);
            if (slot == nullptr)
            {
                return nullptr;
            }

            if (claimed)
            {
                // Defence in depth against OVER-reporting, not a correctness requirement: Forget already
                // zeroes a cell before tombstoning it, and a never-claimed slot is zero from allocation,
                // so a freshly claimed cell is already 0. Zeroing here only guarantees that a slot
                // recycled from a dead thread cannot make a snapshot report that dead thread's last
                // intent. Deliberately NOT done on a repeat call for an already-owned slot -- that would
                // clobber a live pending value out from under an in-flight push.
                slot->Cell.store(0, std::memory_order_release);
            }

            // The cell is an atomic<int64_t> that managed code writes through this raw address; the
            // static_asserts below are what make that reinterpretation well defined in practice.
            return reinterpret_cast<int64_t*>(&slot->Cell);
        }

        // Release a dead thread's cell so a later thread can reuse the slot and so the table does not fill
        // permanently over a long-running process's thread-pool churn. Called ONLY from
        // ContinuousProfiler::ThreadDestroyed, i.e. after the owning thread has exited (same CLR guarantee
        // AgentWorkMap::Forget and TraceContextMap::Reset rely on), so it never races the owning thread's
        // own cell writes.
        //
        // The cell is zeroed BEFORE the key is tombstoned, so a concurrent snapshot that still matches the
        // old key reports nothing for it. That is deliberate UNDER-reporting, and safe only because the
        // owning thread is already dead: it has no in-flight push left to protect. Nothing else in this
        // file is allowed to under-report.
        void Forget(ThreadID threadId) noexcept
        {
            if (threadId == EmptyKey || threadId == TombstoneKey)
            {
                return;
            }

            Slot* table = _slots.load(std::memory_order_acquire);
            if (table == nullptr)
            {
                return;
            }

            Slot* slot = FindSlot(table, threadId);
            if (slot == nullptr)
            {
                return; // this thread never had a cell -> nothing to release.
            }

            slot->Cell.store(0, std::memory_order_release);
            slot->Key.store(TombstoneKey, std::memory_order_release);
        }

        // Copy out every non-zero pending span id. Together with TraceContextMap::SnapshotLiveSpanIds this
        // forms the live set a compaction pass proves against; see RetiredSpanRegistry::CompactAgainst.
        //
        // Same failure contract as SnapshotLiveSpanIds: returns false -- and the caller must reclaim
        // NOTHING -- if the buffer cannot hold every pending value. An unallocated table is not a failure;
        // it reports zero entries and succeeds, which is the correct live set for a map nobody can have
        // written to.
        //
        // No seqlock: a cell is a single atomic int64, so it cannot tear, and a value read mid-push is
        // either the old value or the new one -- both are spans this pass must treat as live.
        bool SnapshotPending(int64_t* out, size_t capacity, size_t& count) const noexcept
        {
            count = 0;
            if (out == nullptr)
            {
                return false;
            }

            const Slot* table = _slots.load(std::memory_order_acquire);
            if (table == nullptr)
            {
                return true; // never allocated -> nothing pending, and that is a complete answer.
            }

            for (size_t i = 0; i < SlotCount; ++i)
            {
                const Slot& slot = table[i];

                if (slot.Key.load(std::memory_order_acquire) == EmptyKey)
                {
                    continue; // never claimed -> nothing to report.
                }

                // Acquire, pairing with the managed side's release write of the cell.
                const int64_t pending = slot.Cell.load(std::memory_order_acquire);
                if (pending == 0)
                {
                    continue; // no push in flight on this thread.
                }

                if (count >= capacity)
                {
                    return false; // cannot report everything -> caller must not reclaim.
                }

                out[count++] = pending;
            }

            return true;
        }

        size_t ClaimedCountForTesting() const noexcept
        {
            const Slot* table = _slots.load(std::memory_order_acquire);
            if (table == nullptr)
            {
                return 0;
            }

            size_t claimed = 0;
            for (size_t i = 0; i < SlotCount; ++i)
            {
                const ThreadID key = table[i].Key.load(std::memory_order_acquire);
                if (key != EmptyKey && key != TombstoneKey)
                {
                    ++claimed;
                }
            }
            return claimed;
        }

    private:
        // 0 is reserved as the empty-slot sentinel. A valid CLR ThreadID is never 0.
        static constexpr ThreadID EmptyKey = 0;

        // All-ones is reserved as the tombstone sentinel -- same scheme and rationale as
        // AgentWorkMap::TombstoneKey: a slot freed by Forget() so a different ThreadID can later reclaim
        // it, distinct from EmptyKey because a tombstone must NOT terminate a probe chain (a live key may
        // sit past it), whereas an empty slot does terminate the chain.
        static constexpr ThreadID TombstoneKey = static_cast<ThreadID>(~static_cast<uint64_t>(0));

        // Same sizing rationale as AgentWorkMap: this bounds the number of DISTINCT LIVE threads that can
        // hold a cell at once, not the total ever seen, because Forget frees a dead thread's slot. 4096
        // slots x 16 bytes is 64 KB, allocated only in a process that actually starts continuous profiling.
        static constexpr size_t SlotCount = 4096;
        static constexpr size_t SlotMask = SlotCount - 1;
        static constexpr int SlotBits = 12; // log2(SlotCount); used to take the HIGH hash bits.
        static_assert(SlotCount == (static_cast<size_t>(1) << SlotBits), "SlotBits must equal log2(SlotCount)");

        // Cap on slots probed per lookup/claim. Exceeding it degrades to "no cell" (the caller pushes
        // without publishing its intent), never to a stall. See TraceContextMap for the full rationale.
        //
        // Like every in-class constant here it has no out-of-line definition, so it must never be
        // ODR-used: never bind it to a reference, never take its address, never pass it to anything taking
        // a reference (Assert::AreEqual included) -- copy it to a local first.
        static constexpr size_t MaxProbes = 64;

        struct Slot
        {
            std::atomic<ThreadID> Key;

            // The span id this thread is about to push, or 0 for "no push in flight". Written by the
            // OWNING managed thread through the raw address CellFor handed it (release), read by the
            // compaction pass on the sampling thread (acquire). Single-writer-per-slot, as everywhere else
            // in this family of maps.
            std::atomic<int64_t> Cell;

            Slot() noexcept : Key(EmptyKey), Cell(0) {}
        };

        // CellFor hands managed code the address of Slot::Cell typed as int64_t*, and the managed side
        // writes it as a plain volatile int64. That is only well defined if the atomic has no extra state
        // and no extra alignment beyond the int64 it wraps -- true for every platform this agent targets,
        // but asserted rather than assumed, because getting it wrong would silently corrupt the adjacent
        // Key field.
        static_assert(sizeof(std::atomic<int64_t>) == sizeof(int64_t),
            "PendingPushMap hands out &Slot::Cell as an int64_t*, so the atomic must be exactly int64-sized");
        static_assert(alignof(std::atomic<int64_t>) == alignof(int64_t),
            "PendingPushMap hands out &Slot::Cell as an int64_t*, so the atomic must be int64-aligned");

        // Same multiplicative hash as TraceContextMap/AgentWorkMap -- see TraceContextMap.h for why the
        // LOW bits must not be used (CLR ThreadIDs are pointer-aligned, so their low bits are always 0).
        static size_t HashOf(ThreadID key) noexcept
        {
            return static_cast<size_t>((static_cast<uint64_t>(key) * 0x9E3779B97F4A7C15ull) >> (64 - SlotBits));
        }

        static const Slot* FindSlot(const Slot* table, ThreadID key) noexcept
        {
            size_t idx = HashOf(key);
            for (size_t probe = 0; probe < MaxProbes; ++probe, idx = (idx + 1) & SlotMask)
            {
                const Slot& slot = table[idx];
                const ThreadID k = slot.Key.load(std::memory_order_acquire);
                if (k == key)
                {
                    return &slot;
                }
                if (k == EmptyKey)
                {
                    return nullptr; // empty slot terminates the chain -> key was never inserted.
                }
                // TombstoneKey or a different live key -> keep probing.
            }
            return nullptr; // probe budget exhausted -> treat as absent, never a stall.
        }

        static Slot* FindSlot(Slot* table, ThreadID key) noexcept
        {
            return const_cast<Slot*>(FindSlot(static_cast<const Slot*>(table), key));
        }

        // Locate the slot for `key`, claiming a free or tombstoned slot if not already present. Sets
        // `claimed` only when this call is what took the slot, so CellFor can tell a first claim (safe to
        // zero the cell) from a repeat lookup (must not touch the cell).
        //
        // Same tombstone-reclaim shape as AgentWorkMap::FindOrClaimSlot: scan the whole chain for the key
        // first -- remembering the earliest tombstone passed -- and only reclaim it once an empty slot
        // confirms the key is absent, so a live slot sitting past an earlier tombstone is never shadowed
        // by a duplicate.
        static Slot* FindOrClaimSlot(Slot* table, ThreadID key, bool& claimed) noexcept
        {
            claimed = false;

            size_t idx = HashOf(key);
            Slot* firstTombstone = nullptr;
            for (size_t probe = 0; probe < MaxProbes; ++probe, idx = (idx + 1) & SlotMask)
            {
                Slot& slot = table[idx];
                ThreadID k = slot.Key.load(std::memory_order_acquire);

                if (k == key)
                {
                    return &slot; // already present -> not a claim.
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

                // Empty slot: the chain ends here, so the key is absent. Prefer reclaiming the earliest
                // tombstone seen; otherwise claim this empty slot.
                if (firstTombstone != nullptr)
                {
                    if (TryClaim(*firstTombstone, TombstoneKey, key, claimed))
                    {
                        return firstTombstone;
                    }
                    firstTombstone = nullptr; // tombstone taken by another key -> fall back to the empty slot.
                }

                if (TryClaim(slot, EmptyKey, key, claimed))
                {
                    return &slot;
                }
                // A different key won this slot; it is now occupied -> keep probing.
            }

            // Probe budget exhausted. If we passed a tombstone, make one last attempt to reclaim it.
            if (firstTombstone != nullptr && TryClaim(*firstTombstone, TombstoneKey, key, claimed))
            {
                return firstTombstone;
            }
            return nullptr; // table effectively full within the probe window -> caller gets no cell.
        }

        // CAS `expectedState` (Empty or Tombstone) to `key`. Returns true if the slot now belongs to
        // `key`, setting `claimed` only when this CAS is what took it -- a slot another thread claimed for
        // the SAME key is usable but is not ours to zero.
        static bool TryClaim(Slot& slot, ThreadID expectedState, ThreadID key, bool& claimed) noexcept
        {
            ThreadID expected = expectedState;
            if (slot.Key.compare_exchange_strong(expected, key, std::memory_order_acq_rel, std::memory_order_relaxed))
            {
                claimed = true;
                return true;
            }
            return expected == key;
        }

        std::atomic<Slot*> _slots{ nullptr };
    };
}}}

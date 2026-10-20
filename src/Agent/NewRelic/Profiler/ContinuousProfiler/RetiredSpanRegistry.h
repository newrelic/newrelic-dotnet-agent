/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
#pragma once
#include <atomic>
#include <cstdint>
#include <cstddef>
#include <new>

#include "TraceContextMap.h"
#include "PendingPushMap.h"

// RetiredSpanRegistry records span ids whose transaction has ENDED, so the continuous-profiling sampler
// can refuse to link a CPU sample to a span that is already dead.
//
// Why this exists as a SEPARATE structure rather than a cross-thread clear of TraceContextMap:
//
//   * A thread's TraceContextMap slot is written only by that thread. Transaction end runs on whichever
//     thread happens to complete (or finalize) the transaction, which in an async pipeline is routinely
//     NOT the thread that pushed the context. That pushing thread's slot therefore never gets cleared,
//     and keeps linking samples to a finished transaction for as long as the thread stays idle
//     (observed live: 272 seconds).
//   * Clearing the pushing thread's slot from the completing thread would violate
//     TraceContextMap's single-writer-per-slot invariant, which is the only reason its seqlock writes
//     are race-free (see TraceContextMap.h). The payload is also 192 bits -- wider than any hardware
//     CAS -- so there is no atomic cross-thread clear available even in principle.
//
// So instead of invalidating the SLOT, this invalidates the VALUE: transaction end inserts the ended
// span ids here, and the sampler's read consults this registry after resolving a slot's value. Nothing
// in this design writes into a TraceContextMap slot from a foreign thread. The invariant is preserved by
// construction.
//
// Keyed on span id ALONE, deliberately, not on the (traceIdHigh, traceIdLow, spanId) triple: the trace id
// a transaction pushes is not stable across its lifetime (inbound distributed-tracing headers accepted
// mid-transaction, and late-resolving activity trace ids, both change what Transaction.TraceId returns),
// so a pair-keyed lookup would miss entries that were pushed under an earlier trace id. Span ids are
// globally unique, immutable once generated, and never reused, so they are a sound key on their own.
// Lookups compare the stored span id exactly, so a hash collision can never cause a wrong accept or a
// wrong reject -- only an extra probe step.
//
// Lifetime and reclamation: an entry is reclaimed ONLY by CompactAgainst (see RetiredSpanRegistry.h's
// companion logic in ContinuousProfiler.h), which proves no live TraceContextMap slot still references
// the value. Never on a time budget, never on capacity pressure -- either of those would reintroduce the
// exact false positive this exists to eliminate, just recast as "ran out of time" or "ran out of room".
//
// Publish-once, not seqlock: unlike TraceContextMap, a slot's payload here is written exactly once (a
// span id is retired once and never modified), so a plain release-store of the State field after the
// payload store is sufficient. Readers acquire-load State and only then read the payload.
namespace NewRelic { namespace Profiler { namespace ContinuousProfiler
{
    class RetiredSpanRegistry
    {
        // The unit test builds span ids that deliberately share a home slot to exercise probing and the
        // overflow path, so it must hash exactly as production does. Friendship lets it call the private
        // HashOf (and track SlotBits) rather than keeping a hand-copied hash that silently goes stale.
        friend class RetiredSpanRegistryTest;

    public:
        RetiredSpanRegistry() noexcept = default;

        ~RetiredSpanRegistry()
        {
            // Only reached at process teardown, when the profiler singleton is destroyed. Deliberately no
            // free on CP stop: a suspend-window reader can be mid-Contains on the sampling thread, and
            // there is no safe point at which a stop can prove otherwise without adding synchronization to
            // the wait-free read path. Holding the table for the process lifetime costs nothing in a
            // process that never enabled CP (it is never allocated there at all).
            delete[] _slots.load(std::memory_order_relaxed);
            delete[] _liveScratch;
            delete[] _pendingScratch;
        }

        RetiredSpanRegistry(const RetiredSpanRegistry&) = delete;
        RetiredSpanRegistry& operator=(const RetiredSpanRegistry&) = delete;

        // Allocate the slot table. Idempotent, and safe to call again on an already-allocated registry
        // (contents are preserved). Called from ContinuousProfiler::Start, never from a hot path or a
        // suspend window -- the table is a couple of MB and is NOT allocated in a process that never
        // starts continuous profiling, matching the lazy-drain-buffer precedent on the managed side.
        // Not thread-safe against a concurrent EnsureAllocated; CP start/stop is already serialized
        // under ContinuousProfilingService's lock.
        bool EnsureAllocated() noexcept
        {
            if (_slots.load(std::memory_order_acquire) != nullptr)
            {
                return true;
            }

            Slot* table = new (std::nothrow) Slot[SlotCount]();
            if (table == nullptr)
            {
                return false; // allocation failure -> registry stays inert; Contains answers false.
            }

            // Compaction's scratch, allocated with the table so a compaction pass never allocates and a
            // process that never starts CP carries neither.
            int64_t* scratch = new (std::nothrow) int64_t[LiveScratchCapacity]();
            if (scratch == nullptr)
            {
                delete[] table;
                return false;
            }

            // Second scratch, for the pending-push half of the live set (see CompactAgainst). Same
            // reasoning: allocated with the table so a pass never allocates, and absent entirely in a
            // process that never starts CP.
            int64_t* pendingScratch = new (std::nothrow) int64_t[PendingScratchCapacity]();
            if (pendingScratch == nullptr)
            {
                delete[] scratch;
                delete[] table;
                return false;
            }

            _liveScratch = scratch;
            _pendingScratch = pendingScratch;
            _slots.store(table, std::memory_order_release); // publish LAST: it is the readiness signal.
            return true;
        }

        // Record that `spanId`'s transaction has ended. Called once per ended span, from whichever thread
        // terminated the transaction -- including the GC finalizer thread for a reaped transaction, so it
        // must never block and never throw. Lock-free and allocation-free.
        //
        // Returns false when the span id is the zero sentinel, when the table was never allocated, or
        // when the probe budget is exhausted with no free or tombstoned slot (overflow). The caller MUST
        // surface an overflow -- a dropped retirement is a missed rejection, i.e. a stale link that
        // survives -- and must never ignore it silently.
        //
        // The two ways a retirement can still be missed by a concurrent reader, both bounded and inherent:
        // an overflow drop (above, counted), and the mid-publish window -- between claiming a slot and
        // release-storing StateOccupied the entry reads as StateClaiming, which Contains treats as not
        // retired. A sample taken in those few microseconds can still link to the just-ended span. Nothing
        // here can close that window without putting synchronization on the suspend-window read path.
        bool TryInsert(int64_t spanId) noexcept
        {
            if (spanId == EmptySpan)
            {
                return false; // zero is "no span"; it must never be retired (every no-context slot is zero).
            }

            Slot* table = _slots.load(std::memory_order_acquire);
            if (table == nullptr)
            {
                return false;
            }

            // Take the insertion sequence BEFORE claiming a slot. CompactAgainst only reclaims entries
            // whose Seq predates the horizon it captured before snapshotting live slots, so an entry
            // inserted concurrently with (or after) that snapshot always survives the pass. Without this,
            // an entry inserted after the snapshot would be tombstoned while a live slot still held its
            // value -- a silent false positive.
            const uint64_t seq = _insertSeq.fetch_add(1, std::memory_order_relaxed);

            // Claim INSIDE the probe loop, exactly as TraceContextMap::FindOrClaimSlot does: a lost CAS
            // means some other span won that slot, so keep probing within the same MaxProbes budget rather
            // than dropping the retirement. Dropping it would be the very stale link this registry exists
            // to eliminate, so overflow is reserved for a genuinely exhausted probe window.
            //
            // A tombstone is only reclaimed once an empty slot proves the span is absent from the chain,
            // so a live entry sitting past an earlier tombstone is never duplicated.
            size_t idx = HashOf(spanId);
            Slot* firstTombstone = nullptr;
            for (size_t probe = 0; probe < MaxProbes; ++probe, idx = (idx + 1) & SlotMask)
            {
                Slot& slot = table[idx];
                const uint32_t state = slot.State.load(std::memory_order_acquire);

                if (state == StateOccupied)
                {
                    if (slot.Span.load(std::memory_order_relaxed) == spanId)
                    {
                        return true; // already retired -> idempotent success, no second entry.
                    }
                    continue; // a different live span -> keep probing.
                }

                if (state == StateClaiming)
                {
                    // Another thread is mid-publish here and its span id is not yet readable, so we cannot
                    // tell whether it is ours -> keep probing. Two threads retiring the SAME span
                    // concurrently can therefore each claim a slot; that is harmless (one extra occupied
                    // slot, never a wrong answer), and compaction must tolerate duplicate entries.
                    continue;
                }

                if (state == StateTombstone)
                {
                    if (firstTombstone == nullptr)
                    {
                        firstTombstone = &slot; // reuse candidate; keep scanning in case the span is live ahead.
                    }
                    continue;
                }

                // StateEmpty: the chain ends here, so the span is absent. Prefer reclaiming the earliest
                // tombstone we passed (keeps chains short); otherwise claim this empty slot.
                if (firstTombstone != nullptr)
                {
                    if (TryClaimAndPublish(*firstTombstone, StateTombstone, spanId, seq))
                    {
                        return true;
                    }
                    firstTombstone = nullptr; // tombstone taken by another span -> fall back to the empty slot.
                }

                if (TryClaimAndPublish(slot, StateEmpty, spanId, seq))
                {
                    return true;
                }
                // A different span won this slot; it is now claimed or occupied -> keep probing.
            }

            // Probe budget exhausted. If we passed a tombstone, make one last attempt to reclaim it.
            if (firstTombstone != nullptr && TryClaimAndPublish(*firstTombstone, StateTombstone, spanId, seq))
            {
                return true;
            }

            _overflowCount.fetch_add(1, std::memory_order_relaxed);
            return false; // probe window genuinely full of live entries -> caller must log/count this.
        }

        // Has this span's transaction ended? Called by the SAMPLER for each sampled thread while that
        // thread is SUSPENDED, so it must be wait-free, lock-free, allocation-free, and must make no CLR
        // calls and read no clock. It is a bounded probe of at most MaxProbes acquire-loads -- the same
        // computational shape as the TraceContextMap slot probe that immediately precedes it.
        //
        // Answers false for the zero sentinel and for a registry that was never allocated (a process that
        // never started continuous profiling), so the read path is safe before any Start.
        bool Contains(int64_t spanId) const noexcept
        {
            if (spanId == EmptySpan)
            {
                return false;
            }

            const Slot* table = _slots.load(std::memory_order_acquire);
            if (table == nullptr)
            {
                return false;
            }

            size_t idx = HashOf(spanId);
            for (size_t probe = 0; probe < MaxProbes; ++probe, idx = (idx + 1) & SlotMask)
            {
                const Slot& slot = table[idx];
                const uint32_t state = slot.State.load(std::memory_order_acquire);

                if (state == StateEmpty)
                {
                    return false; // empty terminates the chain -> never inserted.
                }

                if (state == StateOccupied && slot.Span.load(std::memory_order_relaxed) == spanId)
                {
                    return true; // exact match on a live entry -> retired.
                }

                // Tombstone, mid-publish, or a different span -> keep probing. A tombstone must NOT
                // terminate the scan: a live entry can sit past a reclaimed one.
            }

            return false; // probe budget exhausted -> treat as not retired, never a stall.
        }

        // Reclaim registry capacity by PROVING no live TraceContextMap slot still references an entry.
        // Never reclaims on a time budget and never on capacity pressure: either of those would
        // reintroduce the same false positive this structure exists to eliminate, recast as "ran out of
        // time" or "ran out of room".
        //
        // Runs on the continuous-profiling sampling thread, off the hot path, outside any suspend window,
        // at a fixed low-frequency cadence (see ContinuousProfiler::CompactionIntervalMs). Not wait-free.
        // O(TraceContextMap slot count + registry slot count) atomic loads plus one bounded scan of the
        // snapshot per candidate entry.
        //
        // SINGLE-THREADED: never called concurrently with itself. That is what makes the load-then-store
        // of State below safe without a CAS -- compaction is the only writer that moves a slot out of
        // StateOccupied, so between the load and the store no other thread can change that slot's state.
        // Two concurrent passes would also double-count _reclaimedCount. The caller (Task 3's tick loop)
        // owns this invariant; it is not enforced here.
        //
        // Ordering that makes this safe: the insertion-sequence horizon is captured BEFORE the live-slot
        // snapshot, and only entries whose Seq predates that horizon are eligible. An entry inserted
        // during or after the snapshot therefore always survives to the next pass. Without that, this
        // sequence would silently produce a stale link: snapshot runs -> a thread pushes span S ->
        // S's transaction ends and retires S -> this pass tombstones S's brand-new entry because S was
        // absent from the snapshot -> the pushing thread's slot keeps linking to S forever.
        //
        // What that horizon does and does not prove. For an entry older than the horizon, either the
        // snapshot saw its span still sitting in a slot (in which case the entry is kept) or no slot held
        // it at snapshot time. The second case is where the proof stops: it establishes that no slot HELD
        // the span, NOT that no slot is about to.
        //
        // RESIDUAL WINDOW: a thread can be between CHOOSING a span id and PUBLISHING it -- between
        // WrapperService.PushContinuousProfilingContext reading Segment.SpanId and the resulting
        // TraceContextMap::WriteSlot completing -- and that intent is observable in no slot at all. The
        // slot reads either Span == 0 (claimed but not yet written) or the thread's PREVIOUS span, and
        // both look exactly like "this thread does not reference the span". So if the span was retired
        // before the horizon and no other slot holds it, this pass reclaims the entry and the push then
        // publishes a link with no entry left to reject it -- the same stale link this registry exists to
        // eliminate. Reached by the ordinary IsFinished-then-push race in WrapperService (the transaction
        // can end between the IsFinished check and the push); no finalizer or timeout path is required.
        //
        // Typical width is sub-microsecond, but the TAIL IS UNBOUNDED, so this must not be read as a
        // bounded window: the thread can be preempted, blocked behind a gen2 GC, or paged out anywhere
        // inside it, and the P/Invoke transition on the push is itself an entry into GC-preemptive mode.
        //
        // TWO NARROWING MECHANISMS, NEITHER OF WHICH CLOSES IT -- and they do not close it together
        // either. Do not read either of them as a proof.
        //
        //   1. The live set is the UNION of published TraceContextMap slots AND PendingPushMap cells, so a
        //      thread that has published its INTENT to push a span (before starting the push) is counted as
        //      referencing it. That removes the P/Invoke transition into GC-preemptive mode and the native
        //      probe/write from the vulnerable window, leaving only the managed straight-line hex decode
        //      between choosing the span id and publishing the intent. Narrower, not absent.
        //
        //      The union is ADDITIVE: a cell never REPLACES the slot-based set. The managed push path
        //      returns early on a ReferenceEquals dedupe before writing any cell, and that is sound only
        //      because the slot already holds that span from an earlier push in the same epoch.
        //
        //   2. Two-pass reclamation: an entry is reclaimed only on the SECOND CONSECUTIVE pass that finds
        //      it both unreferenced and older than that pass's own horizon (see Slot::SeenUnreferenced).
        //      This forces an in-flight push to stay unpublished across two whole compaction intervals
        //      rather than merely losing a sub-microsecond race. It is a narrowing of the odds, NOT a proof
        //      of absence -- a thread stalled longer than 2 x CompactionIntervalMs still reaches the bug.
        //
        // Provably closing it requires the push path to publish its intent BEFORE the span id is chosen or
        // decoded, i.e. a managed-supplied in-flight SET rather than a single native cell. A snapshot-only
        // proof provably cannot, because it can only ever observe published state. That closure is
        // deliberately out of scope here.
        void CompactAgainst(const TraceContextMap& liveSlots, const PendingPushMap& pendingPushes) noexcept
        {
            Slot* table = _slots.load(std::memory_order_acquire);
            if (table == nullptr)
            {
                return;
            }

            // Horizon first, snapshots second. See the ordering note above.
            const uint64_t horizon = _insertSeq.load(std::memory_order_acquire);

            size_t liveCount = 0;
            size_t pendingCount = 0;
            if (!liveSlots.SnapshotLiveSpanIds(_liveScratch, LiveScratchCapacity, liveCount)
                || !pendingPushes.SnapshotPending(_pendingScratch, PendingScratchCapacity, pendingCount))
            {
                // Could not observe live state completely -> reclaim nothing AND mark nothing. An aborted
                // pass must not count as a clean pass for any entry, or two aborted-then-clean passes could
                // reclaim on what is really a single observation. Retrying on the next cadence is always
                // correct; guessing never is.
                _abortedCompactionCount.fetch_add(1, std::memory_order_relaxed);
                return;
            }

            for (size_t i = 0; i < SlotCount; ++i)
            {
                Slot& slot = table[i];

                if (slot.State.load(std::memory_order_acquire) != StateOccupied)
                {
                    continue;
                }

                if (slot.Seq.load(std::memory_order_relaxed) >= horizon)
                {
                    continue; // inserted at or after the snapshot -> not eligible this pass.
                }

                const int64_t span = slot.Span.load(std::memory_order_relaxed);
                bool referenced = false;
                for (size_t j = 0; j < liveCount; ++j)
                {
                    if (_liveScratch[j] == span)
                    {
                        referenced = true;
                        break;
                    }
                }
                for (size_t j = 0; !referenced && j < pendingCount; ++j)
                {
                    if (_pendingScratch[j] == span)
                    {
                        referenced = true; // a thread is about to push it -> as live as an already-pushed one.
                        break;
                    }
                }

                if (referenced)
                {
                    // Clear the mark the INSTANT the entry is seen referenced again, so an entry that
                    // oscillates between referenced and unreferenced can never accumulate two
                    // non-consecutive strikes and be reclaimed on them.
                    slot.SeenUnreferenced.store(0, std::memory_order_relaxed);
                    continue; // a live slot (or a pending push) still holds this span -> the entry stays.
                }

                if (slot.SeenUnreferenced.load(std::memory_order_relaxed) == 0)
                {
                    // First consecutive clean observation: mark and keep. The horizon is re-checked
                    // independently above on every pass, so the second pass proves eligibility afresh
                    // rather than inheriting this pass's conclusion.
                    slot.SeenUnreferenced.store(1, std::memory_order_relaxed);
                    continue;
                }

                // Second consecutive clean observation. Tombstone rather than zeroing back to Empty: a
                // live entry can sit past this one in a probe chain, and Empty terminates a chain (see
                // Contains).
                slot.State.store(StateTombstone, std::memory_order_release);
                _reclaimedCount.fetch_add(1, std::memory_order_relaxed);
            }
        }

        uint64_t ReclaimedCountForTesting() const noexcept
        {
            return _reclaimedCount.load(std::memory_order_relaxed);
        }

        uint64_t AbortedCompactionCountForTesting() const noexcept
        {
            return _abortedCompactionCount.load(std::memory_order_relaxed);
        }

        uint64_t OverflowCount() const noexcept
        {
            return _overflowCount.load(std::memory_order_relaxed);
        }

        size_t OccupiedCountForTesting() const noexcept
        {
            const Slot* table = _slots.load(std::memory_order_acquire);
            if (table == nullptr)
            {
                return 0;
            }

            size_t occupied = 0;
            for (size_t i = 0; i < SlotCount; ++i)
            {
                if (table[i].State.load(std::memory_order_acquire) == StateOccupied)
                {
                    ++occupied;
                }
            }
            return occupied;
        }

    private:
        // Zero is the "no span id" value the managed side decomposes a missing/malformed id to, and is
        // also what an unwritten slot holds, so it is reserved and never a real key.
        static const int64_t EmptySpan = 0;

        static const uint32_t StateEmpty = 0;
        static const uint32_t StateClaiming = 1;  // slot claimed, payload not yet published.
        static const uint32_t StateOccupied = 2;
        static const uint32_t StateTombstone = 3; // reclaimed by compaction; reusable, does not end a chain.

        // LOAD-BEARING INVARIANT: a slot NEVER transitions back to StateEmpty once it has left it. Both
        // Contains and TryInsert stop scanning a chain at the first StateEmpty slot, so re-emptying a slot
        // in the middle of a chain would sever every entry behind it -- silently turning live retirements
        // into "not retired" across the whole table. Reclamation (compaction) must therefore publish
        // StateTombstone, which is reusable but does NOT terminate a chain. Nothing here ever writes
        // StateEmpty after construction; future reclamation code must keep it that way.

        // Sized for the number of retirements that can accumulate before a slot can be reclaimed, which is
        // (ended spans per second) x (compaction interval) x (passes needed to reclaim), NOT the number of
        // concurrently-live threads. Two-pass reclamation is part of that product: an entry now survives at
        // least two passes, so the budget is two compaction intervals' worth of retirements, not one.
        //
        // At the fixed 5-second cadence that is a 10-second accumulation window, so this tolerates ~6,500
        // ended spans per second sustained before the overflow path can be reached -- still far above any
        // workload this agent has been measured against. The real cadence is also floored by the sampling
        // interval (compaction runs on the first tick after each deadline), so at the default 10-second
        // interval the window is nearer 20 seconds and the sustained figure nearer ~3,200/s. Overflow is
        // counted and warned about rather than assumed unreachable. 65536 slots x 32 bytes is ~2 MB,
        // allocated only in a process that actually starts continuous profiling.
        static const size_t SlotBits = 16;
        static const size_t SlotCount = static_cast<size_t>(1) << SlotBits;
        static const size_t SlotMask = SlotCount - 1;

        // Cap on how many slots any single lookup or insert probes before giving up, mirroring
        // TraceContextMap::MaxProbes. Bounds the suspend-window reader's cost to O(MaxProbes) acquire
        // loads regardless of table state. Exceeding it degrades to "not retired" (a missed rejection,
        // counted on the insert side) -- never to a stall.
        static const size_t MaxProbes = 64;

        struct Slot
        {
            std::atomic<uint32_t> State;
            std::atomic<int64_t> Span;
            std::atomic<uint64_t> Seq;

            // 1 once a compaction pass has observed this entry as unreferenced-and-eligible; reclaimed only
            // on the NEXT pass that observes the same thing again (see CompactAgainst's two-pass rule).
            // Reset to 0 by any pass that observes the entry referenced, and by TryClaimAndPublish when the
            // slot is (re)claimed.
            //
            // Deliberately a SEPARATE field rather than a fourth State value. State must stay StateOccupied
            // for a marked entry, or two things break silently: Contains would stop answering true for a
            // span that is still retired (a stale link -- the exact bug this registry exists to eliminate),
            // and TryInsert would treat the slot as claimable and hand it to a different span while the
            // marked entry is still live.
            //
            // Written only by the compaction pass (single-threaded, see CompactAgainst) and by
            // TryClaimAndPublish under the claim CAS, so relaxed ordering is sufficient: the State
            // acquire/release pair that publishes the entry also orders this field for every reader.
            std::atomic<uint32_t> SeenUnreferenced;

            Slot() noexcept : State(StateEmpty), Span(EmptySpan), Seq(0), SeenUnreferenced(0) {}
        };

        // Claim one slot and publish `spanId` into it. `expectedState` is the free state the caller
        // observed (StateEmpty or StateTombstone); the CAS to StateClaiming is what makes the claim
        // exclusive. Returns false if another thread won the slot first, which leaves the slot untouched
        // and lets the caller keep probing. The payload stores are relaxed because the release-store of
        // StateOccupied publishes them: any reader that observes Occupied via an acquire-load sees both.
        static bool TryClaimAndPublish(Slot& slot, uint32_t expectedState, int64_t spanId, uint64_t seq) noexcept
        {
            uint32_t expected = expectedState;
            if (!slot.State.compare_exchange_strong(expected, StateClaiming,
                    std::memory_order_acq_rel, std::memory_order_relaxed))
            {
                return false;
            }

            slot.Span.store(spanId, std::memory_order_relaxed);
            slot.Seq.store(seq, std::memory_order_relaxed);
            // Clear the two-pass mark: this slot may be a TOMBSTONE reclaimed from an entry that a previous
            // pass had already marked, and inheriting that mark would let this brand-new entry be reclaimed
            // on its very first clean pass -- a silent one-pass reclamation, exactly what two-pass exists to
            // prevent. Ordered before the release-store of StateOccupied so any pass that sees the entry
            // sees the cleared mark with it.
            slot.SeenUnreferenced.store(0, std::memory_order_relaxed);
            slot.State.store(StateOccupied, std::memory_order_release);
            return true;
        }

        // Knuth multiplicative hash folded to a slot index. The mixing lands in the HIGH bits of the
        // product, so take the top SlotBits rather than masking the low ones.
        static size_t HashOf(int64_t spanId) noexcept
        {
            return static_cast<size_t>((static_cast<uint64_t>(spanId) * 0x9E3779B97F4A7C15ull) >> (64 - SlotBits));
        }

        std::atomic<Slot*> _slots{ nullptr };
        std::atomic<uint64_t> _insertSeq{ 1 };
        std::atomic<uint64_t> _overflowCount{ 0 };

        // Reused scratch for the live-slot snapshot, sized to TraceContextMap's own slot count -- the
        // ceiling on how many distinct spans can be referenced at once. Reused across passes so a
        // compaction never allocates; only ever touched by the sampling thread, serialized by the tick
        // loop. Allocated in EnsureAllocated alongside the slot table, NOT as an inline array member, so a
        // process that never enables continuous profiling carries neither (an inline array would put 32 KB
        // in every process for an off-by-default feature -- the same tax the managed side's lazy drain
        // buffer was introduced to remove).
        //
        // MUST stay >= TraceContextMap::SlotCount, which is private there and so cannot be asserted
        // against here; if that count is ever raised above 4096, raise this with it. The behavioural
        // consequence of getting it wrong is covered, not silent: an undersized buffer fails the snapshot,
        // which aborts the pass and reclaims nothing.
        static const size_t LiveScratchCapacity = 4096;
        int64_t* _liveScratch{ nullptr };

        // Reused scratch for the pending-push snapshot, sized to PendingPushMap's own slot count -- the
        // ceiling on how many threads can have a push in flight at once. Same allocation and ownership
        // discipline as _liveScratch above.
        //
        // MUST stay >= PendingPushMap::SlotCount, which is private there and so cannot be asserted against
        // here; if that count is ever raised above 4096, raise this with it. As with _liveScratch the
        // consequence of getting it wrong is covered, not silent: an undersized buffer fails the snapshot,
        // which aborts the pass and reclaims nothing.
        static const size_t PendingScratchCapacity = 4096;
        int64_t* _pendingScratch{ nullptr };

        std::atomic<uint64_t> _reclaimedCount{ 0 };
        std::atomic<uint64_t> _abortedCompactionCount{ 0 };
    };
}}}

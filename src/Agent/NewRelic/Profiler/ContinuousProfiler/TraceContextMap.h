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

// TraceContextMap stores the CURRENTLY-ACTIVE distributed-tracing context (traceId hi/lo + spanId) for
// each managed thread, keyed by CLR ThreadID. It exists to solve one hard constraint:
//
//   * SetTraceContext / ResetTraceContext are called from ARBITRARY managed app threads (Task 8 wires
//     the extern "C" exports that reach here).
//   * The continuous-profiling sampler READS this context for each sampled thread while the CLR is
//     SUSPENDED -- i.e. while every app thread (including one that may be mid-write here) is frozen.
//
// A conventional mutex-guarded map is therefore a DEADLOCK: an app thread suspended while holding the
// map lock would block the sampler forever. So this structure is entirely LOCK-FREE and, critically,
// its reader NEVER SPINS waiting on a writer. It uses a fixed-size open-addressing table of atomic
// slots plus a per-slot seqlock, but the reader treats a torn/in-progress read as simply "no context
// for this thread" (writes zeros -> Plan A's PprofProfileBuilder emits LinkIndex=0 / no link) rather
// than retrying. Missing a link for one sample of one thread is acceptable; a hang is not.
//
// Key = CLR ThreadID (a pointer-sized UINT_PTR). The setter captures it via
// ICorProfilerInfo::GetCurrentThreadID(); the sampler looks up by the SAME ThreadID it already holds
// from EnumThreads. Keying on the CLR ThreadID (rather than the OS/Win32 thread id) puts both sides in
// ONE id space with no dependency on GetCurrentThreadId() agreeing with the pdwWin32ThreadId that
// GetThreadInfo resolves -- OTel's proven ThreadSpanContextMap uses this same key. The OS thread id is
// still resolved separately for the sample's thread.id attribute; only this map's KEY is the ThreadID.
namespace NewRelic { namespace Profiler { namespace ContinuousProfiler
{
    struct TraceContext
    {
        int64_t TraceIdHigh{ 0 };
        int64_t TraceIdLow{ 0 };
        int64_t SpanId{ 0 };
    };

    class TraceContextMap
    {
        // The unit test builds keys that deliberately share a home slot to exercise probing / tombstone
        // reclaim, so it must hash exactly as production does. Friendship lets it call the private HashOf
        // (and thus track SlotBits) rather than keeping a hand-copied hash that silently goes stale.
        friend class TraceContextMapTest;

    public:
        // Store (or overwrite) the calling thread's active context. Called from an app thread. Uses a
        // seqlock write: bump the slot seq to odd (write in progress), publish the three int64s, then
        // bump to even (complete). Lock-free -- no app thread ever blocks another, and a suspend that
        // freezes this thread mid-write leaves the slot at an odd seq, which the reader treats as "none".
        void Set(ThreadID threadId, int64_t hi, int64_t lo, int64_t span) noexcept
        {
            if (threadId == EmptyKey || threadId == TombstoneKey)
            {
                return; // reserve 0 and all-ones as slot sentinels; a real ThreadID is never either.
            }

            Slot* slot = FindOrClaimSlot(threadId);
            if (slot == nullptr)
            {
                return; // table full -> silently drop; this thread's samples simply carry no link.
            }

            WriteSlot(*slot, hi, lo, span, _generation.load(std::memory_order_relaxed));
        }

        // Clear the calling thread's context (transaction/segment ended) AND free its slot for reuse.
        // First publishes zeros under the seqlock so any read that still matches this Key returns "no
        // context", then tombstones the slot (Key -> TombstoneKey) so a later distinct ThreadID can
        // reclaim it. Freeing is what keeps the open-addressed table from filling permanently over a
        // process lifetime of thread-pool churn -- without it, every distinct ThreadID that ever pushed a
        // context would consume a slot forever. Single-writer-per-slot (the slot's Key is this calling
        // thread's own ThreadID) makes the plain-store tombstone safe: no other thread writes this slot's
        // Key while we own it -- other threads only ever CAS a slot whose Key is Empty or Tombstone.
        void Reset(ThreadID threadId) noexcept
        {
            if (threadId == EmptyKey || threadId == TombstoneKey)
            {
                return;
            }

            Slot* slot = FindSlot(threadId);
            if (slot == nullptr)
            {
                return; // never set for this thread -> nothing to clear.
            }

            // Zero the payload under the seqlock BEFORE tombstoning, so a reader that observes the old Key
            // (tombstone store not yet visible to it) reads zeros -> no link, never stale context. The
            // release on the Key store below then hands the zeroed, seq-even slot to whichever thread later
            // reclaims it, so that thread's relaxed seq load in WriteSlot still sees a settled value.
            WriteSlot(*slot, 0, 0, 0, _generation.load(std::memory_order_relaxed));
            slot->Key.store(TombstoneKey, std::memory_order_release);
        }

        // Read the context stored for a CLR ThreadID. Called by the SAMPLER while the runtime is
        // suspended -- so it must be wait-free. Returns false (out set to zeros) if the thread has no
        // slot, has zero context, or the seqlock indicates a torn/in-progress write (odd or changed
        // seq). The reader performs a SINGLE recheck, never a spin loop: a suspended mid-write writer
        // must never be able to hang the reader.
        bool TryGet(ThreadID threadId, TraceContext& out) const noexcept
        {
            out = TraceContext{};

            if (threadId == EmptyKey || threadId == TombstoneKey)
            {
                return false;
            }

            const Slot* slot = FindSlot(threadId);
            if (slot == nullptr)
            {
                return false;
            }

            const uint32_t seqBefore = slot->Seq.load(std::memory_order_acquire);
            if ((seqBefore & 1u) != 0u)
            {
                return false; // write in progress (possibly on a now-suspended thread) -> treat as none.
            }

            TraceContext value;
            value.TraceIdHigh = slot->Hi.load(std::memory_order_relaxed);
            value.TraceIdLow = slot->Lo.load(std::memory_order_relaxed);
            value.SpanId = slot->Span.load(std::memory_order_relaxed);
            const uint64_t gen = slot->Gen.load(std::memory_order_relaxed);

            // Pairs with the writer's release fence in WriteSlot: an acquire-load alone on seqAfter
            // does not stop the relaxed value loads above from being reordered after it on a weak
            // memory model (e.g. arm64), which could observe a torn read even though seq looks stable.
            // This fence forces the value loads to complete before the seq recheck below.
            std::atomic_thread_fence(std::memory_order_acquire);

            const uint32_t seqAfter = slot->Seq.load(std::memory_order_acquire);
            if (seqAfter != seqBefore)
            {
                return false; // slot changed under us -> torn read, treat as none (no spin/retry).
            }

            if (gen != _generation.load(std::memory_order_relaxed))
            {
                return false; // stamped by an earlier profiling session (or never written) -> no link.
            }

            if (value.TraceIdHigh == 0 && value.TraceIdLow == 0 && value.SpanId == 0)
            {
                return false; // reset/never-set context -> no link.
            }

            out = value;
            return true;
        }

        // Invalidate every context currently stored in the map without touching a single slot.
        //
        // Called when continuous profiling (re)starts. Managed-side trace-context resets can be orphaned
        // across a stop/start cycle, leaving slots that hold a context from the previous session while
        // their owning threads are still live -- so the sampler could ship those stale (traceId, spanId)
        // links on fresh profile data until each owning thread happens to call Set again.
        //
        // A bulk per-slot clear is NOT an option: WriteSlot's relaxed payload stores are only sound under
        // the single-writer-per-slot invariant (a slot is written solely by the thread that owns its Key).
        // Writing slots we do not own could publish a seq-stable slot whose fields are a mix of two
        // writers'. Instead every write stamps the generation it was made in and TryGet accepts a slot only
        // if its stamp matches the current generation, so one relaxed increment retires the entire previous
        // session's contents. Safe to call concurrently with in-flight readers and writers: it touches no
        // slot, and a writer that reads the pre-increment generation just publishes a context that reads as
        // absent -- the same outcome as the stale entry this exists to suppress.
        void NewGeneration() noexcept
        {
            _generation.fetch_add(1, std::memory_order_relaxed);
        }

    private:
        // 0 is reserved as the empty-slot sentinel. A valid CLR ThreadID is never 0.
        static constexpr ThreadID EmptyKey = 0;

        // All-ones is reserved as the tombstone sentinel: a slot whose owning thread's context was Reset
        // and is now free for a different ThreadID to reclaim. A valid CLR ThreadID (a pointer-sized value)
        // is never all-ones. Distinct from EmptyKey because a tombstone must NOT terminate a probe chain
        // (a live key may sit past it), whereas an empty slot does terminate the chain.
        static constexpr ThreadID TombstoneKey = static_cast<ThreadID>(~static_cast<uint64_t>(0));

        // Fixed slot count. Power of two so the hash maps with a mask. Sized well above the number of
        // threads a process realistically parks a trace context on. Slots are freed (tombstoned) by Reset
        // when a thread's transaction ends and reclaimed by any later distinct ThreadID, so thread-pool
        // churn does not grow the table without bound. The ceiling still bounds total memory to a few KB
        // and keeps every operation allocation-free (safe to touch on any thread at any time).
        static constexpr size_t SlotCount = 4096;
        static constexpr size_t SlotMask = SlotCount - 1;
        static constexpr int SlotBits = 12; // log2(SlotCount); used to take the HIGH hash bits.
        static_assert(SlotCount == (static_cast<size_t>(1) << SlotBits), "SlotBits must equal log2(SlotCount)");

        // Cap on how many slots any single lookup/claim probes before giving up. Bounds the READER's cost
        // inside the suspend window to O(MaxProbes) atomic loads per thread regardless of table state -- the
        // pre-fix code probed all SlotCount slots on a miss, which under a degraded/full table would scan
        // 4096 slots per sampled thread every tick while the runtime is stopped. With the high-bit hash and
        // realistic live-thread counts the true chain length is a handful; the bound only caps pathological
        // clustering. A key is always found within MaxProbes because it is inserted within MaxProbes of its
        // home (claim and lookup share this bound). Exceeding it degrades gracefully to "no link" / "drop",
        // which the design already tolerates -- never to a stall.
        static constexpr size_t MaxProbes = 64;

        struct Slot
        {
            std::atomic<ThreadID> Key{ EmptyKey };
            std::atomic<uint32_t> Seq{ 0 };
            std::atomic<int64_t> Hi{ 0 };
            std::atomic<int64_t> Lo{ 0 };
            std::atomic<int64_t> Span{ 0 };

            // Profiling session this slot's payload was published in; part of the seqlock-protected payload.
            // 0 means "never written", which never matches a live generation (those start at 1), so a
            // default-constructed slot is rejected by TryGet on the generation check alone. See NewGeneration.
            std::atomic<uint64_t> Gen{ 0 };
        };

        // Knuth multiplicative hash folded to a slot index. The mixing in a multiplicative hash lands in the
        // HIGH bits of the product, so we take the top SlotBits (>> (64 - SlotBits)). Masking the LOW bits
        // instead (the previous bug) discarded the mixing entirely: CLR ThreadIDs are >=16-byte-aligned
        // pointers, so their low 4 bits are always 0, which collapsed the reachable home buckets to just
        // 1/16th of the table (256 of 4096) and defeated the whole point of hashing.
        static size_t HashOf(ThreadID key) noexcept
        {
            return static_cast<size_t>((static_cast<uint64_t>(key) * 0x9E3779B97F4A7C15ull) >> (64 - SlotBits));
        }

        // Locate an existing slot for `key` via linear probing. Returns nullptr if not present. Const so
        // the suspend-window reader can call it. A tombstone does NOT terminate the scan (a live key may sit
        // beyond a freed slot); only an empty slot does. Bounded by MaxProbes so the suspend-window cost is
        // O(MaxProbes) even under a degraded table.
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
                    return nullptr; // hit an empty slot -> key was never inserted (empty terminates the chain).
                }
                // TombstoneKey or a different key -> keep probing.
                idx = (idx + 1) & SlotMask;
            }
            return nullptr; // probe budget exhausted -> treat as absent (no link), never a stall.
        }

        Slot* FindSlot(ThreadID key) noexcept
        {
            return const_cast<Slot*>(static_cast<const TraceContextMap*>(this)->FindSlot(key));
        }

        // Locate the slot for `key`, claiming a free slot for it if not already present. Called only from
        // writer (app-thread) context. A CAS race between two threads claiming different keys is fine; the
        // loser keeps probing. Returns nullptr only if the probe budget is exhausted without a free slot.
        //
        // Tombstone reuse: a slot freed by Reset can be reclaimed here. Because a given key is only ever
        // inserted by its own owning thread (single-writer-per-key), the key can appear in at most one slot,
        // and no other thread can insert it behind us. So we scan the whole chain for the key first --
        // remembering the earliest tombstone we pass -- and only reclaim that tombstone once we confirm the
        // key is absent (we hit an empty slot, which terminates the chain). This avoids ever creating a
        // duplicate entry for a key whose live slot sits past an earlier tombstone.
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

                // Empty slot: the chain ends here, so the key is absent. Prefer reclaiming the earliest
                // tombstone we saw; otherwise claim this empty slot.
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
            return nullptr; // table effectively full within the probe window -> caller drops (no link).
        }

        // Publish a value into a slot under the per-slot seqlock. Writer-only.
        static void WriteSlot(Slot& slot, int64_t hi, int64_t lo, int64_t span, uint64_t generation) noexcept
        {
            // Relaxed load is sound here only because of the single-writer-per-slot invariant: the
            // slot's key is the writing thread's own CLR ThreadID, so no other thread ever writes this
            // slot's Seq concurrently -- there is nothing else to synchronize with on this load.
            const uint32_t seq = slot.Seq.load(std::memory_order_relaxed);
            slot.Seq.store(seq | 1u, std::memory_order_release);       // mark write in progress (odd).
            std::atomic_thread_fence(std::memory_order_release);
            slot.Hi.store(hi, std::memory_order_relaxed);
            slot.Lo.store(lo, std::memory_order_relaxed);
            slot.Span.store(span, std::memory_order_relaxed);
            slot.Gen.store(generation, std::memory_order_relaxed);
            slot.Seq.store((seq | 1u) + 1u, std::memory_order_release); // mark complete (even, advanced).
        }

        std::array<Slot, SlotCount> _slots{};

        // Current profiling session, bumped by NewGeneration. Starts at 1 so it can never equal the 0 a
        // never-written slot carries.
        std::atomic<uint64_t> _generation{ 1 };
    };
}}}

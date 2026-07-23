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
    public:
        // Store (or overwrite) the calling thread's active context. Called from an app thread. Uses a
        // seqlock write: bump the slot seq to odd (write in progress), publish the three int64s, then
        // bump to even (complete). Lock-free -- no app thread ever blocks another, and a suspend that
        // freezes this thread mid-write leaves the slot at an odd seq, which the reader treats as "none".
        void Set(ThreadID threadId, int64_t hi, int64_t lo, int64_t span) noexcept
        {
            if (threadId == EmptyKey)
            {
                return; // reserve 0 as the "empty slot" sentinel; a real ThreadID is never 0.
            }

            Slot* slot = FindOrClaimSlot(threadId);
            if (slot == nullptr)
            {
                return; // table full -> silently drop; this thread's samples simply carry no link.
            }

            WriteSlot(*slot, hi, lo, span);
        }

        // Clear the calling thread's context (transaction/segment ended). Publishes zeros under the same
        // seqlock so a subsequent read returns "no context". The slot is left claimed (Key stays set) so
        // a thread that repeatedly starts/ends transactions reuses one slot rather than exhausting the
        // table; only genuinely distinct ThreadIDs consume new slots.
        void Reset(ThreadID threadId) noexcept
        {
            if (threadId == EmptyKey)
            {
                return;
            }

            Slot* slot = FindSlot(threadId);
            if (slot == nullptr)
            {
                return; // never set for this thread -> nothing to clear.
            }

            WriteSlot(*slot, 0, 0, 0);
        }

        // Read the context stored for a CLR ThreadID. Called by the SAMPLER while the runtime is
        // suspended -- so it must be wait-free. Returns false (out set to zeros) if the thread has no
        // slot, has zero context, or the seqlock indicates a torn/in-progress write (odd or changed
        // seq). The reader performs a SINGLE recheck, never a spin loop: a suspended mid-write writer
        // must never be able to hang the reader.
        bool TryGet(ThreadID threadId, TraceContext& out) const noexcept
        {
            out = TraceContext{};

            if (threadId == EmptyKey)
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

            if (value.TraceIdHigh == 0 && value.TraceIdLow == 0 && value.SpanId == 0)
            {
                return false; // reset/never-set context -> no link.
            }

            out = value;
            return true;
        }

    private:
        // 0 is reserved as the empty-slot sentinel. A valid CLR ThreadID is never 0.
        static constexpr ThreadID EmptyKey = 0;

        // Fixed slot count. Power of two so the hash maps with a mask. Sized well above the number of
        // threads a process realistically parks a trace context on; slots are reclaimed by ThreadID reuse
        // in Reset (see above) and by re-Set overwriting the same ThreadID, so churn does not grow the
        // table. Open addressing means a claimed slot is never freed, but the ceiling bounds total memory
        // to a few KB and keeps every operation allocation-free (safe to touch on any thread at any time).
        static constexpr size_t SlotCount = 4096;
        static constexpr size_t SlotMask = SlotCount - 1;

        struct Slot
        {
            std::atomic<ThreadID> Key{ EmptyKey };
            std::atomic<uint32_t> Seq{ 0 };
            std::atomic<int64_t> Hi{ 0 };
            std::atomic<int64_t> Lo{ 0 };
            std::atomic<int64_t> Span{ 0 };
        };

        // Cheap integer hash (Knuth multiplicative) folded to the slot index. ThreadID is pointer-sized,
        // so mix with the 64-bit Knuth constant (falls back cleanly on 32-bit where size_t is 32-bit).
        static size_t HashOf(ThreadID key) noexcept
        {
            return static_cast<size_t>((static_cast<uint64_t>(key) * 0x9E3779B97F4A7C15ull) & SlotMask);
        }

        // Locate an existing slot for `key` via linear probing. Returns nullptr if not present. Const so
        // the suspend-window reader can call it. Probes at most SlotCount slots then gives up.
        const Slot* FindSlot(ThreadID key) const noexcept
        {
            size_t idx = HashOf(key);
            for (size_t probe = 0; probe < SlotCount; ++probe)
            {
                const Slot& slot = _slots[idx];
                const ThreadID k = slot.Key.load(std::memory_order_acquire);
                if (k == key)
                {
                    return &slot;
                }
                if (k == EmptyKey)
                {
                    return nullptr; // hit an empty slot -> key was never inserted.
                }
                idx = (idx + 1) & SlotMask;
            }
            return nullptr;
        }

        Slot* FindSlot(ThreadID key) noexcept
        {
            return const_cast<Slot*>(static_cast<const TraceContextMap*>(this)->FindSlot(key));
        }

        // Locate the slot for `key`, claiming a free slot for it if not already present. Called only from
        // writer (app-thread) context, so a CAS race between two threads claiming different keys is fine;
        // the loser simply advances to the next probe slot. Returns nullptr only if the table is full.
        Slot* FindOrClaimSlot(ThreadID key) noexcept
        {
            size_t idx = HashOf(key);
            for (size_t probe = 0; probe < SlotCount; ++probe)
            {
                Slot& slot = _slots[idx];
                ThreadID k = slot.Key.load(std::memory_order_acquire);
                if (k == key)
                {
                    return &slot;
                }
                if (k == EmptyKey)
                {
                    // Try to claim this empty slot for our key.
                    ThreadID expected = EmptyKey;
                    if (slot.Key.compare_exchange_strong(expected, key, std::memory_order_acq_rel))
                    {
                        return &slot;
                    }
                    // Lost the race; `expected` now holds the winner's key.
                    if (expected == key)
                    {
                        return &slot; // another thread claimed it for the SAME key -> reuse it.
                    }
                    // Different key won this slot; keep probing.
                }
                idx = (idx + 1) & SlotMask;
            }
            return nullptr;
        }

        // Publish a value into a slot under the per-slot seqlock. Writer-only.
        static void WriteSlot(Slot& slot, int64_t hi, int64_t lo, int64_t span) noexcept
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
            slot.Seq.store((seq | 1u) + 1u, std::memory_order_release); // mark complete (even, advanced).
        }

        std::array<Slot, SlotCount> _slots{};
    };
}}}

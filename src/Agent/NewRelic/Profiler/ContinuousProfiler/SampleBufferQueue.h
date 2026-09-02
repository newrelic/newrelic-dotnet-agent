/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
#pragma once
#include <array>
#include <cstdint>
#include <cstring>
#include <mutex>
#include <vector>

// SampleBufferQueue is the hand-off between the native sampling thread (producer, one encoded batch
// per tick) and the managed reader that P/Invokes ReadThreadSamples (consumer). It is a fixed
// two-slot queue: two slots give the consumer one full drain interval of jitter tolerance while
// bounding memory, and a saturated queue drops the newest tick rather than blocking the app.
//
// It is deliberately free of CLR/logging dependencies so its ordering behavior can be exercised
// directly; the caller owns all logging.
//
// FIFO ORDERING IS LOAD-BEARING. The obvious implementation -- producer takes the first non-filled
// slot, consumer takes the first filled slot, both scanning from index 0 -- starves slot 1 forever
// once both slots have been filled at least once: with alternating publish/read the scan-from-zero
// consumer always finds slot 0, frees slot 0, and the scan-from-zero producer immediately refills
// slot 0. Whatever is sitting in slot 1 is never read again, so the effective depth collapses from
// two to one and one whole batch is silently lost. Instead, every fill stamps the slot with a
// monotonic sequence number and the consumer always drains the LOWEST-sequence filled slot, which
// is real FIFO regardless of which slot the producer happened to pick.
namespace NewRelic { namespace Profiler { namespace ContinuousProfiler
{
    class SampleBufferQueue
    {
    public:
        // Whether the producer would find a free slot right now. Intended as a pre-flight check so the
        // sampler can skip the expensive suspend/walk/encode cycle on a back-pressure tick instead of
        // paying for it and throwing the result away. Safe as a gate because there is exactly one
        // producer: only the consumer ever frees a slot, so a free slot observed here is still free
        // when the same thread reaches TryPublish. (The converse is racy in the harmless direction --
        // a drain right after a false result costs one skipped tick, not correctness.)
        bool HasFreeSlot() const noexcept
        {
            std::lock_guard<std::mutex> l(_mtx);
            for (const auto& slot : _slots)
            {
                if (!slot.Filled)
                {
                    return true;
                }
            }
            return false;
        }

        // Take ownership of `bytes` (swapped in, leaving the caller's buffer holding this slot's
        // recycled storage) and mark the slot ready to read. Returns false without touching `bytes`
        // when every slot is still filled -- the caller's back-pressure signal.
        bool TryPublish(std::vector<uint8_t>& bytes) noexcept
        {
            std::lock_guard<std::mutex> l(_mtx);
            for (auto& slot : _slots)
            {
                if (slot.Filled)
                {
                    continue;
                }

                slot.Bytes.swap(bytes);
                slot.Sequence = _nextSequence++;
                slot.Filled = true;
                return true;
            }
            return false;
        }

        // Truncation counters for the Read path below: how many batches had to be cut to fit the
        // caller's buffer, and how many bytes were dropped doing so. A truncated batch is silent data
        // loss, so the caller (which owns all logging) polls these to report it.
        uint64_t TruncatedBatchCount() const noexcept
        {
            std::lock_guard<std::mutex> l(_mtx);
            return _truncatedBatches;
        }

        uint64_t TruncatedByteCount() const noexcept
        {
            std::lock_guard<std::mutex> l(_mtx);
            return _truncatedBytes;
        }

        // Copy the OLDEST filled batch into `buf` and free its slot, returning the number of bytes
        // written (0 when nothing is ready). A batch larger than `len` is truncated -- the managed
        // parser tolerates a truncated tail -- and the slot is freed either way so the producer can
        // reuse it, with the loss recorded in the truncation counters above. `buf`/`len` are assumed
        // valid; the caller validates them.
        int32_t Read(int32_t len, unsigned char* buf) noexcept
        {
            std::lock_guard<std::mutex> l(_mtx);

            Slot* oldest = nullptr;
            for (auto& slot : _slots)
            {
                if (slot.Filled && (oldest == nullptr || slot.Sequence < oldest->Sequence))
                {
                    oldest = &slot;
                }
            }

            if (oldest == nullptr)
            {
                return 0;
            }

            const size_t available = oldest->Bytes.size();
            const size_t toCopy = available < static_cast<size_t>(len) ? available : static_cast<size_t>(len);
            if (toCopy > 0)
            {
                std::memcpy(buf, oldest->Bytes.data(), toCopy);
            }

            if (toCopy < available)
            {
                ++_truncatedBatches;
                _truncatedBytes += available - toCopy;
            }

            oldest->Bytes.clear();
            oldest->Filled = false;
            return static_cast<int32_t>(toCopy);
        }

        // Free both slots' byte buffers back to the allocator -- not just clear() them, which retains
        // capacity -- and mark them empty, so a stopped continuous-profiling session does not keep up to
        // two full (multi-MB) batch buffers resident until the profiler is destroyed. Called from
        // ContinuousProfiler::Stop()/Shutdown() once the sampler thread is guaranteed not to be
        // producing; still takes the same lock as the producer/consumer, so it is safe even against a
        // late reader, though by the time the profiler stops the managed reader has finished its final
        // drain. Any batch still sitting undrained is discarded -- the session is ending. _nextSequence
        // stays monotonic: it only orders slots that are currently Filled, and none are after this.
        void Reset() noexcept
        {
            std::lock_guard<std::mutex> l(_mtx);
            for (auto& slot : _slots)
            {
                std::vector<uint8_t>().swap(slot.Bytes);
                slot.Filled = false;
                slot.Sequence = 0;
            }
        }

        // Test seam: total bytes of capacity currently held across both slots. Lets a lifecycle test
        // assert Reset() actually reclaims the batch buffers rather than just marking the slots free.
        size_t TotalCapacityForTesting() const noexcept
        {
            std::lock_guard<std::mutex> l(_mtx);
            size_t total = 0;
            for (const auto& slot : _slots)
            {
                total += slot.Bytes.capacity();
            }
            return total;
        }

    private:
        struct Slot
        {
            std::vector<uint8_t> Bytes;
            uint64_t Sequence{ 0 }; // fill order; only meaningful while Filled
            bool Filled{ false };
        };

        mutable std::mutex _mtx;
        std::array<Slot, 2> _slots;
        uint64_t _nextSequence{ 1 };
        uint64_t _truncatedBatches{ 0 };
        uint64_t _truncatedBytes{ 0 };
    };
}}}

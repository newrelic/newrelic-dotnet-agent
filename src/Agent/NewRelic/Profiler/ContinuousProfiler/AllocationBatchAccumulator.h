/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
#pragma once
#include <cstdint>
#include <limits>
#include <vector>

#include "SampleBufferQueue.h"
#include "SampleBufferWriter.h"

// AllocationBatchAccumulator decides WHEN an allocation sample becomes a published batch. It exists
// because the obvious answer -- one batch per sample, published immediately -- caps delivery at one
// sample per managed drain interval:
//
//   The SampleBufferQueue has two slots and the managed drain frees one per ReadBatch call. If every
//   sample is its own batch, then in steady state every tick beyond the first after a drain finds both
//   slots full and is thrown away. At the shipped defaults (200 samples/minute, a 10 s drain) that is a
//   ceiling of 6 samples/minute -- 3% of the configured budget -- and the survivor is deterministically
//   "the first tick after each drain" rather than the sub-sampler's uniform selection, so the profile is
//   biased as well as sparse. Measured on a real run: 1577 selected ticks dropped, 18 delivered.
//
// So back-pressure here means "batch more samples together", not "drop them". A sample encodes into a
// PENDING batch, and that batch is sealed and published as soon as there is a slot to publish it into.
// When there is no slot, the batch stays OPEN and the next sample appends to it. Nothing is dropped
// until the pending batch itself runs out of room (MaxAllocationBufferBytes -- room for well over a
// thousand samples that share a stack, or a few dozen with wholly distinct ones; see that constant for
// the two regimes) while the queue is still full: a genuinely saturated state, where dropping is the
// only option left.
// In the common non-backpressured case the behavior is byte-for-byte what it was before: one sample per
// batch, published on the spot, no added latency.
//
// TWO THINGS ARE LOAD-BEARING AND EASY TO GET WRONG:
//
//  1. THE WRITER INSTANCE MUST PERSIST WITH THE BATCH. SampleBufferWriter's frame-interning table lives
//     in the WRITER OBJECT, not in the byte buffer, and it is what makes a positive frame code mean
//     "back-reference to the string defined earlier in THIS batch". Keeping the buffer across ticks but
//     constructing a fresh writer around it each tick would restart interning at index 1 while the
//     buffer already contained definitions for 1..n -- redefining indices the batch had already used and
//     silently corrupting every sample after the first. Hence one writer member, alive for as long as
//     the batch it is writing, and BeginBatch() (which clears the buffer and the interning table
//     TOGETHER) called only when actually starting a fresh batch.
//
//  2. A PARTIALLY WRITTEN SAMPLE POISONS THE WHOLE BATCH. The encoder has no rollback: a record that
//     stops mid-field desynchronizes the managed decoder for everything after it. If encoding throws,
//     the owner must call AbandonBatch() rather than leaving the half-written record in place.
//
// Dropped samples are reported IN BAND, as the `skipped` field of the existing BatchStats record (opcode
// 0x07) -- the same field the thread sampler already uses for "samples the walk missed". No new opcode,
// no BufferParser change: the managed drain reads BatchStats.Skipped off each allocation batch and
// reports it as a supportability metric. The count is a DELTA (drops since the previous published
// batch), and it is only retired once the batch carrying it is actually published, so a failed publish
// cannot lose it.
//
// NOT THREAD-SAFE, by design: the owner (AllocationSampler) already serializes every entry point on its
// tick mutex, so a second lock here would be pure overhead. Like SampleBufferQueue, it is deliberately
// free of CLR and logging dependencies -- the owner does all the logging -- which is also what makes it
// directly unit-testable against a real SampleBufferQueue.
namespace NewRelic { namespace Profiler { namespace ContinuousProfiler
{
    class AllocationBatchAccumulator
    {
    public:
        // Bytes that must ALWAYS be left available so an open batch can be closed: a BatchStats record
        // (opcode + int64 + 3x int32) plus the EndBatch opcode. Public because the owner's per-frame
        // overflow check has to reserve it too.
        static constexpr size_t BatchTailBytes = (1 + 8 + 4 + 4 + 4) + 1;

        // A StartBatch record: opcode + version byte + int64 timestamp.
        static constexpr size_t StartBatchBytes = 1 + 1 + 8;

        // Reservation used ONLY by the pre-walk gate (CanAcceptSample), which has to answer "is it worth
        // walking the stack?" before the frames -- and therefore this sample's real size -- are known. It is
        // therefore a deliberate OVER-reservation sized for the expensive regime, not an average:
        //
        //   * a sample appended to a batch that already interned its stack costs ~100 bytes
        //     (AllocationSampler::MaxAllocationBufferBytes documents both regimes, and
        //     AllocationBatchAccumulatorTest measures this one), so 4 KB over-reserves ~40x -- which costs
        //     nothing that matters: the gate closes ~3% of a 128 KB batch early, i.e. it stops accepting
        //     around sample ~1290 instead of ~1330.
        //   * a sample whose frames are all NEW costs ~2.5 KB (roughly 20 fresh 60-char frames), so for the
        //     diverse-stack workload -- the one that can actually fill a batch -- 4 KB is about right, and
        //     erring high is the safe direction: the gate refuses a tick slightly before the batch is truly
        //     full rather than letting BeginSample discover it has no room after paying for a stack walk.
        //
        // Being coarse here is fine precisely BECAUSE BeginSample re-checks with the sample's exact size, so
        // this number never decides whether a resolved sample fits -- only whether resolving it is worth it.
        static constexpr size_t TypicalSampleBytes = 4 * 1024;

        // `queue` and `maxBytes` are the same queue the managed reader drains and the same per-batch byte
        // ceiling a single-sample batch used before -- a pending batch is capped exactly like any other.
        AllocationBatchAccumulator(SampleBufferQueue& queue, size_t maxBytes) noexcept
            : _queue(queue), _writer(_buffer, maxBytes)
        {
        }

        // Cheap pre-flight check for the owner's tick handler: is there anywhere for a sample to GO, so
        // that paying for a stack walk and frame-name resolution is worthwhile? False only in the
        // saturated state (no free slot AND an open batch with no room), which is the one case where
        // dropping the tick before the walk is still the right answer.
        //
        // Safe as a gate because there is exactly one producer: only the consumer frees a queue slot, so
        // a free slot observed here is still free when this same thread publishes, and the pending
        // batch's room is only ever changed by this thread.
        bool CanAcceptSample() const noexcept
        {
            if (_queue.HasFreeSlot())
            {
                return true;
            }

            // No slot to publish into, so the sample joins the pending batch instead of being dropped.
            // A batch that is not open yet is empty, so it always has room.
            if (!_batchOpen)
            {
                return true;
            }

            return _writer.WillFit(TypicalSampleBytes + BatchTailBytes);
        }

        // Count one sample the owner is dropping for a reason of its own (a saturated CanAcceptSample, a
        // failed stack walk after the decision to sample was already made, ...). Feeds the in-band
        // BatchStats.Skipped delta.
        void RecordDroppedSample() noexcept
        {
            ++_droppedPending;
            ++_droppedTotal;
        }

        // Open (or continue) a batch with room for a sample of `requiredBytes`, and return the writer to
        // encode it with -- or nullptr when the sample must be dropped, in which case it has already
        // been counted.
        //
        // `requiredBytes` is the sample's EXACT worst-case encoded size, which is why the caller resolves
        // its frame names first: knowing the real size lets the cap-driven flush happen at a sample
        // boundary instead of truncating a sample's frame list at the buffer's edge. The only sample that
        // still truncates is one whose frames alone exceed the entire buffer cap (the owner's per-frame
        // check catches that), exactly as before this class existed.
        //
        // Can throw (the writer allocates); the owner encodes inside a try/catch that calls
        // AbandonBatch().
        SampleBufferWriter* BeginSample(size_t requiredBytes, int64_t batchTimestampNanos)
        {
            if (_batchOpen && !_writer.WillFit(requiredBytes + BatchTailBytes))
            {
                // The open batch cannot hold this sample. Publishing is the only way to empty it, so a
                // flush is only attempted when a slot is known to be free -- a failed publish would have
                // to throw away every sample already in the batch.
                if (_queue.HasFreeSlot())
                {
                    CloseAndPublish();
                }
                else
                {
                    // Saturated: nothing to flush into and no room to append. Drop THIS sample only --
                    // the batch stays intact, so the samples already in it are still delivered as soon
                    // as a slot frees.
                    RecordDroppedSample();
                    return nullptr;
                }
            }

            if (!_batchOpen)
            {
                // BeginBatch clears the buffer AND resets the frame-interning table, which is why it is
                // called here and nowhere else: doing it between appends to an open batch would corrupt
                // that batch's frame codes (see the header comment).
                _writer.BeginBatch();
                _writer.WriteStartBatch(batchTimestampNanos);
                _batchOpen = true;
                _samplesInBatch = 0;
            }

            return &_writer;
        }

        // The owner has finished writing a complete sample (through its frame-list terminator). Publishes
        // immediately if there is a slot -- which keeps the common case at one sample per batch and one
        // drain interval of latency -- and otherwise leaves the batch open for the next sample to append
        // to, which is the entire point of this class.
        //
        // Can throw (sealing the batch writes to the buffer); see BeginSample.
        void EndSample()
        {
            if (!_batchOpen)
            {
                return; // defensive: no sample was begun
            }

            ++_samplesInBatch;

            if (_queue.HasFreeSlot())
            {
                CloseAndPublish();
            }
        }

        // Seal and publish an open batch if there is a slot for it. Called by the owner from the managed
        // drain path so a batch accumulated under back-pressure is handed over as soon as the reader
        // frees a slot, instead of waiting for the next allocation tick to notice -- which would strand
        // the tail of every allocation burst for as long as the workload stayed quiet. Returns whether a
        // batch was published.
        bool FlushIfPending()
        {
            if (!_batchOpen || !_queue.HasFreeSlot())
            {
                return false;
            }

            return CloseAndPublish();
        }

        // Throw away the open batch, counting everything in it as dropped. The owner's ONLY correct
        // response to an exception raised while encoding a sample: the writer cannot roll back a partial
        // record, and a partial record desynchronizes the managed decoder for every sample after it.
        void AbandonBatch() noexcept
        {
            if (!_batchOpen)
            {
                return;
            }

            _droppedPending += _samplesInBatch;
            _droppedTotal += _samplesInBatch;
            _samplesInBatch = 0;
            _batchOpen = false;
            _buffer.clear();
        }

        // Cumulative count of samples this accumulator has dropped, for diagnostics and tests. The
        // number reported to the managed side is the per-batch DELTA, not this.
        uint64_t DroppedTotal() const noexcept
        {
            return _droppedTotal;
        }

        // Drops not yet carried out to the managed side by a published batch.
        uint64_t DroppedPending() const noexcept
        {
            return _droppedPending;
        }

        // Whether a batch is currently open (i.e. holding samples that are not published yet).
        bool HasOpenBatch() const noexcept
        {
            return _batchOpen;
        }

        // How many samples the open batch is holding.
        uint32_t SamplesInPendingBatch() const noexcept
        {
            return _samplesInBatch;
        }

        AllocationBatchAccumulator(const AllocationBatchAccumulator&) = delete;
        AllocationBatchAccumulator(AllocationBatchAccumulator&&) = delete;
        AllocationBatchAccumulator& operator=(const AllocationBatchAccumulator&) = delete;
        AllocationBatchAccumulator& operator=(AllocationBatchAccumulator&&) = delete;

    private:
        // Seal the open batch (BatchStats + EndBatch) and hand it to the queue. Returns false when the
        // publish failed, in which case the sealed bytes are unrecoverable and every sample in them is
        // counted as dropped.
        bool CloseAndPublish()
        {
            // The drop delta rides out in this batch. int32 because that is the BatchStats field width;
            // a delta that large is impossible in one batch window, but clamp rather than wrap, and keep
            // any remainder pending for the next batch.
            const int32_t skipped = ClampToInt32(_droppedPending);
            _writer.WriteBatchStats(0, 0, 0, skipped);
            _writer.WriteEndBatch();

            const uint32_t samples = _samplesInBatch;
            _batchOpen = false;
            _samplesInBatch = 0;

            if (!_queue.TryPublish(_buffer))
            {
                // Unreachable through either caller (both check HasFreeSlot first, and only the consumer
                // frees a slot) but handled rather than assumed: the batch is already sealed, so its
                // samples are lost. The drop delta written above stays pending, since the batch carrying
                // it never arrived.
                _droppedPending += samples;
                _droppedTotal += samples;
                _buffer.clear();
                return false;
            }

            // Published, so the delta this batch carries is now reported. TryPublish SWAPPED our bytes
            // into the slot, leaving us the slot's recycled (already-drained) storage -- clearing keeps
            // its capacity, so steady-state batching does not reallocate.
            _droppedPending -= static_cast<uint64_t>(skipped);
            _buffer.clear();
            return true;
        }

        static int32_t ClampToInt32(uint64_t value) noexcept
        {
            // Parenthesized to defeat the windows.h `max` function-like macro, and copied to a local
            // because binding a static constexpr member to a const reference odr-uses it, which a
            // header-only class cannot satisfy before C++17 (the Linux build is C++11/14).
            const uint64_t ceiling = static_cast<uint64_t>((std::numeric_limits<int32_t>::max)());
            return static_cast<int32_t>(value < ceiling ? value : ceiling);
        }

        // The queue this accumulator publishes into; owned by the caller and must outlive this object.
        SampleBufferQueue& _queue;

        // The pending batch's bytes. Declared BEFORE _writer, which wraps a reference to it.
        std::vector<uint8_t> _buffer;

        // The batch's encoder. ONE instance for the life of this object, so its per-batch frame-interning
        // table stays consistent across the ticks that append to a single batch (see the header comment).
        SampleBufferWriter _writer;

        // Whether _buffer currently holds an unsealed batch (StartBatch written, EndBatch not yet).
        bool _batchOpen{ false };

        // Samples written into the open batch, so an abandoned or unpublishable batch can be counted
        // accurately rather than as one lost sample.
        uint32_t _samplesInBatch{ 0 };

        // Drops not yet reported to the managed side (written as BatchStats.Skipped on the next
        // successful publish, and only retired then).
        uint64_t _droppedPending{ 0 };

        // Cumulative drops, for diagnostics/tests only.
        uint64_t _droppedTotal{ 0 };
    };
}}}

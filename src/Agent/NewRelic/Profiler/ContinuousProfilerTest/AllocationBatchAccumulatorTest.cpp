// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#include "CppUnitTest.h"
#include <string>
#include <vector>
#include "../ContinuousProfiler/AllocationBatchAccumulator.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;
using namespace NewRelic::Profiler::ContinuousProfiler;

// Exercises the accumulate/flush decisions that fix allocation sampling's delivery ceiling: with a
// two-slot queue and one managed read per drain, one-batch-per-sample capped delivery at ~1 sample per
// drain interval and dropped every other sub-sampled tick. These tests drive the accumulator against a
// REAL SampleBufferQueue (it has no CLR or logging dependencies either) and decode the published bytes,
// so they assert on the actual wire output rather than on internal counters alone.
namespace
{
    // DELIBERATELY SMALLER than production's cap (AllocationSampler::MaxAllocationBufferBytes, 128 KB): the
    // saturation and cap-driven-flush paths have to be reachable without encoding thousands of samples, and
    // the accumulator takes the cap as a constructor argument precisely so a test can shrink it. Nothing here
    // asserts anything about the production value -- that constant's own comment carries its derivation, and
    // MarginalBytesPerAppendedSample_StaysSmallForARepeatedStack pins the per-sample cost it is divided by.
    constexpr size_t MaxBatchBytes = 64 * 1024;

    // Write one allocation sample of a controllable size through the accumulator. Mirrors
    // AllocationSampler::EncodeAndPublish's field order exactly, which is what makes the decoded
    // assertions below meaningful. Returns false when the accumulator refused the sample (a drop).
    // Mirrors AllocationSampler::EncodedStringBytes, INCLUDING its MaxStringChars clamp: without the clamp
    // this helper's size model would silently disagree with production's for any string over 512 chars, and
    // the oversized-sample tests below would only work by accident of the encoder truncating anyway.
    size_t EncodedStringBytes(const std::wstring& value)
    {
        const size_t chars = value.size() < SampleBufferWriter::MaxStringChars
            ? value.size() : SampleBufferWriter::MaxStringChars;
        return 2 + (chars * 2);
    }

    bool AppendSample(AllocationBatchAccumulator& accumulator, int64_t timestampNanos,
        const std::wstring& threadName, uint64_t allocatedSize, const std::wstring& typeName,
        const std::vector<std::wstring>& frames)
    {
        size_t required = 1 + EncodedStringBytes(threadName) + (6 * 8) + EncodedStringBytes(typeName) + 2;
        for (const auto& frame : frames)
        {
            required += 2 + EncodedStringBytes(frame);
        }

        auto* writer = accumulator.BeginSample(required, timestampNanos);
        if (writer == nullptr)
        {
            return false;
        }

        writer->WriteStartAllocationSample();
        writer->WriteThreadName(threadName);
        writer->WriteInt64Field(1234);            // os thread id
        writer->WriteInt64Field(0x11);            // traceIdHigh
        writer->WriteInt64Field(0x22);            // traceIdLow
        writer->WriteInt64Field(0x33);            // spanId
        writer->WriteInt64Field(timestampNanos / 1000000);
        writer->WriteUInt64Field(allocatedSize);
        writer->WriteStringField(typeName);
        for (const auto& frame : frames)
        {
            writer->WriteCodedFrameString(frame);
        }
        writer->WriteFrameListTerminator();

        accumulator.EndSample();
        return true;
    }

    bool AppendSimpleSample(AllocationBatchAccumulator& accumulator, uint64_t allocatedSize)
    {
        return AppendSample(accumulator, 1000000000LL, L"worker", allocatedSize, L"System.Byte[]",
            { L"MyApp.Alloc()", L"MyApp.Caller()" });
    }

    // A frame-name string big enough that a handful of samples fill a 64 KB batch, so the saturation
    // path is reachable without writing thousands of samples.
    std::wstring BigFrame(wchar_t fill)
    {
        return std::wstring(SampleBufferWriter::MaxStringChars, fill);
    }

    bool AppendBigSample(AllocationBatchAccumulator& accumulator, wchar_t fill)
    {
        std::vector<std::wstring> frames;
        for (int i = 0; i < 12; ++i)
        {
            // Distinct per frame so nothing interns to a 2-byte back-reference: each costs its full
            // ~1 KB definition, i.e. ~12 KB per sample.
            auto frame = BigFrame(fill);
            frame.push_back(static_cast<wchar_t>(L'0' + i));
            frames.push_back(frame);
        }
        return AppendSample(accumulator, 1000000000LL, L"worker", 4096, L"System.Byte[]", frames);
    }

    // Minimal decoder for the published bytes -- the inverse of SampleBufferWriter, and deliberately
    // independent of the managed BufferParser so a wire-format regression cannot hide behind a shared
    // implementation. Counts 0x08 records, resolves interned frame codes, and captures BatchStats.skipped.
    struct DecodedBatch
    {
        int Samples{ 0 };
        int Skipped{ 0 };
        bool SawEndBatch{ false };
        bool Malformed{ false };
        // Frame-code POLARITY, which is the only thing that can actually detect an interning table reset
        // mid-batch. A negative code is a self-contained definition; a positive one is a back-reference to a
        // definition earlier in the SAME batch. A writer whose table were rebuilt per tick would emit a fresh
        // definition for every sample's every frame -- decoding correctly (each definition simply overwrites
        // the identical dictionary entry) but producing a far less dense batch. Counting the two kinds is
        // what makes that visible; sample counts and frame strings alone cannot see it.
        int NegativeDefinitions{ 0 };
        int PositiveLookups{ 0 };
        int TotalBytes{ 0 };
        std::vector<std::wstring> FirstSampleFrames;
        std::vector<std::wstring> LastSampleFrames;
        std::vector<uint64_t> AllocatedSizes;
    };

    class Reader
    {
    public:
        Reader(const unsigned char* bytes, size_t length) : _bytes(bytes), _length(length) {}

        bool Done() const { return _pos >= _length; }
        bool Overrun(size_t size) const { return _pos + size > _length; }

        uint8_t Byte() { return _bytes[_pos++]; }

        int16_t Short()
        {
            const int16_t value = static_cast<int16_t>((_bytes[_pos] << 8) | _bytes[_pos + 1]);
            _pos += 2;
            return value;
        }

        int32_t Int()
        {
            int32_t value = 0;
            for (int i = 0; i < 4; ++i) { value = (value << 8) | _bytes[_pos + i]; }
            _pos += 4;
            return value;
        }

        int64_t Long()
        {
            int64_t value = 0;
            for (int i = 0; i < 8; ++i) { value = (value << 8) | _bytes[_pos + i]; }
            _pos += 8;
            return value;
        }

        std::wstring String()
        {
            const int16_t chars = Short();
            std::wstring value;
            value.reserve(static_cast<size_t>(chars));
            for (int16_t i = 0; i < chars; ++i)
            {
                const uint16_t codeUnit = static_cast<uint16_t>(_bytes[_pos] | (_bytes[_pos + 1] << 8));
                value.push_back(static_cast<wchar_t>(codeUnit));
                _pos += 2;
            }
            return value;
        }

    private:
        const unsigned char* _bytes;
        size_t _length;
        size_t _pos{ 0 };
    };

    DecodedBatch Decode(const unsigned char* bytes, int32_t length)
    {
        DecodedBatch decoded;
        Reader reader(bytes, static_cast<size_t>(length));
        std::vector<std::wstring> frameDictionary(1); // index 0 is the terminator, never a real frame

        while (!reader.Done())
        {
            const uint8_t opcode = reader.Byte();
            if (opcode == 0x01) // StartBatch
            {
                reader.Byte(); // version
                reader.Long(); // timestamp
            }
            else if (opcode == 0x08) // AllocationSample
            {
                reader.String();                        // thread name
                reader.Long();                          // os thread id
                reader.Long(); reader.Long(); reader.Long(); // trace context
                reader.Long();                          // timestamp millis
                decoded.AllocatedSizes.push_back(static_cast<uint64_t>(reader.Long()));
                reader.String();                        // type name

                std::vector<std::wstring> frames;
                for (;;)
                {
                    const int16_t code = reader.Short();
                    if (code == 0)
                    {
                        break;
                    }

                    if (code < 0)
                    {
                        ++decoded.NegativeDefinitions;
                        const auto value = reader.String();
                        const size_t index = static_cast<size_t>(-code);
                        if (frameDictionary.size() <= index)
                        {
                            frameDictionary.resize(index + 1);
                        }
                        frameDictionary[index] = value;
                        frames.push_back(value);
                    }
                    else
                    {
                        ++decoded.PositiveLookups;
                        // A positive code that was never defined is precisely the corruption a
                        // reconstructed-per-tick interning table would produce.
                        if (static_cast<size_t>(code) >= frameDictionary.size() || frameDictionary[code].empty())
                        {
                            decoded.Malformed = true;
                            return decoded;
                        }
                        frames.push_back(frameDictionary[code]);
                    }
                }

                if (decoded.Samples == 0)
                {
                    decoded.FirstSampleFrames = frames;
                }
                decoded.LastSampleFrames = frames;
                ++decoded.Samples;
            }
            else if (opcode == 0x07) // BatchStats
            {
                reader.Long(); reader.Int(); reader.Int();
                decoded.Skipped = reader.Int();
            }
            else if (opcode == 0x06) // EndBatch
            {
                decoded.SawEndBatch = true;
                return decoded;
            }
            else
            {
                decoded.Malformed = true;
                return decoded;
            }
        }

        return decoded;
    }

    DecodedBatch ReadAndDecode(SampleBufferQueue& queue, int32_t& bytesRead)
    {
        std::vector<unsigned char> buffer(MaxBatchBytes * 2, 0);
        bytesRead = queue.Read(static_cast<int32_t>(buffer.size()), buffer.data());
        auto decoded = Decode(buffer.data(), bytesRead);
        decoded.TotalBytes = bytesRead;
        return decoded;
    }
}

TEST_CLASS(AllocationBatchAccumulatorTest)
{
public:
    TEST_METHOD(EndSample_WithAFreeSlot_PublishesOneSamplePerBatch)
    {
        // The common, non-backpressured case must behave exactly as it did before batching existed:
        // publish on the spot, one sample per batch, no added latency.
        SampleBufferQueue queue;
        AllocationBatchAccumulator accumulator(queue, MaxBatchBytes);

        Assert::IsTrue(AppendSimpleSample(accumulator, 1024));

        Assert::IsFalse(accumulator.HasOpenBatch(), L"a sample must not be held back while a slot is free");

        int32_t bytesRead = 0;
        const auto decoded = ReadAndDecode(queue, bytesRead);
        Assert::IsTrue(bytesRead > 0);
        Assert::AreEqual(1, decoded.Samples);
        Assert::IsTrue(decoded.SawEndBatch);
        Assert::IsFalse(decoded.Malformed);
        Assert::AreEqual(0ULL, accumulator.DroppedTotal());
    }

    TEST_METHOD(SamplesTakenWhileBothSlotsAreFull_AreDeliveredAsOneBatch_NotDropped)
    {
        // THE REGRESSION THIS TASK FIXES. Both slots stay full for the whole burst, which used to drop
        // every tick in it. They must instead accumulate into one pending batch and be delivered intact
        // once a slot frees.
        SampleBufferQueue queue;
        AllocationBatchAccumulator accumulator(queue, MaxBatchBytes);

        // Fill both slots (published immediately, one sample each) so the queue is saturated.
        Assert::IsTrue(AppendSimpleSample(accumulator, 1));
        Assert::IsTrue(AppendSimpleSample(accumulator, 2));
        Assert::IsFalse(queue.HasFreeSlot(), L"both slots must be full for this test to mean anything");

        // The burst that used to be dropped in its entirety.
        const int burst = 20;
        for (int i = 0; i < burst; ++i)
        {
            Assert::IsTrue(AppendSimpleSample(accumulator, static_cast<uint64_t>(100 + i)),
                L"a sample taken under back-pressure must be accumulated, not refused");
        }

        Assert::AreEqual(0ULL, accumulator.DroppedTotal(), L"nothing may be dropped while the pending batch has room");
        Assert::IsTrue(accumulator.HasOpenBatch());
        Assert::AreEqual(static_cast<uint32_t>(burst), accumulator.SamplesInPendingBatch());

        // Drain the first published batch: that frees a slot, and the pending batch goes out on the next
        // flush (which is what AllocationSampler::ReadAllocationSamples performs before every read).
        int32_t bytesRead = 0;
        auto decoded = ReadAndDecode(queue, bytesRead);
        Assert::AreEqual(1, decoded.Samples);

        Assert::IsTrue(accumulator.FlushIfPending());
        Assert::IsFalse(accumulator.HasOpenBatch());

        // Reads are FIFO, so the second single-sample batch comes out before the accumulated one.
        decoded = ReadAndDecode(queue, bytesRead);
        Assert::AreEqual(1, decoded.Samples);

        decoded = ReadAndDecode(queue, bytesRead);
        Assert::IsFalse(decoded.Malformed, L"the accumulated batch must decode cleanly");
        Assert::IsTrue(decoded.SawEndBatch);
        Assert::AreEqual(burst, decoded.Samples, L"every accumulated sample must be delivered");
        Assert::AreEqual(0ULL, accumulator.DroppedTotal());

        // And the samples are the real ones, in order -- not a repeat of the same bytes.
        Assert::AreEqual(static_cast<size_t>(burst), decoded.AllocatedSizes.size());
        Assert::AreEqual(100ULL, decoded.AllocatedSizes.front());
        Assert::AreEqual(static_cast<uint64_t>(100 + burst - 1), decoded.AllocatedSizes.back());
    }

    TEST_METHOD(AccumulatedBatch_InternsEachFrameOnceForTheWholeBatch)
    {
        // THE INTERNING REGRESSION GUARD, and it has to be written in terms of code POLARITY to be one.
        // If the writer's frame table were rebuilt per tick (i.e. a fresh SampleBufferWriter wrapped around a
        // persistent buffer -- the mistake this design exists to avoid), the batch would still DECODE
        // correctly: every frame would arrive as a self-contained negative definition, each overwriting the
        // identical dictionary entry. Frame strings, sample counts and a "malformed?" check therefore all
        // pass either way. What changes is density: 5 samples x 2 frames would emit 10 definitions and 0
        // back-references instead of 2 definitions and 8 back-references.
        SampleBufferQueue queue;
        AllocationBatchAccumulator accumulator(queue, MaxBatchBytes);

        AppendSimpleSample(accumulator, 1);
        AppendSimpleSample(accumulator, 2);
        Assert::IsFalse(queue.HasFreeSlot());

        // 5 samples sharing exactly 2 distinct frames.
        for (int i = 0; i < 5; ++i)
        {
            Assert::IsTrue(AppendSimpleSample(accumulator, static_cast<uint64_t>(i)));
        }

        int32_t bytesRead = 0;
        ReadAndDecode(queue, bytesRead); // drain one slot
        ReadAndDecode(queue, bytesRead); // and the other, so the flush below has room
        Assert::IsTrue(accumulator.FlushIfPending());

        const auto decoded = ReadAndDecode(queue, bytesRead);
        Assert::IsFalse(decoded.Malformed);
        Assert::AreEqual(5, decoded.Samples);
        Assert::AreEqual(2, decoded.NegativeDefinitions,
            L"each distinct frame must be DEFINED exactly once for the whole batch -- 10 definitions means the interning table was reset per sample");
        Assert::AreEqual(8, decoded.PositiveLookups,
            L"every repeat must be a back-reference into this batch's table -- 0 lookups means the table was reset per sample");

        // And the back-references still resolve to the right strings (density without correctness would be
        // worse than the bug).
        Assert::AreEqual(static_cast<size_t>(2), decoded.FirstSampleFrames.size());
        Assert::AreEqual(std::wstring(L"MyApp.Alloc()"), decoded.FirstSampleFrames[0]);
        Assert::IsTrue(decoded.LastSampleFrames == decoded.FirstSampleFrames,
            L"the last sample's back-referenced frames must resolve to the first sample's definitions");
    }

    TEST_METHOD(MarginalBytesPerAppendedSample_StaysSmallForARepeatedStack)
    {
        // Pins the number MaxAllocationBufferBytes is sized against. Appending sample N to an open batch
        // whose stack is already interned costs only the fixed fields plus 2 bytes per frame -- roughly 100
        // bytes, NOT the ~2 KB of a standalone one-sample OTLP profile. Sizing the cap off the wrong figure
        // (as the first cut of this change did) either wastes memory or, in the other direction, silently
        // caps delivery. If interning regressed, this number would jump by the full definition cost of every
        // frame, so this doubles as a second, quantitative guard on the test above.
        SampleBufferQueue queue;
        AllocationBatchAccumulator accumulator(queue, MaxBatchBytes);

        AppendSimpleSample(accumulator, 1);
        AppendSimpleSample(accumulator, 2);
        Assert::IsFalse(queue.HasFreeSlot());

        const int samples = 50;
        for (int i = 0; i < samples; ++i)
        {
            Assert::IsTrue(AppendSimpleSample(accumulator, static_cast<uint64_t>(i)));
        }

        int32_t bytesRead = 0;
        ReadAndDecode(queue, bytesRead);
        ReadAndDecode(queue, bytesRead);
        Assert::IsTrue(accumulator.FlushIfPending());

        const auto decoded = ReadAndDecode(queue, bytesRead);
        Assert::AreEqual(samples, decoded.Samples);

        const int bytesPerSample = decoded.TotalBytes / samples;
        Assert::IsTrue(bytesPerSample < 200,
            L"a sample appended to a batch that already interned its stack must cost ~100 bytes, not kilobytes");
        Assert::IsTrue(bytesPerSample > 50, L"sanity: the fixed fields alone are ~85 bytes");
    }

    TEST_METHOD(AFullPendingBatch_IsFlushedAtASampleBoundaryWhenASlotFrees)
    {
        // Cap-driven flush: an open batch that cannot fit the next sample is published (not truncated,
        // not dropped) and a fresh batch is started for that sample.
        SampleBufferQueue queue;
        AllocationBatchAccumulator accumulator(queue, MaxBatchBytes);

        // Fill exactly one slot, leaving the other free so the cap-driven flush has somewhere to go.
        Assert::IsTrue(AppendSimpleSample(accumulator, 1));
        Assert::IsTrue(queue.HasFreeSlot());

        int32_t bytesRead = 0;
        ReadAndDecode(queue, bytesRead); // drain it; both slots free again

        // Fill both slots to force accumulation, then pile on big samples until the batch overflows.
        AppendSimpleSample(accumulator, 2);
        AppendSimpleSample(accumulator, 3);
        Assert::IsFalse(queue.HasFreeSlot());

        int accepted = 0;
        for (int i = 0; i < 6 && accumulator.DroppedTotal() == 0; ++i)
        {
            if (AppendBigSample(accumulator, static_cast<wchar_t>(L'a' + i))) { ++accepted; }
        }

        // ~12 KB per sample against a 64 KB cap: the batch must have filled and refused at least one.
        Assert::IsTrue(accepted >= 4, L"the 64 KB batch should hold at least four ~12 KB samples");
        Assert::AreEqual(1ULL, accumulator.DroppedTotal(), L"a saturated accumulator drops exactly the sample it cannot take");

        // Free a slot: the next oversized sample now triggers a cap-driven flush of the full batch
        // instead of a drop, and lands in a fresh batch of its own.
        ReadAndDecode(queue, bytesRead);
        Assert::IsTrue(AppendBigSample(accumulator, L'z'), L"with a slot free, an over-cap sample must flush the batch rather than be dropped");
        Assert::IsTrue(accumulator.HasOpenBatch(), L"the flushed-into fresh batch stays open while the queue is full again");
        Assert::AreEqual(static_cast<uint32_t>(1), accumulator.SamplesInPendingBatch());

        // Reads are FIFO, so the other single-sample batch still queued from before comes out first.
        auto leftover = ReadAndDecode(queue, bytesRead);
        Assert::AreEqual(1, leftover.Samples);

        // Then the flushed batch, which decodes cleanly and carries the drop count from before it.
        const auto decoded = ReadAndDecode(queue, bytesRead);
        Assert::IsFalse(decoded.Malformed);
        Assert::IsTrue(decoded.SawEndBatch);
        Assert::AreEqual(accepted, decoded.Samples, L"every sample accepted into the batch must survive the flush");
        Assert::AreEqual(1, decoded.Skipped, L"the drop must be reported in band on the next published batch");
    }

    TEST_METHOD(DropCount_IsReportedInBandAndOnlyOnce)
    {
        SampleBufferQueue queue;
        AllocationBatchAccumulator accumulator(queue, MaxBatchBytes);

        accumulator.RecordDroppedSample();
        accumulator.RecordDroppedSample();
        accumulator.RecordDroppedSample();
        Assert::AreEqual(3ULL, accumulator.DroppedPending());

        Assert::IsTrue(AppendSimpleSample(accumulator, 1));

        int32_t bytesRead = 0;
        auto decoded = ReadAndDecode(queue, bytesRead);
        Assert::AreEqual(3, decoded.Skipped, L"the pending drop delta must ride out on the next batch");
        Assert::AreEqual(0ULL, accumulator.DroppedPending(), L"a reported delta must be retired");

        // The delta is a delta: a second batch with no further drops reports zero rather than repeating.
        Assert::IsTrue(AppendSimpleSample(accumulator, 2));
        decoded = ReadAndDecode(queue, bytesRead);
        Assert::AreEqual(0, decoded.Skipped);
        Assert::AreEqual(3ULL, accumulator.DroppedTotal(), L"the cumulative total still remembers them");
    }

    TEST_METHOD(AbandonBatch_DiscardsThePartialBatchAndCountsItsSamples)
    {
        // What the owner does when encoding throws: the half-written record cannot be rolled back, so the
        // whole batch is discarded rather than shipped as a stream the decoder would desynchronize on.
        SampleBufferQueue queue;
        AllocationBatchAccumulator accumulator(queue, MaxBatchBytes);

        AppendSimpleSample(accumulator, 1);
        AppendSimpleSample(accumulator, 2);
        Assert::IsFalse(queue.HasFreeSlot());

        AppendSimpleSample(accumulator, 3);
        AppendSimpleSample(accumulator, 4);
        Assert::AreEqual(static_cast<uint32_t>(2), accumulator.SamplesInPendingBatch());

        accumulator.AbandonBatch();

        Assert::IsFalse(accumulator.HasOpenBatch());
        Assert::AreEqual(0u, accumulator.SamplesInPendingBatch());
        Assert::AreEqual(2ULL, accumulator.DroppedTotal(), L"an abandoned batch's samples are dropped samples");

        // And the accumulator is still usable: the next sample starts a clean batch. Both queued batches
        // are drained first so that FIFO ordering puts the new one at the head of the queue.
        int32_t bytesRead = 0;
        ReadAndDecode(queue, bytesRead);
        ReadAndDecode(queue, bytesRead);
        Assert::IsTrue(AppendSimpleSample(accumulator, 5));
        const auto decoded = ReadAndDecode(queue, bytesRead);
        Assert::IsFalse(decoded.Malformed, L"a batch started after an abandon must decode cleanly");
        Assert::AreEqual(1, decoded.Samples);
        Assert::AreEqual(2, decoded.Skipped);
    }

    TEST_METHOD(FlushIfPending_IsANoOpWithNothingPendingOrNoRoom)
    {
        SampleBufferQueue queue;
        AllocationBatchAccumulator accumulator(queue, MaxBatchBytes);

        Assert::IsFalse(accumulator.FlushIfPending(), L"nothing is open, so there is nothing to flush");

        AppendSimpleSample(accumulator, 1);
        AppendSimpleSample(accumulator, 2);
        AppendSimpleSample(accumulator, 3); // accumulates; both slots are full

        Assert::IsTrue(accumulator.HasOpenBatch());
        Assert::IsFalse(accumulator.FlushIfPending(), L"a full queue must leave the pending batch open");
        Assert::IsTrue(accumulator.HasOpenBatch());
        Assert::AreEqual(0ULL, accumulator.DroppedTotal(), L"a no-op flush must not count a drop");
    }

    TEST_METHOD(CanAcceptSample_IsFalseOnlyWhenSaturated)
    {
        SampleBufferQueue queue;
        AllocationBatchAccumulator accumulator(queue, MaxBatchBytes);

        Assert::IsTrue(accumulator.CanAcceptSample(), L"an empty queue can always take a sample");

        AppendSimpleSample(accumulator, 1);
        AppendSimpleSample(accumulator, 2);
        Assert::IsFalse(queue.HasFreeSlot());
        Assert::IsTrue(accumulator.CanAcceptSample(),
            L"a full queue with no open batch must still accept -- this is the drop the fix removes");

        // Fill the pending batch until the pre-walk gate closes.
        for (int i = 0; i < 8 && accumulator.CanAcceptSample(); ++i)
        {
            AppendBigSample(accumulator, static_cast<wchar_t>(L'a' + i));
        }

        Assert::IsFalse(accumulator.CanAcceptSample(), L"a full queue AND a full pending batch is genuinely saturated");

        // Freeing a slot reopens the gate: the batch can be flushed into it.
        int32_t bytesRead = 0;
        ReadAndDecode(queue, bytesRead);
        Assert::IsTrue(accumulator.CanAcceptSample());
    }
};

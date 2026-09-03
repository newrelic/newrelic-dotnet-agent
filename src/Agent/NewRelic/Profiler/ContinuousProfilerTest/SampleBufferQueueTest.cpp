/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
#include "stdafx.h"
#include "CppUnitTest.h"

#include <cstdint>
#include <vector>

#include "../ContinuousProfiler/SampleBufferQueue.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace NewRelic { namespace Profiler { namespace ContinuousProfiler
{
    TEST_CLASS(SampleBufferQueueTest)
    {
    private:
        // Publish a single-byte batch carrying `marker`. TryPublish swaps the caller's buffer into the
        // slot, so a fresh local is used each time.
        static bool PublishMarker(SampleBufferQueue& queue, uint8_t marker)
        {
            std::vector<uint8_t> batch{ marker };
            return queue.TryPublish(batch);
        }

        // Read the oldest batch and return its first byte, or -1 if nothing was ready.
        static int ReadMarker(SampleBufferQueue& queue)
        {
            unsigned char buf[16] = { 0 };
            const int32_t n = queue.Read(static_cast<int32_t>(sizeof(buf)), buf);
            return n > 0 ? static_cast<int>(buf[0]) : -1;
        }

    public:

        // FIFO must hold across many interleaved publish/read cycles -- the header documents that a
        // naive scan-from-zero consumer would starve slot 1 once both slots have been filled once. We
        // publish/read repeatedly and assert every Read yields the OLDEST outstanding batch.
        TEST_METHOD(read_returns_oldest_batch_across_interleaved_cycles)
        {
            SampleBufferQueue queue;

            Assert::IsTrue(PublishMarker(queue, 1));
            Assert::IsTrue(PublishMarker(queue, 2)); // both slots now full

            Assert::AreEqual(1, ReadMarker(queue)); // oldest first
            Assert::IsTrue(PublishMarker(queue, 3)); // refills the freed slot

            // If slot 1 (holding marker 2) were starved, this would wrongly return 3.
            Assert::AreEqual(2, ReadMarker(queue));
            Assert::AreEqual(3, ReadMarker(queue));

            // Another full round to prove ordering is stable regardless of which physical slot is reused.
            Assert::IsTrue(PublishMarker(queue, 4));
            Assert::IsTrue(PublishMarker(queue, 5));
            Assert::AreEqual(4, ReadMarker(queue));
            Assert::IsTrue(PublishMarker(queue, 6));
            Assert::AreEqual(5, ReadMarker(queue));
            Assert::AreEqual(6, ReadMarker(queue));
        }

        // When both slots are filled, TryPublish returns false and must NOT touch the caller's buffer;
        // HasFreeSlot must also report false.
        TEST_METHOD(try_publish_when_full_returns_false_and_leaves_buffer_untouched)
        {
            SampleBufferQueue queue;

            Assert::IsTrue(PublishMarker(queue, 10));
            Assert::IsTrue(PublishMarker(queue, 20));
            Assert::IsFalse(queue.HasFreeSlot());

            std::vector<uint8_t> rejected{ 30, 31, 32 };
            Assert::IsFalse(queue.TryPublish(rejected));

            // Buffer must be untouched (no swap happened) -- still the caller's original three bytes.
            Assert::AreEqual(static_cast<size_t>(3), rejected.size());
            Assert::AreEqual(30, static_cast<int>(rejected[0]));
            Assert::AreEqual(31, static_cast<int>(rejected[1]));
            Assert::AreEqual(32, static_cast<int>(rejected[2]));
        }

        // Draining one slot after both are full must free capacity for a subsequent publish.
        TEST_METHOD(publish_succeeds_after_draining_a_full_queue)
        {
            SampleBufferQueue queue;

            Assert::IsTrue(PublishMarker(queue, 1));
            Assert::IsTrue(PublishMarker(queue, 2));
            Assert::IsFalse(queue.HasFreeSlot());

            Assert::AreEqual(1, ReadMarker(queue)); // frees a slot
            Assert::IsTrue(queue.HasFreeSlot());
            Assert::IsTrue(PublishMarker(queue, 3));
        }

        // A batch larger than `len` is truncated to `len` bytes, but the slot is freed either way.
        TEST_METHOD(read_truncates_to_len_but_still_frees_the_slot)
        {
            SampleBufferQueue queue;

            std::vector<uint8_t> batch{ 0xAA, 0xBB, 0xCC, 0xDD };
            Assert::IsTrue(queue.TryPublish(batch));

            unsigned char buf[2] = { 0 };
            const int32_t n = queue.Read(2, buf); // available (4) > len (2) -> truncate
            Assert::AreEqual(2, static_cast<int>(n));
            Assert::AreEqual(0xAA, static_cast<int>(buf[0]));
            Assert::AreEqual(0xBB, static_cast<int>(buf[1]));

            // Slot freed despite truncation: both slots should now be available again.
            Assert::IsTrue(queue.HasFreeSlot());
            Assert::IsTrue(PublishMarker(queue, 1));
            Assert::IsTrue(PublishMarker(queue, 2)); // both slots free before these -> both succeed
        }

        // Truncation must be observable, not silent: the dropped tail is counted so the caller can report
        // it. A read that fits entirely must leave the counters alone.
        TEST_METHOD(read_counts_truncated_batches_and_dropped_bytes)
        {
            SampleBufferQueue queue;

            std::vector<uint8_t> batch{ 1, 2, 3, 4, 5 };
            Assert::IsTrue(queue.TryPublish(batch));

            unsigned char tight[2] = { 0 }; // not `small`: rpcndr.h #defines that to char
            Assert::AreEqual(2, static_cast<int>(queue.Read(2, tight)));
            Assert::AreEqual(1, static_cast<int>(queue.TruncatedBatchCount()));
            Assert::AreEqual(3, static_cast<int>(queue.TruncatedByteCount())); // 5 available - 2 copied

            // A second truncating read accumulates on top of the first.
            std::vector<uint8_t> another{ 1, 2, 3, 4 };
            Assert::IsTrue(queue.TryPublish(another));
            Assert::AreEqual(2, static_cast<int>(queue.Read(2, tight)));
            Assert::AreEqual(2, static_cast<int>(queue.TruncatedBatchCount()));
            Assert::AreEqual(5, static_cast<int>(queue.TruncatedByteCount())); // 3 + (4 - 2)

            // A batch that fits leaves the counters untouched.
            unsigned char roomy[16] = { 0 };
            Assert::IsTrue(PublishMarker(queue, 9));
            Assert::AreEqual(1, static_cast<int>(queue.Read(static_cast<int32_t>(sizeof(roomy)), roomy)));
            Assert::AreEqual(2, static_cast<int>(queue.TruncatedBatchCount()));
            Assert::AreEqual(5, static_cast<int>(queue.TruncatedByteCount()));
        }

        // No batch ready is not a truncation -- an empty read must not touch the counters.
        TEST_METHOD(read_with_nothing_filled_does_not_count_truncation)
        {
            SampleBufferQueue queue;

            unsigned char buf[1] = { 0 };
            Assert::AreEqual(0, static_cast<int>(queue.Read(1, buf)));
            Assert::AreEqual(0, static_cast<int>(queue.TruncatedBatchCount()));
            Assert::AreEqual(0, static_cast<int>(queue.TruncatedByteCount()));
        }

        // Reading an empty queue returns 0.
        TEST_METHOD(read_with_nothing_filled_returns_zero)
        {
            SampleBufferQueue queue;

            unsigned char buf[8] = { 0 };
            Assert::AreEqual(0, static_cast<int>(queue.Read(static_cast<int32_t>(sizeof(buf)), buf)));
        }
    };
}}}

/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
#include "stdafx.h"
#include "CppUnitTest.h"

#include <cstdint>
#include <vector>

#include "../ContinuousProfiler/SampleBufferWriter.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace NewRelic { namespace Profiler { namespace ContinuousProfiler
{
    TEST_CLASS(SampleBufferWriterTest)
    {
    private:
        // Decode a big-endian int16 at `pos` -- mirrors BufferParser.ReadShort on the managed side.
        static int16_t ReadI16BE(const std::vector<uint8_t>& b, size_t pos)
        {
            return static_cast<int16_t>((static_cast<uint16_t>(b[pos]) << 8) | static_cast<uint16_t>(b[pos + 1]));
        }

    public:

        // WillFit is inclusive of maxBytes: exactly at the ceiling fits, one byte over does not.
        TEST_METHOD(will_fit_is_inclusive_of_max_bytes)
        {
            std::vector<uint8_t> buffer;
            SampleBufferWriter writer(buffer, 10);

            Assert::IsTrue(writer.WillFit(10));
            Assert::IsFalse(writer.WillFit(11));
        }

        // First sight of a frame writes a NEGATIVE index + the UTF-16LE string; a repeat writes only the
        // POSITIVE back-reference. Verified by decoding the raw bytes.
        TEST_METHOD(coded_frame_string_interns_then_back_references)
        {
            std::vector<uint8_t> buffer;
            SampleBufferWriter writer(buffer, 1024);
            writer.BeginBatch();

            writer.WriteCodedFrameString(_X("Foo"));

            // Definition: code -1, then string prefix 3, then 'F','o','o' as UTF-16LE.
            Assert::AreEqual(static_cast<int>(-1), static_cast<int>(ReadI16BE(buffer, 0)));
            Assert::AreEqual(static_cast<int>(3), static_cast<int>(ReadI16BE(buffer, 2)));
            Assert::AreEqual(static_cast<int>('F'), static_cast<int>(buffer[4]));
            Assert::AreEqual(0, static_cast<int>(buffer[5]));
            Assert::AreEqual(static_cast<int>('o'), static_cast<int>(buffer[6]));
            Assert::AreEqual(0, static_cast<int>(buffer[7]));
            Assert::AreEqual(static_cast<int>('o'), static_cast<int>(buffer[8]));
            Assert::AreEqual(0, static_cast<int>(buffer[9]));

            const size_t afterDefine = buffer.size();
            Assert::AreEqual(static_cast<size_t>(10), afterDefine);

            writer.WriteCodedFrameString(_X("Foo")); // repeat -> positive back-reference, no string bytes.
            Assert::AreEqual(static_cast<size_t>(12), buffer.size());
            Assert::AreEqual(static_cast<int>(1), static_cast<int>(ReadI16BE(buffer, afterDefine)));
        }

        // Once the interning table hits MaxFrameIndex the encoder must emit every further frame INLINE
        // (definition code -MaxFrameIndex) every time, never handing out a positive index that could wrap
        // and collide. Drive the table to the ceiling with distinct strings, then prove two successive
        // writes of the SAME new frame both emit the inline definition code (not a back-reference).
        TEST_METHOD(coded_frame_string_emits_inline_after_index_ceiling)
        {
            std::vector<uint8_t> buffer;
            SampleBufferWriter writer(buffer, 32u * 1024u * 1024u);
            writer.BeginBatch();

            // Interning indices 1..(MaxFrameIndex-1) leaves _nextFrameIndex == MaxFrameIndex, so the next
            // distinct frame trips the overflow branch. Deterministic loop, no sleeps; completes well
            // under a second.
            for (int i = 0; i < SampleBufferWriter::MaxFrameIndex - 1; ++i)
            {
                writer.WriteCodedFrameString(to_xstring(static_cast<unsigned int>(i)));
            }

            const size_t firstInlinePos = buffer.size();
            writer.WriteCodedFrameString(_X("overflow-frame"));
            Assert::AreEqual(
                static_cast<int>(-SampleBufferWriter::MaxFrameIndex),
                static_cast<int>(ReadI16BE(buffer, firstInlinePos)));

            const size_t secondInlinePos = buffer.size();
            writer.WriteCodedFrameString(_X("overflow-frame")); // same frame again -> STILL inline, never a back-ref.
            Assert::AreEqual(
                static_cast<int>(-SampleBufferWriter::MaxFrameIndex),
                static_cast<int>(ReadI16BE(buffer, secondInlinePos)));
        }

        // Strings longer than MaxStringChars (512) are truncated: the char-count prefix caps at 512 and
        // only 512 chars' worth of UTF-16LE bytes follow.
        TEST_METHOD(write_string_truncates_at_max_string_chars)
        {
            std::vector<uint8_t> buffer;
            SampleBufferWriter writer(buffer, 4096);

            const xstring_t longName(600, _X('x'));
            writer.WriteThreadName(longName);

            Assert::AreEqual(static_cast<int>(SampleBufferWriter::MaxStringChars), static_cast<int>(ReadI16BE(buffer, 0)));
            // 2-byte prefix + 512 chars * 2 bytes.
            Assert::AreEqual(static_cast<size_t>(2 + SampleBufferWriter::MaxStringChars * 2), buffer.size());
        }

        // StartBatch encodes the version byte then a big-endian int64 timestamp.
        TEST_METHOD(write_start_batch_is_big_endian)
        {
            std::vector<uint8_t> buffer;
            SampleBufferWriter writer(buffer, 1024);

            writer.WriteStartBatch(0x0102030405060708LL);

            Assert::AreEqual(0x01, static_cast<int>(buffer[0])); // StartBatch opcode
            Assert::AreEqual(3, static_cast<int>(buffer[1]));    // BatchVersion
            const uint8_t expected[8] = { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08 };
            for (int i = 0; i < 8; ++i)
            {
                Assert::AreEqual(static_cast<int>(expected[i]), static_cast<int>(buffer[2 + i]));
            }
        }

        // BatchStats encodes int64 micros + three big-endian int32s in field order.
        TEST_METHOD(write_batch_stats_is_big_endian_in_field_order)
        {
            std::vector<uint8_t> buffer;
            SampleBufferWriter writer(buffer, 1024);

            writer.WriteBatchStats(0x1122334455667788LL, 0x01020304, 0x0A0B0C0D, 0x00000005);

            Assert::AreEqual(0x07, static_cast<int>(buffer[0])); // BatchStats opcode
            const uint8_t expected[20] = {
                0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77, 0x88, // micros (int64)
                0x01, 0x02, 0x03, 0x04,                         // threadCount (int32)
                0x0A, 0x0B, 0x0C, 0x0D,                         // frameCount (int32)
                0x00, 0x00, 0x00, 0x05,                         // skipped (int32)
            };
            for (int i = 0; i < 20; ++i)
            {
                Assert::AreEqual(static_cast<int>(expected[i]), static_cast<int>(buffer[1 + i]));
            }
        }

        // BeginBatch clears the buffer AND the interning table, so a frame seen before BeginBatch is
        // re-interned (negative) afterward rather than emitted as a back-reference.
        TEST_METHOD(begin_batch_resets_buffer_and_frame_table)
        {
            std::vector<uint8_t> buffer;
            SampleBufferWriter writer(buffer, 1024);
            writer.BeginBatch();

            writer.WriteCodedFrameString(_X("Foo")); // interned as index 1
            Assert::IsTrue(writer.Size() > 0);

            writer.BeginBatch();
            Assert::AreEqual(static_cast<size_t>(0), writer.Size()); // buffer cleared

            writer.WriteCodedFrameString(_X("Foo")); // table cleared -> re-interned, negative code again
            Assert::AreEqual(static_cast<int>(-1), static_cast<int>(ReadI16BE(buffer, 0)));
        }
    };
}}}

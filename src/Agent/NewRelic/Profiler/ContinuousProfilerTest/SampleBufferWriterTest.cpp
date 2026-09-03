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

        // Truncation must not split a surrogate pair. With a pair straddling the 512-char cap, the cap is
        // pulled back to 511 so no unpaired leading surrogate is emitted (which the managed decoder would
        // turn into U+FFFD, and only for some truncation offsets -- breaking frame dedup).
        TEST_METHOD(write_string_does_not_split_a_surrogate_pair_at_the_cap)
        {
            std::vector<uint8_t> buffer;
            SampleBufferWriter writer(buffer, 4096);

            // 511 'x' then U+1F600 (D83D DE00): the low surrogate sits at index 512, one past the cap.
            xstring_t name(SampleBufferWriter::MaxStringChars - 1, _X('x'));
            name.push_back(static_cast<xchar_t>(0xD83D));
            name.push_back(static_cast<xchar_t>(0xDE00));

            writer.WriteThreadName(name);

            Assert::AreEqual(static_cast<int>(SampleBufferWriter::MaxStringChars) - 1, static_cast<int>(ReadI16BE(buffer, 0)));
            Assert::AreEqual(static_cast<size_t>(2 + (SampleBufferWriter::MaxStringChars - 1) * 2), buffer.size());
            // Last emitted code unit is the final 'x', not the orphaned leading surrogate.
            Assert::AreEqual(static_cast<int>('x'), static_cast<int>(buffer[buffer.size() - 2]));
            Assert::AreEqual(0, static_cast<int>(buffer[buffer.size() - 1]));
        }

        // The back-off is only for a SPLIT pair: a pair that ends exactly at the cap must be kept whole.
        TEST_METHOD(write_string_keeps_a_surrogate_pair_that_ends_at_the_cap)
        {
            std::vector<uint8_t> buffer;
            SampleBufferWriter writer(buffer, 4096);

            // 510 'x' then the full pair at indices 510/511, then trailing chars that get truncated away.
            xstring_t name(SampleBufferWriter::MaxStringChars - 2, _X('x'));
            name.push_back(static_cast<xchar_t>(0xD83D));
            name.push_back(static_cast<xchar_t>(0xDE00));
            name.append(50, _X('y'));

            writer.WriteThreadName(name);

            Assert::AreEqual(static_cast<int>(SampleBufferWriter::MaxStringChars), static_cast<int>(ReadI16BE(buffer, 0)));
            // Trailing low surrogate (DE00) is intact as the last UTF-16LE code unit.
            Assert::AreEqual(0x00, static_cast<int>(buffer[buffer.size() - 2]));
            Assert::AreEqual(0xDE, static_cast<int>(buffer[buffer.size() - 1]));
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

        // The producer contract: WillFit is advisory -- the field writers themselves never enforce the
        // ceiling, so the producer MUST gate on WillFit and skip a sample that would overflow. Prove that a
        // sample larger than the remaining capacity is correctly reported as not fitting, that skipping it
        // leaves the already-written bytes untouched (no corruption), and that the reserved trailer still
        // fits afterwards -- mirroring EncodeAndPublish's mid-batch truncation in ContinuousProfiler.h.
        TEST_METHOD(will_fit_refuses_an_oversized_sample_without_corrupting_the_buffer)
        {
            std::vector<uint8_t> buffer;
            SampleBufferWriter writer(buffer, 12); // room for StartBatch (10) + a 1-byte trailer, nothing more.
            writer.BeginBatch();

            writer.WriteStartBatch(0x0102030405060708LL);
            const size_t afterStartBatch = buffer.size();
            Assert::AreEqual(static_cast<size_t>(10), afterStartBatch);

            // A real sample is far larger than the 2 bytes left; the producer must refuse it.
            Assert::IsFalse(writer.WillFit(20));

            // Refusing means writing nothing: the buffer is byte-for-byte what it was before.
            Assert::AreEqual(afterStartBatch, buffer.size());
            const uint8_t expectedStartBatch[10] = { 0x01, 0x03, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08 };
            for (int i = 0; i < 10; ++i)
            {
                Assert::AreEqual(static_cast<int>(expectedStartBatch[i]), static_cast<int>(buffer[i]));
            }

            // The reserved trailer byte still fits and lands correctly -- the whole point of reserving it.
            Assert::IsTrue(writer.WillFit(1));
            writer.WriteEndBatch();
            Assert::AreEqual(static_cast<size_t>(11), buffer.size());
            Assert::AreEqual(0x06, static_cast<int>(buffer[10])); // EndBatch opcode
        }

        // Full producer loop under a tight ceiling: emit samples until the next one would overflow, then
        // break and write the trailer -- exactly EncodeAndPublish's shape. The batch stays within maxBytes
        // (never overruns), remains well-formed (StartBatch ... BatchStats/EndBatch), and only the samples
        // that actually fit are emitted.
        TEST_METHOD(producer_truncates_mid_batch_and_never_overruns_max_bytes)
        {
            // Sized to admit a couple of samples then force truncation: StartBatch(10) + 2*sample(39) +
            // trailer(22) = 110 fits, but a 3rd sample would need another 39 and does not.
            const size_t maxBytes = 120;
            std::vector<uint8_t> buffer;
            SampleBufferWriter writer(buffer, maxBytes);
            writer.BeginBatch();
            writer.WriteStartBatch(0);

            // Reserve enough for WriteBatchStats (1 + 8 + 4 + 4 + 4 = 21) + WriteEndBatch (1) = 22.
            const size_t trailerBytes = 22;

            // Each sample: opcode(1) + empty name prefix(2) + 4 int64(32) + 2 bool(2) + terminator(2) = 39.
            const size_t sampleBytes = 1 + 2 + 32 + 2 + 2;

            int emitted = 0;
            for (int i = 0; i < 100; ++i)
            {
                if (!writer.WillFit(sampleBytes + trailerBytes))
                {
                    break; // buffer full mid-batch -> truncate the rest, just like EncodeAndPublish.
                }
                writer.WriteStartSample();
                writer.WriteThreadName(_X(""));
                writer.WriteInt64Field(1);
                writer.WriteInt64Field(2);
                writer.WriteInt64Field(3);
                writer.WriteInt64Field(4);
                writer.WriteBoolField(true);
                writer.WriteBoolField(false);
                writer.WriteFrameListTerminator();
                ++emitted;
            }

            writer.WriteBatchStats(0, emitted, 0, 0);
            writer.WriteEndBatch();

            // The gate held: the finished batch never exceeded the hard ceiling.
            Assert::IsTrue(buffer.size() <= maxBytes);
            // The tight ceiling admitted at least one but not all 100 samples -> a real mid-batch truncation.
            Assert::IsTrue(emitted >= 1);
            Assert::IsTrue(emitted < 100);
            // Well-formed framing: opens with StartBatch, closes with EndBatch.
            Assert::AreEqual(0x01, static_cast<int>(buffer.front()));
            Assert::AreEqual(0x06, static_cast<int>(buffer.back()));
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

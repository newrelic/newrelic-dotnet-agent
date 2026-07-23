/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
#pragma once
#include <cstdint>
#include <limits>
#include <map>
#include <vector>

#include "../Common/xplat.h"

// SampleBufferWriter is the NATIVE encoder that produces the byte stream the managed
// NewRelic.Agent.Core.ContinuousProfiling.BufferParser decodes. It is the exact inverse of
// BufferParser.Parse -- every opcode value, byte order, string encoding and the frame-interning sign
// convention below is chosen to round-trip through that decoder field-for-field. If you change one
// side you MUST change the other. The frozen contract lives in:
//   src/Agent/NewRelic/Agent/Core/ContinuousProfiling/BufferParser.cs
//
// Wire format (mirrors BufferParser exactly):
//   * Integers (short/int/long) are BIG-ENDIAN.
//   * Strings are a BIG-ENDIAN int16 CHAR count (capped at 512), followed by that many UTF-16LE code
//     units (2 bytes each, low byte first). This matches BufferParser.ReadString: a big-endian short
//     prefix then Encoding.Unicode (== UTF-16LE) over charCount*2 bytes.
//   * Opcodes: StartBatch 0x01, StartSample 0x02, EndBatch 0x06, BatchStats 0x07.
//   * StartBatch payload: 1 version byte + int64 timestamp.
//   * BatchStats payload: int64 microsSuspended + int32 threadCount + int32 frameCount + int32 skipped.
//   * Per-sample payload (after StartSample): thread name (string) -> OS thread id (int64) ->
//     traceIdHigh (int64) -> traceIdLow (int64) -> spanId (int64) -> [v2+] onCpu (1 byte 0/1) ->
//     frame list -> terminator short 0.
//   * Frame list: 2-byte big-endian short codes terminated by 0. FIRST sight of a frame string writes
//     a NEGATIVE short (-index, index starting at 1) then the UTF-16 string, and remembers the index;
//     a SUBSEQUENT sight writes the POSITIVE index (a back-reference). This is the inverse of
//     BufferParser.ReadSample's dict[-code]=value (define) / dict[code] (lookup) logic.
//
// The encoder appends to a caller-owned byte buffer and never allocates while the runtime is suspended
// -- it runs only AFTER ResumeRuntime (see ContinuousProfiler.h). Encoding is guarded by a fixed max
// buffer size: WillFit() lets the producer refuse a sample that would overflow so it can be truncated
// + counted rather than growing without bound.
namespace NewRelic { namespace Profiler { namespace ContinuousProfiler
{
    // Opcodes -- must match BufferParser's private constants exactly.
    enum class BufferOpcode : uint8_t
    {
        StartBatch = 0x01,
        StartSample = 0x02,
        EndBatch = 0x06,
        BatchStats = 0x07,
    };

    class SampleBufferWriter
    {
    public:
        // Batch format version. v2 adds a 1-byte on-CPU flag per sample (after spanId, before the frame list).
        // Bumped only in lock-step with BufferParser (which branches on version >= 2).
        static constexpr uint8_t BatchVersion = 2;

        // BufferParser.ReadString caps a string at 512 chars; we cap identically so the big-endian
        // int16 char-count prefix never goes negative and both sides agree on the truncation point.
        static constexpr size_t MaxStringChars = 512;

        // Highest interning index we will ever hand out as a positive back-reference. Frame codes are
        // int16, and a code's sign is the define/lookup discriminator (negative => definition follows,
        // positive => back-reference), so the index must stay strictly within positive int16 range.
        // Once the table reaches this ceiling we STOP interning and emit every further frame inline as a
        // self-contained definition (see WriteCodedFrameString) -- a silent int16 wrap would otherwise
        // produce a positive code that collides with an existing back-reference and corrupt the stream.
        // Parenthesized to defeat the windows.h `max` function-like macro (min/max clash).
        static constexpr int16_t MaxFrameIndex = (std::numeric_limits<int16_t>::max)(); // 32767

        // Wrap a caller-owned buffer. maxBytes is the hard ceiling: the writer never grows past it, so
        // the producer can bound total memory and skip/truncate on overflow.
        SampleBufferWriter(std::vector<uint8_t>& buffer, size_t maxBytes) noexcept
            : _buffer(buffer), _maxBytes(maxBytes)
        {
        }

        // Reset per-batch state: clear the target buffer and the frame-interning dictionary so a fresh
        // batch starts with an empty per-batch string table (BufferParser builds its dict per Parse call).
        void BeginBatch() noexcept
        {
            _buffer.clear();
            _frameCodes.clear();
            _nextFrameIndex = 1; // index 0 is reserved as the frame-list terminator
        }

        // Bytes written so far.
        size_t Size() const noexcept
        {
            return _buffer.size();
        }

        // True if appending `additionalBytes` would stay within the max buffer size.
        bool WillFit(size_t additionalBytes) const noexcept
        {
            return _buffer.size() + additionalBytes <= _maxBytes;
        }

        //
        // Opcode writers -- the inverse of BufferParser's opcode switch.
        //

        // StartBatch 0x01 = version byte + int64 timestamp.
        void WriteStartBatch(int64_t timestamp)
        {
            WriteOpcode(BufferOpcode::StartBatch);
            WriteByte(BatchVersion);
            WriteLong(timestamp);
        }

        // StartSample 0x02. The per-sample payload is written by the caller via the field writers below.
        void WriteStartSample()
        {
            WriteOpcode(BufferOpcode::StartSample);
        }

        // BatchStats 0x07 = int64 microsSuspended + int32 threadCount + int32 frameCount + int32 skipped.
        // Field order and sizes match BufferParser's `pos += 8 + 4 + 4 + 4` skip exactly.
        void WriteBatchStats(int64_t microsSuspended, int32_t threadCount, int32_t frameCount, int32_t skipped)
        {
            WriteOpcode(BufferOpcode::BatchStats);
            WriteLong(microsSuspended);
            WriteInt(threadCount);
            WriteInt(frameCount);
            WriteInt(skipped);
        }

        // EndBatch 0x06 -- BufferParser returns as soon as it reads this.
        void WriteEndBatch()
        {
            WriteOpcode(BufferOpcode::EndBatch);
        }

        //
        // Per-sample field writers (call between WriteStartSample and WriteFrameListTerminator, in the
        // order BufferParser.ReadSample reads them).
        //

        // Thread name as a UTF-16 length-prefixed string.
        void WriteThreadName(const xstring_t& name)
        {
            WriteString(name);
        }

        // OS thread id / trace-context ids -- each an int64.
        void WriteInt64Field(int64_t value)
        {
            WriteLong(value);
        }

        // Per-sample on-CPU flag (v2+): a single 0/1 byte. Inverse of BufferParser.ReadBool.
        void WriteBoolField(bool value)
        {
            WriteByte(value ? 1 : 0);
        }

        // Write one frame using the per-batch interning table. First sight of `frame` emits a NEGATIVE
        // short (-index) followed by the UTF-16 string and remembers the index; a repeat emits the
        // POSITIVE index back-reference. Inverse of BufferParser's dict[-code]=value / dict[code].
        void WriteCodedFrameString(const xstring_t& frame)
        {
            const auto existing = _frameCodes.find(frame);
            if (existing != _frameCodes.end())
            {
                WriteShort(existing->second); // positive back-reference
                return;
            }

            // Overflow guard: once the interning table is full we can no longer hand out a new positive
            // index without risking an int16 wrap (a wrapped positive code would collide with an existing
            // back-reference and corrupt the frame stream). Instead, emit the frame INLINE every time
            // using a reserved definition code (-MaxFrameIndex) that is NEVER handed out as a real intern
            // index -- so the parser always reconstructs the correct string and no positive lookup ever
            // reads the clobbered dict[MaxFrameIndex] slot. Deterministic, never corrupts, never wraps.
            if (_nextFrameIndex >= MaxFrameIndex)
            {
                WriteShort(static_cast<int16_t>(-MaxFrameIndex)); // negative => "definition follows"
                WriteString(frame);
                return;
            }

            const int16_t index = _nextFrameIndex++;
            _frameCodes.emplace(frame, index);
            WriteShort(static_cast<int16_t>(-index)); // negative => "definition follows"
            WriteString(frame);
        }

        // Frame-list terminator: a short 0 (BufferParser breaks the frame loop on code == 0).
        void WriteFrameListTerminator()
        {
            WriteShort(0);
        }

    private:
        // Append a single raw byte.
        void WriteByte(uint8_t b)
        {
            _buffer.push_back(b);
        }

        void WriteOpcode(BufferOpcode opcode)
        {
            WriteByte(static_cast<uint8_t>(opcode));
        }

        // Big-endian int16 (most-significant byte first) -- inverse of BufferParser.ReadShort.
        void WriteShort(int16_t value)
        {
            const uint16_t v = static_cast<uint16_t>(value);
            WriteByte(static_cast<uint8_t>((v >> 8) & 0xFF));
            WriteByte(static_cast<uint8_t>(v & 0xFF));
        }

        // Big-endian int32 -- BufferParser only skips int32s (BatchStats), but the byte order is fixed
        // by the shared contract so any future int32 read decodes correctly.
        void WriteInt(int32_t value)
        {
            const uint32_t v = static_cast<uint32_t>(value);
            for (int shift = 24; shift >= 0; shift -= 8)
            {
                WriteByte(static_cast<uint8_t>((v >> shift) & 0xFF));
            }
        }

        // Big-endian int64 -- inverse of BufferParser.ReadLong.
        void WriteLong(int64_t value)
        {
            const uint64_t v = static_cast<uint64_t>(value);
            for (int shift = 56; shift >= 0; shift -= 8)
            {
                WriteByte(static_cast<uint8_t>((v >> shift) & 0xFF));
            }
        }

        // Big-endian int16 char-count prefix, then the UTF-16LE code units. Each xchar_t is a 2-byte
        // UTF-16 code unit; we emit it as (low byte, high byte) explicitly so the encoding is UTF-16LE
        // regardless of host byte order -- exactly what Encoding.Unicode.GetString expects on the
        // managed side. Capped at MaxStringChars to keep the char count within a signed int16.
        void WriteString(const xstring_t& str)
        {
            size_t charCount = str.size();
            if (charCount > MaxStringChars)
            {
                charCount = MaxStringChars;
            }

            WriteShort(static_cast<int16_t>(charCount));
            for (size_t i = 0; i < charCount; ++i)
            {
                const uint16_t codeUnit = static_cast<uint16_t>(str[i]);
                WriteByte(static_cast<uint8_t>(codeUnit & 0xFF));        // low byte first (LE)
                WriteByte(static_cast<uint8_t>((codeUnit >> 8) & 0xFF)); // high byte
            }
        }

        // The caller-owned output buffer.
        std::vector<uint8_t>& _buffer;

        // Hard ceiling on the buffer size (WillFit gate).
        size_t _maxBytes;

        // Per-batch frame-string interning table: frame name -> positive back-reference index (>= 1).
        // Rebuilt every BeginBatch so it mirrors BufferParser's per-Parse dictionary lifetime.
        std::map<xstring_t, int16_t> _frameCodes;

        // Next interning index to hand out; starts at 1 because 0 is the frame-list terminator.
        int16_t _nextFrameIndex{ 1 };
    };
}}}

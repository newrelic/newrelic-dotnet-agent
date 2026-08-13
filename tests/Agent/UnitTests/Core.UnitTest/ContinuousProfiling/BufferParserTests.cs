// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NewRelic.Agent.Core.ContinuousProfiling;
using NUnit.Framework;

namespace NewRelic.Agent.Core.UnitTest.ContinuousProfiling;

[TestFixture]
public class BufferParserTests
{
    private const byte StartBatch = 0x01, StartSample = 0x02, EndBatch = 0x06, BatchStats = 0x07;

    private static void WriteShort(MemoryStream s, short v) { s.WriteByte((byte)(v >> 8)); s.WriteByte((byte)v); }
    private static void WriteLong(MemoryStream s, long v) { for (var i = 7; i >= 0; i--) s.WriteByte((byte)(v >> (i * 8))); }
    private static void WriteString(MemoryStream s, string v)
    {
        var bytes = Encoding.Unicode.GetBytes(v); // UTF-16LE
        WriteShort(s, (short)v.Length);
        s.Write(bytes, 0, bytes.Length);
    }

    private static byte[] OneSampleBatch(string thread, long osId, long tHigh, long tLow, long span, string[] framesLeafFirst)
    {
        using var s = new MemoryStream();
        s.WriteByte(StartBatch); s.WriteByte(1); WriteLong(s, 123456789L); // version + timestamp
        s.WriteByte(StartSample);
        WriteString(s, thread); WriteLong(s, osId); WriteLong(s, tHigh); WriteLong(s, tLow); WriteLong(s, span);
        short next = 1;
        foreach (var f in framesLeafFirst) { WriteShort(s, (short)-next); WriteString(s, f); next++; } // all new defs
        WriteShort(s, 0); // end of frames
        s.WriteByte(EndBatch);
        return s.ToArray();
    }

    [Test]
    public void Parse_single_sample_yields_thread_frames_and_context()
    {
        var buf = OneSampleBatch("worker-1", 4242, 0x11, 0x22, 0x33, new[] { "A.B.Leaf()", "A.B.Root()" });
        var samples = BufferParser.Parse(buf, buf.Length);

        Assert.That(samples, Has.Count.EqualTo(1));
        var s = samples[0];
        Assert.Multiple(() =>
        {
            Assert.That(s.ThreadName, Is.EqualTo("worker-1"));
            Assert.That(s.OsThreadId, Is.EqualTo(4242));
            Assert.That(s.TraceIdHigh, Is.EqualTo(0x11));
            Assert.That(s.TraceIdLow, Is.EqualTo(0x22));
            Assert.That(s.SpanId, Is.EqualTo(0x33));
            Assert.That(s.Frames, Is.EqualTo(new[] { "A.B.Leaf()", "A.B.Root()" }));
        });
    }

    [Test]
    public void Parse_back_reference_reuses_interned_frame()
    {
        using var ms = new MemoryStream();
        ms.WriteByte(StartBatch); ms.WriteByte(1); WriteLong(ms, 1L);
        // sample 1 defines frame -1
        ms.WriteByte(StartSample); WriteString(ms, "t1"); WriteLong(ms, 1); WriteLong(ms, 0); WriteLong(ms, 0); WriteLong(ms, 0);
        WriteShort(ms, -1); WriteString(ms, "Shared.Frame()"); WriteShort(ms, 0);
        // sample 2 back-references code 1
        ms.WriteByte(StartSample); WriteString(ms, "t2"); WriteLong(ms, 2); WriteLong(ms, 0); WriteLong(ms, 0); WriteLong(ms, 0);
        WriteShort(ms, 1); WriteShort(ms, 0);
        ms.WriteByte(EndBatch);
        var buf = ms.ToArray();

        var samples = BufferParser.Parse(buf, buf.Length);
        Assert.That(samples[1].Frames, Is.EqualTo(new[] { "Shared.Frame()" }));
    }

    [Test]
    public void Parse_truncated_buffer_returns_completed_samples_without_throwing()
    {
        var buf = OneSampleBatch("worker", 1, 0, 0, 0, new[] { "F()" });
        // pass a length that cuts off mid-batch
        var samples = BufferParser.Parse(buf, buf.Length - 3);
        Assert.That(samples, Is.Not.Null); // no throw; partial tolerated
    }

    [Test]
    public void Parse_empty_buffer_returns_empty()
    {
        Assert.That(BufferParser.Parse(new byte[0], 0), Is.Empty);
    }

    [Test]
    public void Parse_null_buffer_returns_empty()
    {
        Assert.That(BufferParser.Parse(null, 10), Is.Empty);
    }

    [Test]
    public void Parse_negative_length_returns_empty()
    {
        Assert.That(BufferParser.Parse(new byte[] { StartBatch }, -1), Is.Empty);
    }

    [Test]
    public void Parse_unknown_back_reference_yields_unknown_placeholder()
    {
        using var ms = new MemoryStream();
        ms.WriteByte(StartBatch); ms.WriteByte(1); WriteLong(ms, 1L);
        ms.WriteByte(StartSample); WriteString(ms, "t1"); WriteLong(ms, 1); WriteLong(ms, 0); WriteLong(ms, 0); WriteLong(ms, 0);
        WriteShort(ms, 99); // back-reference to a code that was never defined
        WriteShort(ms, 0);
        ms.WriteByte(EndBatch);
        var buf = ms.ToArray();

        var samples = BufferParser.Parse(buf, buf.Length);
        Assert.That(samples[0].Frames, Is.EqualTo(new[] { "<unknown>" }));
    }

    [Test]
    public void Parse_batch_stats_are_captured_without_affecting_samples()
    {
        using var ms = new MemoryStream();
        ms.WriteByte(StartBatch); ms.WriteByte(1); WriteLong(ms, 1L);
        ms.WriteByte(StartSample); WriteString(ms, "t1"); WriteLong(ms, 1); WriteLong(ms, 0); WriteLong(ms, 0); WriteLong(ms, 0);
        WriteShort(ms, 0); // no frames
        ms.WriteByte(BatchStats);
        WriteLong(ms, 42L); // microsSuspended
        ms.Write(new byte[] { 0, 0, 0, 1 }, 0, 4); // threads
        ms.Write(new byte[] { 0, 0, 0, 2 }, 0, 4); // frames
        ms.Write(new byte[] { 0, 0, 0, 3 }, 0, 4); // skipped
        ms.WriteByte(EndBatch);
        var buf = ms.ToArray();

        var samples = BufferParser.Parse(buf, buf.Length, out var stats);

        Assert.That(samples, Has.Count.EqualTo(1));
        Assert.That(stats, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(stats!.MicrosSuspended, Is.EqualTo(42L));
            Assert.That(stats.Threads, Is.EqualTo(1));
            Assert.That(stats.Frames, Is.EqualTo(2));
            Assert.That(stats.Skipped, Is.EqualTo(3));
        });
    }

    [Test]
    public void Parse_without_batch_stats_yields_null_stats()
    {
        var buf = OneSampleBatch("worker", 1, 0, 0, 0, new[] { "F()" });
        var samples = BufferParser.Parse(buf, buf.Length, out var stats);
        Assert.That(samples, Has.Count.EqualTo(1));
        Assert.That(stats, Is.Null);
    }

    [Test]
    public void Parse_two_arg_overload_still_returns_samples()
    {
        var buf = OneSampleBatch("worker", 1, 0, 0, 0, new[] { "F()" });
        Assert.That(BufferParser.Parse(buf, buf.Length), Has.Count.EqualTo(1));
    }

    [Test]
    public void Parse_unknown_opcode_stops_cleanly()
    {
        using var ms = new MemoryStream();
        ms.WriteByte(StartBatch); ms.WriteByte(1); WriteLong(ms, 1L);
        ms.WriteByte(StartSample); WriteString(ms, "t1"); WriteLong(ms, 1); WriteLong(ms, 0); WriteLong(ms, 0); WriteLong(ms, 0);
        WriteShort(ms, 0); // no frames
        ms.WriteByte(0xFF); // unrecognized opcode
        ms.WriteByte(EndBatch);
        var buf = ms.ToArray();

        var samples = BufferParser.Parse(buf, buf.Length);
        Assert.That(samples, Has.Count.EqualTo(1));
    }

    [Test]
    public void Parse_buffer_truncated_mid_string_throws_internally_and_returns_completed_samples()
    {
        using var ms = new MemoryStream();
        ms.WriteByte(StartBatch); ms.WriteByte(1); WriteLong(ms, 1L);
        ms.WriteByte(StartSample); WriteString(ms, "t1"); WriteLong(ms, 1); WriteLong(ms, 0); WriteLong(ms, 0); WriteLong(ms, 0);
        WriteShort(ms, -1); WriteString(ms, "Frame()");
        var buf = ms.ToArray();
        // truncate mid-way through the frame string bytes so ReadString runs off the end of the array
        var truncated = new byte[buf.Length - 3];
        System.Array.Copy(buf, truncated, truncated.Length);

        var samples = BufferParser.Parse(truncated, truncated.Length);
        Assert.That(samples, Is.Empty); // sample never completed; no throw
    }

    // --- batch v2 OnCpu flag ---

    private static byte[] Int64BE(long v)
    {
        var bytes = new byte[8];
        for (var i = 0; i < 8; i++) bytes[i] = (byte)(v >> ((7 - i) * 8));
        return bytes;
    }

    private static byte[] ShortBE(short v) => new[] { (byte)(v >> 8), (byte)v };

    private static byte[] Utf16Str(string v)
    {
        var b = new List<byte>();
        b.AddRange(ShortBE((short)v.Length));
        b.AddRange(Encoding.Unicode.GetBytes(v)); // UTF-16LE
        return b.ToArray();
    }

    private static byte[] BuildV2Batch(bool onCpu)
    {
        var b = new List<byte>();
        b.Add(0x01);                 // StartBatch
        b.Add(0x02);                 // version = 2
        b.AddRange(Int64BE(123L));   // timestamp
        b.Add(0x02);                 // StartSample
        b.AddRange(Utf16Str("t1"));  // thread name
        b.AddRange(Int64BE(4242L));  // osThreadId
        b.AddRange(Int64BE(0L));     // traceHigh
        b.AddRange(Int64BE(0L));     // traceLow
        b.AddRange(Int64BE(0L));     // spanId
        b.Add((byte)(onCpu ? 1 : 0));// OnCpu (v2 only)
        b.AddRange(ShortBE(-1));     // frame define, index 1
        b.AddRange(Utf16Str("Frame.One"));
        b.AddRange(ShortBE(0));      // frame terminator
        b.Add(0x06);                 // EndBatch
        return b.ToArray();
    }

    [Test]
    public void Parse_v2_capturesOnCpuTrue()
    {
        var buf = BuildV2Batch(onCpu: true);
        var samples = BufferParser.Parse(buf, buf.Length);
        Assert.That(samples, Has.Count.EqualTo(1));
        Assert.That(samples[0].OnCpu, Is.True);
        Assert.That(samples[0].Frames, Is.EqualTo(new[] { "Frame.One" }));
    }

    [Test]
    public void Parse_v2_capturesOnCpuFalse()
    {
        var buf = BuildV2Batch(onCpu: false);
        var samples = BufferParser.Parse(buf, buf.Length);
        Assert.That(samples[0].OnCpu, Is.False);
    }

    [Test]
    public void Parse_v1_hasNoOnCpuByte_defaultsFalse()
    {
        // v1 batch: same layout but version byte 1 and NO OnCpu byte after spanId.
        var b = new List<byte> { 0x01, 0x01 };
        b.AddRange(Int64BE(123L));
        b.Add(0x02);
        b.AddRange(Utf16Str("t1"));
        b.AddRange(Int64BE(4242L)); b.AddRange(Int64BE(0L)); b.AddRange(Int64BE(0L)); b.AddRange(Int64BE(0L));
        b.AddRange(ShortBE(-1)); b.AddRange(Utf16Str("Frame.One")); b.AddRange(ShortBE(0));
        b.Add(0x06);
        var buf = b.ToArray();
        var samples = BufferParser.Parse(buf, buf.Length);
        Assert.That(samples[0].OnCpu, Is.False);
        Assert.That(samples[0].Frames, Is.EqualTo(new[] { "Frame.One" }));
    }

    [Test]
    public void Parse_v2_truncatedBeforeOnCpuByte_returnsWhatParsed()
    {
        var full = BuildV2Batch(onCpu: true);

        // Parse's `length` param only bounds the outer opcode-dispatch loop; reads within a sample
        // go straight to the array regardless of `length` (see the mid-string truncation test above).
        // So to actually exercise the truncation/no-throw path, physically shorten the array right
        // after spanId (before the OnCpu byte) by rebuilding the same prefix independently.
        var prefix = new List<byte>();
        prefix.Add(0x01);                 // StartBatch
        prefix.Add(0x02);                 // version = 2
        prefix.AddRange(Int64BE(123L));   // timestamp
        prefix.Add(0x02);                 // StartSample
        prefix.AddRange(Utf16Str("t1"));  // thread name
        prefix.AddRange(Int64BE(4242L));  // osThreadId
        prefix.AddRange(Int64BE(0L));     // traceHigh
        prefix.AddRange(Int64BE(0L));     // traceLow
        prefix.AddRange(Int64BE(0L));     // spanId
        var cutLength = prefix.Count;     // ends right before the OnCpu byte

        var truncated = new byte[cutLength];
        Array.Copy(full, truncated, cutLength);

        var samples = BufferParser.Parse(truncated, truncated.Length);
        Assert.That(samples, Is.Empty);
    }
}

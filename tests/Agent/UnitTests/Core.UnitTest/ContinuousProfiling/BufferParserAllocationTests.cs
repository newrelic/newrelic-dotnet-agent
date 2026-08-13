// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Text;
using NUnit.Framework;

namespace NewRelic.Agent.Core.ContinuousProfiling.Tests;

[TestFixture]
public class BufferParserAllocationTests
{
    // Hand-encodes one 0x08 AllocationSample record using the SAME field order/types the native
    // SampleBufferWriter.WriteAllocationSample will use: opcode, thread name (string), OS thread id
    // (int64), traceIdHigh/traceIdLow/spanId (int64 x3), timestampMillis (int64), allocatedSize
    // (int64, bit-reinterpreted uint64), typeName (string), frame list, terminator.
    private static byte[] EncodeOneAllocationSample()
    {
        var bytes = new List<byte>();
        void WriteShort(short v) { bytes.Add((byte)((v >> 8) & 0xFF)); bytes.Add((byte)(v & 0xFF)); }
        void WriteLong(long v) { for (var shift = 56; shift >= 0; shift -= 8) bytes.Add((byte)((v >> shift) & 0xFF)); }
        void WriteString(string s)
        {
            WriteShort((short)s.Length);
            bytes.AddRange(Encoding.Unicode.GetBytes(s));
        }

        bytes.Add(0x01); // StartBatch
        bytes.Add(2);    // version
        WriteLong(1000L); // timestamp

        bytes.Add(0x08); // AllocationSample
        WriteString("worker-thread");
        WriteLong(4242L);           // OS thread id
        WriteLong(111L);            // traceIdHigh
        WriteLong(222L);            // traceIdLow
        WriteLong(333L);            // spanId
        WriteLong(1700000000000L);  // timestampMillis
        WriteLong(unchecked((long)65536UL)); // allocatedSize (bit pattern of a ulong)
        WriteString("MyApp.Widget");
        WriteShort(-1); WriteString("MyApp.Widget.Create()"); // frame, first sight -> define
        WriteShort(0); // frame list terminator

        bytes.Add(0x06); // EndBatch
        return bytes.ToArray();
    }

    // Two 0x08 records in ONE batch, which is what the native sampler emits once back-pressure makes it
    // accumulate several samples into a single published batch. The second sample's frame is a POSITIVE
    // back-reference to the definition the first sample wrote -- the per-batch interning table is shared
    // across every sample in the batch, so this is the shape a multi-sample batch really has, and the shape
    // that would break if the native encoder ever reset its interning table mid-batch. The batch also carries
    // a 0x07 BatchStats record, which is how the allocation sampler reports dropped samples in band.
    private static byte[] EncodeTwoAllocationSamplesWithSharedFrameAndStats(int skipped)
    {
        var bytes = new List<byte>();
        void WriteShort(short v) { bytes.Add((byte)((v >> 8) & 0xFF)); bytes.Add((byte)(v & 0xFF)); }
        void WriteInt(int v) { for (var shift = 24; shift >= 0; shift -= 8) bytes.Add((byte)((v >> shift) & 0xFF)); }
        void WriteLong(long v) { for (var shift = 56; shift >= 0; shift -= 8) bytes.Add((byte)((v >> shift) & 0xFF)); }
        void WriteString(string s)
        {
            WriteShort((short)s.Length);
            bytes.AddRange(Encoding.Unicode.GetBytes(s));
        }
        void WriteSampleFields(long osThreadId, ulong allocatedSize)
        {
            bytes.Add(0x08);
            WriteString("worker-thread");
            WriteLong(osThreadId);
            WriteLong(111L); WriteLong(222L); WriteLong(333L);
            WriteLong(1700000000000L);
            WriteLong(unchecked((long)allocatedSize));
            WriteString("MyApp.Widget");
        }

        bytes.Add(0x01); // StartBatch
        bytes.Add(2);    // version
        WriteLong(1000L);

        WriteSampleFields(1, 1024UL);
        WriteShort(-1); WriteString("MyApp.Widget.Create()"); // first sight -> define index 1
        WriteShort(0);

        WriteSampleFields(2, 2048UL);
        WriteShort(1); // back-reference to the frame defined by the first sample
        WriteShort(0);

        bytes.Add(0x07); // BatchStats
        WriteLong(0L);   // microsSuspended (not meaningful for an allocation batch)
        WriteInt(0);     // threads
        WriteInt(0);     // frames
        WriteInt(skipped);

        bytes.Add(0x06); // EndBatch
        return bytes.ToArray();
    }

    [Test]
    public void Parse_DecodesEveryAllocationSampleInAMultiSampleBatch_SharingTheFrameTable()
    {
        var buffer = EncodeTwoAllocationSamplesWithSharedFrameAndStats(skipped: 12);

        var samples = BufferParser.Parse(buffer, buffer.Length, out var stats, out var allocations);

        Assert.That(samples, Is.Empty);
        Assert.That(allocations, Has.Count.EqualTo(2), "both allocation records in the batch must decode");
        Assert.That(allocations[0].OsThreadId, Is.EqualTo(1L));
        Assert.That(allocations[1].OsThreadId, Is.EqualTo(2L));
        Assert.That(allocations[0].AllocatedSize, Is.EqualTo(1024UL));
        Assert.That(allocations[1].AllocatedSize, Is.EqualTo(2048UL));
        Assert.That(allocations[1].Frames, Is.EqualTo(new[] { "MyApp.Widget.Create()" }),
            "the second sample's positive frame code must resolve through the batch's shared interning table");

        Assert.That(stats, Is.Not.Null, "an allocation batch may carry BatchStats");
        Assert.That(stats.Skipped, Is.EqualTo(12), "Skipped is how the native allocation sampler reports dropped samples");
    }

    [Test]
    public void Parse_DecodesOneAllocationSample_WithTraceContextAndFrames()
    {
        var buffer = EncodeOneAllocationSample();

        var samples = BufferParser.Parse(buffer, buffer.Length, out _, out var allocations);

        Assert.That(samples, Is.Empty, "no thread samples in this batch");
        Assert.That(allocations, Has.Count.EqualTo(1));
        var alloc = allocations[0];
        Assert.That(alloc.ThreadName, Is.EqualTo("worker-thread"));
        Assert.That(alloc.OsThreadId, Is.EqualTo(4242L));
        Assert.That(alloc.TraceIdHigh, Is.EqualTo(111L));
        Assert.That(alloc.TraceIdLow, Is.EqualTo(222L));
        Assert.That(alloc.SpanId, Is.EqualTo(333L));
        Assert.That(alloc.TimestampMillis, Is.EqualTo(1700000000000L));
        Assert.That(alloc.AllocatedSize, Is.EqualTo(65536UL));
        Assert.That(alloc.TypeName, Is.EqualTo("MyApp.Widget"));
        Assert.That(alloc.Frames, Is.EqualTo(new[] { "MyApp.Widget.Create()" }));
    }
}

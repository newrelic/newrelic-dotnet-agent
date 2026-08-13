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

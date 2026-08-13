// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Text;

namespace NewRelic.Agent.Core.ContinuousProfiling;

public static class BufferParser
{
    private const byte StartBatch = 0x01;
    private const byte StartSample = 0x02;
    private const byte EndBatch = 0x06;
    private const byte BatchStatsOpcode = 0x07;

    /// <summary>
    /// Per-sweep native BatchStats (opcode 0x07). <see cref="MicrosSuspended"/> is the actual runtime-suspend
    /// (stop-the-world) window for this sample sweep; <see cref="Skipped"/> is threads/frames the stack walk
    /// couldn't capture. These are the direct CP overhead + fidelity signals, and mirror OTel's FinalStats
    /// (microsSuspended / threads / frames / cache-misses) for like-for-like comparison.
    /// </summary>
    public sealed class BatchStats
    {
        public long MicrosSuspended { get; }
        public int Threads { get; }
        public int Frames { get; }
        public int Skipped { get; }

        public BatchStats(long microsSuspended, int threads, int frames, int skipped)
        {
            MicrosSuspended = microsSuspended;
            Threads = threads;
            Frames = frames;
            Skipped = skipped;
        }
    }

    public static IReadOnlyList<ManagedThreadSample> Parse(byte[] buffer, int length)
        => Parse(buffer, length, out _);

    /// <summary>
    /// Parse overload that also captures the batch's <see cref="BatchStats"/> (null when the batch carried
    /// none). Callers use it to surface the suspend-window / coverage counters.
    /// </summary>
    public static IReadOnlyList<ManagedThreadSample> Parse(byte[] buffer, int length, out BatchStats stats)
    {
        stats = null;
        var samples = new List<ManagedThreadSample>();
        if (buffer == null || length <= 0)
            return samples;

        var frameDictionary = new Dictionary<int, string>();
        var pos = 0;
        var version = 0;
        try
        {
            while (pos < length)
            {
                var opcode = buffer[pos++];
                switch (opcode)
                {
                    case StartBatch:
                        version = buffer[pos++];
                        pos += 8; // timestamp (int64)
                        break;
                    case StartSample:
                        samples.Add(ReadSample(buffer, ref pos, frameDictionary, version, length));
                        break;
                    case BatchStatsOpcode:
                        {
                            var micros = ReadLong(buffer, ref pos, length);   // microsSuspended (int64)
                            var threads = ReadInt(buffer, ref pos, length);   // threads
                            var frames = ReadInt(buffer, ref pos, length);    // frames
                            var skipped = ReadInt(buffer, ref pos, length);   // skipped
                            stats = new BatchStats(micros, threads, frames, skipped);
                            break;
                        }
                    case EndBatch:
                        return samples;
                    default:
                        return samples; // unknown opcode -> stop cleanly
                }
            }
        }
        catch (Exception)
        {
            // truncated/garbage past `length`: return what parsed cleanly (Global Constraint: never throw)
        }
        return samples;
    }

    private static ManagedThreadSample ReadSample(byte[] b, ref int pos, Dictionary<int, string> dict, int version, int length)
    {
        var threadName = ReadString(b, ref pos, length);
        var osThreadId = ReadLong(b, ref pos, length);
        var traceHigh = ReadLong(b, ref pos, length);
        var traceLow = ReadLong(b, ref pos, length);
        var spanId = ReadLong(b, ref pos, length);
        var onCpu = version >= 2 && ReadBool(b, ref pos, length);
        var isAgentWork = version >= 3 && ReadBool(b, ref pos, length);

        var frames = new List<string>();
        while (true)
        {
            var code = ReadShort(b, ref pos, length);
            if (code == 0) break;
            if (code < 0)
            {
                var value = ReadString(b, ref pos, length);
                dict[-code] = value;
                frames.Add(value);
            }
            else
            {
                frames.Add(dict.TryGetValue(code, out var v) ? v : "<unknown>");
            }
        }
        return new ManagedThreadSample(threadName, osThreadId, traceHigh, traceLow, spanId, frames, onCpu, isAgentWork);
    }

    // `length` is the logical bound for this parse and may be smaller than the physical buffer
    // (a reused, oversized array). Field reads must stay inside it -- not just inside the array --
    // so a future writer that ever emits a partial record can't read stale bytes from a prior batch.
    private static void RequireBound(int pos, int size, int length)
    {
        if (pos + size > length)
            throw new IndexOutOfRangeException();
    }

    private static bool ReadBool(byte[] b, ref int pos, int length)
    {
        RequireBound(pos, 1, length);
        var v = b[pos];
        pos += 1;
        return v != 0;
    }

    private static short ReadShort(byte[] b, ref int pos, int length)
    {
        RequireBound(pos, 2, length);
        var v = (short)((b[pos] << 8) | b[pos + 1]);
        pos += 2;
        return v;
    }

    private static int ReadInt(byte[] b, ref int pos, int length)
    {
        RequireBound(pos, 4, length);
        var v = 0;
        for (var i = 0; i < 4; i++) v = (v << 8) | b[pos + i];
        pos += 4;
        return v;
    }

    private static long ReadLong(byte[] b, ref int pos, int length)
    {
        RequireBound(pos, 8, length);
        long v = 0;
        for (var i = 0; i < 8; i++) v = (v << 8) | b[pos + i];
        pos += 8;
        return v;
    }

    private static string ReadString(byte[] b, ref int pos, int length)
    {
        var charCount = ReadShort(b, ref pos, length);
        var byteCount = charCount * 2;
        RequireBound(pos, byteCount, length);
        var s = Encoding.Unicode.GetString(b, pos, byteCount);
        pos += byteCount;
        return s;
    }
}

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
                        samples.Add(ReadSample(buffer, ref pos, frameDictionary, version));
                        break;
                    case BatchStatsOpcode:
                        {
                            var micros = ReadLong(buffer, ref pos);   // microsSuspended (int64)
                            var threads = ReadInt(buffer, ref pos);   // threads
                            var frames = ReadInt(buffer, ref pos);    // frames
                            var skipped = ReadInt(buffer, ref pos);   // skipped
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

    private static ManagedThreadSample ReadSample(byte[] b, ref int pos, Dictionary<int, string> dict, int version)
    {
        var threadName = ReadString(b, ref pos);
        var osThreadId = ReadLong(b, ref pos);
        var traceHigh = ReadLong(b, ref pos);
        var traceLow = ReadLong(b, ref pos);
        var spanId = ReadLong(b, ref pos);
        var onCpu = version >= 2 && ReadBool(b, ref pos);

        var frames = new List<string>();
        while (true)
        {
            var code = ReadShort(b, ref pos);
            if (code == 0) break;
            if (code < 0)
            {
                var value = ReadString(b, ref pos);
                dict[-code] = value;
                frames.Add(value);
            }
            else
            {
                frames.Add(dict.TryGetValue(code, out var v) ? v : "<unknown>");
            }
        }
        return new ManagedThreadSample(threadName, osThreadId, traceHigh, traceLow, spanId, frames, onCpu);
    }

    private static bool ReadBool(byte[] b, ref int pos)
    {
        var v = b[pos];
        pos += 1;
        return v != 0;
    }

    private static short ReadShort(byte[] b, ref int pos)
    {
        var v = (short)((b[pos] << 8) | b[pos + 1]);
        pos += 2;
        return v;
    }

    private static int ReadInt(byte[] b, ref int pos)
    {
        var v = 0;
        for (var i = 0; i < 4; i++) v = (v << 8) | b[pos + i];
        pos += 4;
        return v;
    }

    private static long ReadLong(byte[] b, ref int pos)
    {
        long v = 0;
        for (var i = 0; i < 8; i++) v = (v << 8) | b[pos + i];
        pos += 8;
        return v;
    }

    private static string ReadString(byte[] b, ref int pos)
    {
        var charCount = ReadShort(b, ref pos);
        var byteCount = charCount * 2;
        var s = Encoding.Unicode.GetString(b, pos, byteCount);
        pos += byteCount;
        return s;
    }
}

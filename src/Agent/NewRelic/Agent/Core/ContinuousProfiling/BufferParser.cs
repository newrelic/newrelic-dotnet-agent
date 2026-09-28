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

    // v1: base layout. v2 adds a 1-byte OnCpu flag after spanId -- REMOVED in v6 (cpu/off_cpu sample
    // buckets merged back into a single cpu bucket). v3 adds a 1-byte IsAgentWork flag, now directly
    // after spanId. v4 appends two int32s to the BatchStats record (deferredThreads +
    // actualSamplePeriodMs). v5 appends two more (truncatedThreads + droppedTicks -- native
    // data-loss counters, Cluster 1). Keep in sync with SampleBufferWriter.h's BatchVersion.
    private const byte MinSupportedBatchVersion = 1;
    private const byte MaxSupportedBatchVersion = 6;

    // StartBatch payload: 1 version byte + int64 timestamp.
    private const int StartBatchHeaderSize = 9;

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

        /// <summary>
        /// Threads this sweep saw live but deferred to a later round-robin tick because they exceeded the
        /// native capture-slot capacity (see PlanCaptureWindow / overflowCount in ContinuousProfiler.h). Zero
        /// on any process under that capacity, and always zero for a batch older than v4. Non-zero here is the
        /// signal that per-thread totals are round-robin under-sampled and must be scaled up (fix 3) rather
        /// than read as a genuinely idle app.
        /// </summary>
        public int DeferredThreads { get; }

        /// <summary>
        /// The real tick-to-tick wall-clock period this sweep represents, in milliseconds, as measured by the
        /// native sampler (0 when it had no preceding tick to measure against, e.g. the first tick of a
        /// session, or for a batch older than v4). Larger than the configured interval when a capture overran
        /// it (fix 2); callers credit each sample this much time instead of the configured interval.
        /// </summary>
        public int ActualSamplePeriodMs { get; }

        /// <summary>
        /// Threads captured this sweep but dropped because the native fixed-size encode buffer filled
        /// mid-batch (see EncodeAndPublish in ContinuousProfiler.h). This is silent per-tick data loss --
        /// the native side raises it to a Warn and counts it here so the managed side can surface a
        /// supportability metric, matching the OTLP egress side's drop handling. Always zero for a batch
        /// older than v5.
        /// </summary>
        public int TruncatedThreads { get; }

        /// <summary>
        /// Whole ticks the native sampler discarded under back-pressure (both output buffers full, managed
        /// reader behind) since the last successfully published batch. Accumulated natively across the
        /// dropped ticks -- which produce no batch of their own -- and shipped on the next batch that does
        /// publish. Non-zero here means the managed drain is not keeping up with the native sampler. Always
        /// zero for a batch older than v5.
        /// </summary>
        public int DroppedTicks { get; }

        public BatchStats(long microsSuspended, int threads, int frames, int skipped)
            : this(microsSuspended, threads, frames, skipped, deferredThreads: 0, actualSamplePeriodMs: 0)
        {
        }

        public BatchStats(long microsSuspended, int threads, int frames, int skipped, int deferredThreads, int actualSamplePeriodMs)
            : this(microsSuspended, threads, frames, skipped, deferredThreads, actualSamplePeriodMs, truncatedThreads: 0, droppedTicks: 0)
        {
        }

        public BatchStats(long microsSuspended, int threads, int frames, int skipped, int deferredThreads, int actualSamplePeriodMs, int truncatedThreads, int droppedTicks)
        {
            MicrosSuspended = microsSuspended;
            Threads = threads;
            Frames = frames;
            Skipped = skipped;
            DeferredThreads = deferredThreads;
            ActualSamplePeriodMs = actualSamplePeriodMs;
            TruncatedThreads = truncatedThreads;
            DroppedTicks = droppedTicks;
        }
    }

    public static IReadOnlyList<ManagedThreadSample> Parse(byte[] buffer, int length)
        => Parse(buffer, length, out _, out _);

    /// <summary>
    /// Parse overload that also captures the batch's <see cref="BatchStats"/> (null when the batch carried
    /// none). Callers use it to surface the suspend-window / coverage counters.
    /// </summary>
    public static IReadOnlyList<ManagedThreadSample> Parse(byte[] buffer, int length, out BatchStats stats)
        => Parse(buffer, length, out stats, out _);

    /// <summary>
    /// Parse overload that also reports whether the batch was rejected as corrupt (truncated header,
    /// unknown/future <c>BatchVersion</c>, or a sample/stats opcode seen before <c>StartBatch</c>).
    /// A rejected batch never returns fabricated samples parsed under the wrong layout assumptions --
    /// <paramref name="parseFailed"/> is the caller's signal to log and increment an error metric
    /// instead of treating an empty/partial result as "nothing to report".
    /// </summary>
    public static IReadOnlyList<ManagedThreadSample> Parse(byte[] buffer, int length, out BatchStats stats, out bool parseFailed)
    {
        stats = null;
        parseFailed = false;
        var samples = new List<ManagedThreadSample>();
        if (buffer == null || length <= 0)
            return samples;

        var frameDictionary = new Dictionary<int, string>();
        var pos = 0;
        var version = 0;
        var batchStarted = false;
        try
        {
            while (pos < length)
            {
                var opcode = buffer[pos++];
                switch (opcode)
                {
                    case StartBatch:
                        RequireBound(pos, StartBatchHeaderSize, length);
                        version = buffer[pos];
                        pos += StartBatchHeaderSize;
                        if (version < MinSupportedBatchVersion || version > MaxSupportedBatchVersion)
                        {
                            parseFailed = true;
                            return samples;
                        }
                        batchStarted = true;
                        break;
                    case StartSample:
                        if (!batchStarted)
                        {
                            parseFailed = true;
                            return samples;
                        }
                        samples.Add(ReadSample(buffer, ref pos, frameDictionary, version, length));
                        break;
                    case BatchStatsOpcode:
                        {
                            if (!batchStarted)
                            {
                                parseFailed = true;
                                return samples;
                            }
                            var micros = ReadLong(buffer, ref pos, length);   // microsSuspended (int64)
                            var threads = ReadInt(buffer, ref pos, length);   // threads
                            var frames = ReadInt(buffer, ref pos, length);    // frames
                            var skipped = ReadInt(buffer, ref pos, length);   // skipped
                            // v4 appended two int32s here (deferredThreads, actualSamplePeriodMs). Gated on the
                            // batch version -- set by StartBatch, which always precedes BatchStats in the
                            // stream -- so a v1..v3 batch reads exactly the four legacy fields and defaults the
                            // new two to 0. Must stay byte-for-byte in step with SampleBufferWriter.h's
                            // WriteBatchStats field order.
                            var deferredThreads = 0;
                            var actualSamplePeriodMs = 0;
                            if (version >= 4)
                            {
                                deferredThreads = ReadInt(buffer, ref pos, length);      // deferred/round-robin-skipped threads
                                actualSamplePeriodMs = ReadInt(buffer, ref pos, length); // real tick period (ms)
                            }
                            // v5 appended two more int32s: truncatedThreads (captured-but-dropped when the
                            // native encode buffer filled mid-batch) and droppedTicks (whole ticks discarded
                            // under back-pressure). Gated the same way -- a v1..v4 batch defaults both to 0.
                            var truncatedThreads = 0;
                            var droppedTicks = 0;
                            if (version >= 5)
                            {
                                truncatedThreads = ReadInt(buffer, ref pos, length); // native mid-batch encode-buffer truncation
                                droppedTicks = ReadInt(buffer, ref pos, length);     // whole ticks dropped under back-pressure
                            }
                            stats = new BatchStats(micros, threads, frames, skipped, deferredThreads, actualSamplePeriodMs, truncatedThreads, droppedTicks);
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
            // truncated/garbage past `length`: stop and report rather than fabricating a batch from
            // whatever happened to parse before the cut (Global Constraint: never throw out to the caller)
            parseFailed = true;
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
        // v2..v5 wrote a 1-byte on-CPU flag here; v6 removed it (cpu/off_cpu buckets merged). Skip and
        // discard the byte for older batches so the isAgentWork read below stays aligned.
        if (version >= 2 && version < 6)
        {
            ReadBool(b, ref pos, length);
        }
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
        return new ManagedThreadSample(threadName, osThreadId, traceHigh, traceLow, spanId, frames, isAgentWork);
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
        // Length prefix is unsigned (0..65535 chars) -- unlike ReadShort's callers in ReadSample, where
        // the sign carries the interned-vs-inline distinction, so this can't reuse ReadShort directly.
        // Reading it as signed would make a >32767-char string (or a version/format-skewed writer)
        // decode a negative charCount, which GetString then throws on.
        var charCount = ReadUShort(b, ref pos, length);
        var byteCount = charCount * 2;
        RequireBound(pos, byteCount, length);
        var s = Encoding.Unicode.GetString(b, pos, byteCount);
        pos += byteCount;
        return s;
    }

    private static ushort ReadUShort(byte[] b, ref int pos, int length)
    {
        RequireBound(pos, 2, length);
        var v = (ushort)((b[pos] << 8) | b[pos + 1]);
        pos += 2;
        return v;
    }
}

// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Threading;
using NewRelic.Agent.Extensions.Logging;

namespace NewRelic.Agent.Core.ContinuousProfiling;

/// <summary>
/// Pushes the current New Relic trace/span context down to the native continuous profiler so CPU samples
/// correlate to transactions. Runs on the application thread (the caller is the wrapper pipeline), so this
/// must stay cheap and never throw into the customer's app.
///
/// <para>Decomposition contract (must match <see cref="OtlpProfileBuilder"/> exactly so correlation
/// round-trips): the 16-byte trace id is a 32-char hex string; its first 16 hex chars form the high 8 bytes
/// (a big-endian long) and its last 16 hex chars form the low 8 bytes (a big-endian long). The 8-byte span
/// id is a 16-char hex string parsed as one big-endian long. <see cref="OtlpProfileBuilder"/> re-emits those
/// longs most-significant-byte-first, reproducing the original hex ids. A missing/malformed id decomposes to
/// zero, which <see cref="OtlpProfileBuilder"/> encodes as "no linked span" (link index 0).</para>
/// </summary>
public class ContinuousProfilingContext : IContinuousProfilingContext
{
    private const int TraceIdHexLength = 32; // 16 bytes
    private const int SpanIdHexLength = 16;   // 8 bytes
    private const int HexCharsPerLong = 16;

    // Process-wide seam the hot path reads through. Defaults to an inert (disabled) instance so the wrapper
    // pipeline pays only a single volatile field read + an IsEnabled==false branch when CP is off. The
    // continuous-profiling session assigns a live, enabled instance here on start and swaps it back on stop.
    private static volatile IContinuousProfilingContext _instance = new ContinuousProfilingContext();

    public static IContinuousProfilingContext Instance
    {
        get => _instance;
        set => _instance = value ?? new ContinuousProfilingContext();
    }

    // volatile: written on the (rare) lifecycle transition thread, read on every app thread's hot path.
    private volatile INativeContinuousProfiler _native;

    // Per-thread push change-detection. The wrapper pipeline pushes the current trace/span on BOTH entry
    // and exit of every instrumented method -- the hottest path in the agent. Within a transaction, on a
    // given thread, (traceId, spanId) is stable: Transaction.TraceId and Segment.SpanId hand back the SAME
    // string instances across calls, so reference equality is a correct "unchanged" test. When unchanged we
    // skip the hex decompose + both P/Invokes entirely (the native map already holds this thread's context).
    // A genuinely new context is always a new string instance, so a real change is never skipped; a coincidental
    // equal-value-but-distinct instance merely causes one harmless redundant push. Keyed per thread to match
    // the native map's per-CLR-thread keying.
    [ThreadStatic] private static string _lastPushedTraceId;
    [ThreadStatic] private static string _lastPushedSpanId;
    [ThreadStatic] private static int _lastPushedEpoch;

    // Bumped whenever a native profiler is (re)armed via Enable. A per-thread guard left over from a previous
    // session must never suppress the first push into a freshly-armed (empty) native map -- e.g. a long
    // transaction whose id instances outlive a continuous-profiling stop -> start (retune without restart).
    // Comparing the epoch invalidates every thread's guard on re-arm without cross-thread bookkeeping.
    private static int _epoch;

    public bool IsEnabled => _native != null;

    /// <summary>Arms the context: subsequent pushes forward to the given native profiler.</summary>
    public void Enable(INativeContinuousProfiler native)
    {
        _native = native ?? throw new ArgumentNullException(nameof(native));
        Interlocked.Increment(ref _epoch); // invalidate stale per-thread change-detection guards
    }

    /// <summary>Disarms the context: pushes become no-ops again with zero native traffic.</summary>
    public void Disable()
    {
        _native = null;
    }

    public void PushTraceContext(string traceId, string spanId)
    {
        var native = _native;
        if (native == null)
            return;

        // Change-detection: skip when this thread already pushed the same (traceId, spanId) instances under
        // the current native session. Cheap: two reference compares + an int compare, no allocation.
        var epoch = Volatile.Read(ref _epoch);
        if (epoch == _lastPushedEpoch
            && ReferenceEquals(traceId, _lastPushedTraceId)
            && ReferenceEquals(spanId, _lastPushedSpanId))
        {
            return;
        }

        try
        {
            DecomposeTraceId(traceId, out var high, out var low);
            var span = DecomposeId(spanId, SpanIdHexLength);
            native.SetTraceContext(high, low, span);

            // Record what we pushed so an identical follow-up push on this thread is skipped. Only updated
            // on success, so a failed push is retried rather than silently suppressed.
            _lastPushedTraceId = traceId;
            _lastPushedSpanId = spanId;
            _lastPushedEpoch = epoch;
        }
        catch (Exception ex)
        {
            // Never let a correlation push surface in the instrumented application.
            Log.Finest(ex, "[ContinuousProfiling] Failed to push trace context to the native profiler.");
        }
    }

    public void ResetTraceContext()
    {
        var native = _native;
        if (native == null)
            return;

        try
        {
            native.ResetTraceContext();

            // The native map no longer holds this thread's context, so clear the guard: an identical push
            // after a reset must go through rather than be suppressed as "unchanged".
            _lastPushedTraceId = null;
            _lastPushedSpanId = null;
        }
        catch (Exception ex)
        {
            Log.Finest(ex, "[ContinuousProfiling] Failed to reset trace context in the native profiler.");
        }
    }

    /// <summary>
    /// Splits a 32-char hex trace id into its high and low 8-byte halves, each a big-endian long. Anything
    /// that is not exactly 32 hex chars (null, wrong length, non-hex) decomposes to (0, 0) == "no trace".
    /// </summary>
    private static void DecomposeTraceId(string traceId, out long high, out long low)
    {
        high = 0;
        low = 0;

        if (traceId == null || traceId.Length != TraceIdHexLength)
            return;

        if (!TryParseHexLong(traceId, 0, out var parsedHigh) || !TryParseHexLong(traceId, HexCharsPerLong, out var parsedLow))
            return;

        high = parsedHigh;
        low = parsedLow;
    }

    /// <summary>
    /// Parses a single big-endian long from a hex string of the given exact length. Any other length,
    /// null, or a non-hex character yields 0 (== "no id").
    /// </summary>
    private static long DecomposeId(string id, int expectedLength)
    {
        if (id == null || id.Length != expectedLength)
            return 0;

        return TryParseHexLong(id, 0, out var value) ? value : 0;
    }

    /// <summary>
    /// Reads 16 hex chars starting at <paramref name="offset"/> as one big-endian 64-bit value. Bit-exact
    /// (the full unsigned range is preserved into the sign bit) so it round-trips through
    /// <see cref="OtlpProfileBuilder"/>'s most-significant-byte-first encoding.
    /// </summary>
    private static bool TryParseHexLong(string s, int offset, out long value)
    {
        ulong result = 0;
        for (var i = 0; i < HexCharsPerLong; i++)
        {
            var nibble = HexValue(s[offset + i]);
            if (nibble < 0)
            {
                value = 0;
                return false;
            }

            result = (result << 4) | (uint)nibble;
        }

        value = unchecked((long)result);
        return true;
    }

    private static int HexValue(char c)
    {
        if (c >= '0' && c <= '9')
            return c - '0';
        if (c >= 'a' && c <= 'f')
            return 10 + (c - 'a');
        if (c >= 'A' && c <= 'F')
            return 10 + (c - 'A');
        return -1;
    }
}

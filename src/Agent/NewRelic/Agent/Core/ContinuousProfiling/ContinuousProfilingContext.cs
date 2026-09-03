// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Threading;
using NewRelic.Agent.Extensions.Logging;

namespace NewRelic.Agent.Core.ContinuousProfiling;

/// <summary>
/// Pushes the current trace/span context to the native continuous profiler for CPU-sample correlation.
/// Runs on the application's hot path, so must stay cheap and never throw.
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

    // Process-wide seam the hot path reads through; defaults to an inert (disabled) instance so CP-off
    // costs only one volatile read + a false branch. The CP session swaps in a live instance on start.
    private static volatile IContinuousProfilingContext _instance = new ContinuousProfilingContext();

    // Process-wide fast-path pre-filter for the wrapper hot path (WrapperService), NOT the authority.
    // It lets the disabled path (every customer without continuous profiling) pay only a static volatile
    // bool read plus a not-taken branch, instead of two interface dispatches through Instance.IsEnabled --
    // a field, deliberately, so the JIT need not inline through a property getter to fold the read into the
    // branch. The per-instance IsEnabled check inside PushContinuousProfilingContext stays authoritative.
    //
    // Why a coarse pre-filter is sufficient and safe:
    //   * Publish ordering: ContinuousProfilingService.StartLocked calls Enable(_native) (which sets this
    //     true) and only THEN publishes Instance = the live context. So there is a brief window where
    //     AnyEnabled is already true while Instance is still the inert default whose IsEnabled is false.
    //     During that window the hot path calls the helper but the helper's own IsEnabled guard no-ops it --
    //     correct, not a bug. AnyEnabled can be true-but-not-yet-live; it is never the final word.
    //   * Single-owner invariant: ContinuousProfilingService owns exactly one live context at a time.
    //     StopLocked disables the live one (clearing this to false) and swaps in a fresh inert instance
    //     whose Disable is never called. Because only the live context is ever Disable()d, an unconditional
    //     clear here is correct. Enable re-sets it, so even a spurious clear self-heals on the next start.
    public static volatile bool AnyEnabled;

    public static IContinuousProfilingContext Instance
    {
        get => _instance;
        set => _instance = value ?? new ContinuousProfilingContext();
    }

    // volatile: written on the (rare) lifecycle transition thread, read on every app thread's hot path.
    private volatile INativeContinuousProfiler _native;

    // The native profiler this context was most recently armed with, RETAINED across Disable().
    //
    // SetAgentWork/ResetAgentWork are the two halves of a native per-thread nesting-DEPTH counter
    // (AgentWorkMap.h) that requires strict 1:1 pairing: an increment whose decrement never arrives pins
    // that thread's slot at depth >= 1 for the rest of the process, so every later sample on it --
    // including real application work -- is tagged agent work and filtered out of the profile. Silent,
    // permanent coverage loss, and the slot is by design never tombstoned.
    //
    // Scheduler now captures Instance ONCE per timer callback and drives both halves through that single
    // captured instance, so an Instance swap mid-callback can no longer split a pair across two objects.
    // That leaves exactly one way to orphan an increment: the captured context being Disable()d between
    // its set and its reset -- precisely what a CP stop/retune does (StopLocked calls Disable() on the
    // live context and then republishes a fresh inert Instance). By then _native is null, so a reset
    // gated on _native would silently drop the decrement. Gating the reset on this field instead keeps
    // the decrement flowing to the SAME native counter the increment hit, while new sets stay gated on
    // _native and so are still correctly suppressed after Disable().
    //
    // Only ever written by Enable, so a context that was never armed (the inert default) still no-ops
    // resets, and a reset arriving after Disable on a never-set thread merely reaches native Decrement,
    // which clamps at depth 0 -- a no-op. Retaining the reference is not a leak: the
    // INativeContinuousProfiler is the process-lifetime P/Invoke shim already held by
    // ContinuousProfilingService for as long as the agent lives.
    private volatile INativeContinuousProfiler _nativeForAgentWorkReset;

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
        if (native == null)
            throw new ArgumentNullException(nameof(native));

        // Publish the reset target BEFORE _native: _native is what admits a SetAgentWork, so ordering it
        // second guarantees no admitted increment can ever observe a null reset target for its decrement.
        _nativeForAgentWorkReset = native;
        _native = native;
        Interlocked.Increment(ref _epoch); // invalidate stale per-thread change-detection guards
        AnyEnabled = true; // arm the hot-path pre-filter after _native is live so pushes can begin
    }

    /// <summary>
    /// Disarms the context: pushes and new <see cref="SetAgentWork"/> calls become no-ops again with zero
    /// native traffic. Deliberately does NOT disarm <see cref="ResetAgentWork"/> -- a reset already paired
    /// against a set made while this context was armed must still reach native, or the thread's
    /// agent-work depth counter stays stuck (see <see cref="_nativeForAgentWorkReset"/>).
    /// </summary>
    public void Disable()
    {
        _native = null;
        AnyEnabled = false; // clear the hot-path pre-filter; safe unconditionally (single-owner invariant, see AnyEnabled)
    }

    public void PushTraceContext(string traceId, string spanId)
    {
        var native = _native;
        if (native == null)
            return;

        // Skip if this thread already pushed the same (traceId, spanId) instances this epoch --
        // two reference compares + an int compare, no allocation.
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

            // Only recorded on success, so a failed push is retried rather than silently suppressed.
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

            // Clear the guard: after a reset, an identical push must go through, not be suppressed as unchanged.
            _lastPushedTraceId = null;
            _lastPushedSpanId = null;
        }
        catch (Exception ex)
        {
            Log.Finest(ex, "[ContinuousProfiling] Failed to reset trace context in the native profiler.");
        }
    }

    public void SetAgentWork()
    {
        var native = _native;
        if (native == null)
            return;

        try
        {
            native.SetAgentWork();
        }
        catch (Exception ex)
        {
            Log.Finest(ex, "[ContinuousProfiling] Failed to set agent-work flag in the native profiler.");
        }
    }

    /// <summary>
    /// Decrements the calling thread's native agent-work depth. Gated on the RETAINED native reference,
    /// not on <see cref="_native"/>, so a reset still lands after this context has been disabled by a
    /// stop/retune -- see <see cref="_nativeForAgentWorkReset"/> for why an orphaned increment is
    /// permanently damaging.
    /// </summary>
    public void ResetAgentWork()
    {
        var native = _nativeForAgentWorkReset;
        if (native == null)
            return;

        try
        {
            native.ResetAgentWork();
        }
        catch (Exception ex)
        {
            Log.Finest(ex, "[ContinuousProfiling] Failed to reset agent-work flag in the native profiler.");
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

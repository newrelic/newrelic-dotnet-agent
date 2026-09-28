// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
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

    // This thread's native pending-push cell (a single int64 in the native PendingPushMap) plus the epoch it
    // was resolved for. The cell is where the thread publishes "I am about to push span S" before it starts
    // the push, so a compaction pass can see an intent that no native TraceContextMap slot reflects yet --
    // without it, a span retired between the decode and the slot write could be reclaimed while this thread
    // is still on its way to linking samples to it.
    //
    // [ThreadStatic] is mandatory, not merely convenient: the address is valid only until the owning thread
    // exits (ThreadDestroyed -> PendingPushMap::Forget), after which the native slot is tombstoned and can be
    // reclaimed by a different thread. A pointer cached anywhere outliving the thread would become a foreign
    // write into another thread's cell. Nothing here escapes the thread that resolved it.
    //
    // The epoch does double duty: it keeps resolution once-per-thread (never once-per-push -- the resolve is
    // itself a P/Invoke) while still re-resolving after a stop/start re-arm, so a cell cannot survive across
    // a session boundary. A FAILED resolution is remembered the same way (cell stays Zero, epoch is still
    // stamped), so a thread the native side cannot serve does not P/Invoke on every push.
    [ThreadStatic] private static IntPtr _pendingPushCell;
    [ThreadStatic] private static int _pendingPushCellEpoch;

    // Once-per-process latch for the cell-unavailable log. Deliberately not per-thread and not per-push: the
    // condition is benign (it degrades to the pre-cell behaviour) and a per-thread log on a thread-pool-heavy
    // host would be pure noise.
    private static int _loggedPendingPushCellUnavailable;

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
            // Resolved on this thread's first push of the session and cached; see _pendingPushCell.
            var cell = ResolvePendingPushCell(native, epoch);

            DecomposeTraceId(traceId, out var high, out var low);
            var span = DecomposeId(spanId, SpanIdHexLength);

            try
            {
                // Publish the intent BEFORE the P/Invoke. This is the earliest feasible point: the cell holds
                // an int64 and no int64 span id exists until DecomposeId has produced one (publishing the hex
                // string instead is not an option -- the reader is native code that cannot read managed
                // strings). It still removes the expensive part of the window: only the straight-line decode
                // above is left unprotected, not the managed-to-native transition and the slot write.
                if (cell != IntPtr.Zero)
                    PublishPendingSpanId(cell, span);

                native.SetTraceContext(high, low, span);
            }
            finally
            {
                // Cleared on EVERY path, exceptions included: a cell left non-zero pins one registry entry
                // until this thread exits, which for a thread-pool worker is indefinite. A leak, not a
                // correctness failure -- but an unbounded-in-time one, hence the finally.
                if (cell != IntPtr.Zero)
                    PublishPendingSpanId(cell, 0);
            }

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
            // Drop any pending intent this thread left behind before clearing its slot, so the two halves of
            // the live set agree. Only touched when this thread's cell was resolved for the CURRENT epoch --
            // a pointer from a previous session must never be written through.
            if (_pendingPushCell != IntPtr.Zero && _pendingPushCellEpoch == Volatile.Read(ref _epoch))
                PublishPendingSpanId(_pendingPushCell, 0);

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

    /// <summary>
    /// Marks a terminating transaction's span ids as ended in the native profiler.
    ///
    /// <para>This is the cross-thread half of the stale-link fix. <see cref="ResetTraceContext"/> clears
    /// only the calling thread's own native slot, so a transaction whose context was pushed on thread A
    /// but which ends on thread B leaves A's slot pointing at a finished span for as long as A stays
    /// idle. Retiring the span ids themselves is thread-agnostic and closes that gap without any
    /// cross-thread write into A's slot.</para>
    ///
    /// <para>Gated on <see cref="_native"/>, so retirement stops as soon as the context is disabled.
    /// This is deliberate: retiring while continuous profiling is paused would grow registry occupancy for
    /// the whole pause with no compaction pass running to reclaim it, and those entries could never reject
    /// anything anyway -- the next <c>Start()</c> calls <c>TraceContextMap::NewGeneration()</c>, which
    /// invalidates every slot written before it, so every link a paused-period retirement might have
    /// rejected is already unreachable. A context that was never armed (the inert default) also no-ops.</para>
    ///
    /// <para>May run on the GC finalizer thread (a transaction reaped rather than ended cleanly), so it
    /// must never block and never throw.</para>
    ///
    /// <para>Decodes here rather than at the call site on purpose: the hex-to-long contract documented on
    /// this class must match <see cref="OtlpProfileBuilder"/> exactly, so it lives in exactly one place.
    /// A malformed or null id decodes to zero, which is the native "no span" sentinel, and is dropped
    /// rather than sent across the ABI.</para>
    /// </summary>
    public void RetireSpans(IReadOnlyList<string> spanIdHex)
    {
        if (spanIdHex == null || spanIdHex.Count == 0)
            return;

        var native = _native;
        if (native == null)
            return;

        try
        {
            // One array per terminating transaction. Transaction end is not a hot path (it fires once per
            // transaction, not once per instrumented call), so a single right-sized allocation here is far
            // cheaper than the per-call push overhead already paid throughout the transaction's life.
            var decoded = new long[spanIdHex.Count];
            var count = 0;
            for (var i = 0; i < spanIdHex.Count; i++)
            {
                var spanId = DecomposeId(spanIdHex[i], SpanIdHexLength);
                if (spanId != 0)
                    decoded[count++] = spanId;
            }

            if (count == 0)
                return;

            native.RetireSpans(decoded, count);
        }
        catch (Exception ex)
        {
            Log.Finest(ex, "[ContinuousProfiling] Failed to retire ended spans in the native profiler.");
        }
    }

    /// <summary>
    /// This thread's pending-push cell, resolved from native at most once per thread per armed session and
    /// cached in <see cref="_pendingPushCell"/>. Returns <see cref="IntPtr.Zero"/> when the native side
    /// cannot supply one, which is benign: the caller pushes without publishing its intent first, exactly as
    /// it did before the cell existed. Never throws and never fails a push.
    /// </summary>
    private static IntPtr ResolvePendingPushCell(INativeContinuousProfiler native, int epoch)
    {
        if (_pendingPushCellEpoch == epoch)
            return _pendingPushCell;

        var cell = IntPtr.Zero;
        try
        {
            cell = native.GetPendingPushCell();
            if (cell == IntPtr.Zero)
                LogPendingPushCellUnavailableOnce(null);
        }
        catch (Exception ex)
        {
            LogPendingPushCellUnavailableOnce(ex);
            cell = IntPtr.Zero;
        }

        // Stamped even on failure, so a thread native cannot serve stops re-attempting the P/Invoke.
        _pendingPushCell = cell;
        _pendingPushCellEpoch = epoch;
        return cell;
    }

    /// <summary>
    /// Writes a span id (or 0 to clear) into this thread's pending-push cell with RELEASE semantics, pairing
    /// with the acquire-load in the native <c>PendingPushMap::SnapshotPending</c>. A plain store happens to
    /// be sufficient under x64's memory model but not under ARM64's, which this agent ships, so the barrier
    /// is required rather than defensive. The native side static_asserts the cell is int64-sized and
    /// int64-aligned, so an unaligned or torn write is not possible.
    /// </summary>
    private static unsafe void PublishPendingSpanId(IntPtr cell, long spanId)
    {
        Volatile.Write(ref *(long*)cell, spanId);
    }

    private static void LogPendingPushCellUnavailableOnce(Exception ex)
    {
        if (Interlocked.Exchange(ref _loggedPendingPushCellUnavailable, 1) != 0)
            return;

        // Finest, once per process: the profile is still correct without a cell, only more conservative about
        // when a retired-span entry becomes reclaimable. The status-only failure (native returned no cell
        // rather than throwing) carries no exception, hence the two overloads.
        const string message = "[ContinuousProfiling] No pending-push cell available; trace-context pushes will not publish their intent. Retired-span reclamation falls back to the two-pass guarantee alone.";
        if (ex == null)
            Log.Finest(message);
        else
            Log.Finest(ex, message);
    }

    public void SetAgentWork()
    {
        var native = _native;
        if (native == null)
            return;

        ManagedThreadIdRegistry.Instance.EnsureRegistered();

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

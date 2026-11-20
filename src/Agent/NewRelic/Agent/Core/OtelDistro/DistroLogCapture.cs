// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Threading;
using NewRelic.Agent.Extensions.Logging;

namespace NewRelic.Agent.Core.OtelDistro;

/// <summary>What the OTel log processor should do with an incoming record, decided by <see cref="DistroLogCapture.ClassifyOtelRecord"/>.</summary>
public enum DistroLogDecision
{
    /// <summary>No capture scope is open: forward the record (agent/normal path).</summary>
    Forward,

    /// <summary>A scope is open but not yet decided: build the record and hand it to <see cref="DistroLogCapture.TryDefer"/>.</summary>
    Defer,

    /// <summary>The open scope is already marked New Relic-recorded: a known duplicate, already counted. Drop without building anything.</summary>
    DropDuplicate,
}

/// <summary>
/// The same-thread, defer-then-decide dedupe between New Relic's logging wrappers and OpenTelemetry's own log bridges in
/// distro mode.
///
/// In distro mode New Relic's logging wrappers (MEL <c>Logger.Log</c>, log4net/Sitecore <c>CallAppenders</c>, Serilog's
/// dispatch, NLog <c>LoggerImpl.Write</c>) record each log line and forward it through Core. OTel's log bridges capture
/// the same line synchronously inside those same methods (on .NET the ILogger bridge runs inside MEL's provider-dispatch
/// loop; OTel's log4net bridge appender runs inside <c>Logger.CallAppenders</c>). The call target raises a capture scope
/// around an NR log-forwarding call whose tracer actually started (<see cref="Enter"/> / <see cref="CreateFrame"/> /
/// <see cref="TryCloseFrame"/>), and while the scope is open the OTel log processor does NOT forward its record
/// immediately -- it hands the already-copied <see cref="DistroLogRecord"/> to <see cref="TryDefer"/>, which holds it in
/// a thread-static pending list for the scope.
///
/// Whether New Relic actually recorded the line is only known once the call unwinds, so the decision is deferred to the
/// outermost frame's close:
///   - <see cref="NotifyNrRecorded"/> marks the scope the moment New Relic's own log path records a line while the scope
///     is open (wired at the one Core seam every NR logging wrapper hits, <c>Agent.RecordLogMessage</c>, before level and
///     deny-list filtering -- so a line New Relic deliberately filtered still counts as "New Relic handled it").
///   - On the outermost close: if the scope was marked, the pending OTel copies are discarded (duplicates of what New
///     Relic forwarded); otherwise they are flushed to the sink in order (fallback: New Relic recorded nothing, so OTel's
///     copy is the only one).
/// Nested NR log sites (MEL <c>Logger.Log</c> dispatching to a Serilog/NLog provider that is itself an NR site) share the
/// one outermost scope. Each frame carries its absolute level, so a close resets the depth authoritatively and a skipped
/// inner close is healed by its outer close.
///
/// Fallback contract -- the pending OTel copies are flushed (never dropped) whenever New Relic records nothing for the
/// call: the tracer did not start (agent not registered / no tracer factory match -- the call target does not open a
/// scope then), the MEL wrapper no-ops because a known MEL provider (Serilog/NLog) is registered and that library's NR
/// site is off or out of range, or the library's NR XML is turned off (its site gate is off, so no scope opens and the
/// OTel record is forwarded straight through). NLog is a special case: OTel has no NLog capture here unless the customer
/// opts into OTel's NLog bridge (default off); when they do while NR's NLog XML is on, NLog lines can be forwarded twice
/// -- the plugin warns about that (it does not touch the bridge setting), it is not covered by this dedupe because OTel's
/// NLog bridge emits before NR's inner hook runs.
///
/// Limits, by design (documented, bounded):
///   - Same thread only (state is <c>[ThreadStatic]</c>): NR's log-forwarding methods are synchronous (void), so OTel
///     emits on the same thread between the frame open and the outermost close. A copy raised on another thread is
///     forwarded, not deduped.
///   - The pending list is bounded (<see cref="MaxPendingRecords"/>); on overflow the oldest pending copy is discarded if
///     the scope is already marked (a known duplicate) or flushed otherwise, to bound memory without losing a line that
///     New Relic is not recording. A stuck depth (an outermost close never ran -- only possible if OTel's try/finally End
///     is skipped) can at worst delay or forward a bounded number of OTel copies on that thread until the process
///     restarts; the absolute-level close heals the realistic skipped-inner-close case.
/// Never throws.
/// </summary>
public static class DistroLogCapture
{
    /// <summary>Counts OTel log records discarded as duplicates (New Relic recorded the same line).</summary>
    public const string DuplicateOtelRecordsDroppedMetricName = "Supportability/Dotnet/OtelDistro/Logs/DuplicateOtelRecordsDropped";

    /// <summary>Counts OTel log records forwarded as a fallback (New Relic recorded nothing for the call).</summary>
    public const string OtelRecordsFallbackForwardedMetricName = "Supportability/Dotnet/OtelDistro/Logs/OtelRecordsFallbackForwarded";

    /// <summary>The most OTel copies held for one outermost scope before the oldest is discarded/flushed to bound memory.</summary>
    public const int MaxPendingRecords = 64;

    // How often (ms) a running count is pushed to DistroConfigMetrics: once on first occurrence, then at most this often
    // per metric. The per-line path only does an Interlocked add and a cheap timestamp compare; the metric gate (and its
    // lock) is taken rarely, never per log line. Residual counts since the last push are exact in the Total properties.
    private const int MetricRecordIntervalMs = 10000;

    [ThreadStatic]
    private static int _depth;

    [ThreadStatic]
    private static bool _nrRecorded;

    [ThreadStatic]
    private static List<DistroLogRecord> _pending;

    // One cleared list kept per thread and swapped back in after each outermost exit, so consecutive scopes reuse it
    // instead of allocating a new List per scope. A re-entrant scope during a flush (when the spare is in use) allocates
    // a fresh list as a rare fallback.
    [ThreadStatic]
    private static List<DistroLogRecord> _spare;

    // Reusable per-thread, per-level frame markers (index = level - 1). One frame per nesting level is in flight per
    // thread at a time (synchronous sites), so the marker carries this call's level and finish through the
    // CallTargetState object slot with no per-call allocation.
    [ThreadStatic]
    private static LogFrame[] _frames;

    // True while a deferred record is being flushed to the sink. The flush re-enters Core's log path
    // (DistroLogSink.Record -> Agent.RecordLogMessage -> NotifyNrRecorded); without this guard an overflow flush, which
    // runs while the scope is still open, would wrongly mark the scope "NR recorded".
    [ThreadStatic]
    private static bool _flushing;

    private static readonly ThrottledMetric Dropped = new ThrottledMetric(DuplicateOtelRecordsDroppedMetricName);
    private static readonly ThrottledMetric FallbackForwarded = new ThrottledMetric(OtelRecordsFallbackForwardedMetricName);

    private static int _loggedDrop;
    private static int _loggedFallback;

    // The sink deferred records are flushed to on the fallback path. Set once when the log processor attaches.
    private static volatile Action<DistroLogRecord> _sink;

    /// <summary>True while this thread is inside an NR log-forwarding capture scope.</summary>
    public static bool IsCapturing => _depth > 0;

    /// <summary>Process total of OTel records discarded as duplicates (exact; the metric is sampled, this is not).</summary>
    public static long DuplicatesDroppedTotal => Dropped.Count;

    /// <summary>Process total of OTel records forwarded as a fallback (exact; the metric is sampled, this is not).</summary>
    public static long FallbackForwardedTotal => FallbackForwarded.Count;

    /// <summary>Where the fallback path flushes deferred records. The OTel log processor sets this when it attaches.</summary>
    public static void SetSink(Action<DistroLogRecord> sink)
    {
        _sink = sink;
    }

    /// <summary>
    /// Opens (or nests within) a capture scope and returns this call's absolute frame level. The outermost call
    /// (level 1) starts a fresh scope. Paired with exactly one <see cref="ExitFrame"/> (rollback) or one
    /// <see cref="CreateFrame"/>+<see cref="TryCloseFrame"/> for the returned level.
    /// </summary>
    public static int Enter()
    {
        var level = ++_depth;
        if (level == 1)
        {
            _nrRecorded = false;
            _pending?.Clear();
        }

        return level;
    }

    /// <summary>
    /// Makes the marker to stash in the CallTargetState object slot for a kept frame: records <paramref name="finish"/>
    /// in this thread's reusable holder for <paramref name="level"/> and returns it. <see cref="TryCloseFrame"/> reads it
    /// back. Never allocates after the per-thread, per-level holder exists.
    /// </summary>
    public static object CreateFrame(int level, object finish)
    {
        var frames = _frames;
        if (frames == null || frames.Length < level)
        {
            Array.Resize(ref _frames, Math.Max(level, 8));
            frames = _frames;
        }

        var frame = frames[level - 1] ?? (frames[level - 1] = new LogFrame(level));
        frame.Finish = finish;
        return frame;
    }

    /// <summary>
    /// If <paramref name="marker"/> is a log frame (from <see cref="CreateFrame"/>), closes its scope, outputs its
    /// finish delegate and returns true; otherwise outputs null and returns false (a non-log call). Never throws.
    /// </summary>
    public static bool TryCloseFrame(object marker, out object finish)
    {
        if (marker is LogFrame frame)
        {
            finish = frame.Finish;
            frame.Finish = null;
            ExitFrame(frame.Level);
            return true;
        }

        finish = null;
        return false;
    }

    /// <summary>
    /// Decides what the OTel log processor should do with an incoming record WITHOUT needing the record object, so the
    /// common MEL path (where New Relic's wrapper already recorded the line in Begin, marking the scope, before OTel's
    /// provider runs inside <c>Logger.Log</c>) drops the OTel copy before anything is allocated:
    /// <list type="bullet">
    /// <item><see cref="DistroLogDecision.Forward"/> when no scope is open.</item>
    /// <item><see cref="DistroLogDecision.DropDuplicate"/> when the open scope is already marked New Relic-recorded (and
    /// is not mid-flush): the record is counted here as a duplicate, and the caller returns without building it.</item>
    /// <item><see cref="DistroLogDecision.Defer"/> when a scope is open but not yet decided: the caller builds the record
    /// and hands it to <see cref="TryDefer"/>.</item>
    /// </list>
    /// Allocation-free. Never throws.
    /// </summary>
    public static DistroLogDecision ClassifyOtelRecord()
    {
        if (_depth <= 0)
        {
            return DistroLogDecision.Forward;
        }

        if (_nrRecorded && !_flushing)
        {
            CountDuplicatesDropped(1);
            return DistroLogDecision.DropDuplicate;
        }

        return DistroLogDecision.Defer;
    }

    /// <summary>
    /// While a scope is open, holds the already-copied <paramref name="record"/> for the scope's end decision and returns
    /// true (the caller must not forward it now). Returns false when no scope is open, or when holding the record failed,
    /// so the caller forwards normally. On overflow the oldest held copy is discarded (if the scope is already marked) or
    /// flushed (otherwise) to keep memory bounded. Never throws.
    /// </summary>
    public static bool TryDefer(DistroLogRecord record)
    {
        if (_depth <= 0 || record == null)
        {
            return false;
        }

        try
        {
            var pending = _pending ??= TakeList();
            if (pending.Count >= MaxPendingRecords)
            {
                var oldest = pending[0];
                pending.RemoveAt(0);
                if (_nrRecorded)
                {
                    // Already a known duplicate; drop it rather than forward it.
                    CountDuplicatesDropped(1);
                }
                else
                {
                    Flush(oldest);
                    CountFallbackForwarded(1);
                }
            }

            pending.Add(record);
            return true;
        }
        catch (Exception)
        {
            // Could not hold the record; let the caller forward it directly rather than lose it.
            return false;
        }
    }

    /// <summary>
    /// Marks the current scope as one where New Relic recorded the line. Wired at <c>Agent.RecordLogMessage</c> before
    /// its filtering. One thread-static read; in agent mode the depth is always 0, so it returns immediately.
    /// </summary>
    public static void NotifyNrRecorded()
    {
        if (_depth > 0 && !_flushing)
        {
            _nrRecorded = true;
        }
    }

    /// <summary>
    /// Closes the frame at absolute <paramref name="level"/>. A nested frame (level &gt; 1) just lowers the depth to just
    /// below it (resetting it authoritatively, which heals a skipped inner close). The outermost frame (level 1) decides:
    /// a scope New Relic recorded in discards its pending OTel copies (duplicates); otherwise they are flushed in order
    /// (fallback). Also used directly to roll back a frame whose tracer did not start. Never throws.
    /// </summary>
    public static void ExitFrame(int level)
    {
        if (level > 1)
        {
            if (_depth > level - 1)
            {
                _depth = level - 1;
            }

            return;
        }

        // Outermost: reset the scope first (depth 0 so a flush's own RecordLogMessage does not re-mark), and detach the
        // pending list so a re-entrant NR log site during the flush gets a fresh list instead of corrupting this one.
        _depth = 0;
        var toFlush = _pending;
        var nrRecorded = _nrRecorded;
        _pending = null;
        _nrRecorded = false;
        if (toFlush == null)
        {
            return;
        }

        var count = toFlush.Count;
        try
        {
            if (count == 0)
            {
                return;
            }

            if (nrRecorded)
            {
                CountDuplicatesDropped(count);
                return;
            }

            // Fallback: forward each, isolated so one throw neither skips the rest nor the count.
            for (var i = 0; i < count; i++)
            {
                Flush(toFlush[i]);
            }

            CountFallbackForwarded(count);
        }
        finally
        {
            // Clear references so the held records are collectable, then keep the list as this thread's spare for the
            // next scope to reuse (a re-entrant scope during the flush above may have taken/left its own; keep one).
            toFlush.Clear();
            ReturnList(toFlush);
        }
    }

    private static void CountDuplicatesDropped(long count)
    {
        Dropped.Add(count);
        if (Interlocked.Exchange(ref _loggedDrop, 1) == 0)
        {
            TryLogDebug("OTel distro: discarding OTel log records captured inside a New Relic logging wrapper so each line is forwarded once; New Relic's wrapper is the log path for that library. Later discards are not logged.");
        }
    }

    private static void CountFallbackForwarded(long count)
    {
        FallbackForwarded.Add(count);
        if (Interlocked.Exchange(ref _loggedFallback, 1) == 0)
        {
            TryLogDebug("OTel distro: New Relic recorded nothing for an instrumented logging call, so OTel's captured log records are forwarded instead (New Relic's instrumentation for that library is off or did not run). Later fallbacks are not logged.");
        }
    }

    // Reuses this thread's spare list if one is free, else allocates (first scope on the thread, or a re-entrant scope
    // while the spare is in use).
    private static List<DistroLogRecord> TakeList()
    {
        var spare = _spare;
        if (spare != null)
        {
            _spare = null;
            spare.Clear();
            return spare;
        }

        return new List<DistroLogRecord>();
    }

    private static void ReturnList(List<DistroLogRecord> list)
    {
        if (_spare == null)
        {
            _spare = list;
        }
    }

    /// <summary>Test seam: clears this thread's scope and the process counters and once-logged flags.</summary>
    public static void ResetForTesting()
    {
        _depth = 0;
        _nrRecorded = false;
        _flushing = false;
        _pending?.Clear();
        _pending = null;
        _spare = null;
        _frames = null;
        _sink = null;
        Dropped.Reset();
        FallbackForwarded.Reset();
        Interlocked.Exchange(ref _loggedDrop, 0);
        Interlocked.Exchange(ref _loggedFallback, 0);
    }

    private static void Flush(DistroLogRecord record)
    {
        var sink = _sink;
        if (sink == null)
        {
            return;
        }

        // Guard so the sink's own RecordLogMessage does not mark the still-open scope (overflow flushes at depth > 0),
        // and isolate a throwing sink so it never skips the rest of the flush.
        var prior = _flushing;
        _flushing = true;
        try
        {
            sink(record);
        }
        catch (Exception)
        {
        }
        finally
        {
            _flushing = prior;
        }
    }

    private static void TryLogDebug(string message)
    {
        try
        {
            Log.Debug(message);
        }
        catch (Exception)
        {
        }
    }

    // One reusable frame holder per thread per nesting level.
    private sealed class LogFrame
    {
        public readonly int Level;
        public object Finish;

        public LogFrame(int level)
        {
            Level = level;
        }
    }

    // A lock-free running count whose supportability metric is pushed to DistroConfigMetrics on first occurrence and then
    // at most once per MetricRecordIntervalMs (the delta since the last push). Keeps the per-log-line path off the
    // DistroConfigMetrics gate.
    private sealed class ThrottledMetric
    {
        private readonly string _metricName;
        private long _count;
        private long _recordedThrough;
        private int _recordedOnce;
        private int _lastTick;

        public ThrottledMetric(string metricName)
        {
            _metricName = metricName;
        }

        public long Count => Interlocked.Read(ref _count);

        public void Add(long n)
        {
            if (n <= 0)
            {
                return;
            }

            Interlocked.Add(ref _count, n);

            var now = System.Environment.TickCount;
            if (Interlocked.Exchange(ref _recordedOnce, 1) == 0)
            {
                Volatile.Write(ref _lastTick, now);
                PushDelta();
                return;
            }

            var last = Volatile.Read(ref _lastTick);
            if (unchecked((uint)(now - last)) < MetricRecordIntervalMs)
            {
                return;
            }

            if (Interlocked.CompareExchange(ref _lastTick, now, last) != last)
            {
                return;
            }

            PushDelta();
        }

        public void Reset()
        {
            Interlocked.Exchange(ref _count, 0);
            Interlocked.Exchange(ref _recordedThrough, 0);
            Interlocked.Exchange(ref _recordedOnce, 0);
            Interlocked.Exchange(ref _lastTick, 0);
        }

        private void PushDelta()
        {
            var total = Interlocked.Read(ref _count);
            var through = Interlocked.Exchange(ref _recordedThrough, total);
            var delta = total - through;
            if (delta > 0)
            {
                DistroConfigMetrics.Record(_metricName, (int)delta);
            }
        }
    }
}

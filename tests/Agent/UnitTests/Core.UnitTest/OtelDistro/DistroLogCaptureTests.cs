// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using NewRelic.Agent.Extensions.Logging;
using NUnit.Framework;

namespace NewRelic.Agent.Core.OtelDistro;

[TestFixture]
public class DistroLogCaptureTests
{
    private DistroRecordingLogger _logger;
    private List<string> _metrics;
    private List<DistroLogRecord> _flushed;

    [SetUp]
    public void SetUp()
    {
        DistroLogCapture.ResetForTesting();
        DistroConfigMetrics.ResetForTesting();
        _metrics = new List<string>();
        DistroConfigMetrics.SetMetricSink(_metrics.Add);
        _flushed = new List<DistroLogRecord>();
        DistroLogCapture.SetSink(_flushed.Add);
        _logger = new DistroRecordingLogger();
        Log.Initialize(_logger);
    }

    [TearDown]
    public void TearDown()
    {
        Log.Initialize(new NoOpLogger());
        DistroConfigMetrics.ResetForTesting();
        DistroLogCapture.ResetForTesting();
    }

    private static DistroLogRecord Record(string message) => new DistroLogRecord { FormattedMessage = message };

    [Test]
    public void Enter_ReturnsAbsoluteFrameLevel_AndIsCapturing()
    {
        Assert.That(DistroLogCapture.IsCapturing, Is.False);

        Assert.Multiple(() =>
        {
            Assert.That(DistroLogCapture.Enter(), Is.EqualTo(1));
            Assert.That(DistroLogCapture.Enter(), Is.EqualTo(2));
            Assert.That(DistroLogCapture.IsCapturing, Is.True);
        });

        DistroLogCapture.ExitFrame(2);
        Assert.That(DistroLogCapture.IsCapturing, Is.True, "outer frame still open");
        DistroLogCapture.ExitFrame(1);
        Assert.That(DistroLogCapture.IsCapturing, Is.False);
    }

    [Test]
    public void TryDefer_OutsideScope_ReturnsFalse_AndHoldsNothing()
    {
        var deferred = DistroLogCapture.TryDefer(Record("x"));

        Assert.Multiple(() =>
        {
            Assert.That(deferred, Is.False);
            Assert.That(_flushed, Is.Empty);
        });
    }

    [Test]
    public void OutermostExit_NrRecorded_DiscardsPendingAndCounts()
    {
        DistroLogCapture.Enter();
        var deferred = DistroLogCapture.TryDefer(Record("otel-copy"));
        DistroLogCapture.NotifyNrRecorded();

        DistroLogCapture.ExitFrame(1);

        Assert.Multiple(() =>
        {
            Assert.That(deferred, Is.True);
            Assert.That(_flushed, Is.Empty, "NR recorded the line, so the OTel copy is discarded");
            Assert.That(DistroLogCapture.DuplicatesDroppedTotal, Is.EqualTo(1));
            Assert.That(DistroLogCapture.FallbackForwardedTotal, Is.EqualTo(0));
            Assert.That(_metrics, Is.EqualTo(new[] { DistroLogCapture.DuplicateOtelRecordsDroppedMetricName }));
            Assert.That(_logger.Messages(Level.Debug), Has.Length.EqualTo(1));
        });
    }

    [Test]
    public void OutermostExit_NrDidNotRecord_FlushesPendingInOrderAndCounts()
    {
        DistroLogCapture.Enter();
        DistroLogCapture.TryDefer(Record("a"));
        DistroLogCapture.TryDefer(Record("b"));

        DistroLogCapture.ExitFrame(1);

        Assert.Multiple(() =>
        {
            Assert.That(_flushed.Select(r => r.FormattedMessage), Is.EqualTo(new[] { "a", "b" }), "fallback forwards in order");
            Assert.That(DistroLogCapture.FallbackForwardedTotal, Is.EqualTo(2));
            Assert.That(DistroLogCapture.DuplicatesDroppedTotal, Is.EqualTo(0));
            Assert.That(_metrics, Is.EqualTo(new[] { DistroLogCapture.OtelRecordsFallbackForwardedMetricName, DistroLogCapture.OtelRecordsFallbackForwardedMetricName }));
            Assert.That(_logger.Messages(Level.Debug), Has.Length.EqualTo(1));
        });
    }

    [Test]
    public void NestedScopes_ShareOneOutermostDecision()
    {
        // MEL Logger.Log (outer) dispatches to a Serilog/NLog provider (inner NR site). The mark can come from the inner
        // frame; the decision happens once, at the outermost exit, over every record deferred in the whole scope.
        DistroLogCapture.Enter();                       // level 1 (MEL)
        DistroLogCapture.TryDefer(Record("otel-from-mel"));
        DistroLogCapture.Enter();                       // level 2 (inner NR site)
        DistroLogCapture.NotifyNrRecorded();            // NR recorded via the inner site
        DistroLogCapture.ExitFrame(2);                  // inner closes, no decision yet

        DistroLogCapture.ExitFrame(1);                  // outermost decides

        Assert.Multiple(() =>
        {
            Assert.That(_flushed, Is.Empty, "the inner NR record covers the line, so the MEL OTel copy is discarded");
            Assert.That(DistroLogCapture.DuplicatesDroppedTotal, Is.EqualTo(1));
        });
    }

    [Test]
    public void SkippedInnerExit_IsHealedByOutermostExit()
    {
        DistroLogCapture.Enter();          // 1
        DistroLogCapture.Enter();          // 2  (its ExitFrame(2) will be "skipped")
        DistroLogCapture.TryDefer(Record("a"));

        DistroLogCapture.ExitFrame(1);     // outermost authoritatively resets depth to 0

        Assert.Multiple(() =>
        {
            Assert.That(DistroLogCapture.IsCapturing, Is.False, "depth reset authoritatively");
            Assert.That(_flushed.Select(r => r.FormattedMessage), Is.EqualTo(new[] { "a" }));
        });
    }

    [Test]
    public void NestedExit_ResetsDepthToJustBelowThatFrame()
    {
        DistroLogCapture.Enter(); // 1
        DistroLogCapture.Enter(); // 2
        DistroLogCapture.Enter(); // 3

        DistroLogCapture.ExitFrame(2); // heals a skipped level-3 exit: depth -> 1

        Assert.That(DistroLogCapture.IsCapturing, Is.True);
        DistroLogCapture.ExitFrame(1);
        Assert.That(DistroLogCapture.IsCapturing, Is.False);
    }

    [Test]
    public void Overflow_FlushesOldestToBoundMemory()
    {
        DistroLogCapture.Enter();
        for (var i = 0; i < DistroLogCapture.MaxPendingRecords; i++)
        {
            DistroLogCapture.TryDefer(Record("r" + i));
        }

        // One past the bound: the oldest ("r0") is flushed immediately to make room.
        DistroLogCapture.TryDefer(Record("overflow"));

        Assert.Multiple(() =>
        {
            Assert.That(_flushed.Select(r => r.FormattedMessage), Is.EqualTo(new[] { "r0" }));
            Assert.That(DistroLogCapture.FallbackForwardedTotal, Is.EqualTo(1));
        });
    }

    [Test]
    public void OverflowFlush_DoesNotMarkTheStillOpenScope()
    {
        // The real flush re-enters Core's log path (DistroLogSink.Record -> Agent.RecordLogMessage -> NotifyNrRecorded).
        // Simulate that: the sink marks on each flush. The overflow flush happens while the scope is open, but the guard
        // must stop it marking the scope, so the remaining records are still forwarded (fallback), not discarded.
        DistroLogCapture.SetSink(r =>
        {
            _flushed.Add(r);
            DistroLogCapture.NotifyNrRecorded();
        });

        DistroLogCapture.Enter();
        for (var i = 0; i < DistroLogCapture.MaxPendingRecords; i++)
        {
            DistroLogCapture.TryDefer(Record("r" + i));
        }

        DistroLogCapture.TryDefer(Record("overflow")); // flushes "r0" while depth > 0
        DistroLogCapture.ExitFrame(1);

        Assert.Multiple(() =>
        {
            Assert.That(DistroLogCapture.DuplicatesDroppedTotal, Is.EqualTo(0), "the overflow flush must not have marked the scope");
            // r0 (overflow) + the remaining 64 at scope end = all forwarded.
            Assert.That(_flushed, Has.Count.EqualTo(DistroLogCapture.MaxPendingRecords + 1));
            Assert.That(_flushed[0].FormattedMessage, Is.EqualTo("r0"));
        });
    }

    [Test]
    public void NotifyNrRecorded_OutsideScope_IsANoOp()
    {
        DistroLogCapture.NotifyNrRecorded();

        // Opening a scope after the stray mark starts unmarked, so the OTel copy is forwarded, not discarded.
        DistroLogCapture.Enter();
        DistroLogCapture.TryDefer(Record("a"));
        DistroLogCapture.ExitFrame(1);

        Assert.That(_flushed.Select(r => r.FormattedMessage), Is.EqualTo(new[] { "a" }));
    }

    [Test]
    public void OutermostExit_NoPending_DoesNothing()
    {
        DistroLogCapture.Enter();
        DistroLogCapture.NotifyNrRecorded();

        DistroLogCapture.ExitFrame(1);

        Assert.Multiple(() =>
        {
            Assert.That(_flushed, Is.Empty);
            Assert.That(_metrics, Is.Empty);
            Assert.That(DistroLogCapture.DuplicatesDroppedTotal, Is.EqualTo(0));
        });
    }

    [Test]
    public void DebugReasons_LoggedAtMostOnceEach()
    {
        // First drop and first fallback each log once; repeats do not.
        DistroLogCapture.Enter();
        DistroLogCapture.TryDefer(Record("drop"));
        DistroLogCapture.NotifyNrRecorded();
        DistroLogCapture.ExitFrame(1);

        DistroLogCapture.Enter();
        DistroLogCapture.TryDefer(Record("drop2"));
        DistroLogCapture.NotifyNrRecorded();
        DistroLogCapture.ExitFrame(1);

        DistroLogCapture.Enter();
        DistroLogCapture.TryDefer(Record("fwd"));
        DistroLogCapture.ExitFrame(1);

        DistroLogCapture.Enter();
        DistroLogCapture.TryDefer(Record("fwd2"));
        DistroLogCapture.ExitFrame(1);

        Assert.That(_logger.Messages(Level.Debug), Has.Length.EqualTo(2), "one drop reason + one fallback reason");
    }

    [Test]
    public void ResetForTesting_ClearsScopeTotalsAndOnceFlags()
    {
        DistroLogCapture.Enter();
        DistroLogCapture.TryDefer(Record("a"));
        DistroLogCapture.NotifyNrRecorded();
        DistroLogCapture.ExitFrame(1);

        DistroLogCapture.ResetForTesting();

        Assert.Multiple(() =>
        {
            Assert.That(DistroLogCapture.IsCapturing, Is.False);
            Assert.That(DistroLogCapture.DuplicatesDroppedTotal, Is.EqualTo(0));
            Assert.That(DistroLogCapture.FallbackForwardedTotal, Is.EqualTo(0));
        });
    }

    [Test]
    public void CreateFrame_CarriesLevelAndFinish_TryCloseFrameReadsThemBackAndClosesScope()
    {
        Action<object, Exception> finish = (_, __) => { };

        var level = DistroLogCapture.Enter();
        var marker = DistroLogCapture.CreateFrame(level, finish);

        Assert.That(DistroLogCapture.TryCloseFrame(marker, out var recovered), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(recovered, Is.SameAs(finish));
            Assert.That(DistroLogCapture.IsCapturing, Is.False, "closing the frame exited the scope");
        });
    }

    [Test]
    public void TryCloseFrame_NonFrameState_ReturnsFalse()
    {
        Assert.Multiple(() =>
        {
            Assert.That(DistroLogCapture.TryCloseFrame(null, out var f1), Is.False);
            Assert.That(f1, Is.Null);
            Assert.That(DistroLogCapture.TryCloseFrame("not a frame", out var f2), Is.False);
            Assert.That(f2, Is.Null);
        });
    }

    [Test]
    public void CreateFrame_ReusesOnePerLevel_NoAllocationPerCall()
    {
        var m1 = DistroLogCapture.CreateFrame(DistroLogCapture.Enter(), (Action<object, Exception>)((_, __) => { }));
        DistroLogCapture.TryCloseFrame(m1, out _);
        var m2 = DistroLogCapture.CreateFrame(DistroLogCapture.Enter(), (Action<object, Exception>)((_, __) => { }));
        DistroLogCapture.TryCloseFrame(m2, out _);

        Assert.That(m2, Is.SameAs(m1), "the level-1 frame holder is reused across calls on the thread");
    }

    [Test]
    public void Overflow_WhenAlreadyMarked_DiscardsOldestAsDuplicate()
    {
        DistroLogCapture.Enter();
        DistroLogCapture.NotifyNrRecorded(); // scope already known to be an NR-recorded line
        for (var i = 0; i < DistroLogCapture.MaxPendingRecords; i++)
        {
            DistroLogCapture.TryDefer(Record("r" + i));
        }

        DistroLogCapture.TryDefer(Record("overflow")); // oldest is a known duplicate -> dropped, not flushed

        Assert.Multiple(() =>
        {
            Assert.That(_flushed, Is.Empty, "a marked scope discards the overflowed copy rather than forwarding it");
            Assert.That(DistroLogCapture.DuplicatesDroppedTotal, Is.EqualTo(1));
            Assert.That(DistroLogCapture.FallbackForwardedTotal, Is.EqualTo(0));
        });
    }

    // ---- Early-drop query (optimization 1) ----

    [Test]
    public void ClassifyOtelRecord_NoScope_Forward()
    {
        Assert.That(DistroLogCapture.ClassifyOtelRecord(), Is.EqualTo(DistroLogDecision.Forward));
    }

    [Test]
    public void ClassifyOtelRecord_ScopeOpenButNotMarked_Defer()
    {
        DistroLogCapture.Enter();

        Assert.That(DistroLogCapture.ClassifyOtelRecord(), Is.EqualTo(DistroLogDecision.Defer));
    }

    [Test]
    public void ClassifyOtelRecord_ScopeAlreadyMarked_DropDuplicate_AndCounts()
    {
        DistroLogCapture.Enter();
        DistroLogCapture.NotifyNrRecorded();

        var first = DistroLogCapture.ClassifyOtelRecord();
        var second = DistroLogCapture.ClassifyOtelRecord();
        DistroLogCapture.ExitFrame(1);

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.EqualTo(DistroLogDecision.DropDuplicate));
            Assert.That(second, Is.EqualTo(DistroLogDecision.DropDuplicate));
            Assert.That(DistroLogCapture.DuplicatesDroppedTotal, Is.EqualTo(2), "each early drop is counted");
            Assert.That(_flushed, Is.Empty);
            Assert.That(_metrics, Is.EqualTo(new[] { DistroLogCapture.DuplicateOtelRecordsDroppedMetricName }), "first occurrence recorded once");
        });
    }

    [Test]
    public void ClassifyOtelRecord_WhileFlushing_DoesNotDrop()
    {
        var captured = false;
        var duringFlush = DistroLogDecision.DropDuplicate;
        DistroLogCapture.SetSink(r =>
        {
            if (!captured)
            {
                captured = true;
                duringFlush = DistroLogCapture.ClassifyOtelRecord();
            }

            _flushed.Add(r);
        });

        DistroLogCapture.Enter();
        for (var i = 0; i < DistroLogCapture.MaxPendingRecords; i++)
        {
            DistroLogCapture.TryDefer(Record("r" + i));
        }

        DistroLogCapture.TryDefer(Record("overflow")); // flushes the oldest with depth > 0 and _flushing set
        DistroLogCapture.ExitFrame(1);

        // During a flush the query must not say DropDuplicate; here the scope is unmarked, so Defer.
        Assert.That(duringFlush, Is.EqualTo(DistroLogDecision.Defer));
    }

    [Test]
    public void ClassifyOtelRecord_DropDuplicate_AllocatesZeroBytesAfterWarmup()
    {
        DistroLogCapture.Enter();
        DistroLogCapture.NotifyNrRecorded();

        // Warm up so the first-occurrence metric push and JIT tiering are out of the way.
        for (var i = 0; i < 1000; i++)
        {
            DistroLogCapture.ClassifyOtelRecord();
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 10000; i++)
        {
            DistroLogCapture.ClassifyOtelRecord();
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        DistroLogCapture.ExitFrame(1);

        Assert.That(allocated, Is.EqualTo(0), "the early-drop query must allocate nothing");
    }

    [Test]
    public void Defer_ConsecutiveScopes_ReuseTheHeldList_NoPerScopeAllocation()
    {
        DistroLogCapture.SetSink(_ => { });
        var record = Record("x");

        // Warm up: the first scope allocates the list and pushes the first metric.
        for (var i = 0; i < 1000; i++)
        {
            DistroLogCapture.Enter();
            DistroLogCapture.TryDefer(record);
            DistroLogCapture.ExitFrame(1);
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 10000; i++)
        {
            DistroLogCapture.Enter();
            DistroLogCapture.TryDefer(record);
            DistroLogCapture.ExitFrame(1);
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.That(allocated, Is.EqualTo(0), "consecutive scopes must reuse the held list, allocating nothing");
    }

    [Test]
    public void OutermostExit_ClearsHeldRecordReferences_SoTheyAreCollectable()
    {
        DistroLogCapture.SetSink(_ => { });
        var weak = DeferDropAndReturnWeakRef();

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Assert.That(weak.IsAlive, Is.False, "the dropped record must not be retained by the dedupe's pending/spare list");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference DeferDropAndReturnWeakRef()
    {
        var record = new DistroLogRecord { FormattedMessage = "gc-me" };
        var weak = new WeakReference(record);
        DistroLogCapture.Enter();
        DistroLogCapture.TryDefer(record);
        DistroLogCapture.NotifyNrRecorded();
        DistroLogCapture.ExitFrame(1);
        return weak;
    }

    [Test]
    public void FallbackFlush_ReentrantLogSiteDuringFlush_DoesNotCorruptTheFlush()
    {
        var reentered = false;
        DistroLogCapture.SetSink(r =>
        {
            _flushed.Add(r);
            if (!reentered)
            {
                reentered = true;
                // Simulate an instrumented log call happening on this thread during the flush: a fresh outermost scope.
                // The outermost exit detached the pending list before flushing, so this re-entrant Enter gets a new list
                // and does not corrupt the flush in progress.
                DistroLogCapture.Enter();
                DistroLogCapture.TryDefer(Record("reentrant"));
                DistroLogCapture.ExitFrame(1);
            }
        });

        DistroLogCapture.Enter();
        DistroLogCapture.TryDefer(Record("a"));
        DistroLogCapture.TryDefer(Record("b"));
        DistroLogCapture.TryDefer(Record("c"));
        DistroLogCapture.ExitFrame(1);

        // All three outer records are forwarded, plus the re-entrant one flushed during the first record's flush.
        Assert.That(_flushed.Select(r => r.FormattedMessage), Is.EqualTo(new[] { "a", "reentrant", "b", "c" }));
    }

    [Test]
    public void FallbackFlush_IsolatesAThrowingSink_RestStillForwardedAndCounted()
    {
        DistroLogCapture.SetSink(r =>
        {
            if (r.FormattedMessage == "b")
            {
                throw new InvalidOperationException("boom");
            }

            _flushed.Add(r);
        });

        DistroLogCapture.Enter();
        DistroLogCapture.TryDefer(Record("a"));
        DistroLogCapture.TryDefer(Record("b"));
        DistroLogCapture.TryDefer(Record("c"));

        DistroLogCapture.ExitFrame(1);

        Assert.Multiple(() =>
        {
            // "b" threw but did not skip "a"/"c", and the count still reflects all three.
            Assert.That(_flushed.Select(r => r.FormattedMessage), Is.EqualTo(new[] { "a", "c" }));
            Assert.That(DistroLogCapture.FallbackForwardedTotal, Is.EqualTo(3));
        });
    }
}

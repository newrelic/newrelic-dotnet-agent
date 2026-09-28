// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Runtime.InteropServices;
using System.Threading;
using Google.Protobuf;
using NewRelic.Agent.Core.ContinuousProfiling;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.UnitTest.ContinuousProfiling;

[TestFixture]
public class ContinuousProfilingContextTests
{
    private INativeContinuousProfiler _native;
    private ContinuousProfilingContext _context;
    private IManagedThreadIdRegistry _previousManagedThreadIdRegistryInstance;

    // Stands in for the native pending-push cell: real unmanaged memory, so the production pointer write
    // is exercised rather than mocked away. One per test, freed in TearDown.
    private IntPtr _cell;

    [SetUp]
    public void SetUp()
    {
        _native = Mock.Create<INativeContinuousProfiler>();
        _context = new ContinuousProfilingContext();
        _previousManagedThreadIdRegistryInstance = ManagedThreadIdRegistry.Instance;
        _cell = Marshal.AllocHGlobal(sizeof(long));
        Marshal.WriteInt64(_cell, 0);

        // AnyEnabled is a process-wide static; Enable/Disable below flip it. Reset it (and the shared
        // Instance seam) around every test so an enabled context can't leak into unrelated fixtures.
        ContinuousProfilingContext.AnyEnabled = false;
    }

    [TearDown]
    public void TearDown()
    {
        ContinuousProfilingContext.AnyEnabled = false;
        ContinuousProfilingContext.Instance = new ContinuousProfilingContext();
        ManagedThreadIdRegistry.Instance = _previousManagedThreadIdRegistryInstance;

        // Safe to free even though a thread may still have this pointer cached: the cache is invalidated by
        // the epoch bump every Enable() performs, so the next test re-resolves before it writes anything.
        Marshal.FreeHGlobal(_cell);
        _cell = IntPtr.Zero;
    }

    [Test]
    public void Not_enabled_by_default()
    {
        Assert.That(_context.IsEnabled, Is.False);
    }

    [Test]
    public void PushTraceContext_does_nothing_when_disabled()
    {
        _context.PushTraceContext("0123456789abcdeffedcba9876543210", "1122334455667788");

        Mock.Assert(() => _native.SetTraceContext(Arg.AnyLong, Arg.AnyLong, Arg.AnyLong), Occurs.Never());
    }

    [Test]
    public void ResetTraceContext_does_nothing_when_disabled()
    {
        _context.ResetTraceContext();

        Mock.Assert(() => _native.ResetTraceContext(), Occurs.Never());
    }

    [Test]
    public void Enable_marks_the_context_enabled()
    {
        _context.Enable(_native);

        Assert.That(_context.IsEnabled, Is.True);
    }

    [Test]
    public void Enable_rejects_a_null_native_profiler_and_leaves_the_context_disarmed()
    {
        Assert.Throws<ArgumentNullException>(() => _context.Enable(null));

        Assert.Multiple(() =>
        {
            Assert.That(_context.IsEnabled, Is.False);
            Assert.That(ContinuousProfilingContext.AnyEnabled, Is.False, "a failed Enable must not arm the hot-path pre-filter");
        });

        // The failed Enable must also not have armed the agent-work reset target.
        _context.ResetAgentWork();
        Mock.Assert(() => _native.ResetAgentWork(), Occurs.Never());
    }

    [Test]
    public void Disable_marks_the_context_disabled()
    {
        _context.Enable(_native);

        _context.Disable();

        Assert.That(_context.IsEnabled, Is.False);
    }

    [Test]
    public void AnyEnabled_is_false_on_a_fresh_fixture()
    {
        // SetUp resets it; no Enable has run yet.
        Assert.That(ContinuousProfilingContext.AnyEnabled, Is.False);
    }

    [Test]
    public void AnyEnabled_tracks_enable_disable_and_reenable()
    {
        Assert.That(ContinuousProfilingContext.AnyEnabled, Is.False, "should start disabled");

        _context.Enable(_native);
        Assert.That(ContinuousProfilingContext.AnyEnabled, Is.True, "Enable must arm the hot-path pre-filter");

        _context.Disable();
        Assert.That(ContinuousProfilingContext.AnyEnabled, Is.False, "Disable must clear the hot-path pre-filter");

        _context.Enable(_native);
        Assert.That(ContinuousProfilingContext.AnyEnabled, Is.True, "re-Enable must re-arm the hot-path pre-filter");
    }

    [Test]
    public void PushTraceContext_decomposes_trace_and_span_ids_to_match_OtlpProfileBuilder()
    {
        _context.Enable(_native);

        // A known W3C-style 32-char (16-byte) trace id and 16-char (8-byte) span id.
        const string traceId = "0123456789abcdeffedcba9876543210";
        const string spanId = "1122334455667788";

        long capturedHigh = 0, capturedLow = 0, capturedSpan = 0;
        Mock.Arrange(() => _native.SetTraceContext(Arg.AnyLong, Arg.AnyLong, Arg.AnyLong))
            .DoInstead((long h, long l, long s) => { capturedHigh = h; capturedLow = l; capturedSpan = s; });

        _context.PushTraceContext(traceId, spanId);

        Mock.Assert(() => _native.SetTraceContext(Arg.AnyLong, Arg.AnyLong, Arg.AnyLong), Occurs.Once());

        // Exact expected longs (bit-for-bit): high = first 8 bytes big-endian, low = last 8 bytes big-endian.
        Assert.Multiple(() =>
        {
            Assert.That(capturedHigh, Is.EqualTo(unchecked((long)0x0123456789abcdefUL)));
            Assert.That(capturedLow, Is.EqualTo(unchecked((long)0xfedcba9876543210UL)));
            Assert.That(capturedSpan, Is.EqualTo(unchecked((long)0x1122334455667788UL)));
        });

        // Cross-check against OtlpProfileBuilder's Link encoding: feeding these longs back through the
        // builder must reproduce the original hex ids, proving the decomposition round-trips.
        var request = OtlpProfileBuilder.Build(
            new[] { new ManagedThreadSample("t", 1, capturedHigh, capturedLow, capturedSpan, new[] { "F()" }) },
            0, 0, "svc");

        // link_table[0] is the zero value; the sample's link is at index 1.
        var link = request.Dictionary.LinkTable[1];
        Assert.Multiple(() =>
        {
            Assert.That(ToHex(link.TraceId), Is.EqualTo(traceId));
            Assert.That(ToHex(link.SpanId), Is.EqualTo(spanId));
        });
    }

    [Test]
    public void PushTraceContext_with_uppercase_hex_still_round_trips()
    {
        _context.Enable(_native);

        long capturedHigh = 0, capturedLow = 0, capturedSpan = 0;
        Mock.Arrange(() => _native.SetTraceContext(Arg.AnyLong, Arg.AnyLong, Arg.AnyLong))
            .DoInstead((long h, long l, long s) => { capturedHigh = h; capturedLow = l; capturedSpan = s; });

        _context.PushTraceContext("0123456789ABCDEFFEDCBA9876543210", "1122334455667788");

        Assert.Multiple(() =>
        {
            Assert.That(capturedHigh, Is.EqualTo(unchecked((long)0x0123456789abcdefUL)));
            Assert.That(capturedLow, Is.EqualTo(unchecked((long)0xfedcba9876543210UL)));
            Assert.That(capturedSpan, Is.EqualTo(unchecked((long)0x1122334455667788UL)));
        });
    }

    [Test]
    public void PushTraceContext_with_null_trace_id_pushes_zeros()
    {
        _context.Enable(_native);

        _context.PushTraceContext(null, "1122334455667788");

        Mock.Assert(() => _native.SetTraceContext(0L, 0L, Arg.AnyLong), Occurs.Once());
    }

    [Test]
    public void PushTraceContext_with_wrong_length_trace_id_pushes_zero_trace()
    {
        _context.Enable(_native);

        // 30 chars, not a valid 32-char trace id -> no linked trace.
        _context.PushTraceContext("0123456789abcdeffedcba98765432", "1122334455667788");

        Mock.Assert(() => _native.SetTraceContext(0L, 0L, unchecked((long)0x1122334455667788UL)), Occurs.Once());
    }

    [Test]
    public void PushTraceContext_with_wrong_length_span_id_pushes_zero_span()
    {
        _context.Enable(_native);

        // 14-char span id -> no span.
        _context.PushTraceContext("0123456789abcdeffedcba9876543210", "112233445566");

        Mock.Assert(() => _native.SetTraceContext(unchecked((long)0x0123456789abcdefUL), unchecked((long)0xfedcba9876543210UL), 0L), Occurs.Once());
    }

    [Test]
    public void PushTraceContext_with_non_hex_characters_pushes_zeros_and_does_not_throw()
    {
        _context.Enable(_native);

        Assert.DoesNotThrow(() => _context.PushTraceContext("zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz", "zzzzzzzzzzzzzzzz"));

        Mock.Assert(() => _native.SetTraceContext(0L, 0L, 0L), Occurs.Once());
    }

    [Test]
    public void ResetTraceContext_forwards_to_native_when_enabled()
    {
        _context.Enable(_native);

        _context.ResetTraceContext();

        Mock.Assert(() => _native.ResetTraceContext(), Occurs.Once());
    }

    [Test]
    public void PushTraceContext_never_throws_when_native_throws()
    {
        _context.Enable(_native);
        Mock.Arrange(() => _native.SetTraceContext(Arg.AnyLong, Arg.AnyLong, Arg.AnyLong)).Throws(new InvalidOperationException("boom"));

        Assert.DoesNotThrow(() => _context.PushTraceContext("0123456789abcdeffedcba9876543210", "1122334455667788"));
    }

    [Test]
    public void ResetTraceContext_never_throws_when_native_throws()
    {
        _context.Enable(_native);
        Mock.Arrange(() => _native.ResetTraceContext()).Throws(new InvalidOperationException("boom"));

        Assert.DoesNotThrow(() => _context.ResetTraceContext());
    }

    [Test]
    public void Instance_is_disabled_by_default()
    {
        // The process-wide default instance must be inert so the hot path pays nothing when CP is off.
        Assert.That(ContinuousProfilingContext.Instance.IsEnabled, Is.False);
    }

    [Test]
    public void PushTraceContext_skips_redundant_push_of_same_instances()
    {
        _context.Enable(_native);

        // Same string instances pushed repeatedly (the common case: wrapper enter+exit within one segment).
        var traceId = "0123456789abcdeffedcba9876543210";
        var spanId = "1122334455667788";

        _context.PushTraceContext(traceId, spanId);
        _context.PushTraceContext(traceId, spanId);
        _context.PushTraceContext(traceId, spanId);

        // Change-detection: only the first push reaches native; the rest are skipped.
        Mock.Assert(() => _native.SetTraceContext(Arg.AnyLong, Arg.AnyLong, Arg.AnyLong), Occurs.Once());
    }

    [Test]
    public void PushTraceContext_pushes_again_when_span_changes()
    {
        _context.Enable(_native);
        var traceId = "0123456789abcdeffedcba9876543210";

        _context.PushTraceContext(traceId, "1122334455667788");
        _context.PushTraceContext(traceId, "8877665544332211"); // different span -> real change -> push

        Mock.Assert(() => _native.SetTraceContext(Arg.AnyLong, Arg.AnyLong, Arg.AnyLong), Occurs.Exactly(2));
    }

    [Test]
    public void PushTraceContext_change_detection_is_by_reference_not_value()
    {
        _context.Enable(_native);

        // Two DISTINCT string instances with identical value. The guard compares by reference (the real
        // Transaction/Segment hand back stable instances), so distinct instances push again -- a redundant
        // push is harmless; a missed push is not. This documents that reference semantics.
        var traceA = new string('a', 32);
        var traceB = new string('a', 32);
        var spanId = "1122334455667788";

        _context.PushTraceContext(traceA, spanId);
        _context.PushTraceContext(traceB, spanId);

        Mock.Assert(() => _native.SetTraceContext(Arg.AnyLong, Arg.AnyLong, Arg.AnyLong), Occurs.Exactly(2));
    }

    [Test]
    public void PushTraceContext_pushes_again_after_reenable()
    {
        _context.Enable(_native);
        var traceId = "0123456789abcdeffedcba9876543210";
        var spanId = "1122334455667788";

        _context.PushTraceContext(traceId, spanId);
        _context.PushTraceContext(traceId, spanId); // skipped (unchanged)

        // Re-arm the session (retune without restart). Even though the ids are the same instances, the
        // freshly-armed native map is empty, so the guard must not suppress the first post-enable push.
        _context.Disable();
        _context.Enable(_native);

        _context.PushTraceContext(traceId, spanId); // must reach native despite identical instances

        Mock.Assert(() => _native.SetTraceContext(Arg.AnyLong, Arg.AnyLong, Arg.AnyLong), Occurs.Exactly(2));
    }

    [Test]
    public void SetAgentWork_does_nothing_when_never_enabled()
    {
        _context.SetAgentWork();

        Mock.Assert(() => _native.SetAgentWork(), Occurs.Never());
    }

    [Test]
    public void ResetAgentWork_does_nothing_when_never_enabled()
    {
        // A context that was never armed has no native counter to decrement -- notably the inert default
        // Instance, which the wrapper/scheduler paths may still call a reset on.
        _context.ResetAgentWork();

        Mock.Assert(() => _native.ResetAgentWork(), Occurs.Never());
    }

    [Test]
    public void SetAgentWork_is_suppressed_after_Disable()
    {
        _context.Enable(_native);
        _context.Disable();

        _context.SetAgentWork();

        Mock.Assert(() => _native.SetAgentWork(), Occurs.Never());
    }

    // Regression: SetAgentWork/ResetAgentWork drive a native per-thread nesting-DEPTH counter that
    // requires strict 1:1 pairing. Scheduler captures this context once per timer callback, so a
    // continuous-profiling stop/retune can Disable() it BETWEEN the set and the reset. If Disable also
    // silenced the reset, that thread's slot would stay at depth >= 1 for the rest of the process and
    // every later sample on it -- application work included -- would be filtered out as agent work.
    [Test]
    public void ResetAgentWork_still_forwards_after_Disable_so_an_outstanding_set_is_not_orphaned()
    {
        _context.Enable(_native);
        _context.SetAgentWork();

        _context.Disable(); // e.g. ContinuousProfilingService.StopLocked mid-callback

        _context.ResetAgentWork();

        Mock.Assert(() => _native.SetAgentWork(), Occurs.Once());
        Mock.Assert(() => _native.ResetAgentWork(), Occurs.Once());
    }

    [Test]
    public void ResetAgentWork_still_forwards_after_Disable_without_a_preceding_set()
    {
        // Native Decrement clamps at depth 0, so an unpaired reset on a once-armed context is harmless;
        // what must not happen is the reset being dropped, since the context cannot tell the two apart.
        _context.Enable(_native);
        _context.Disable();

        _context.ResetAgentWork();

        Mock.Assert(() => _native.ResetAgentWork(), Occurs.Once());
    }

    [Test]
    public void ResetAgentWork_after_Disable_targets_the_native_profiler_that_took_the_set()
    {
        // A stop/start retune re-arms with a (possibly different) native profiler. A reset outstanding
        // from the previous session must decrement the counter its set incremented -- the FIRST native --
        // not whichever profiler happens to be armed when the reset lands.
        var secondNative = Mock.Create<INativeContinuousProfiler>();

        _context.Enable(_native);
        _context.SetAgentWork();
        _context.Disable();

        var secondSessionContext = new ContinuousProfilingContext();
        secondSessionContext.Enable(secondNative);

        // The captured (old) context's reset must not be redirected to the newly armed profiler.
        _context.ResetAgentWork();

        Mock.Assert(() => _native.ResetAgentWork(), Occurs.Once());
        Mock.Assert(() => secondNative.ResetAgentWork(), Occurs.Never());
    }

    [Test]
    public void Disable_does_not_suppress_a_reset_on_a_re_enabled_context()
    {
        _context.Enable(_native);
        _context.Disable();
        _context.Enable(_native); // retune restarts the same context object

        _context.SetAgentWork();
        _context.ResetAgentWork();

        Mock.Assert(() => _native.SetAgentWork(), Occurs.Once());
        Mock.Assert(() => _native.ResetAgentWork(), Occurs.Once());
    }

    [Test]
    public void SetAgentWork_forwards_to_native_when_enabled()
    {
        _context.Enable(_native);

        _context.SetAgentWork();

        Mock.Assert(() => _native.SetAgentWork(), Occurs.Once());
    }

    [Test]
    public void ResetAgentWork_forwards_to_native_when_enabled()
    {
        _context.Enable(_native);

        _context.ResetAgentWork();

        Mock.Assert(() => _native.ResetAgentWork(), Occurs.Once());
    }

    [Test]
    public void SetAgentWork_never_throws_when_native_throws()
    {
        _context.Enable(_native);
        Mock.Arrange(() => _native.SetAgentWork()).Throws(new InvalidOperationException("boom"));

        Assert.DoesNotThrow(() => _context.SetAgentWork());
    }

    [Test]
    public void SetAgentWork_RegistersCurrentThreadWithManagedThreadIdRegistry_WhenEnabled()
    {
        var fakeRegistry = Mock.Create<IManagedThreadIdRegistry>();
        var previousInstance = ManagedThreadIdRegistry.Instance;
        ManagedThreadIdRegistry.Instance = fakeRegistry;

        try
        {
            _context.Enable(_native);
            _context.SetAgentWork();

            Mock.Assert(() => fakeRegistry.EnsureRegistered(), Occurs.Once());
        }
        finally
        {
            ManagedThreadIdRegistry.Instance = previousInstance;
        }
    }

    [Test]
    public void SetAgentWork_DoesNotRegister_WhenNeverEnabled()
    {
        var fakeRegistry = Mock.Create<IManagedThreadIdRegistry>();
        var previousInstance = ManagedThreadIdRegistry.Instance;
        ManagedThreadIdRegistry.Instance = fakeRegistry;

        try
        {
            // _context was never Enable()d in [SetUp] -- _native is still null, the CP-disabled state.
            _context.SetAgentWork();

            Mock.Assert(() => fakeRegistry.EnsureRegistered(), Occurs.Never());
        }
        finally
        {
            ManagedThreadIdRegistry.Instance = previousInstance;
        }
    }

    [Test]
    public void ResetAgentWork_never_throws_when_native_throws()
    {
        _context.Enable(_native);
        Mock.Arrange(() => _native.ResetAgentWork()).Throws(new InvalidOperationException("boom"));

        Assert.DoesNotThrow(() => _context.ResetAgentWork());
    }

    [Test]
    public void ResetTraceContext_clears_change_detection_guard()
    {
        _context.Enable(_native);
        var traceId = "0123456789abcdeffedcba9876543210";
        var spanId = "1122334455667788";

        _context.PushTraceContext(traceId, spanId); // push #1
        _context.ResetTraceContext();               // native cleared -> guard must clear too
        _context.PushTraceContext(traceId, spanId); // same instances, but guard cleared -> push #2

        Mock.Assert(() => _native.SetTraceContext(Arg.AnyLong, Arg.AnyLong, Arg.AnyLong), Occurs.Exactly(2));
    }

    [Test]
    public void ResetTraceContext_leaves_the_epoch_guard_set_which_only_suppresses_a_redundant_no_trace_repush()
    {
        // Finding (investigated per the L27 generation-counter design): ResetTraceContext nulls
        // _lastPushedTraceId/_lastPushedSpanId but deliberately does NOT clear the per-thread _lastPushedEpoch.
        // That is correct, not a residual bug. Nulling the id guards alone already forces any real (non-null,
        // distinct-instance) push through after a reset -- proven by ResetTraceContext_clears_change_detection_guard
        // above, since ReferenceEquals(realId, null) is false. The ONLY push the retained epoch can still suppress
        // is a redundant re-push of the "no trace" state (null ids -> zeros), which native already reflects the
        // instant ResetTraceContext ran -- so skipping it is the intended micro-optimization, not a dropped update.
        // The epoch's sole purpose is to invalidate every thread's guard on an Enable() re-arm (the generation
        // counter); a reset does not, and must not, bump it.
        _context.Enable(_native);
        var traceId = "0123456789abcdeffedcba9876543210";
        var spanId = "1122334455667788";

        _context.PushTraceContext(traceId, spanId); // push #1 -> reaches native, records epoch + id guards
        _context.ResetTraceContext();               // native reset; id guards nulled, epoch retained

        // A redundant "no trace" (null,null) push after the reset must be skipped: with the epoch still matching
        // and both id guards null, all three guard fields match the no-trace state native already holds. Were the
        // epoch cleared on reset instead, this push would go through and native.SetTraceContext would occur twice.
        _context.PushTraceContext(null, null);

        Mock.Assert(() => _native.SetTraceContext(Arg.AnyLong, Arg.AnyLong, Arg.AnyLong), Occurs.Once(),
            "only push #1 reaches native; the post-reset (null,null) re-push is correctly suppressed because the retained epoch plus nulled id guards match the no-trace state native already holds after the reset");
        Mock.Assert(() => _native.ResetTraceContext(), Occurs.Once());
    }

    [Test]
    public void PushTraceContext_change_detection_state_does_not_bleed_across_threads()
    {
        // The change-detection guards (_lastPushedTraceId/_lastPushedSpanId/_lastPushedEpoch) are [ThreadStatic]
        // to match the native map's per-CLR-thread keying. This proves that state established on one thread never
        // suppresses a push on another: two threads each push the SAME string instances, and each must reach
        // native once (its own second push deduped on that thread, but never deduped against the other thread).
        _context.Enable(_native);
        var traceId = "0123456789abcdeffedcba9876543210";
        var spanId = "1122334455667788";

        // Thread A: pushes the instances twice. Its own second push is skipped by A's per-thread guard.
        RunOnDedicatedThread(() =>
        {
            _context.PushTraceContext(traceId, spanId);
            _context.PushTraceContext(traceId, spanId);
        });

        // Thread B: pushes the SAME instances twice. If A's [ThreadStatic] guard had bled into B, B's first push
        // would be wrongly suppressed and the total native count would be 1 instead of 2. B's own second push is
        // again skipped by B's independent per-thread guard.
        RunOnDedicatedThread(() =>
        {
            _context.PushTraceContext(traceId, spanId);
            _context.PushTraceContext(traceId, spanId);
        });

        // Exactly one native push per thread: within-thread dedup works independently on each, and neither
        // thread's guard suppressed the other's first push.
        Mock.Assert(() => _native.SetTraceContext(Arg.AnyLong, Arg.AnyLong, Arg.AnyLong), Occurs.Exactly(2),
            "each thread's first push must reach native (no cross-thread bleed) while each thread's second push is deduped by its own per-thread guard");
    }

    // ---- pending-push cell (the managed half of PendingPushMap) ----------------------------------
    //
    // The cell lets a compaction pass see a span id this thread has already decoded but not yet
    // published into its native slot. A real (unmanaged, 8-byte) cell is used rather than a mocked
    // pointer so the pointer write itself is exercised.

    [Test]
    public void PushTraceContext_publishes_the_decoded_span_id_into_the_cell_before_pushing_and_clears_it_after()
    {
        ArrangeResolvableCell();
        _context.Enable(_native);

        // Captured INSIDE the native call, so the ordering (publish -> push) is asserted, not assumed.
        long cellDuringPush = -1;
        Mock.Arrange(() => _native.SetTraceContext(Arg.AnyLong, Arg.AnyLong, Arg.AnyLong))
            .DoInstead(() => { cellDuringPush = Marshal.ReadInt64(_cell); });

        _context.PushTraceContext("0123456789abcdeffedcba9876543210", "1122334455667788");

        Assert.Multiple(() =>
        {
            Assert.That(cellDuringPush, Is.EqualTo(unchecked((long)0x1122334455667788UL)),
                "the decoded span id must be visible in the cell while the push is in flight");
            Assert.That(Marshal.ReadInt64(_cell), Is.Zero, "the cell must be cleared once the push returns");
        });
    }

    // A stale non-zero cell pins one registry entry until the thread dies, so the clear has to survive
    // an exception -- hence the finally, not a success-path clear.
    [Test]
    public void PushTraceContext_clears_the_cell_even_when_the_native_push_throws()
    {
        ArrangeResolvableCell();
        _context.Enable(_native);
        Mock.Arrange(() => _native.SetTraceContext(Arg.AnyLong, Arg.AnyLong, Arg.AnyLong)).Throws<InvalidOperationException>();

        _context.PushTraceContext("0123456789abcdeffedcba9876543210", "1122334455667788");

        Assert.That(Marshal.ReadInt64(_cell), Is.Zero);
    }

    // A deduped push is sound WITHOUT a cell write because this thread's native slot already holds that
    // span, and the compaction live set is the UNION of slots and cells -- the cell is additive to the
    // slot-based set, never a replacement for it.
    [Test]
    public void PushTraceContext_skipped_by_the_dedupe_does_not_touch_the_cell()
    {
        ArrangeResolvableCell();
        _context.Enable(_native);
        var traceId = "0123456789abcdeffedcba9876543210";
        var spanId = "1122334455667788";

        _context.PushTraceContext(traceId, spanId); // resolves the cell and clears it again
        Marshal.WriteInt64(_cell, 0x5A5A5A5A5A5A5A5AL); // sentinel: a redundant push must not write here

        _context.PushTraceContext(traceId, spanId);

        Assert.Multiple(() =>
        {
            Assert.That(Marshal.ReadInt64(_cell), Is.EqualTo(0x5A5A5A5A5A5A5A5AL));
            Mock.Assert(() => _native.SetTraceContext(Arg.AnyLong, Arg.AnyLong, Arg.AnyLong), Occurs.Once());
        });
    }

    [Test]
    public void ResetTraceContext_clears_the_cell()
    {
        ArrangeResolvableCell();
        _context.Enable(_native);

        _context.PushTraceContext("0123456789abcdeffedcba9876543210", "1122334455667788"); // resolve the cell
        Marshal.WriteInt64(_cell, 0x5A5A5A5A5A5A5A5AL); // simulate an in-flight intent left behind

        _context.ResetTraceContext();

        Assert.That(Marshal.ReadInt64(_cell), Is.Zero);
    }

    // Every GetPendingPushCell failure means "push without publishing intent first", which is exactly the
    // pre-cell behaviour. It must never cost the push.
    [Test]
    public void PushTraceContext_still_pushes_normally_when_the_cell_cannot_be_resolved()
    {
        Mock.Arrange(() => _native.GetPendingPushCell()).Returns(IntPtr.Zero);
        _context.Enable(_native);

        Assert.DoesNotThrow(() => _context.PushTraceContext("0123456789abcdeffedcba9876543210", "1122334455667788"));

        Mock.Assert(() => _native.SetTraceContext(
            unchecked((long)0x0123456789abcdefUL), unchecked((long)0xfedcba9876543210UL), unchecked((long)0x1122334455667788UL)),
            Occurs.Once());
    }

    [Test]
    public void PushTraceContext_still_pushes_normally_when_cell_resolution_throws()
    {
        Mock.Arrange(() => _native.GetPendingPushCell()).Throws<InvalidOperationException>();
        _context.Enable(_native);

        Assert.DoesNotThrow(() => _context.PushTraceContext("0123456789abcdeffedcba9876543210", "1122334455667788"));

        Mock.Assert(() => _native.SetTraceContext(Arg.AnyLong, Arg.AnyLong, Arg.AnyLong), Occurs.Once());
    }

    [Test]
    public void PushTraceContext_resolves_the_cell_once_per_thread_not_once_per_push()
    {
        ArrangeResolvableCell();
        _context.Enable(_native);

        _context.PushTraceContext("0123456789abcdeffedcba9876543210", "1122334455667788");
        _context.PushTraceContext("0123456789abcdeffedcba9876543210", "8877665544332211");
        _context.PushTraceContext("fedcba98765432100123456789abcdef", "8877665544332211");

        Mock.Assert(() => _native.GetPendingPushCell(), Occurs.Once());
    }

    // A thread whose resolution failed must not P/Invoke GetPendingPushCell again on every push.
    [Test]
    public void PushTraceContext_does_not_retry_a_failed_cell_resolution_on_every_push()
    {
        Mock.Arrange(() => _native.GetPendingPushCell()).Returns(IntPtr.Zero);
        _context.Enable(_native);

        _context.PushTraceContext("0123456789abcdeffedcba9876543210", "1122334455667788");
        _context.PushTraceContext("0123456789abcdeffedcba9876543210", "8877665544332211");

        Mock.Assert(() => _native.GetPendingPushCell(), Occurs.Once());
    }

    // ---- RetireSpans ----------------------------------------------------------------------------

    // The decode must match the push path bit-for-bit, or a retirement would never match the value that
    // was pushed. Asserted against the SAME expected long the existing
    // PushTraceContext_decomposes_trace_and_span_ids_to_match_OtlpProfileBuilder test pins.
    [Test]
    public void RetireSpans_decodes_hex_span_ids_the_same_way_PushTraceContext_does()
    {
        _context.Enable(_native);

        long[] captured = null;
        var capturedCount = 0;
        Mock.Arrange(() => _native.RetireSpans(Arg.IsAny<long[]>(), Arg.AnyInt))
            .DoInstead((long[] ids, int count) => { captured = ids; capturedCount = count; });

        _context.RetireSpans(new[] { "1122334455667788" });

        Assert.That(capturedCount, Is.EqualTo(1));
        Assert.That(captured[0], Is.EqualTo(unchecked((long)0x1122334455667788UL)));
    }

    [Test]
    public void RetireSpans_forwards_every_span_in_the_batch()
    {
        _context.Enable(_native);

        var capturedCount = 0;
        Mock.Arrange(() => _native.RetireSpans(Arg.IsAny<long[]>(), Arg.AnyInt))
            .DoInstead((long[] ids, int count) => { capturedCount = count; });

        _context.RetireSpans(new[] { "1122334455667788", "8877665544332211" });

        Assert.That(capturedCount, Is.EqualTo(2));
    }

    // A malformed or null id decodes to zero, which is the native "no span" sentinel and is rejected
    // there. Dropping it here keeps the batch tight and never sends the sentinel across the ABI.
    [Test]
    public void RetireSpans_skips_null_and_malformed_span_ids()
    {
        _context.Enable(_native);

        long[] captured = null;
        var capturedCount = 0;
        Mock.Arrange(() => _native.RetireSpans(Arg.IsAny<long[]>(), Arg.AnyInt))
            .DoInstead((long[] ids, int count) => { captured = ids; capturedCount = count; });

        _context.RetireSpans(new[] { null, "nothex", "short", "1122334455667788" });

        Assert.That(capturedCount, Is.EqualTo(1));
        Assert.That(captured[0], Is.EqualTo(unchecked((long)0x1122334455667788UL)));
    }

    [Test]
    public void RetireSpans_does_not_call_native_when_every_span_id_is_unusable()
    {
        _context.Enable(_native);

        _context.RetireSpans(new[] { null, "nothex" });

        Mock.Assert(() => _native.RetireSpans(Arg.IsAny<long[]>(), Arg.AnyInt), Occurs.Never());
    }

    [Test]
    public void RetireSpans_does_nothing_when_never_enabled()
    {
        _context.RetireSpans(new[] { "1122334455667788" });

        Mock.Assert(() => _native.RetireSpans(Arg.IsAny<long[]>(), Arg.AnyInt), Occurs.Never());
    }

    [Test]
    public void RetireSpans_does_nothing_for_an_empty_or_null_batch()
    {
        _context.Enable(_native);

        _context.RetireSpans(new string[0]);
        _context.RetireSpans(null);

        Mock.Assert(() => _native.RetireSpans(Arg.IsAny<long[]>(), Arg.AnyInt), Occurs.Never());
    }

    [Test]
    public void RetireSpans_never_throws_when_native_throws()
    {
        _context.Enable(_native);
        Mock.Arrange(() => _native.RetireSpans(Arg.IsAny<long[]>(), Arg.AnyInt)).Throws<InvalidOperationException>();

        Assert.DoesNotThrow(() => _context.RetireSpans(new[] { "1122334455667788" }));
    }

    // Retirement IS suppressed after Disable, like a push. Retiring while continuous profiling is paused
    // grows registry occupancy for the whole pause with no compaction running, and those entries could
    // never reject anything anyway: the next Start() calls TraceContextMap::NewGeneration(), which
    // invalidates every slot written before it.
    [Test]
    public void RetireSpans_is_suppressed_after_Disable()
    {
        _context.Enable(_native);
        _context.Disable();

        _context.RetireSpans(new[] { "1122334455667788" });

        Mock.Assert(() => _native.RetireSpans(Arg.IsAny<long[]>(), Arg.AnyInt), Occurs.Never());
    }

    private void ArrangeResolvableCell()
    {
        Mock.Arrange(() => _native.GetPendingPushCell()).Returns(_cell);
    }

    private static void RunOnDedicatedThread(Action action)
    {
        Exception captured = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { captured = ex; }
        }) { IsBackground = true };
        thread.Start();
        Assert.That(thread.Join(TimeSpan.FromSeconds(10)), Is.True, "the dedicated worker thread must complete");
        if (captured != null)
            throw new Exception("The dedicated worker thread threw.", captured);
    }

    private static string ToHex(ByteString bytes)
    {
        var chars = new char[bytes.Length * 2];
        var i = 0;
        foreach (var b in bytes)
        {
            chars[i++] = GetHexChar(b >> 4);
            chars[i++] = GetHexChar(b & 0xF);
        }
        return new string(chars);
    }

    private static char GetHexChar(int nibble) => (char)(nibble < 10 ? '0' + nibble : 'a' + (nibble - 10));
}

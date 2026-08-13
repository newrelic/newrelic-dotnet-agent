// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
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

    [SetUp]
    public void SetUp()
    {
        _native = Mock.Create<INativeContinuousProfiler>();
        _context = new ContinuousProfilingContext();
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
    public void Disable_marks_the_context_disabled()
    {
        _context.Enable(_native);

        _context.Disable();

        Assert.That(_context.IsEnabled, Is.False);
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
            new[] { new ManagedThreadSample("t", 1, capturedHigh, capturedLow, capturedSpan, new[] { "F()" }, onCpu: false) },
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

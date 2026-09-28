// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Core.ContinuousProfiling;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.UnitTest.ContinuousProfiling;

[TestFixture]
public class NativeContinuousProfilerSampleSourceTests
{
    private INativeMethods _native;
    private NativeContinuousProfilerSampleSource _source;

    [SetUp]
    public void SetUp()
    {
        _native = Mock.Create<INativeMethods>();
        _source = new NativeContinuousProfilerSampleSource(_native);
    }

    [Test]
    public void Start_forwards_the_interval_to_ContinuousProfilerStart()
    {
        _source.Start(7500);

        Mock.Assert(() => _native.ContinuousProfilerStart(7500), Occurs.Once());
    }

    [Test]
    public void Stop_forwards_to_ContinuousProfilerStop()
    {
        _source.Stop();

        Mock.Assert(() => _native.ContinuousProfilerStop(), Occurs.Once());
    }

    [Test]
    public void Shutdown_forwards_to_ContinuousProfilerShutdown()
    {
        _source.Shutdown();

        Mock.Assert(() => _native.ContinuousProfilerShutdown(), Occurs.Once());
    }

    [Test]
    public void ReadBatch_delegates_to_ContinuousProfilerReadThreadSamples_with_buffer_length()
    {
        var buffer = new byte[128];
        Mock.Arrange(() => _native.ContinuousProfilerReadThreadSamples(buffer.Length, buffer)).Returns(42);

        var bytesRead = _source.ReadBatch(buffer);

        Assert.That(bytesRead, Is.EqualTo(42));
        Mock.Assert(() => _native.ContinuousProfilerReadThreadSamples(buffer.Length, buffer), Occurs.Once());
    }

    [Test]
    public void SetTraceContext_forwards_all_three_ids()
    {
        _source.SetTraceContext(0x11, 0x22, 0x33);

        Mock.Assert(() => _native.ContinuousProfilerSetTraceContext(0x11, 0x22, 0x33), Occurs.Once());
    }

    [Test]
    public void ResetTraceContext_forwards_to_ContinuousProfilerResetTraceContext()
    {
        _source.ResetTraceContext();

        Mock.Assert(() => _native.ContinuousProfilerResetTraceContext(), Occurs.Once());
    }

    [Test]
    public void RetireSpans_delegates_to_the_native_methods()
    {
        var spanIds = new long[] { 11, 22 };

        _source.RetireSpans(spanIds, 2);

        Mock.Assert(() => _native.ContinuousProfilerRetireSpans(spanIds, 2), Occurs.Once());
    }

    // A successful resolution hands the caller the cell address the native side wrote to the out param.
    [Test]
    public void GetPendingPushCell_returns_the_cell_the_native_methods_produced()
    {
        var produced = new IntPtr(0x1234);
        Mock.Arrange(() => _native.ContinuousProfilerGetPendingPushCell(out produced)).Returns(0);

        var cell = _source.GetPendingPushCell();

        Assert.That(cell, Is.EqualTo(new IntPtr(0x1234)));
    }

    // A non-zero status is reported as IntPtr.Zero: the caller degrades to pushing without publishing its
    // intent, which is the pre-cell behaviour, rather than dereferencing whatever the out param holds.
    [Test]
    public void GetPendingPushCell_returns_Zero_when_the_native_call_fails()
    {
        var produced = new IntPtr(0x1234);
        Mock.Arrange(() => _native.ContinuousProfilerGetPendingPushCell(out produced)).Returns(unchecked((int)0x80004005));

        var cell = _source.GetPendingPushCell();

        Assert.That(cell, Is.EqualTo(IntPtr.Zero));
    }

    [Test]
    public void SetAgentWork_forwards_to_ContinuousProfilerSetAgentWork()
    {
        _source.SetAgentWork();

        Mock.Assert(() => _native.ContinuousProfilerSetAgentWork(), Occurs.Once());
    }

    [Test]
    public void ResetAgentWork_forwards_to_ContinuousProfilerResetAgentWork()
    {
        _source.ResetAgentWork();

        Mock.Assert(() => _native.ContinuousProfilerResetAgentWork(), Occurs.Once());
    }
}

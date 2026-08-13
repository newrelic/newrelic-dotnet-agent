// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

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
}

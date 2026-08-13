// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Core.ContinuousProfiling;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.UnitTest.ContinuousProfiling;

[TestFixture]
public class NativeContinuousProfilerAllocationSampleSourceTests
{
    private INativeMethods _nativeMethods;
    private NativeContinuousProfilerAllocationSampleSource _source;

    [SetUp]
    public void SetUp()
    {
        _nativeMethods = Mock.Create<INativeMethods>();
        _source = new NativeContinuousProfilerAllocationSampleSource(_nativeMethods);
    }

    [Test]
    public void Start_DelegatesToNativeAllocationSamplerStart()
    {
        _source.Start(200);
        Mock.Assert(() => _nativeMethods.AllocationSamplerStart(200), Occurs.Once());
    }

    [Test]
    public void Stop_DelegatesToNativeAllocationSamplerStop()
    {
        _source.Stop();
        Mock.Assert(() => _nativeMethods.AllocationSamplerStop(), Occurs.Once());
    }

    [Test]
    public void Shutdown_DelegatesToNativeAllocationSamplerShutdown()
    {
        _source.Shutdown();
        Mock.Assert(() => _nativeMethods.AllocationSamplerShutdown(), Occurs.Once());
    }

    [Test]
    public void ReadBatch_DelegatesToNativeReadAllocationSamples()
    {
        var destination = new byte[1024];
        Mock.Arrange(() => _nativeMethods.ContinuousProfilerReadAllocationSamples(1024, destination)).Returns(42);

        var result = _source.ReadBatch(destination);

        Assert.That(result, Is.EqualTo(42));
    }
}

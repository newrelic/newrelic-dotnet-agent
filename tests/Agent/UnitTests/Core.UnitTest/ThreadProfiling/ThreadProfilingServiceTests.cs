// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using NewRelic.Agent.Core.ContinuousProfiling;
using NewRelic.Agent.Core.DataTransport;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.ThreadProfiling;

[TestFixture]
public class ThreadProfilingServiceTests
{
    private IDataTransportService _dataTransportService;
    private INativeMethods _nativeMethods;
    private ThreadProfilingService _threadProfilingService;

    [SetUp]
    public void SetUp()
    {
        _dataTransportService = Mock.Create<IDataTransportService>();
        _nativeMethods = Mock.Create<INativeMethods>();
        _threadProfilingService = new ThreadProfilingService(_dataTransportService, _nativeMethods);
    }

    [TearDown]
    public void TearDown()
    {
        _threadProfilingService.Dispose();
    }

    [Test]
    public void StartThreadProfilingSession_StartsNewSession_ReturnsTrue()
    {
        var profileSessionId = 1;
        uint frequencyInMsec = 100;
        uint durationInMsec = 1000;

        var result = _threadProfilingService.StartThreadProfilingSession(profileSessionId, frequencyInMsec, durationInMsec);

        Assert.That(result, Is.True);
    }

    [Test]
    public void StartThreadProfilingSession_ReturnsFalse_WhenContinuousProfilingActive()
    {
        var cpControl = Mock.Create<IContinuousProfilingSessionControl>();
        Mock.Arrange(() => cpControl.IsActive).Returns(true);
        var service = new ThreadProfilingService(_dataTransportService, _nativeMethods, continuousProfilingSessionControl: cpControl);

        var result = service.StartThreadProfilingSession(1, 100, 1000);

        Assert.That(result, Is.False);

        service.Dispose();
    }

    [Test]
    public void StartThreadProfilingSession_StartsNormally_WhenContinuousProfilingInactive()
    {
        var cpControl = Mock.Create<IContinuousProfilingSessionControl>();
        Mock.Arrange(() => cpControl.IsActive).Returns(false);
        var service = new ThreadProfilingService(_dataTransportService, _nativeMethods, continuousProfilingSessionControl: cpControl);

        var result = service.StartThreadProfilingSession(1, 100, 1000);

        Assert.That(result, Is.True);

        // Dispose deterministically joins the real sampling worker this test started, so it can't leak a
        // live worker thread into whatever test runs next.
        service.Dispose();
    }

    [Test]
    public void StartThreadProfilingSession_StartsNormally_WhenNoContinuousProfilingControl()
    {
        // The default-constructed service (no CP control wired) must behave exactly as before.
        var result = _threadProfilingService.StartThreadProfilingSession(1, 100, 1000);

        Assert.That(result, Is.True);

        // TearDown disposes _threadProfilingService, which joins the real worker this test started.
    }

    [Test]
    public void IsThreadProfilingActive_TracksSamplerRunningFlag()
    {
        var sampler = Mock.Create<IThreadProfilingSampler>();
        Mock.Arrange(() => sampler.Start(Arg.IsAny<uint>(), Arg.IsAny<uint>(), Arg.IsAny<ISampleSink>(), Arg.IsAny<INativeMethods>())).Returns(true);
        var service = new ThreadProfilingService(_dataTransportService, _nativeMethods, sampler: sampler);
        var status = (IThreadProfilingStatus)service;

        Mock.Arrange(() => sampler.IsRunning).Returns(false);
        Assert.That(status.IsThreadProfilingActive, Is.False);

        service.StartThreadProfilingSession(1, 100, 1000);
        Mock.Arrange(() => sampler.IsRunning).Returns(true);
        Assert.That(status.IsThreadProfilingActive, Is.True);

        Mock.Arrange(() => sampler.IsRunning).Returns(false);
        Assert.That(status.IsThreadProfilingActive, Is.False);

        service.Dispose();
    }

    [Test]
    public void IsThreadProfilingActive_False_WhenSamplerNotRunning_EvenWithNonZeroSessionId()
    {
        // Failure mode (a): a session started (so the reported session id is non-zero) but the sampler
        // worker is not running -- e.g. PerformAggregation threw before clearing the id, stranding it for
        // the process lifetime. IsThreadProfilingActive must follow the sampler, not the stranded id, or
        // continuous profiling's deferred-start guard would refuse to start forever.
        var sampler = Mock.Create<IThreadProfilingSampler>();
        Mock.Arrange(() => sampler.Start(Arg.IsAny<uint>(), Arg.IsAny<uint>(), Arg.IsAny<ISampleSink>(), Arg.IsAny<INativeMethods>())).Returns(true);
        Mock.Arrange(() => sampler.IsRunning).Returns(false);
        var service = new ThreadProfilingService(_dataTransportService, _nativeMethods, sampler: sampler);
        var status = (IThreadProfilingStatus)service;

        service.StartThreadProfilingSession(1, 100, 1000); // sets the non-zero reported session id

        Assert.That(status.IsThreadProfilingActive, Is.False);

        service.Dispose();
    }

    [Test]
    public void IsThreadProfilingActive_True_AfterStop_WhileSamplerWorkerStillRunning()
    {
        // Failure mode (b): a normal stop_profiler clears the reported session id immediately, but the
        // sampler worker keeps running until it winds down. IsThreadProfilingActive must stay true so
        // continuous profiling keeps deferring instead of starting concurrently in that window.
        var sampler = Mock.Create<IThreadProfilingSampler>();
        Mock.Arrange(() => sampler.Start(Arg.IsAny<uint>(), Arg.IsAny<uint>(), Arg.IsAny<ISampleSink>(), Arg.IsAny<INativeMethods>())).Returns(true);
        Mock.Arrange(() => sampler.IsRunning).Returns(true); // worker still running after the stop request
        var service = new ThreadProfilingService(_dataTransportService, _nativeMethods, sampler: sampler);
        var status = (IThreadProfilingStatus)service;

        service.StartThreadProfilingSession(1, 100, 1000);
        service.StopThreadProfilingSession(1); // clears the reported session id immediately

        Assert.That(status.IsThreadProfilingActive, Is.True);

        service.Dispose();
    }

    [Test]
    public void StopThreadProfilingSession_StopsSession_ReturnsTrue()
    {
        var profileSessionId = 1;
        uint frequencyInMsec = 100;
        uint durationInMsec = 1000;

        _threadProfilingService.StartThreadProfilingSession(profileSessionId, frequencyInMsec, durationInMsec);
        var result = _threadProfilingService.StopThreadProfilingSession(profileSessionId);

        Assert.That(result, Is.True);
    }

    [Test]
    public void StopThreadProfilingSession_WhenNotStarted_ReturnsFalse()
    {
        var result = _threadProfilingService.StopThreadProfilingSession(9999);

        Assert.That(result, Is.False);
    }


    [Test]
    public void StopThreadProfilingSession_AfterStarted_InvalidSessionId_ReturnsFalse()
    {
        var profileSessionId = 1;
        uint frequencyInMsec = 100;
        uint durationInMsec = 1000;

        _threadProfilingService.StartThreadProfilingSession(profileSessionId, frequencyInMsec, durationInMsec);

        var bogusProfileSessionId = 9999;
        var result = _threadProfilingService.StopThreadProfilingSession(bogusProfileSessionId);

        Assert.That(result, Is.False);
    }

    [Test]
    public void Dispose_WithActiveSession_StopsSampler()
    {
        // Mock sampler keeps this deterministic (no real worker thread): Dispose must route through the
        // stop path and stop the sampler, so a disposed service never orphans a live sampling worker.
        var sampler = Mock.Create<IThreadProfilingSampler>();
        Mock.Arrange(() => sampler.Start(Arg.IsAny<uint>(), Arg.IsAny<uint>(), Arg.IsAny<ISampleSink>(), Arg.IsAny<INativeMethods>())).Returns(true);
        var service = new ThreadProfilingService(_dataTransportService, _nativeMethods, sampler: sampler);

        service.StartThreadProfilingSession(1, 100, 1000);
        service.Dispose();

        Mock.Assert(() => sampler.Stop(), Occurs.Once());
    }

    [Test]
    public void Dispose_CalledTwice_StopsSamplerOnlyOnce()
    {
        // The _disposed guard makes a second Dispose a no-op -- TearDown/shutdown may dispose more than
        // once, and base.Dispose() releases subscriptions that must not be released twice.
        var sampler = Mock.Create<IThreadProfilingSampler>();
        Mock.Arrange(() => sampler.Start(Arg.IsAny<uint>(), Arg.IsAny<uint>(), Arg.IsAny<ISampleSink>(), Arg.IsAny<INativeMethods>())).Returns(true);
        var service = new ThreadProfilingService(_dataTransportService, _nativeMethods, sampler: sampler);

        service.StartThreadProfilingSession(1, 100, 1000);
        service.Dispose();
        Assert.DoesNotThrow(() => service.Dispose());

        Mock.Assert(() => sampler.Stop(), Occurs.Once());
    }

    [Test]
    public void Dispose_WithNoSession_DoesNotThrow()
    {
        // No session was ever started (no sampler), so Dispose must be a safe no-op. TearDown disposes
        // the same instance again -- the guard keeps that safe too.
        Assert.DoesNotThrow(() => _threadProfilingService.Dispose());
    }

    [Test]
    public void Stop_ShutdownPath_SuppressesReportSend()
    {
        // Stop() is the agent-shutdown path (AgentManager.StopServices). It must still stop the sampler
        // but must NOT send data -- a synchronous collector POST on shutdown could stall CLR exit.
        // SamplingComplete stands in for the worker's finally block, which is what actually reports.
        var sampler = Mock.Create<IThreadProfilingSampler>();
        Mock.Arrange(() => sampler.Start(Arg.IsAny<uint>(), Arg.IsAny<uint>(), Arg.IsAny<ISampleSink>(), Arg.IsAny<INativeMethods>())).Returns(true);
        var service = new ThreadProfilingService(_dataTransportService, _nativeMethods, sampler: sampler);

        service.StartThreadProfilingSession(1, 100, 1000);
        service.Stop();
        service.SamplingComplete();

        Mock.Assert(() => sampler.Stop(), Occurs.Once());
        Mock.Assert(() => _dataTransportService.SendThreadProfilingData(Arg.IsAny<IEnumerable<ThreadProfilingModel>>()), Occurs.Never());
    }

    [Test]
    public void StopThreadProfilingSession_CommandPath_ReportsData()
    {
        // The stop_profiler collector-command path (reportData defaults true) must still report, so the
        // shutdown suppression above does not regress the normal collector-requested stop.
        var sampler = Mock.Create<IThreadProfilingSampler>();
        Mock.Arrange(() => sampler.Start(Arg.IsAny<uint>(), Arg.IsAny<uint>(), Arg.IsAny<ISampleSink>(), Arg.IsAny<INativeMethods>())).Returns(true);
        var service = new ThreadProfilingService(_dataTransportService, _nativeMethods, sampler: sampler);

        service.StartThreadProfilingSession(1, 100, 1000);
        service.StopThreadProfilingSession(1);
        service.SamplingComplete();

        Mock.Assert(() => _dataTransportService.SendThreadProfilingData(Arg.IsAny<IEnumerable<ThreadProfilingModel>>()), Occurs.Once());

        service.Dispose();
    }

    [Test]
    public void SampleAcquired_AllErrorCode0_UpdatesTreeWithThreadSnapshots()
    {
        // Arrange
        var threadSnapshots = new[]
        {
            new ThreadSnapshot { ThreadId = (UIntPtr)1, ErrorCode = 0, FunctionIDs = new[] { (UIntPtr)1, (UIntPtr)2 } },
            new ThreadSnapshot { ThreadId = (UIntPtr)2, ErrorCode = 0, FunctionIDs = new[] { (UIntPtr)3 } }
        };

        var expectedBucketNodeCount = threadSnapshots.Sum(ts => ts.FunctionIDs.Length);

        // Act
        _threadProfilingService.SampleAcquired(threadSnapshots);

        // Assert
        Assert.That(_threadProfilingService.GetTotalBucketNodeCount(), Is.EqualTo(expectedBucketNodeCount));
    }

    [Test]
    public void SampleAcquired_NonZeroErrorCode_DoesNotUpdateTreeWithThreadSnapshots()
    {
        // Arrange
        var threadSnapshots = new[]
        {
            new ThreadSnapshot { ThreadId = (UIntPtr)1, ErrorCode = 1, FunctionIDs = new[] { (UIntPtr)1, (UIntPtr)2 } },
            new ThreadSnapshot { ThreadId = (UIntPtr)2, ErrorCode = 2, FunctionIDs = new[] { (UIntPtr)3 } },
            new ThreadSnapshot { ThreadId = (UIntPtr)2, ErrorCode = 2, FunctionIDs = new[] { (UIntPtr)3 } } // duplicate to exercise a code path in AddFailedThreadProfile()
        };

        // Act
        _threadProfilingService.SampleAcquired(threadSnapshots);

        // Assert
        Assert.That(_threadProfilingService.GetTotalBucketNodeCount(), Is.EqualTo(0));
    }

    [Test]
    public void FullCycleTest_IsSuccessful()
    {

        // Arrange
        var typeOfFidTypeMethodName = typeof(FidTypeMethodName);
        var sizeOfFidTypeMethodName = Marshal.SizeOf(typeOfFidTypeMethodName);
        var fidGizmo = new FidTypeMethodName() { FunctionID = UIntPtr.Zero, MethodName = "SomeMethod", TypeName = "SomeType" };
        IntPtr fidGizmoIntPtr = Marshal.AllocHGlobal(Marshal.SizeOf(fidGizmo) * 3);
        Marshal.StructureToPtr(fidGizmo, fidGizmoIntPtr, false);
        Marshal.StructureToPtr(fidGizmo, fidGizmoIntPtr + sizeOfFidTypeMethodName, false);
        Marshal.StructureToPtr(fidGizmo, fidGizmoIntPtr + sizeOfFidTypeMethodName * 2, false);

        Mock.Arrange(() =>
                _nativeMethods.RequestFunctionNames(Arg.IsAny<UIntPtr[]>(), Arg.AnyInt, out fidGizmoIntPtr))
            .Returns(0);

        var actualModels = new List<ThreadProfilingModel>();
        Mock.Arrange(() =>
                _dataTransportService.SendThreadProfilingData(Arg.IsAny<IEnumerable<ThreadProfilingModel>>()))
            .DoInstead((IEnumerable<ThreadProfilingModel> models) =>
            {
                actualModels.AddRange(models);
            });

        var threadSnapshots = new[]
        {
            new ThreadSnapshot { ThreadId = (UIntPtr)1, ErrorCode = 0, FunctionIDs = new[] { (UIntPtr)1, (UIntPtr)2 } },
            new ThreadSnapshot { ThreadId = (UIntPtr)2, ErrorCode = 0, FunctionIDs = new[] { (UIntPtr)3 } }
        };

        // Act
        _threadProfilingService.Start();
        _threadProfilingService.StartThreadProfilingSession(1, 60000, 120000);
        _threadProfilingService.SampleAcquired(threadSnapshots);
        // The stop_profiler collector-command path (reportData defaults true) signals and joins the
        // sampling worker, whose finally block performs the single aggregation/send via SamplingComplete
        // -- so we must not call SamplingComplete manually here or the data would be aggregated twice.
        // (Note: Stop(), the agent-shutdown path, suppresses the send and is covered separately.)
        _threadProfilingService.StopThreadProfilingSession(1);

        // Assert
        Mock.Assert(() => _dataTransportService.SendThreadProfilingData(Arg.IsAny<IEnumerable<ThreadProfilingModel>>()), Occurs.Once());
        Assert.That(actualModels, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(actualModels[0].TotalThreadCount, Is.EqualTo(2));
            Assert.That(actualModels[0].NumberOfSamples, Is.EqualTo(1));
            Assert.That((actualModels[0].Samples["OTHER"] as ProfileNodes), Is.Empty);
        });

        // Teardown
        Marshal.FreeHGlobal(fidGizmoIntPtr);
    }

    [Test]
    public void PerformAggregation_HandlesException()
    {
        Mock.Arrange(() => _dataTransportService.SendThreadProfilingData(Arg.IsAny<IEnumerable<ThreadProfilingModel>>()))
            .Throws(new Exception("Test Exception", new Exception("Test Inner Exception")));

        try
        {
            _threadProfilingService.PerformAggregation();
        }
        catch
        {
            Assert.Fail("Exception was not handled");
        }
    }


    [Test]
    public void AddNodeToPruningList_AddsNodeToPruningList()
    {
        // Arrange
        var node = new ProfileNode((UIntPtr)1, 0, 0);

        // Act
        _threadProfilingService.AddNodeToPruningList(node);

        // Assert
        var actualCount = _threadProfilingService.PruningList.Count;
        Assert.Multiple(() =>
        {
            Assert.That(actualCount, Is.EqualTo(1));
            Assert.That(_threadProfilingService.PruningList[0], Is.EqualTo(node));
        });
    }

    [Test]
    public void SortPruningTree_DoesNothing_WhenMaxAggregatedNodesIsNotExceeded()
    {
        // Arrange
        var node1 = new ProfileNode((UIntPtr)1, 5, 1);
        var node2 = new ProfileNode((UIntPtr)2, 10, 0);
        var node3 = new ProfileNode((UIntPtr)3, 5, 0);

        _threadProfilingService.AddNodeToPruningList(node1);
        _threadProfilingService.AddNodeToPruningList(node2);
        _threadProfilingService.AddNodeToPruningList(node3);

        // Act
        _threadProfilingService.SortPruningTree();

        // Assert
        var pruningList = _threadProfilingService.PruningList;
        Assert.That(pruningList, Has.Count.EqualTo(3));
        Assert.Multiple(() =>
        {
            Assert.That(pruningList[0], Is.EqualTo(node1));
            Assert.That(pruningList[1], Is.EqualTo(node2));
            Assert.That(pruningList[2], Is.EqualTo(node3));
        });
    }

    [Test]
    public void SortPruningTree_SortsPruningListBasedOnRunnableCountAndDepth()
    {
        // Arrange
        var node1 = new ProfileNode((UIntPtr)1, 5, 1);
        var node2 = new ProfileNode((UIntPtr)2, 10, 0);
        var node3 = new ProfileNode((UIntPtr)3, 5, 0);

        var threadProfilingService = new ThreadProfilingService(_dataTransportService, _nativeMethods, maxAggregatedNodes: 1);

        threadProfilingService.AddNodeToPruningList(node1);
        threadProfilingService.AddNodeToPruningList(node2);
        threadProfilingService.AddNodeToPruningList(node3);

        // Act
        threadProfilingService.SortPruningTree();

        // Assert
        var pruningList = threadProfilingService.PruningList;
        Assert.That(pruningList, Has.Count.EqualTo(3));
        Assert.Multiple(() =>
        {
            Assert.That(pruningList[0], Is.EqualTo(node2));
            Assert.That(pruningList[1], Is.EqualTo(node3));
            Assert.That(pruningList[2], Is.EqualTo(node1));
        });
    }

    [Test]
    public void ResetCache_ClearsPruningList()
    {
        // Arrange
        var node1 = new ProfileNode((UIntPtr)1, 5, 1);
        var node2 = new ProfileNode((UIntPtr)2, 10, 0);
        var node3 = new ProfileNode((UIntPtr)3, 5, 0);

        _threadProfilingService.AddNodeToPruningList(node1);
        _threadProfilingService.AddNodeToPruningList(node2);
        _threadProfilingService.AddNodeToPruningList(node3);

        // Act
        _threadProfilingService.ResetCache();

        // Assert
        Assert.That(_threadProfilingService.PruningList, Is.Empty);
    }
}
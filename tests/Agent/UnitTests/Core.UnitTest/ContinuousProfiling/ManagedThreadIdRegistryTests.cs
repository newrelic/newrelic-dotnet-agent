// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Runtime.CompilerServices;
using System.Threading;
using NewRelic.Agent.Core.ContinuousProfiling;
using NewRelic.Agent.Core.Utilities;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.UnitTest.ContinuousProfiling;

[TestFixture]
public class ManagedThreadIdRegistryTests
{
    private ICurrentOsThreadIdProvider _osThreadIdProvider;
    private ManagedThreadIdRegistry _registry;

    [SetUp]
    public void SetUp()
    {
        _osThreadIdProvider = Mock.Create<ICurrentOsThreadIdProvider>();
        _registry = new ManagedThreadIdRegistry(_osThreadIdProvider);
    }

    [Test]
    public void EnsureRegistered_RecordsCurrentThreadsOsTidAndManagedId()
    {
        Mock.Arrange(() => _osThreadIdProvider.GetCurrentOsThreadId()).Returns(4242L);

        _registry.EnsureRegistered();

        var found = _registry.TryGetManagedThreadId(4242L, out var managedThreadId);

        Assert.That(found, Is.True);
        Assert.That(managedThreadId, Is.EqualTo(Thread.CurrentThread.ManagedThreadId));
    }

    [Test]
    public void TryGetManagedThreadId_ReturnsFalse_WhenOsTidNeverRegistered()
    {
        var found = _registry.TryGetManagedThreadId(999999L, out var managedThreadId);

        Assert.That(found, Is.False);
        Assert.That(managedThreadId, Is.EqualTo(0));
    }

    [Test]
    public void EnsureRegistered_OnlyReadsOsThreadIdOnceEvenIfCalledManyTimesOnSameThread()
    {
        Mock.Arrange(() => _osThreadIdProvider.GetCurrentOsThreadId()).Returns(4242L);

        _registry.EnsureRegistered();
        _registry.EnsureRegistered();
        _registry.EnsureRegistered();

        Mock.Assert(() => _osThreadIdProvider.GetCurrentOsThreadId(), Occurs.Once());
    }

    [Test]
    public void EnsureRegistered_RegistersMultipleThreads_OnSameRegistry()
    {
        // Use a fresh mock and registry (rather than the SetUp-provided _registry) so this test's
        // multi-thread registrations are isolated from any other test's registry instance
        var mockProvider = Mock.Create<ICurrentOsThreadIdProvider>();
        var sharedRegistry = new ManagedThreadIdRegistry(mockProvider);

        var mainThreadId = Thread.CurrentThread.ManagedThreadId;
        var mainThreadOsTid = 1111L;
        var secondThreadOsTid = 2222L;
        var secondThreadManagedId = 0;

        // Main thread: arrange mock to return its OS TID
        Mock.Arrange(() => mockProvider.GetCurrentOsThreadId()).Returns(mainThreadOsTid);

        // Register main thread
        sharedRegistry.EnsureRegistered();

        // Verify main thread is registered
        var foundMain = sharedRegistry.TryGetManagedThreadId(mainThreadOsTid, out var mainThreadManagedId);
        Assert.That(foundMain, Is.True);
        Assert.That(mainThreadManagedId, Is.EqualTo(mainThreadId));

        // Create second thread that calls EnsureRegistered on the same registry with a different OS TID
        var secondThread = new Thread(() =>
        {
            secondThreadManagedId = Thread.CurrentThread.ManagedThreadId;
            // Arrange for this thread's call to return the second thread's OS TID
            Mock.Arrange(() => mockProvider.GetCurrentOsThreadId()).Returns(secondThreadOsTid);

            // Call EnsureRegistered on the SAME shared registry instance
            sharedRegistry.EnsureRegistered();
        });

        secondThread.Start();
        secondThread.Join();

        // Verify both threads are now registered with their respective OS TIDs
        var foundSecond = sharedRegistry.TryGetManagedThreadId(secondThreadOsTid, out var secondThreadManagedIdRetrieved);
        Assert.That(foundSecond, Is.True);
        Assert.That(secondThreadManagedIdRetrieved, Is.EqualTo(secondThreadManagedId));

        // Verify main thread still resolves correctly
        var foundMainAgain = sharedRegistry.TryGetManagedThreadId(mainThreadOsTid, out var mainManagedIdAgain);
        Assert.That(foundMainAgain, Is.True);
        Assert.That(mainManagedIdAgain, Is.EqualTo(mainThreadId));
    }

    [Test]
    public void TryGetManagedThreadId_ReturnsFalse_WhenRegisteringThreadHasBeenCollected()
    {
        var mockProvider = Mock.Create<ICurrentOsThreadIdProvider>();
        var registry = new ManagedThreadIdRegistry(mockProvider);
        const long recycledOsTid = 5555L;
        Mock.Arrange(() => mockProvider.GetCurrentOsThreadId()).Returns(recycledOsTid);

        RegisterOnBackgroundThreadThenLetItGoOutOfScope(registry);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var found = registry.TryGetManagedThreadId(recycledOsTid, out var managedThreadId);

        Assert.That(found, Is.False);
        Assert.That(managedThreadId, Is.EqualTo(0));
    }

    // Isolated into its own (non-inlinable) method so the Thread object has no surviving root once this
    // method returns -- in Debug builds, a local kept in the calling test method's own frame can stay
    // reachable for the rest of that frame's lifetime regardless of being set to null, which would make
    // the GC below unable to collect it.
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    private static void RegisterOnBackgroundThreadThenLetItGoOutOfScope(ManagedThreadIdRegistry registry)
    {
        var registeringThread = new Thread(() => registry.EnsureRegistered());
        registeringThread.Start();
        registeringThread.Join();
    }
}

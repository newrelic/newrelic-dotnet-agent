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

    [Test]
    public void EnsureRegistered_TwoRegistryInstances_BothRegisterIndependently_OnSameThread()
    {
        // Pins the property that ruled out a bare [ThreadStatic] bool gate: two different registry
        // instances calling EnsureRegistered on the SAME physical thread must both register, not have
        // the second call find the first instance's gate and wrongly no-op.
        var providerA = Mock.Create<ICurrentOsThreadIdProvider>();
        var providerB = Mock.Create<ICurrentOsThreadIdProvider>();
        var registryA = new ManagedThreadIdRegistry(providerA);
        var registryB = new ManagedThreadIdRegistry(providerB);
        Mock.Arrange(() => providerA.GetCurrentOsThreadId()).Returns(7001L);
        Mock.Arrange(() => providerB.GetCurrentOsThreadId()).Returns(7002L);

        registryA.EnsureRegistered();
        registryB.EnsureRegistered();

        var foundA = registryA.TryGetManagedThreadId(7001L, out var managedIdA);
        var foundB = registryB.TryGetManagedThreadId(7002L, out var managedIdB);

        Assert.Multiple(() =>
        {
            Assert.That(foundA, Is.True);
            Assert.That(managedIdA, Is.EqualTo(Thread.CurrentThread.ManagedThreadId));
            Assert.That(foundB, Is.True);
            Assert.That(managedIdB, Is.EqualTo(Thread.CurrentThread.ManagedThreadId));
        });
    }

    [Test]
    public void EnsureRegistered_NewThreadReusingARecycledManagedThreadId_RegistersItsOwnMapping()
    {
        // Regression test for the bug tippmar-nr flagged on PR #3828: a ManagedThreadId-keyed gate
        // finds a dead thread's entry already set when a new thread reuses that id, and silently skips
        // registering its own OS TID -- a permanent miss for the life of the process. The [ThreadStatic]
        // gate must NOT reproduce that: a new thread always gets fresh per-thread storage regardless of
        // which ManagedThreadId the CLR handed it.
        var mockProvider = Mock.Create<ICurrentOsThreadIdProvider>();
        var registry = new ManagedThreadIdRegistry(mockProvider);
        const long firstThreadOsTid = 6001L;
        const long secondThreadOsTid = 6002L;

        Mock.Arrange(() => mockProvider.GetCurrentOsThreadId()).Returns(firstThreadOsTid);
        var firstThreadManagedId = RegisterOnBackgroundThreadAndReturnManagedId(registry);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        // Precondition: the dead thread's own mapping must already be unresolvable (proves the miss
        // below is caused by id reuse hitting the gate, not by the first mapping still being alive).
        Assert.That(registry.TryGetManagedThreadId(firstThreadOsTid, out _), Is.False);

        // The CLR reuses a freed ManagedThreadId opportunistically, not deterministically -- spin up
        // threads until one lands on the recycled id, bounded so the test can't hang if it never does.
        const int maxAttempts = 2000;
        var reusedId = false;
        for (var attempt = 0; attempt < maxAttempts && !reusedId; attempt++)
        {
            reusedId = TryRegisterOnNewThreadIfItReusesManagedId(registry, mockProvider, firstThreadManagedId, secondThreadOsTid);
        }

        if (!reusedId)
        {
            Assert.Inconclusive($"CLR did not reuse managed thread id {firstThreadManagedId} within {maxAttempts} attempts; cannot exercise the reuse path on this run.");
        }

        var found = registry.TryGetManagedThreadId(secondThreadOsTid, out var resolvedManagedId);

        Assert.Multiple(() =>
        {
            Assert.That(found, Is.True);
            Assert.That(resolvedManagedId, Is.EqualTo(firstThreadManagedId));
        });
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    private static int RegisterOnBackgroundThreadAndReturnManagedId(ManagedThreadIdRegistry registry)
    {
        var managedId = 0;
        var registeringThread = new Thread(() =>
        {
            managedId = Thread.CurrentThread.ManagedThreadId;
            registry.EnsureRegistered();
        });
        registeringThread.Start();
        registeringThread.Join();
        return managedId;
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    private static bool TryRegisterOnNewThreadIfItReusesManagedId(ManagedThreadIdRegistry registry, ICurrentOsThreadIdProvider mockProvider, int targetManagedId, long osTidToRegisterWith)
    {
        var matched = false;
        var thread = new Thread(() =>
        {
            if (Thread.CurrentThread.ManagedThreadId == targetManagedId)
            {
                Mock.Arrange(() => mockProvider.GetCurrentOsThreadId()).Returns(osTidToRegisterWith);
                registry.EnsureRegistered();
                matched = true;
            }
        });
        thread.Start();
        thread.Join();
        return matched;
    }
}

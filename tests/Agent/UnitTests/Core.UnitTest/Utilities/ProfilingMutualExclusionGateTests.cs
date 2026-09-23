// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace NewRelic.Agent.Core.Utilities;

[TestFixture]
public class ProfilingMutualExclusionGateTests
{
    [Test]
    public void Acquire_ProvidesMutualExclusion()
    {
        Task innerTask;

        using (ProfilingMutualExclusionGate.Acquire())
        {
            innerTask = Task.Run(() =>
            {
                using (ProfilingMutualExclusionGate.Acquire())
                {
                }
            });

            var completedWhileHeld = innerTask.Wait(200);
            Assert.That(completedWhileHeld, Is.False, "A second Acquire() must block while the first is held.");
        }

        Assert.That(innerTask.Wait(5000), Is.True, "The second Acquire() must complete once the first is released.");
    }

    [Test]
    public void Dispose_IsIdempotent()
    {
        var releaser = ProfilingMutualExclusionGate.Acquire();

        Assert.DoesNotThrow(() =>
        {
            releaser.Dispose();
            releaser.Dispose();
        });
    }

    [Test]
    public void Acquire_ReleasesOnDispose_AllowingReacquisition()
    {
        using (ProfilingMutualExclusionGate.Acquire())
        {
        }

        // If the first Acquire() failed to release, this would hang/timeout.
        var reacquired = Task.Run(() =>
        {
            using (ProfilingMutualExclusionGate.Acquire())
            {
            }
        }).Wait(5000);

        Assert.That(reacquired, Is.True);
    }

    // Cluster M gap (a): Releaser.Dispose() from a thread that never called Acquire().
    //
    // This is unsupported usage -- every real call site is a same-thread `using` -- so the documented,
    // intentional behavior is: the stray cross-thread Dispose throws SynchronizationLockException (Monitor
    // is thread-affine) rather than silently succeeding. Critically, it must NOT strand the gate: the
    // owning thread's own Dispose must still release the lock afterward.
    //
    // This is also the concrete regression proof for the orphan-prevention invariant. Literal OOM injection
    // between Monitor.Enter and the Releaser allocation isn't practical to force in a unit test, but the
    // *release-path* half of the same invariant is: with the pre-fix Dispose ordering
    // (`_released = true; Monitor.Exit(...)`), the wrong-thread Dispose flipped _released to true before the
    // throw, so the owner's later Dispose no-opped and the lock was held forever -- this test would then
    // hang at the re-acquire below. It passes only because Dispose now sets _released ONLY after a
    // successful Monitor.Exit.
    [Test]
    public void Dispose_FromNonAcquiringThread_ThrowsAndDoesNotOrphanTheGate()
    {
        var releaser = ProfilingMutualExclusionGate.Acquire();

        Exception caught = null;
        var wrongThread = new Thread(() =>
        {
            try
            {
                releaser.Dispose();
            }
            catch (Exception ex)
            {
                caught = ex;
            }
        });
        wrongThread.Start();
        Assert.That(wrongThread.Join(5000), Is.True, "The cross-thread Dispose attempt should complete promptly.");

        Assert.That(caught, Is.TypeOf<SynchronizationLockException>(),
            "Disposing from a thread that never Acquired must throw SynchronizationLockException, not silently succeed.");

        // The stray cross-thread Dispose must not have stranded the gate: the owning thread can still release it.
        Assert.DoesNotThrow(() => releaser.Dispose(),
            "The owning thread's Dispose must still release the lock after a failed cross-thread Dispose.");

        // Proof the lock is genuinely free again -- would hang/time out if the gate had been orphaned.
        var reacquired = Task.Run(() =>
        {
            using (ProfilingMutualExclusionGate.Acquire())
            {
            }
        }).Wait(5000);

        Assert.That(reacquired, Is.True, "The gate must be re-acquirable, proving it was never orphaned.");
    }

    // Cluster M gap (b): Monitor is reentrant, so nested Acquire() calls on ONE thread do NOT provide
    // mutual exclusion against each other -- the second Acquire returns immediately rather than blocking.
    // This test makes that behavior explicit and intentional. The gate's exclusion guarantee is strictly
    // cross-thread (proven by Acquire_ProvidesMutualExclusion); it is NOT a same-thread guard, and callers
    // must never rely on it to detect re-entrancy within a single thread.
    [Test]
    public void Acquire_IsReentrantOnSameThread_ProvidingNoNestedExclusion()
    {
        var nestedAcquireReturned = false;

        using (ProfilingMutualExclusionGate.Acquire())
        {
            // Same-thread nested Acquire: Monitor's reentrancy means this returns immediately instead of
            // deadlocking against the outer hold. If the gate serialized per-call (it does not), this
            // would deadlock the test thread forever.
            using (ProfilingMutualExclusionGate.Acquire())
            {
                nestedAcquireReturned = true;
            }
        }

        Assert.That(nestedAcquireReturned, Is.True,
            "Nested Acquire() on the same thread must return without blocking -- Monitor is reentrant, so the gate provides no same-thread exclusion.");
    }
}

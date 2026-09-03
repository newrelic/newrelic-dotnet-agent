// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

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
}

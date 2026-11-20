// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Threading.Tasks;
using NUnit.Framework;

namespace NewRelic.Agent.Core.OtelDistro;

[TestFixture]
public class DistroResolutionGuardTests
{
    [Test]
    public void RunOnce_NotInProgress_ReturnsTheResult()
    {
        var result = DistroResolutionGuard.RunOnce(() => "resolved");

        Assert.That(result, Is.EqualTo("resolved"));
    }

    [Test]
    public void RunOnce_ReentrantCall_ReturnsNullWithoutRunningAndOuterStillReturnsItsResult()
    {
        var innerRan = false;
        string inner = "unset";

        var outer = DistroResolutionGuard.RunOnce(() =>
        {
            inner = DistroResolutionGuard.RunOnce(() =>
            {
                innerRan = true;
                return "inner";
            });
            return "outer";
        });

        Assert.Multiple(() =>
        {
            Assert.That(inner, Is.Null);
            Assert.That(innerRan, Is.False);
            Assert.That(outer, Is.EqualTo("outer"));
        });
    }

    [Test]
    public void RunOnce_AfterCompletion_RunsAgain()
    {
        DistroResolutionGuard.RunOnce(() => "first");

        Assert.That(DistroResolutionGuard.RunOnce(() => "second"), Is.EqualTo("second"));
    }

    [Test]
    public void RunOnce_ResolveThrows_PropagatesAndClearsTheMark()
    {
        Assert.Throws<InvalidOperationException>(() => DistroResolutionGuard.RunOnce<string>(() => throw new InvalidOperationException("boom")));

        Assert.That(DistroResolutionGuard.RunOnce(() => "after"), Is.EqualTo("after"));
    }

    [Test]
    public void RunOnce_InProgressOnOneThread_DoesNotBlockAnotherThread()
    {
        string other = null;

        DistroResolutionGuard.RunOnce(() =>
        {
            other = Task.Run(() => DistroResolutionGuard.RunOnce(() => "other thread")).GetAwaiter().GetResult();
            return "outer";
        });

        Assert.That(other, Is.EqualTo("other thread"));
    }
}

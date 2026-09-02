// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Threading;
using NewRelic.Agent.Core.ContinuousProfiling;
using NewRelic.Agent.Core.Fixtures;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.Time;

[TestFixture]
public class SchedulerTests
{
    private Scheduler _scheduler;
    private IContinuousProfilingContext _originalContext;

    [SetUp]
    public void SetUp()
    {
        _scheduler = new Scheduler();
        _originalContext = ContinuousProfilingContext.Instance;
    }

    [TearDown]
    public void TearDown()
    {
        _scheduler.Dispose();
        // ContinuousProfilingContext.Instance is process-wide static state -- always restore it so a
        // mock swapped in by one test can never leak into an unrelated test elsewhere.
        ContinuousProfilingContext.Instance = _originalContext;
    }

    [Test]
    public void ExecuteOnce_ExecutesTheGivenAction()
    {
        var wasExecuted = false;

        _scheduler.ExecuteOnce(() => wasExecuted = true, TimeSpan.FromMilliseconds(1));

        AssertEventuallyTrue(() => wasExecuted);
    }

    [Test]
    public void ExecuteOnce_LogsExceptions()
    {
        using (var logging = new TestUtilities.Logging())
        {
            _scheduler.ExecuteOnce(() =>
            {
                throw new Exception();
            }, TimeSpan.FromMilliseconds(1));
            AssertEventuallyTrue(() => logging.ErrorCount == 1);
        }
    }

    [Test]
    public void ExecuteEvery_ExecutesTheGivenAction()
    {
        var wasExecuted = false;

        _scheduler.ExecuteEvery(() => wasExecuted = true, TimeSpan.FromMilliseconds(1));

        AssertEventuallyTrue(() => wasExecuted);
    }

    [Test]
    public void ExecuteEvery_ExecutesTheGivenActionMultipleTimes()
    {
        var wasExecuted = false;

        _scheduler.ExecuteEvery(() => wasExecuted = true, TimeSpan.FromMilliseconds(1));
        AssertEventuallyTrue(() => wasExecuted);
        wasExecuted = false;

        AssertEventuallyTrue(() => wasExecuted);
    }

    [Test]
    public void ExecuteEvery_LogsExceptions()
    {
        using (var logging = new TestUtilities.Logging())
        {
            _scheduler.ExecuteEvery(() =>
            {
                throw new Exception();
            }, TimeSpan.FromMilliseconds(1));

            AssertEventuallyTrue(() => logging.ErrorCount >= 1);

        }
    }

    [Test]
    public void StopExecuting_StopsTheGivenActionFromExecutingAgain()
    {
        var wasExecuted = false;
        Action setWasExecuted = () => wasExecuted = true;

        _scheduler.ExecuteEvery(setWasExecuted, TimeSpan.FromMilliseconds(1));
        AssertEventuallyTrue(() => wasExecuted);

        _scheduler.StopExecuting(setWasExecuted);
        Thread.Sleep(TimeSpan.FromMilliseconds(5));
        wasExecuted = false;
        Thread.Sleep(TimeSpan.FromMilliseconds(5));

        Assert.That(wasExecuted, Is.False);
    }

    [Test]
    public void StopExecuting_DoesNotStopOtherActions()
    {
        var wasExecuted = false;

        _scheduler.ExecuteEvery(() => wasExecuted = true, TimeSpan.FromMilliseconds(1));
        _scheduler.StopExecuting(() => { });
        wasExecuted = false;

        AssertEventuallyTrue(() => wasExecuted);
    }

    [Test]
    public void ExecuteOnce_SetsAndResetsAgentWorkAroundTheAction()
    {
        var mockContext = Mock.Create<IContinuousProfilingContext>();
        ContinuousProfilingContext.Instance = mockContext;
        var wasExecuted = false;

        _scheduler.ExecuteOnce(() => wasExecuted = true, TimeSpan.FromMilliseconds(1));

        AssertEventuallyTrue(() => wasExecuted);
        Mock.Assert(() => mockContext.SetAgentWork(), Occurs.Once());
        Mock.Assert(() => mockContext.ResetAgentWork(), Occurs.Once());
    }

    [Test]
    public void ExecuteOnce_ResetsAgentWorkEvenWhenTheActionThrows()
    {
        var mockContext = Mock.Create<IContinuousProfilingContext>();
        ContinuousProfilingContext.Instance = mockContext;

        using (var logging = new TestUtilities.Logging())
        {
            _scheduler.ExecuteOnce(() => throw new Exception(), TimeSpan.FromMilliseconds(1));
            AssertEventuallyTrue(() => logging.ErrorCount == 1);
        }

        Mock.Assert(() => mockContext.ResetAgentWork(), Occurs.Once());
    }

    [Test]
    public void ExecuteOnce_DoesNotTrackAgentWork_WhenTrackAsAgentWorkIsFalse()
    {
        var mockContext = Mock.Create<IContinuousProfilingContext>();
        ContinuousProfilingContext.Instance = mockContext;
        var wasExecuted = false;

        _scheduler.ExecuteOnce(() => wasExecuted = true, TimeSpan.FromMilliseconds(1), trackAsAgentWork: false);

        AssertEventuallyTrue(() => wasExecuted);
        Mock.Assert(() => mockContext.SetAgentWork(), Occurs.Never());
        Mock.Assert(() => mockContext.ResetAgentWork(), Occurs.Never());
    }

    [Test]
    public void ExecuteEvery_SetsAndResetsAgentWorkAroundEachExecution()
    {
        var mockContext = Mock.Create<IContinuousProfilingContext>();
        ContinuousProfilingContext.Instance = mockContext;
        var wasExecuted = false;

        _scheduler.ExecuteEvery(() => wasExecuted = true, TimeSpan.FromMilliseconds(1));

        AssertEventuallyTrue(() => wasExecuted);
        Mock.Assert(() => mockContext.SetAgentWork(), Occurs.AtLeastOnce());
        Mock.Assert(() => mockContext.ResetAgentWork(), Occurs.AtLeastOnce());
    }

    [Test]
    public void ExecuteEvery_ResetsAgentWorkEvenWhenTheActionThrows()
    {
        var mockContext = Mock.Create<IContinuousProfilingContext>();
        ContinuousProfilingContext.Instance = mockContext;

        using (var logging = new TestUtilities.Logging())
        {
            _scheduler.ExecuteEvery(() => throw new Exception(), TimeSpan.FromMilliseconds(1));
            AssertEventuallyTrue(() => logging.ErrorCount >= 1);
        }

        Mock.Assert(() => mockContext.ResetAgentWork(), Occurs.AtLeastOnce());
    }

    [Test]
    public void ExecuteEvery_DoesNotTrackAgentWork_WhenTrackAsAgentWorkIsFalse()
    {
        var mockContext = Mock.Create<IContinuousProfilingContext>();
        ContinuousProfilingContext.Instance = mockContext;
        var wasExecuted = false;

        _scheduler.ExecuteEvery(() => wasExecuted = true, TimeSpan.FromMilliseconds(1), trackAsAgentWork: false);

        AssertEventuallyTrue(() => wasExecuted);
        Mock.Assert(() => mockContext.SetAgentWork(), Occurs.Never());
        Mock.Assert(() => mockContext.ResetAgentWork(), Occurs.Never());
    }

    // Regression: SetAgentWork/ResetAgentWork are the two halves of a native per-thread nesting-DEPTH
    // counter and must be paired against the SAME context instance. The callback previously read the
    // static Instance separately for each half, so a continuous-profiling stop/retune landing mid-callback
    // (which swaps Instance for a fresh inert instance) sent the set to one object and the reset to
    // another -- permanently pinning that ThreadPool thread's native slot at depth >= 1.
    [Test]
    public void ExecuteOnce_ResetsAgentWorkOnTheContextCapturedAtCallbackStart_EvenIfInstanceIsSwappedMidCallback()
    {
        var capturedContext = Mock.Create<IContinuousProfilingContext>();
        var replacementContext = Mock.Create<IContinuousProfilingContext>();
        ContinuousProfilingContext.Instance = capturedContext;
        var wasExecuted = false;

        _scheduler.ExecuteOnce(() =>
        {
            // Simulate ContinuousProfilingService.StopLocked republishing Instance while this callback
            // (which has already called SetAgentWork) is in flight.
            ContinuousProfilingContext.Instance = replacementContext;
            wasExecuted = true;
        }, TimeSpan.FromMilliseconds(1));

        AssertEventuallyTrue(() => wasExecuted);

        Mock.Assert(() => capturedContext.SetAgentWork(), Occurs.Once());
        // The pairing reset must land on the context that took the set...
        Mock.Assert(() => capturedContext.ResetAgentWork(), Occurs.Once());
        // ...and never on the instance published after the set.
        Mock.Assert(() => replacementContext.SetAgentWork(), Occurs.Never());
        Mock.Assert(() => replacementContext.ResetAgentWork(), Occurs.Never());
    }

    [Test]
    public void ExecuteEvery_ResetsAgentWorkOnTheContextCapturedAtCallbackStart_EvenIfInstanceIsSwappedMidCallback()
    {
        // Count both halves per instance: with a recurring timer, later ticks legitimately capture the
        // replacement instance, so the invariant that actually matters is that NO instance ever saw an
        // unpaired half -- set count == reset count on each, which is exactly what the native depth
        // counter requires.
        var capturedContext = Mock.Create<IContinuousProfilingContext>();
        var replacementContext = Mock.Create<IContinuousProfilingContext>();
        var capturedSets = 0;
        var capturedResets = 0;
        var replacementSets = 0;
        var replacementResets = 0;
        Mock.Arrange(() => capturedContext.SetAgentWork()).DoInstead(() => Interlocked.Increment(ref capturedSets));
        Mock.Arrange(() => capturedContext.ResetAgentWork()).DoInstead(() => Interlocked.Increment(ref capturedResets));
        Mock.Arrange(() => replacementContext.SetAgentWork()).DoInstead(() => Interlocked.Increment(ref replacementSets));
        Mock.Arrange(() => replacementContext.ResetAgentWork()).DoInstead(() => Interlocked.Increment(ref replacementResets));

        ContinuousProfilingContext.Instance = capturedContext;
        var wasExecuted = false;

        Action action = () =>
        {
            ContinuousProfilingContext.Instance = replacementContext;
            wasExecuted = true;
        };

        _scheduler.ExecuteEvery(action, TimeSpan.FromMilliseconds(1));
        AssertEventuallyTrue(() => wasExecuted);

        // Wait for any in-flight callback to finish so no pair is mid-execution when we compare counts.
        _scheduler.StopExecuting(action, TimeSpan.FromSeconds(5));

        Assert.Multiple(() =>
        {
            Assert.That(capturedSets, Is.GreaterThanOrEqualTo(1), "the first tick must have taken a set on the originally published instance");
            Assert.That(capturedResets, Is.EqualTo(capturedSets), "every set on the captured context must be paired by a reset on that same context");
            Assert.That(replacementResets, Is.EqualTo(replacementSets), "every set on the replacement context must be paired by a reset on that same context");
        });
    }

    private static void AssertEventuallyTrue(Func<bool> wasExecutedFunc)
    {
        Assertions.Eventually(wasExecutedFunc, TimeSpan.FromSeconds(5));
    }
}
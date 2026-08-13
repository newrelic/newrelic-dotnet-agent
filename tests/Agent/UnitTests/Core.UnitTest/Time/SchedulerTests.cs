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

    private static void AssertEventuallyTrue(Func<bool> wasExecutedFunc)
    {
        Assertions.Eventually(wasExecutedFunc, TimeSpan.FromSeconds(5));
    }
}
// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NUnit.Framework;
using Telerik.JustMock;

namespace Agent.Extensions.Tests;

public class DelegatesTests
{
    #region OnSuccess

    [Test]
    public void GetDelegateFor_RunsOnSuccess_IfNoException()
    {
        // Arrange
        var called = false;

        // Act
        var myDelegate = Delegates.GetDelegateFor(onSuccess: () => called = true);
        myDelegate(result: null, exception: null);

        Assert.That(called, Is.True);
    }

    [Test]
    public void GetDelegateFor_RunsOnSuccessWithValue_IfNoException()
    {
        // Arrange
        const string expectedValue = "expectedValue";
        var passedValue = null as string;

        // Act
        var myDelegate = Delegates.GetDelegateFor<string>(onSuccess: value => passedValue = value);
        myDelegate(result: expectedValue, exception: null);

        Assert.That(passedValue, Is.EqualTo(expectedValue));
    }

    [Test]
    public void GetDelegateFor_DoesNotRunOnSuccess_IfResultIsOfWrongType()
    {
        // Arrange
        var called = false;

        // Act
        var myDelegate = Delegates.GetDelegateFor<string>(onSuccess: _ => called = true);
        myDelegate(result: 42, exception: null);

        Assert.That(called, Is.False);
    }

    [Test]
    public void GetDelegateFor_DoesNotRunOnSuccess_IfException()
    {
        // Arrange
        var called = false;

        // Act
        var myDelegate = Delegates.GetDelegateFor(onSuccess: () => called = true);
        myDelegate(result: null, exception: new Exception());

        Assert.That(called, Is.False);
    }

    #endregion OnSuccess

    #region OnFailure

    [Test]
    public void GetDelegateFor_RunsOnFailure_IfException()
    {
        // Arrange
        var expectedException = new Exception();
        var passedException = null as Exception;

        // Act
        var myDelegate = Delegates.GetDelegateFor(onFailure: ex => passedException = ex);
        myDelegate(result: null, exception: expectedException);

        Assert.That(passedException, Is.EqualTo(expectedException));
    }

    [Test]
    public void GetDelegateFor_DoesNotRunOnFailure_IfNoException()
    {
        // Arrange
        var called = false;

        // Act
        var myDelegate = Delegates.GetDelegateFor(onFailure: _ => called = true);
        myDelegate(result: null, exception: null);

        Assert.That(called, Is.False);
    }

    #endregion OnFailure

    #region OnComplete

    [Test]
    public void GetDelegateFor_RunsOnComplete_IfNoException()
    {
        // Arrange
        var called = false;

        // Act
        var myDelegate = Delegates.GetDelegateFor(onComplete: () => called = true);
        myDelegate(result: null, exception: null);

        Assert.That(called, Is.True);
    }

    [Test]
    public void GetDelegateFor_RunsOnComplete_IfException()
    {
        // Arrange
        var called = false;

        // Act
        var myDelegate = Delegates.GetDelegateFor(onComplete: () => called = true);
        myDelegate(result: null, exception: new Exception());

        Assert.That(called, Is.True);
    }

    #endregion OnComplete

    #region Order of calls

    [Test]
    public void GetDelegateFor_RunsOnCompleteAfterOnSuccess()
    {
        // Arrange
        var expectedThingsCalled = new[] { "onSuccess", "onComplete" };
        var thingsCalled = new List<string>();

        // Act
        var myDelegate = Delegates.GetDelegateFor(
            onComplete: () => thingsCalled.Add("onComplete"),
            onSuccess: () => thingsCalled.Add("onSuccess")
        );
        myDelegate(result: null, exception: null);

        Assert.That(thingsCalled, Is.EqualTo(expectedThingsCalled));
    }

    [Test]
    public void GetDelegateFor_RunsOnCompleteAfterOnFailure()
    {
        // Arrange
        var expectedThingsCalled = new[] { "onFailure", "onComplete" };
        var thingsCalled = new List<string>();

        // Act
        var myDelegate = Delegates.GetDelegateFor(
            onComplete: () => thingsCalled.Add("onComplete"),
            onFailure: _ => thingsCalled.Add("onFailure")
        );
        myDelegate(result: null, exception: new Exception());

        Assert.That(thingsCalled, Is.EqualTo(expectedThingsCalled));
    }

    #endregion Order of calls

    #region GetAsyncDelegateFor

    // The typed GetAsyncDelegateFor overload used to gate onSuccess on the caller's T -- a concrete
    // Task<TResult> -- rather than on Task, unlike its three sibling overloads. GetDelegateFor skips
    // onSuccess outright on a type mismatch and onFailure needs an exception, so a result that was
    // some OTHER Task ran nothing at all: the segment stayed open and a held transaction was never
    // released. WrapperService's runtime-async normalizer can produce exactly that, substituting
    // Task<object> when the declared result type is an open generic.

    // A completed task whose runtime type is Task<object>, not Task<string>. Completed so that
    // OnSuccess takes its synchronous branch and the whole delegate runs before the assertion.
    private static Task<object> MismatchedTask() => Task.FromResult<object>("payload");

    [Test]
    public void GetAsyncDelegateFor_EndsTheSegment_WhenTheTaskIsNotTheDeclaredTaskType()
    {
        var agent = Mock.Create<IAgent>();
        var segment = Mock.Create<ISegment>();

        var myDelegate = Delegates.GetAsyncDelegateFor<Task<string>>(agent, segment, false, _ => { });
        myDelegate(MismatchedTask(), null);

        Mock.Assert(() => segment.End(), Occurs.Once());
    }

    [Test]
    public void GetAsyncDelegateFor_RemovesTheSegmentFromTheCallStack_WhenTheTaskIsNotTheDeclaredTaskType()
    {
        var agent = Mock.Create<IAgent>();
        var segment = Mock.Create<ISegment>();

        var myDelegate = Delegates.GetAsyncDelegateFor<Task<string>>(agent, segment, false, _ => { });
        myDelegate(MismatchedTask(), null);

        Mock.Assert(() => segment.RemoveSegmentFromCallStack(), Occurs.Once());
    }

    [Test]
    public void GetAsyncDelegateFor_ReleasesTheHeldTransaction_WhenTheTaskIsNotTheDeclaredTaskType()
    {
        // holdTransactionOpen is what HttpClient's SendAsync wrapper passes, and an unreleased hold
        // keeps the whole transaction alive, not just the one segment.
        var transaction = Mock.Create<ITransaction>();
        var agent = Mock.Create<IAgent>();
        Mock.Arrange(() => agent.CurrentTransaction).Returns(transaction);
        var segment = Mock.Create<ISegment>();

        var myDelegate = Delegates.GetAsyncDelegateFor<Task<string>>(agent, segment, true, _ => { });
        myDelegate(MismatchedTask(), null);

        Mock.Assert(() => transaction.Hold(), Occurs.Once());
        Mock.Assert(() => transaction.Release(), Occurs.Once());
    }

    [Test]
    public void GetAsyncDelegateFor_PassesNullToOnComplete_WhenTheTaskIsNotTheDeclaredTaskType()
    {
        // The residual effect, and why this is far smaller than leaking the segment: OnSuccess
        // narrows with `as`, so a mismatched task arrives as null instead of throwing. onComplete
        // still runs, which is what lets a caller notice rather than be skipped silently.
        var agent = Mock.Create<IAgent>();
        var segment = Mock.Create<ISegment>();
        var onCompleteCalled = false;
        Task<string> passed = null;

        var myDelegate = Delegates.GetAsyncDelegateFor<Task<string>>(agent, segment, false, task =>
        {
            onCompleteCalled = true;
            passed = task;
        });
        myDelegate(MismatchedTask(), null);

        Assert.Multiple(() =>
        {
            Assert.That(onCompleteCalled, Is.True);
            Assert.That(passed, Is.Null);
        });
    }

    [Test]
    public void GetAsyncDelegateFor_PassesTheTaskAndEndsTheSegment_WhenTheTaskMatchesTheDeclaredType()
    {
        // The control: the ordinary path -- the only one any shipped wrapper takes today -- must be
        // untouched, otherwise the tests above would pass just as happily against a delegate that
        // ignored its type argument entirely.
        var agent = Mock.Create<IAgent>();
        var segment = Mock.Create<ISegment>();
        var expected = Task.FromResult("payload");
        Task<string> passed = null;

        var myDelegate = Delegates.GetAsyncDelegateFor<Task<string>>(agent, segment, false, task => passed = task);
        myDelegate(expected, null);

        Assert.That(passed, Is.SameAs(expected));
        Mock.Assert(() => segment.End(), Occurs.Once());
    }

    [Test]
    public void GetAsyncDelegateFor_EndsTheSegmentWithTheException_WhenTheWrappedMethodThrew()
    {
        // onFailure is unaffected by the gating change, but it is the other half of "the segment
        // always closes" and cheap to pin.
        var agent = Mock.Create<IAgent>();
        var segment = Mock.Create<ISegment>();
        var thrown = new InvalidOperationException("boom");

        var myDelegate = Delegates.GetAsyncDelegateFor<Task<string>>(agent, segment, false, _ => { });
        myDelegate(null, thrown);

        Mock.Assert(() => segment.End(thrown), Occurs.Once());
    }

    #endregion GetAsyncDelegateFor
}
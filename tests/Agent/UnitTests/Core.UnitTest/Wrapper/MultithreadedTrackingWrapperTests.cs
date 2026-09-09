// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Extensions.Providers.Wrapper;
using NUnit.Framework;

namespace NewRelic.Agent.Core.Wrapper;

[TestFixture]
public class MultithreadedTrackingWrapperTests
{
    private const string WrapperName = "MultithreadedTrackingWrapper";

    private MultithreadedTrackingWrapper _wrapper;

    [SetUp]
    public void SetUp()
    {
        _wrapper = new MultithreadedTrackingWrapper();
    }

    private static InstrumentedMethodInfo MakeInfo(string requestedWrapperName, bool isAsync, bool isRuntimeAsync = false)
    {
        var method = new Method(typeof(MultithreadedTrackingWrapperTests), "SomeMethod", string.Empty);

        return new InstrumentedMethodInfo(0, method, requestedWrapperName, isAsync, null, null, false, isRuntimeAsync);
    }

    [Test]
    public void CanWrap_IsTrue_ForASynchronousMethodWithAMatchingWrapperName()
    {
        Assert.That(_wrapper.CanWrap(MakeInfo(WrapperName, isAsync: false)).CanWrap, Is.True);
    }

    [Test]
    public void CanWrap_IsFalse_ForANonMatchingWrapperName()
    {
        Assert.That(_wrapper.CanWrap(MakeInfo("SomeOtherWrapper", isAsync: false)).CanWrap, Is.False);
    }

    [Test]
    public void CanWrap_IsFalse_ForAnAsyncMethod()
    {
        var response = _wrapper.CanWrap(MakeInfo(WrapperName, isAsync: true));

        Assert.Multiple(() =>
        {
            Assert.That(response.CanWrap, Is.False);
            Assert.That(response.AdditionalInformation, Does.Contain("not intended to be used with async-await"));
        });
    }

    /// <summary>
    /// This wrapper creates a transaction and calls AttachToAsync() without the paired
    /// DetachFromPrimary(). For a runtime-async method this is problematic: a
    /// runtime-async method's after-delegate fires only at true completion, meaning the transaction
    /// will sit in the creating thread's primary (thread-local) storage for the method's whole life,
    /// and completing on a different thread will strand it there finished forever, silently dropping the
    /// segments of any later continuation that lands on that thread.
    ///
    /// The guard above therefore rejects runtime-async methods on their own merits, independent of
    /// how they are classified as async. If it is ever relaxed to admit them, this wrapper needs
    /// DetachFromPrimary() after its AttachToAsync() in the same change. Do not simply delete this
    /// test or the one below to make a relaxation compile.
    /// </summary>
    [Test]
    public void CanWrap_IsFalse_ForARuntimeAsyncMethod()
    {
        // As WrapperService builds it: a classifiable runtime-async method is marked async, because
        // the synthesized completed Task makes the IsAsync promise about the result slot true.
        var response = _wrapper.CanWrap(MakeInfo(WrapperName, isAsync: true, isRuntimeAsync: true));

        Assert.Multiple(() =>
        {
            Assert.That(response.CanWrap, Is.False, "a runtime-async method must never reach this wrapper");
            Assert.That(response.AdditionalInformation, Does.Contain("not intended to be used with async-await"));
        });
    }

    /// <summary>
    /// The runtime-async rejection must not depend on IsAsync also being set. WrapperService
    /// promotes IsAsync for runtime-async methods today, but a future change that decoupled the
    /// two would otherwise let a runtime-async method through this wrapper and reproduce the
    /// stranded-transaction bug described above.
    /// </summary>
    [Test]
    public void CanWrap_IsFalse_ForARuntimeAsyncMethodNotMarkedAsync()
    {
        var response = _wrapper.CanWrap(MakeInfo(WrapperName, isAsync: false, isRuntimeAsync: true));

        Assert.Multiple(() =>
        {
            Assert.That(response.CanWrap, Is.False, "IsRuntimeAsync alone must be enough to reject");
            Assert.That(response.AdditionalInformation, Does.Contain("not intended to be used with async-await"));
        });
    }
}

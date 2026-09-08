// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.Wrapper;

[TestFixture]
public class OtherTransactionWrapperTests
{
    private OtherTransactionWrapper _wrapper;
    private IAgent _agent;
    private ITransaction _noTransaction;
    private ITransaction _createdTransaction;
    private ISegment _segment;

    // What agent.CurrentTransaction yields. Starts as "none in progress" and becomes the created
    // transaction once CreateTransaction runs, mirroring the real sequence.
    private ITransaction _current;

    [SetUp]
    public void SetUp()
    {
        _wrapper = new OtherTransactionWrapper();
        _agent = Mock.Create<IAgent>();
        _noTransaction = Mock.Create<ITransaction>();
        _createdTransaction = Mock.Create<ITransaction>();
        _segment = Mock.Create<ISegment>();

        Mock.Arrange(() => _noTransaction.IsValid).Returns(false);
        Mock.Arrange(() => _createdTransaction.IsValid).Returns(true);
        Mock.Arrange(() => _segment.IsValid).Returns(true);
        Mock.Arrange(() => _createdTransaction.StartMethodSegment(Arg.IsAny<MethodCall>(), Arg.AnyString, Arg.AnyString, Arg.IsAny<bool>()))
            .Returns(_segment);

        Mock.Arrange(() => _agent.Configuration.ForceNewTransactionOnNewThread).Returns(false);
        Mock.Arrange(() => _agent.CurrentTransaction).Returns(() => _current);
        Mock.Arrange(() => _agent.CreateTransaction(Arg.IsAny<bool>(), Arg.AnyString, Arg.AnyString, Arg.IsAny<bool>(), Arg.IsAny<Action>()))
            .Returns(() =>
            {
                _current = _createdTransaction;
                return _createdTransaction;
            });

        _current = _noTransaction;
    }

    private static InstrumentedMethodCall MakeCall(bool isAsync, bool isRuntimeAsync)
    {
        var method = new Method(typeof(OtherTransactionWrapperTests), "SomeMethod", string.Empty);
        var methodCall = new MethodCall(method, new object(), new object[0], isAsync);
        var info = new InstrumentedMethodInfo(0, method, "OtherTransactionWrapper", isAsync, null, null, false, isRuntimeAsync);

        return new InstrumentedMethodCall(methodCall, info);
    }

    // NR-610232. A runtime-async method's after-delegate fires only at true completion, so without
    // this the transaction would sit in the creating thread's primary (thread-local) storage for the
    // method's whole life and be stranded there, finished, once it completed on another thread.
    [Test]
    public void BeforeWrappedMethod_DetachesFromPrimary_WhenRuntimeAsyncTransactionIsCreated()
    {
        _wrapper.BeforeWrappedMethod(MakeCall(isAsync: true, isRuntimeAsync: true), _agent, _noTransaction);

        Mock.Assert(() => _createdTransaction.DetachFromPrimary(), Occurs.Once());
    }

    [Test]
    public void BeforeWrappedMethod_StillAttachesToAsync_WhenRuntimeAsync()
    {
        _wrapper.BeforeWrappedMethod(MakeCall(isAsync: true, isRuntimeAsync: true), _agent, _noTransaction);

        Mock.Assert(() => _createdTransaction.AttachToAsync(), Occurs.Once());
    }

    // State-machine async gets the same effect for free from its early Detach(), so it must be
    // left exactly as it was.
    [Test]
    public void BeforeWrappedMethod_DoesNotDetachFromPrimary_ForStateMachineAsync()
    {
        _wrapper.BeforeWrappedMethod(MakeCall(isAsync: true, isRuntimeAsync: false), _agent, _noTransaction);

        Assert.Multiple(() =>
        {
            Mock.Assert(() => _createdTransaction.DetachFromPrimary(), Occurs.Never());
            Mock.Assert(() => _createdTransaction.AttachToAsync(), Occurs.Once());
        });
    }

    [Test]
    public void BeforeWrappedMethod_DoesNotTouchStorageContexts_ForSynchronousMethods()
    {
        _wrapper.BeforeWrappedMethod(MakeCall(isAsync: false, isRuntimeAsync: false), _agent, _noTransaction);

        Assert.Multiple(() =>
        {
            Mock.Assert(() => _createdTransaction.DetachFromPrimary(), Occurs.Never());
            Mock.Assert(() => _createdTransaction.AttachToAsync(), Occurs.Never());
        });
    }

    // If a transaction was already in progress, this wrapper did not put it in primary storage, so
    // it must not remove it -- a synchronous caller further up may still be relying on it there.
    [Test]
    public void BeforeWrappedMethod_DoesNotDetachFromPrimary_WhenTheTransactionAlreadyExisted()
    {
        _current = _createdTransaction;

        _wrapper.BeforeWrappedMethod(MakeCall(isAsync: true, isRuntimeAsync: true), _agent, _createdTransaction);

        Mock.Assert(() => _createdTransaction.DetachFromPrimary(), Occurs.Never());
    }
}

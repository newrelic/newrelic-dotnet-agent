// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Threading;
using NewRelic.Agent.Api;
using NewRelic.Agent.Core.Segments;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NUnit.Framework;

namespace CompositeTests;

[TestFixture]
public class AsyncContextTransactionDeferenceTests
{
    private const uint AttributeInstrumentation = 1 << 20;

    private CompositeTestAgent _compositeTestAgent;
    private IAgent _agent;

    [SetUp]
    public void SetUp()
    {
        _compositeTestAgent = new CompositeTestAgent(shouldAllowThreads: false, includeAsyncLocalStorage: true, useRealContextStorage: true);
        _agent = _compositeTestAgent.GetAgent();
    }

    [TearDown]
    public void TearDown()
    {
        _compositeTestAgent.Dispose();
    }

    [Test]
    public void ExecutionContextRun_OfLiveTransaction_OnThreadWithAttachedTransaction_SwitchesToItAndBack()
    {
        // Pins: inside another live transaction's flow, the current transaction and segment parents come from that flow.
        var b = StartTransactionInOwnFlow("B");
        ITransaction a = null, currentInside = null, currentAfter = null;
        ISegment aOuter = null, aInner = null, bInner = null;

        RunOnNewThread(() =>
        {
            a = CreateAttachedTransaction("A");
            aOuter = _agent.StartTransactionSegmentOrThrow("A-outer");

            ExecutionContext.Run(b.Context.CreateCopy(), _ =>
            {
                currentInside = _agent.CurrentTransaction;
                bInner = _agent.StartTransactionSegmentOrThrow("B-inner");
                bInner.End();
            }, null);

            currentAfter = _agent.CurrentTransaction;
            aInner = _agent.StartTransactionSegmentOrThrow("A-inner");
            aInner.End();
            aOuter.End();
            a.End();
        });
        EndTransaction(b);

        Assert.Multiple(() =>
        {
            Assert.That(currentInside, Is.SameAs(b.Transaction));
            Assert.That(currentAfter, Is.SameAs(a));
            Assert.That(Segments(b.Transaction), Does.Contain(bInner));
            Assert.That(((Segment)bInner).ParentUniqueId, Is.EqualTo(((Segment)b.Outer).UniqueId));
            Assert.That(Segments(a), Does.Contain(aInner));
            Assert.That(Segments(a), Does.Not.Contain(bInner));
            Assert.That(((Segment)aInner).ParentUniqueId, Is.EqualTo(((Segment)aOuter).UniqueId));
        });
    }

    [Test]
    public void StaleFinishedThreadLocalTransaction_DoesNotDetachLiveFlowTransaction_InWrapperService()
    {
        // Pins: a finished thread-local transaction does not make WrapperService detach the live flow transaction.
        var b = StartTransactionInOwnFlow("B");
        ITransaction currentBefore = null, currentAfter = null;
        AfterWrappedMethodDelegate afterWrappedMethod = null;
        var segmentCountBefore = Segments(b.Transaction).Count;

        RunOnNewThread(() =>
        {
            var a = CreateAttachedTransaction("A");
            RunOnNewThread(() => a.End());

            ExecutionContext.Run(b.Context.CreateCopy(), _ =>
            {
                currentBefore = _agent.CurrentTransaction;
                afterWrappedMethod = _compositeTestAgent.GetWrapperService().BeforeWrappedMethod(typeof(AsyncContextTransactionDeferenceTests),
                    "MyMethod", string.Empty, new object(), new object[0], "NewRelic.Agent.Core.Wrapper.DefaultWrapper", null, AttributeInstrumentation, 0, null);
                currentAfter = _agent.CurrentTransaction;
                afterWrappedMethod(null, null);
            }, null);
        });
        EndTransaction(b);

        Assert.Multiple(() =>
        {
            Assert.That(currentBefore, Is.SameAs(b.Transaction));
            Assert.That(currentAfter, Is.SameAs(b.Transaction));
            Assert.That(afterWrappedMethod, Is.Not.EqualTo(Delegates.NoOp));
            Assert.That(Segments(b.Transaction), Has.Count.EqualTo(segmentCountBefore + 1));
        });
    }

    [Test]
    public void StalePoolThread_WithUnfinishedAttachedTransaction_ReturnsLiveFlowTransaction()
    {
        // Pins: an unfinished transaction left in thread-local storage by earlier work does not capture a later flow's segments.
        var b = StartTransactionInOwnFlow("B");
        ITransaction a = null, current = null;
        ISegment bInner = null;

        RunOnNewThread(() =>
        {
            ExecutionContext.Run(ExecutionContext.Capture().CreateCopy(), _ =>
            {
                a = CreateAttachedTransaction("A");
                _agent.StartTransactionSegmentOrThrow("A-work").End();
            }, null);

            ExecutionContext.Run(b.Context.CreateCopy(), _ =>
            {
                current = _agent.CurrentTransaction;
                bInner = _agent.StartTransactionSegmentOrThrow("B-inner");
                bInner.End();
            }, null);

            a.End();
        });
        EndTransaction(b);

        Assert.Multiple(() =>
        {
            Assert.That(current, Is.SameAs(b.Transaction));
            Assert.That(Segments(b.Transaction), Does.Contain(bInner));
            Assert.That(((Segment)bInner).ParentUniqueId, Is.EqualTo(((Segment)b.Outer).UniqueId));
            Assert.That(Segments(a), Has.Count.EqualTo(1));
        });
    }

    private (ITransaction Transaction, ISegment Outer, ExecutionContext Context) StartTransactionInOwnFlow(string name)
    {
        ITransaction transaction = null;
        ISegment outer = null;
        ExecutionContext context = null;
        RunOnNewThread(() =>
        {
            transaction = CreateAttachedTransaction(name);
            outer = _agent.StartTransactionSegmentOrThrow(name + "-outer");
            context = ExecutionContext.Capture();
        });
        return (transaction, outer, context);
    }

    private ITransaction CreateAttachedTransaction(string name)
    {
        var transaction = _agent.CreateTransaction(
            isWeb: true,
            category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
            transactionDisplayName: name,
            doNotTrackAsUnitOfWork: true);
        transaction.AttachToAsync();
        return transaction;
    }

    private static void EndTransaction((ITransaction Transaction, ISegment Outer, ExecutionContext Context) flow)
    {
        flow.Outer.End();
        flow.Transaction.End();
    }

    private static IList<Segment> Segments(ITransaction transaction)
    {
        return ((IInternalTransaction)transaction).Segments;
    }

    // A dedicated thread keeps thread-local and async state out of the test runner's threads.
    private static void RunOnNewThread(Action action)
    {
        Exception failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.Start();
        thread.Join();
        if (failure != null)
            ExceptionDispatchInfo.Capture(failure).Throw();
    }
}

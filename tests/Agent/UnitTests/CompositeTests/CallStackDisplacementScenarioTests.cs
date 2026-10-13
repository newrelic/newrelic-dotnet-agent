// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using NewRelic.Agent.Api;
using NewRelic.Agent.Core.CallStack;
using NewRelic.Agent.Core.Segments;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Providers.Storage.AsyncLocal;
using NUnit.Framework;

namespace CompositeTests;

// Each test starts or resolves a transaction while the async flow carries another transaction's call stack entry.
[TestFixture]
public class CallStackDisplacementScenarioTests
{
    private const uint AsyncFlag = 1 << 23;

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
    public void ChildFlowOfEndedTransaction_WrapperStartsNewTransactionOverLeftoverEntry()
    {
        // A fire-and-forget child flow (Task.Run, timer, consumer loop) forked inside A outlives A.
        ITransaction a = null;
        ISegment aOuter = null;
        ExecutionContext child = null;
        RunOnNewThread(() =>
        {
            a = CreateAttachedTransaction("A");
            aOuter = _agent.StartTransactionSegmentOrThrow("A-outer");
            child = ExecutionContext.Capture();
            aOuter.End();
            a.End();
        });

        var r = RunWrapperInFlow(child, "OtherTransactionWrapper", 1);

        Assert.Multiple(() =>
        {
            Assert.That(r.CurrentBefore, Is.SameAs(a), "child flow still resolves the finished transaction");
            Assert.That(r.CurrentBeforeFinished, Is.True);
            Assert.That(r.EntryBefore, Is.EqualTo(Describe(a, ((Segment)aOuter).UniqueId)), "child flow carries A's entry");
            Assert.That(r.B, Is.Not.SameAs(a), "wrapper started a new transaction");
            Assert.That(r.BRoot.ParentUniqueId, Is.Null);
            Assert.That(((Segment)r.BInner).ParentUniqueId, Is.EqualTo(r.BRoot.UniqueId));
            Assert.That(r.B.IsFinished, Is.True);
            Assert.That(r.CurrentAfter.IsValid, Is.False, "nothing in the flow can read A's entry afterwards");
            Assert.That(Segments(a), Has.Count.EqualTo(1));
        });
    }

    [Test]
    public void ForceNewTransactionOnAsync_StartsNewTransactionOverLiveEntry()
    {
        // AsyncForceNewTransactionWrapper (or forceNewTransactionOnNewThread) on work that Task.Run moved off A's thread.
        ITransaction a = null;
        ISegment aOuter = null, aAfter = null;
        WrapperRun r = null;
        RunOnNewThread(() =>
        {
            a = CreateAttachedTransaction("A");
            aOuter = _agent.StartTransactionSegmentOrThrow("A-outer");
            var child = ExecutionContext.Capture();
            r = RunWrapperInFlow(child, "AsyncForceNewTransactionWrapper", 2);
            aAfter = _agent.StartTransactionSegmentOrThrow("A-after");
            aAfter.End();
            aOuter.End();
            a.End();
        });

        Assert.Multiple(() =>
        {
            Assert.That(r.CurrentBefore, Is.SameAs(a));
            Assert.That(r.CurrentBeforeFinished, Is.False, "A is live while B covers its entry");
            Assert.That(r.EntryBefore, Is.EqualTo(Describe(a, ((Segment)aOuter).UniqueId)));
            Assert.That(r.B, Is.Not.SameAs(a));
            Assert.That(r.BRoot.ParentUniqueId, Is.Null);
            Assert.That(((Segment)r.BInner).ParentUniqueId, Is.EqualTo(r.BRoot.UniqueId));
            Assert.That(r.CurrentAfter.IsValid, Is.False, "the child flow no longer resolves A");
            Assert.That(((Segment)aAfter).ParentUniqueId, Is.EqualTo(((Segment)aOuter).UniqueId), "A's own flow is unaffected");
        });
    }

    [Test]
    public void TooManyNestedAttempts_ReplacementTransactionPushesOverLiveEntry()
    {
        ITransaction a = null, b = null, currentAfter = null;
        ISegment aOuter = null, bRoot = null;
        string entryBefore = null;
        RunOnNewThread(() =>
        {
            a = CreateAttachedTransaction("A");
            aOuter = _agent.StartTransactionSegmentOrThrow("A-outer");
            entryBefore = CurrentEntry();
            b = a;
            for (var i = 0; i < 200 && b == a; i++)
                b = _agent.CreateTransaction(isWeb: false, category: "Custom", transactionDisplayName: "nested", doNotTrackAsUnitOfWork: true);
            bRoot = _agent.StartTransactionSegmentOrThrow("B-root");
            bRoot.End();
            b.End();
            currentAfter = _agent.CurrentTransaction;
            aOuter.End();
            a.End();
        });

        Assert.Multiple(() =>
        {
            Assert.That(b, Is.Not.SameAs(a));
            Assert.That(entryBefore, Is.EqualTo(Describe(a, ((Segment)aOuter).UniqueId)));
            Assert.That(((Segment)bRoot).ParentUniqueId, Is.Null);
            Assert.That(currentAfter.IsValid, Is.False, "the replacement released A from this flow");
        });
    }

    [Test]
    public void UnattachedThreadLocalTransaction_RunningLiveFlowsContinuation_AttachesOverLiveEntry()
    {
        // B runs synchronously on thread X (thread-local, not attached) and runs a continuation of live A inline
        // (TaskCompletionSource.SetResult without RunContinuationsAsynchronously, or Task.Wait inlining).
        var aFlow = StartTransactionInOwnFlow("A");
        ITransaction b = null;
        ISegment bOuter = null;
        ExecutionContext aNextFlow = null;
        Exception thrown = null;
        RunOnNewThread(() =>
        {
            b = _agent.CreateTransaction(isWeb: true, category: "Action", transactionDisplayName: "B", doNotTrackAsUnitOfWork: true);
            // Pad B so its segment ids differ from A's.
            _agent.StartTransactionSegmentOrThrow("B-pad-1").End();
            _agent.StartTransactionSegmentOrThrow("B-pad-2").End();
            bOuter = _agent.StartTransactionSegmentOrThrow("B-outer");
            ExecutionContext.Run(aFlow.Context.CreateCopy(), _ =>
            {
                try
                {
                    _agent.CurrentTransaction.AttachToAsync();
                    var inner = _agent.StartTransactionSegmentOrThrow("inner");
                    _agent.StartTransactionSegmentOrThrow("inner-2").End();
                    inner.End();
                    aNextFlow = ExecutionContext.Capture();
                }
                catch (Exception e)
                {
                    thrown = e;
                }
            }, null);
            _agent.StartTransactionSegmentOrThrow("B-after").End();
            bOuter.End();
            b.End();
        });
        // A's next continuation resumes on another thread with the flow captured at its next await.
        RunOnNewThread(() => ExecutionContext.Run(aNextFlow.CreateCopy(), _ =>
        {
            var aNext = _agent.StartTransactionSegmentOrThrow("A-next");
            // Without owner checks, A-next takes B's segment id as its parent and End() throws ArgumentOutOfRangeException.
            aNext.End();
        }, null));
        aFlow.Outer.End();
        aFlow.Transaction.End();

        Assert.That(thrown, Is.Null);
    }

    private WrapperRun RunWrapperInFlow(ExecutionContext flow, string wrapperName, ulong functionId)
    {
        var r = new WrapperRun();
        RunOnNewThread(() => ExecutionContext.Run(flow.CreateCopy(), _ =>
        {
            r.CurrentBefore = _agent.CurrentTransaction;
            r.CurrentBeforeFinished = r.CurrentBefore.IsFinished;
            r.EntryBefore = CurrentEntry();
            var completion = new TaskCompletionSource<object>();
            var after = _compositeTestAgent.GetWrapperService().BeforeWrappedMethod(typeof(CallStackDisplacementScenarioTests),
                "Consume", string.Empty, new object(), new object[0], wrapperName, null, AsyncFlag, functionId, null);
            r.B = _agent.CurrentTransaction;
            r.BRoot = Segments(r.B)[0];
            r.BInner = _agent.StartTransactionSegmentOrThrow("B-inner");
            r.BInner.End();
            after(completion.Task, null);
            completion.SetResult(null);
            r.CurrentAfter = _agent.CurrentTransaction;
        }, null));
        return r;
    }

    // The top async-local call stack entry, as "<owner manager hash>:<segment id>".
    private static string CurrentEntry()
    {
        var entry = new AsyncLocalStorage<CallStackEntry>().GetData();
        return entry == null ? "null" : $"{entry.Owner.GetHashCode()}:{entry.Id}";
    }

    private static string Describe(ITransaction transaction, int id)
    {
        return $"{((IInternalTransaction)transaction).CallStackManager.GetHashCode()}:{id}";
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
        var transaction = _agent.CreateTransaction(isWeb: true, category: "Action", transactionDisplayName: name, doNotTrackAsUnitOfWork: true);
        transaction.AttachToAsync();
        return transaction;
    }

    private static IList<Segment> Segments(ITransaction transaction)
    {
        return ((IInternalTransaction)transaction).Segments;
    }

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

    private sealed class WrapperRun
    {
        public ITransaction CurrentBefore;
        public bool CurrentBeforeFinished;
        public string EntryBefore;
        public ITransaction B;
        public Segment BRoot;
        public ISegment BInner;
        public ITransaction CurrentAfter;
    }
}

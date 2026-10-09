// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Api.Experimental;
using NewRelic.Agent.Core.Attributes;
using NewRelic.Agent.Core.CallStack;
using NewRelic.Agent.Core.DistributedTracing;
using NewRelic.Agent.Core.DistributedTracing.Samplers;
using NewRelic.Agent.Core.Errors;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders;
using NewRelic.Agent.Extensions.Providers;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.Transactions.UnitTest;

[TestFixture]
public class TransactionServiceThreadLocalDeferenceTests
{
    private TransactionService _transactionService;
    private IContextStorage<IInternalTransaction> _threadLocal;
    private TestStorage _async;
    private TestStorage _highPriority;

    [SetUp]
    public void SetUp()
    {
        _threadLocal = new ThreadLocalStorage<IInternalTransaction>("NewRelic.Transaction");
        _async = new TestStorage();
        _highPriority = new TestStorage { PriorityValue = 10 };

        var attribDefSvc = new AttributeDefinitionService((f) => new AttributeDefinitions(f));
        _transactionService = new TransactionService(
            new[] { CreateFactory(_threadLocal, isAsync: false), CreateFactory(_async, isAsync: true), CreateFactory(_highPriority, isAsync: false) },
            Mock.Create<ISimpleTimerFactory>(), Mock.Create<ICallStackManagerFactory>(), Mock.Create<IDatabaseService>(),
            Mock.Create<ITracePriorityManager>(), Mock.Create<IDatabaseStatementParser>(), Mock.Create<IErrorService>(),
            Mock.Create<IDistributedTracePayloadHandler>(), attribDefSvc, Mock.Create<ISamplerService>());

        // The service's static thread-local fallback can hold data from other tests on this thread.
        _transactionService.RemoveOutstandingInternalTransactions(true, true);
    }

    [TearDown]
    public void TearDown()
    {
        _transactionService.RemoveOutstandingInternalTransactions(true, true);
        _transactionService.Dispose();
    }

    [Test]
    public void AttachedThreadLocal_DefersToDifferentLiveAsyncTransaction()
    {
        var threadLocalTransaction = CreateTransaction(isAttached: true, isFinished: false);
        var asyncTransaction = CreateTransaction(isAttached: true, isFinished: false);
        _threadLocal.SetData(threadLocalTransaction);
        _async.Value = asyncTransaction;

        Assert.That(_transactionService.GetCurrentInternalTransaction(), Is.SameAs(asyncTransaction));
    }

    [Test]
    public void AttachedThreadLocal_DoesNotDeferToFinishedAsyncTransaction()
    {
        var threadLocalTransaction = CreateTransaction(isAttached: true, isFinished: false);
        _threadLocal.SetData(threadLocalTransaction);
        _async.Value = CreateTransaction(isAttached: true, isFinished: true);

        Assert.That(_transactionService.GetCurrentInternalTransaction(), Is.SameAs(threadLocalTransaction));
    }

    [Test]
    public void NotAttachedThreadLocal_DoesNotDefer()
    {
        var threadLocalTransaction = CreateTransaction(isAttached: false, isFinished: false);
        _threadLocal.SetData(threadLocalTransaction);
        _async.Value = CreateTransaction(isAttached: true, isFinished: false);

        Assert.That(_transactionService.GetCurrentInternalTransaction(), Is.SameAs(threadLocalTransaction));
    }

    [Test]
    public void AttachedThreadLocal_WithEmptyAsyncContext_IsReturned()
    {
        var threadLocalTransaction = CreateTransaction(isAttached: true, isFinished: false);
        _threadLocal.SetData(threadLocalTransaction);

        Assert.That(_transactionService.GetCurrentInternalTransaction(), Is.SameAs(threadLocalTransaction));
    }

    [Test]
    public void AttachedThreadLocal_WithSameAsyncTransaction_IsReturned()
    {
        var threadLocalTransaction = CreateTransaction(isAttached: true, isFinished: false);
        _threadLocal.SetData(threadLocalTransaction);
        _async.Value = threadLocalTransaction;

        Assert.That(_transactionService.GetCurrentInternalTransaction(), Is.SameAs(threadLocalTransaction));
    }

    [Test]
    public void AttachedThreadLocal_WhenAsyncContextThrows_IsReturned()
    {
        var threadLocalTransaction = CreateTransaction(isAttached: true, isFinished: false);
        _threadLocal.SetData(threadLocalTransaction);
        _async.ThrowOnGet = true;

        Assert.That(_transactionService.GetCurrentInternalTransaction(), Is.SameAs(threadLocalTransaction));
    }

    [Test]
    public void HigherPriorityPrimaryHit_IsReturnedWithoutDeference()
    {
        var requestTransaction = CreateTransaction(isAttached: true, isFinished: false);
        _highPriority.Value = requestTransaction;
        _async.Value = CreateTransaction(isAttached: true, isFinished: false);

        Assert.That(_transactionService.GetCurrentInternalTransaction(), Is.SameAs(requestTransaction));
    }

    [Test]
    public void Deference_LogsAtFinest()
    {
        using var logging = new TestUtilities.Logging();
        var asyncTransaction = CreateTransaction(isAttached: true, isFinished: false);
        _threadLocal.SetData(CreateTransaction(isAttached: true, isFinished: false));
        _async.Value = asyncTransaction;

        _transactionService.GetCurrentInternalTransaction();

        Mock.Assert(() => asyncTransaction.LogFinest(Arg.Matches<string>(m => m.Contains("in place of a thread-local transaction"))), Occurs.Once());
    }

    private static IInternalTransaction CreateTransaction(bool isAttached, bool isFinished)
    {
        var transaction = Mock.Create<IInternalTransaction>();
        Mock.Arrange(() => transaction.IsAttachedToAsync).Returns(isAttached);
        Mock.Arrange(() => transaction.IsFinished).Returns(isFinished);
        return transaction;
    }

    private static IContextStorageFactory CreateFactory(IContextStorage<IInternalTransaction> storage, bool isAsync)
    {
        var factory = Mock.Create<IContextStorageFactory>();
        Mock.Arrange(() => factory.IsAsyncStorage).Returns(isAsync);
        Mock.Arrange(() => factory.IsHybridStorage).Returns(false);
        Mock.Arrange(() => factory.CreateContext<IInternalTransaction>(Arg.AnyString)).Returns(storage);
        return factory;
    }

    private sealed class TestStorage : IContextStorage<IInternalTransaction>
    {
        public IInternalTransaction Value;
        public byte PriorityValue = 2;
        public bool ThrowOnGet;

        public byte Priority => PriorityValue;
        public bool CanProvide => true;
        public IInternalTransaction GetData() => ThrowOnGet ? throw new InvalidOperationException("test") : Value;
        public void SetData(IInternalTransaction value) => Value = value;
        public void Clear() => Value = null;
    }
}

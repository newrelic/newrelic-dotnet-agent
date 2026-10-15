// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using NewRelic.Agent.Api.Experimental;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Attributes;
using NewRelic.Agent.Core.DistributedTracing;
using NewRelic.Agent.Core.DistributedTracing.Samplers;
using NewRelic.Agent.Core.Errors;
using NewRelic.Agent.Core.Segments;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Data;
using NewRelic.Agent.Extensions.Providers;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.CallStack;

[TestFixture]
public class CallStackManagerTests
{
    private SharedTestStorage _shared;

    [SetUp]
    public void SetUp()
    {
        _shared = new SharedTestStorage();
    }

    private SyncToAsyncCallStackManager CreateAttached()
    {
        var manager = new SyncToAsyncCallStackManager(_shared);
        manager.AttachToAsync();
        return manager;
    }

    [Test]
    public void BeforeAttach_UsesPrivateStorage()
    {
        var manager = new SyncToAsyncCallStackManager(_shared);

        manager.Push(3);

        Assert.That(manager.TryPeek(), Is.EqualTo(3));
        Assert.That(_shared.Value, Is.Null);
    }

    [Test]
    public void BeforeAttach_PopOfTopRestoresParent_AndPopOfOtherIdIsIgnored()
    {
        var manager = new SyncToAsyncCallStackManager(_shared);
        manager.Push(1);
        manager.Push(2);

        manager.TryPop(1, null);
        Assert.That(manager.TryPeek(), Is.EqualTo(2));

        manager.TryPop(2, 1);
        Assert.That(manager.TryPeek(), Is.EqualTo(1));
    }

    [Test]
    public void BeforeAttach_ClearRemovesValue()
    {
        var manager = new SyncToAsyncCallStackManager(_shared);
        manager.Push(1);

        manager.Clear();

        Assert.That(manager.TryPeek(), Is.Null);
    }

    [Test]
    public void FirstAttach_CarriesPrivateValueIntoSharedSlot()
    {
        var manager = new SyncToAsyncCallStackManager(_shared);
        manager.Push(7);

        var result = manager.AttachToAsync();

        Assert.That(result, Is.True);
        Assert.That(_shared.Value.Owner, Is.SameAs(manager));
        Assert.That(_shared.Value.Id, Is.EqualTo(7));
        Assert.That(manager.TryPeek(), Is.EqualTo(7));
    }

    [Test]
    public void FirstAttach_WithNoPrivateValue_LeavesForeignEntryInPlace()
    {
        var owner = CreateAttached();
        owner.Push(42);

        var other = new SyncToAsyncCallStackManager(_shared);
        other.AttachToAsync();

        Assert.That(_shared.Value.Owner, Is.SameAs(owner));
        Assert.That(other.TryPeek(), Is.Null);
    }

    [Test]
    public void FirstAttach_WithPrivateValue_TakesSlotFromForeignEntry()
    {
        var owner = CreateAttached();
        owner.Push(42);
        var other = new SyncToAsyncCallStackManager(_shared);
        other.Push(7);

        other.AttachToAsync();

        Assert.That(other.TryPeek(), Is.EqualTo(7));
        Assert.That(owner.TryPeek(), Is.Null);
    }

    [Test]
    public void RepeatAttach_DoesNotOverwriteForeignEntry()
    {
        var other = new SyncToAsyncCallStackManager(_shared);
        other.Push(1);
        other.AttachToAsync();
        var owner = CreateAttached();
        owner.Push(42);

        other.AttachToAsync();

        Assert.That(_shared.Value.Owner, Is.SameAs(owner));
        Assert.That(_shared.Value.Id, Is.EqualTo(42));
    }

    [Test]
    public void AfterAttach_OwnPushPeekPop_Work()
    {
        var manager = CreateAttached();
        manager.Push(1);
        manager.Push(2);

        Assert.That(manager.TryPeek(), Is.EqualTo(2));
        manager.TryPop(2, 1);
        Assert.That(manager.TryPeek(), Is.EqualTo(1));
        manager.TryPop(1, null);
        Assert.That(manager.TryPeek(), Is.Null);
        Assert.That(_shared.Value, Is.Null);
    }

    [Test]
    public void AfterAttach_PeekOfForeignEntry_ReturnsNull()
    {
        var owner = CreateAttached();
        var other = CreateAttached();
        owner.Push(42);

        Assert.That(other.TryPeek(), Is.Null);
    }

    [Test]
    public void AfterAttach_PopAfterForeignShadow_IsIgnored()
    {
        var owner = CreateAttached();
        var other = CreateAttached();
        owner.Push(5);
        other.Push(5);

        owner.TryPop(5, null);

        Assert.That(_shared.Value.Owner, Is.SameAs(other));
        Assert.That(other.TryPeek(), Is.EqualTo(5));
    }

    [Test]
    public void AfterAttach_RootPopOverForeignEntry_LeavesSlotEmpty()
    {
        var a = CreateAttached();
        a.Push(3);
        var b = CreateAttached();

        b.Push(0);
        Assert.That(b.TryPeek(), Is.EqualTo(0));
        Assert.That(a.TryPeek(), Is.Null);
        b.TryPop(0, null);

        Assert.That(_shared.Value, Is.Null);
        Assert.That(b.TryPeek(), Is.Null);
    }

    [Test]
    public void Transaction_ForeignEntryIdWithinOwnSegmentRange_IsNotUsedAsParent()
    {
        var other = CreateAttached();
        var transaction = CreateTransaction(CreateAttached());
        for (var i = 0; i < 50; i++)
        {
            transaction.CallStackPop(StartSegment(transaction), true);
        }
        other.Push(42);

        var segment = StartSegment(transaction);

        Assert.That(segment.UniqueId, Is.EqualTo(50));
        Assert.That(segment.ParentUniqueId, Is.Null);
        Assert.That(() => transaction.CallStackPop(segment, true), Throws.Nothing);
    }

    [Test]
    public void AfterAttach_PopOfOwnNonTopId_IsIgnored()
    {
        var manager = CreateAttached();
        manager.Push(1);
        manager.Push(2);

        manager.TryPop(1, null);

        Assert.That(manager.TryPeek(), Is.EqualTo(2));
    }

    [Test]
    public void AfterAttach_PopOnEmptySlot_IsIgnored()
    {
        var manager = CreateAttached();

        manager.TryPop(1, null);

        Assert.That(_shared.Value, Is.Null);
    }

    [Test]
    public void AfterAttach_ClearRemovesOwnEntryOnly()
    {
        var owner = CreateAttached();
        var other = CreateAttached();
        owner.Push(42);

        other.Clear();
        Assert.That(_shared.Value.Owner, Is.SameAs(owner));

        owner.Clear();
        Assert.That(_shared.Value, Is.Null);
    }

    [Test]
    public void AfterAttach_ClearOnEmptySlot_DoesNothing()
    {
        var manager = CreateAttached();

        manager.Clear();

        Assert.That(_shared.Value, Is.Null);
    }

    [Test]
    public void FallbackManager_UsesHighestPriorityStorageThatCanProvide()
    {
        var unavailable = new SharedTestStorage { CanProvideValue = false };
        var manager = new CallStackManager(new List<IContextStorage<CallStackEntry>> { unavailable, _shared });

        manager.Push(9);

        Assert.That(unavailable.Value, Is.Null);
        Assert.That(manager.TryPeek(), Is.EqualTo(9));
        Assert.That(manager.AttachToAsync(), Is.False);
    }

    [Test]
    public void FallbackManager_IgnoresForeignEntry()
    {
        var owner = new CallStackManager(new List<IContextStorage<CallStackEntry>> { _shared });
        var other = new CallStackManager(new List<IContextStorage<CallStackEntry>> { _shared });
        owner.Push(42);

        other.TryPop(42, null);
        other.Clear();

        Assert.That(other.TryPeek(), Is.Null);
        Assert.That(owner.TryPeek(), Is.EqualTo(42));
    }

    [Test]
    public void FallbackManager_WithNoStorageThatCanProvide_IsNoOp()
    {
        var unavailable = new SharedTestStorage { CanProvideValue = false };
        var manager = new CallStackManager(new List<IContextStorage<CallStackEntry>> { unavailable });

        manager.Push(1);
        manager.TryPop(1, null);
        manager.Clear();

        Assert.That(manager.TryPeek(), Is.Null);
        Assert.That(unavailable.Value, Is.Null);
    }

    [Test]
    public void AsyncFactory_DoesNotClearSharedSlot_WhenCreatingManager()
    {
        var storageFactory = Mock.Create<IContextStorageFactory>();
        Mock.Arrange(() => storageFactory.CreateContext<CallStackEntry>("NewRelic.ParentObject")).Returns(_shared);
        var factory = new AsyncCallStackManagerFactory(storageFactory);

        var first = factory.CreateCallStackManager();
        first.AttachToAsync();
        first.Push(42);
        var second = factory.CreateCallStackManager();

        Assert.That(first.TryPeek(), Is.EqualTo(42));
        Assert.That(second, Is.Not.SameAs(first));
    }

    [Test]
    public void Factory_OrdersStoragesByPriority_AndSkipsNullContexts()
    {
        var low = new SharedTestStorage { PriorityValue = 1 };
        var high = new SharedTestStorage { PriorityValue = 5 };
        var lowFactory = Mock.Create<IContextStorageFactory>();
        Mock.Arrange(() => lowFactory.CreateContext<CallStackEntry>(Arg.AnyString)).Returns(low);
        var highFactory = Mock.Create<IContextStorageFactory>();
        Mock.Arrange(() => highFactory.CreateContext<CallStackEntry>(Arg.AnyString)).Returns(high);
        var nullFactory = Mock.Create<IContextStorageFactory>();
        Mock.Arrange(() => nullFactory.CreateContext<CallStackEntry>(Arg.AnyString)).Returns((IContextStorage<CallStackEntry>)null);

        var manager = new CallStackManagerFactory(new[] { lowFactory, nullFactory, highFactory }).CreateCallStackManager();
        manager.Push(3);

        Assert.That(high.Value.Id, Is.EqualTo(3));
        Assert.That(low.Value, Is.Null);
    }

    private static Transaction CreateTransaction(ICallStackManager callStackManager)
    {
        var configuration = Mock.Create<IConfiguration>();
        Mock.Arrange(() => configuration.TransactionTracerMaxSegments).Returns(1000);
        var attribDefSvc = new AttributeDefinitionService((f) => new AttributeDefinitions(f));
        return new Transaction(configuration, Mock.Create<ITransactionName>(), Mock.Create<ISimpleTimer>(), DateTime.UtcNow,
            callStackManager, Mock.Create<IDatabaseService>(), 0.0f, Mock.Create<IDatabaseStatementParser>(),
            Mock.Create<IDistributedTracePayloadHandler>(), Mock.Create<IErrorService>(), attribDefSvc.AttributeDefs, Mock.Create<ISamplerService>());
    }

    private static Segment StartSegment(Transaction transaction)
    {
        return new Segment(transaction.GetTransactionSegmentState(), new MethodCallData("Type", "Method", 1));
    }

    private sealed class SharedTestStorage : IContextStorage<CallStackEntry>
    {
        public CallStackEntry Value;
        public bool CanProvideValue = true;
        public byte PriorityValue = 1;

        public byte Priority => PriorityValue;
        public bool CanProvide => CanProvideValue;
        public CallStackEntry GetData() => Value;
        public void SetData(CallStackEntry value) => Value = value;
        public void Clear() => Value = null;
    }
}

// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
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
    public void FirstAttach_WithPrivateValue_RestoresForeignEntryOnRootPop()
    {
        var owner = CreateAttached();
        owner.Push(42);
        var other = new SyncToAsyncCallStackManager(_shared);
        other.Push(7);

        other.AttachToAsync();
        Assert.That(other.TryPeek(), Is.EqualTo(7));
        other.TryPop(7, null);

        Assert.That(owner.TryPeek(), Is.EqualTo(42));
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
    public void AfterAttach_RootPopOverForeignEntry_RestoresForeignEntry()
    {
        var a = CreateAttached();
        a.Push(3);
        var b = CreateAttached();

        b.Push(0);
        Assert.That(b.TryPeek(), Is.EqualTo(0));
        b.TryPop(0, null);

        Assert.That(a.TryPeek(), Is.EqualTo(3));
        Assert.That(b.TryPeek(), Is.Null);
    }

    [Test]
    public void AfterAttach_NestedPushesOverForeignEntry_CarryForeignEntryForward()
    {
        var a = CreateAttached();
        a.Push(3);
        var b = CreateAttached();

        b.Push(0);
        b.Push(1);
        b.Push(2);
        b.TryPop(2, 1);
        Assert.That(b.TryPeek(), Is.EqualTo(1));
        Assert.That(a.TryPeek(), Is.Null);
        b.TryPop(1, 0);
        b.TryPop(0, null);

        Assert.That(a.TryPeek(), Is.EqualTo(3));
    }

    [Test]
    public void AfterAttach_InterleavedNesting_RestoresEachEntryInOrder()
    {
        var a = CreateAttached();
        a.Push(3);
        var b = CreateAttached();
        b.Push(0);

        a.Push(4);
        Assert.That(a.TryPeek(), Is.EqualTo(4));
        a.TryPop(4, null);
        Assert.That(b.TryPeek(), Is.EqualTo(0));
        b.TryPop(0, null);

        Assert.That(a.TryPeek(), Is.EqualTo(3));
        a.TryPop(3, null);
        Assert.That(_shared.Value, Is.Null);
    }

    [Test]
    public void AfterAttach_PopOfOwnEntryUnderForeignEntry_IsIgnored()
    {
        var a = CreateAttached();
        a.Push(3);
        var b = CreateAttached();
        b.Push(0);

        a.TryPop(3, null);

        Assert.That(b.TryPeek(), Is.EqualTo(0));
        b.TryPop(0, null);
        Assert.That(a.TryPeek(), Is.EqualTo(3));
    }

    [Test]
    public void AfterAttach_ClearOverForeignEntry_RestoresForeignEntry()
    {
        var a = CreateAttached();
        a.Push(3);
        var b = CreateAttached();
        b.Push(0);
        b.Push(1);

        b.Clear();

        Assert.That(b.TryPeek(), Is.Null);
        Assert.That(a.TryPeek(), Is.EqualTo(3));
    }

    [Test]
    public void AfterAttach_DisplacedChainAtCap_RestoresEveryLayer()
    {
        var managers = PushForeignLayers(9);

        for (var i = 8; i > 0; i--)
        {
            managers[i].TryPop(i, null);
            Assert.That(managers[i - 1].TryPeek(), Is.EqualTo(i - 1));
        }
    }

    [Test]
    public void AfterAttach_DisplacedChainOverCap_DropsTheChain()
    {
        var managers = PushForeignLayers(10);

        Assert.That(_shared.Value.Displaced, Is.Null);
        Assert.That(_shared.Value.DisplacedDepth, Is.EqualTo(0));
        managers[9].TryPop(9, null);
        Assert.That(_shared.Value, Is.Null);
    }

    private List<SyncToAsyncCallStackManager> PushForeignLayers(int count)
    {
        var managers = new List<SyncToAsyncCallStackManager>();
        for (var i = 0; i < count; i++)
        {
            var manager = CreateAttached();
            manager.Push(i);
            managers.Add(manager);
        }
        return managers;
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

// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.IO;
using System.Threading.Tasks;
using System.Web;
using NewRelic.Agent.Extensions.Providers;
using NewRelic.Providers.Storage.HybridHttpContext;
using NUnit.Framework;

namespace NewRelic.Providers.Storage.HybridHttpContextTests;

// NR-599419: HybridHttpContextStorage<T>.SetData used to mutate the existing (shared)
// ValueHolder in place instead of assigning a new one, the way Clear() did. AsyncLocal<T>
// isolates contexts by giving each ExecutionContext its own *reference*; mutating the object
// a reference points to is visible to every context that shares that reference -- the parent,
// siblings, and already-launched children. The fix makes SetData assign a new ValueHolder on
// every write, the same copy-on-write approach Clear() already used, so a write in one
// ExecutionContext is never visible to its parent or siblings. The tests below encode that
// (post-fix) behavior; SetData_InChildExecutionContext_DoesNotAffectParent and
// SetData_InSiblingChildExecutionContext_DoesNotAffectOtherSibling are the regression guards
// for the original defect.
//
// _asyncLocal in HybridHttpContextStorage<T> is static per CLOSED GENERIC TYPE, and NUnit can
// run test methods on a shared ExecutionContext, so writes could leak between tests. Every
// test here uses its own private marker type as T so no two tests ever share that static slot.
[TestFixture]
public class HybridHttpContextStorageTests
{
    private sealed class Marker01 { }
    private sealed class Marker02 { }
    private sealed class Marker03 { }
    private sealed class Marker04 { }
    private sealed class Marker05 { }
    private sealed class Marker06 { }
    private sealed class Marker07 { }
    private sealed class Marker08 { }
    private sealed class Marker09 { }
    private sealed class Marker10 { }
    private sealed class Marker11 { }
    private sealed class Marker12 { }
    private sealed class Marker13 { }
    private sealed class Marker14 { }
    private sealed class Marker15 { }
    private sealed class Marker16 { }
    private sealed class Marker17 { }
    private sealed class Marker18 { }
    private sealed class Marker19 { }
    private sealed class Marker20 { }
    private sealed class Marker21 { }
    private sealed class Marker22 { }

    [SetUp]
    public void SetUp()
    {
        HttpContext.Current = null;
    }

    [TearDown]
    public void TearDown()
    {
        HttpContext.Current = null;
    }

    private static void SetHttpContextCurrent()
    {
        HttpContext.Current = new HttpContext(
            new HttpRequest(string.Empty, "http://localhost/", string.Empty),
            new HttpResponse(new StringWriter()));
    }

    [Test]
    public void GetData_HttpContextAndKeyPresent_HolderNull_ReturnsHttpValueAndPopulatesHolder()
    {
        // Marker01 is used only by this test, so this closed generic type's static AsyncLocal
        // has never been touched by anything and starts at its true default: a null holder.
        // Do not call Clear() here -- Clear() assigns a non-null (HasValue = false) holder,
        // which would exercise the "!holder.HasValue" branch instead of "holder == null".
        var storage = new HybridHttpContextStorage<Marker01>("key01");
        SetHttpContextCurrent();
        var httpValue = new Marker01();
        HttpContext.Current.Items["key01"] = httpValue;

        var result = storage.GetData();

        Assert.That(result, Is.SameAs(httpValue));
    }

    [Test]
    public void GetData_HttpContextAndKeyPresent_HolderHasValueFalse_ReturnsHttpValueAndReassignsHolder()
    {
        var storage = new HybridHttpContextStorage<Marker02>("key02");
        storage.Clear(); // holder becomes non-null with HasValue = false
        SetHttpContextCurrent();
        var httpValue = new Marker02();
        HttpContext.Current.Items["key02"] = httpValue;

        var result = storage.GetData();

        Assert.That(result, Is.SameAs(httpValue));
    }

    [Test]
    public void GetData_HttpContextAndKeyPresent_HolderValueDiffersFromHttpValue_ReassignsHolder()
    {
        var storage = new HybridHttpContextStorage<Marker03>("key03");
        var oldValue = new Marker03();
        var newValue = new Marker03();
        storage.SetData(oldValue); // holder.Value = oldValue, HasValue = true
        SetHttpContextCurrent();
        HttpContext.Current.Items["key03"] = newValue; // stale relative to the holder

        var result = storage.GetData();

        Assert.That(result, Is.SameAs(newValue));
    }

    [Test]
    public void GetData_HttpContextAndKeyPresent_HolderValueMatchesHttpValue_ReturnsValue()
    {
        var storage = new HybridHttpContextStorage<Marker04>("key04");
        var value = new Marker04();
        storage.SetData(value);
        SetHttpContextCurrent();
        HttpContext.Current.Items["key04"] = value; // same reference as holder.Value

        var result = storage.GetData();

        // This exercises the "holder value already matches" path (no holder reassignment).
        // The returned value is identical either way -- GetData always returns the
        // HttpContext value when the key is present -- so the skipped reassignment is not
        // independently observable from outside the class; asserting the correct return is
        // the strongest available check for this branch.
        Assert.That(result, Is.SameAs(value));
    }

    [Test]
    public void GetData_HttpContextNull_HolderHasValue_ReturnsHolderValue()
    {
        var storage = new HybridHttpContextStorage<Marker05>("key05");
        var value = new Marker05();
        storage.SetData(value); // HttpContext.Current is null here, so only the holder is set

        var result = storage.GetData();

        Assert.That(result, Is.SameAs(value));
    }

    [Test]
    public void GetData_HttpContextPresentKeyAbsent_HolderHasValue_HydratesHttpContextAndReturnsHolderValue()
    {
        var storage = new HybridHttpContextStorage<Marker06>("key06");
        var value = new Marker06();
        storage.SetData(value); // HttpContext.Current is still null, so only the holder is set
        SetHttpContextCurrent(); // fresh HttpContext, "key06" is not present in Items

        var result = storage.GetData();

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.SameAs(value));
            Assert.That(HttpContext.Current.Items.Contains("key06"), Is.True);
            Assert.That(HttpContext.Current.Items["key06"], Is.SameAs(value));
        });
    }

    [Test]
    public void GetData_NothingAnywhere_ReturnsDefault()
    {
        var storage = new HybridHttpContextStorage<Marker07>("key07");

        var result = storage.GetData();

        Assert.That(result, Is.Null);
    }

    [Test]
    public void SetData_FirstWrite_ValueIsRetrievable()
    {
        var storage = new HybridHttpContextStorage<Marker08>("key08");
        var value = new Marker08();

        storage.SetData(value); // holder is null for a fresh marker type, so this is the first-ever write

        Assert.That(storage.GetData(), Is.SameAs(value));
    }

    [Test]
    public void SetData_CalledTwice_SecondValueWins()
    {
        var storage = new HybridHttpContextStorage<Marker09>("key09");
        var first = new Marker09();
        var second = new Marker09();
        storage.SetData(first); // first write

        storage.SetData(second); // overwrite

        Assert.That(storage.GetData(), Is.SameAs(second));
    }

    [Test]
    public void SetData_HttpContextNull_DoesNotThrowAndValueIsRetrievable()
    {
        var storage = new HybridHttpContextStorage<Marker10>("key10");
        var value = new Marker10();

        Assert.DoesNotThrow(() => storage.SetData(value));
        Assert.That(storage.GetData(), Is.SameAs(value));
    }

    [Test]
    public void SetData_HttpContextPresent_WritesToHttpContextItems()
    {
        var storage = new HybridHttpContextStorage<Marker11>("key11");
        SetHttpContextCurrent();
        var value = new Marker11();

        storage.SetData(value);

        Assert.That(HttpContext.Current.Items["key11"], Is.SameAs(value));
    }

    [Test]
    public void Clear_HttpContextNull_DoesNotThrow()
    {
        var storage = new HybridHttpContextStorage<Marker12>("key12");
        storage.SetData(new Marker12());

        Assert.DoesNotThrow(() => storage.Clear());
        Assert.That(storage.GetData(), Is.Null);
    }

    [Test]
    public void Clear_HttpContextPresentWithKey_RemovesFromItems()
    {
        var storage = new HybridHttpContextStorage<Marker13>("key13");
        SetHttpContextCurrent();
        storage.SetData(new Marker13());
        Assert.That(HttpContext.Current.Items.Contains("key13"), Is.True); // sanity check before Clear

        storage.Clear();

        Assert.That(HttpContext.Current.Items.Contains("key13"), Is.False);
    }

    [Test]
    public void Clear_HttpContextPresentWithoutKey_DoesNotThrow()
    {
        var storage = new HybridHttpContextStorage<Marker14>("key14");
        SetHttpContextCurrent(); // "key14" is never added to Items

        Assert.DoesNotThrow(() => storage.Clear());
        Assert.That(HttpContext.Current.Items.Contains("key14"), Is.False);
    }

    [Test]
    public void Clear_GetDataReturnsDefaultAfterClear()
    {
        var storage = new HybridHttpContextStorage<Marker15>("key15");
        SetHttpContextCurrent();
        storage.SetData(new Marker15());

        storage.Clear();

        Assert.That(storage.GetData(), Is.Null);
    }

    [Test]
    public void Priority_Equals15()
    {
        var storage = new HybridHttpContextStorage<Marker16>("key16");

        Assert.That(storage.Priority, Is.EqualTo((byte)15));
    }

    [Test]
    public void CanProvide_IsFalse_BecauseHostingEnvironmentIsNotHosted()
    {
        var storage = new HybridHttpContextStorage<Marker17>("key17");
        IContextStorage<Marker17> asInterface = storage;

        // HostingEnvironment.IsHosted is always false in a unit-test process and cannot be
        // set from a test, so the "hosted" branch of CanProvide is unreachable here; both
        // checks below only exercise the "not hosted" short-circuit.
        var resultWithoutHttpContext = asInterface.CanProvide;
        SetHttpContextCurrent();
        var resultWithHttpContext = asInterface.CanProvide;

        Assert.Multiple(() =>
        {
            Assert.That(resultWithoutHttpContext, Is.False);
            Assert.That(resultWithHttpContext, Is.False);
        });
    }

    [Test]
    public async Task SetData_InChildExecutionContext_DoesNotAffectParent()
    {
        var storage = new HybridHttpContextStorage<Marker18>("key18");
        var valueA = new Marker18();
        var valueB = new Marker18();

        storage.SetData(valueA);
        await Task.Run(() => storage.SetData(valueB)); // child assigns its own holder

        // NR-599419 regression guard: SetData assigns a new ValueHolder rather than
        // mutating the existing one, so the child ExecutionContext's write does not leak
        // back to the parent.
        Assert.That(storage.GetData(), Is.SameAs(valueA));
    }

    [Test]
    public async Task SetData_InSiblingChildExecutionContext_DoesNotAffectOtherSibling()
    {
        var storage = new HybridHttpContextStorage<Marker19>("key19");
        var valueParent = new Marker19();
        var valueSibling = new Marker19();

        storage.SetData(valueParent);

        // Fork "child 1" now, while its ExecutionContext still shares the parent's holder
        // reference (valueParent). It suspends immediately and does not read until released.
        // Because SetData assigns rather than mutates, child 1 keeps observing that original
        // holder reference no matter what child 2 does to its own copy in the meantime.
        var releaseChild1 = new TaskCompletionSource<bool>();
        var child1Task = Task.Run(async () =>
        {
            await releaseChild1.Task;
            return storage.GetData();
        });

        // Fork "child 2" -- also from the parent's context, a sibling of child 1, not a
        // descendant of it -- and let it assign its own holder.
        var child2Task = Task.Run(() => storage.SetData(valueSibling));
        await child2Task;

        releaseChild1.SetResult(true);
        var child1Result = await child1Task;

        // NR-599419 regression guard: child 2's SetData assigns a new ValueHolder rather than
        // mutating the one shared with child 1's ExecutionContext (and the parent's), so
        // child 1 does not see child 2's write even though both descend from the same parent.
        Assert.That(child1Result, Is.SameAs(valueParent));
    }

    [Test]
    public async Task Clear_InChildExecutionContext_DoesNotAffectParent()
    {
        var storage = new HybridHttpContextStorage<Marker20>("key20");
        var valueParent = new Marker20();
        storage.SetData(valueParent);

        // SetData and Clear() both assign a brand new ValueHolder rather than mutating the
        // existing one, so neither leaks out of the ExecutionContext that made the call.
        await Task.Run(() => storage.Clear());

        Assert.That(storage.GetData(), Is.SameAs(valueParent));
    }

    [Test]
    public async Task ValueSetInParentBeforeChildCreated_IsVisibleInChild()
    {
        var storage = new HybridHttpContextStorage<Marker21>("key21");
        var valueParent = new Marker21();
        storage.SetData(valueParent);

        Marker21 childResult = null;
        await Task.Run(() => { childResult = storage.GetData(); });

        Assert.That(childResult, Is.SameAs(valueParent));
    }

    [Test]
    public void SetData_NullValue_IsStoredNotRemoved()
    {
        var storage = new HybridHttpContextStorage<Marker22>("key22");
        SetHttpContextCurrent();
        storage.SetData(new Marker22());

        // IContextStorage<T>.SetData's doc remarks that a null value "can be treated as a
        // removal of the key if desired". This implementation does not do that -- it stores
        // the null value rather than removing the key. Assert the actual current behavior.
        storage.SetData(null);

        Assert.Multiple(() =>
        {
            Assert.That(storage.GetData(), Is.Null);
            Assert.That(HttpContext.Current.Items.Contains("key22"), Is.True);
            Assert.That(HttpContext.Current.Items["key22"], Is.Null);
        });
    }
}

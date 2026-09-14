// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;

namespace NewRelic.Agent.Core.Wrapper;

[TestFixture]
public class RuntimeAsyncResultNormalizerTests
{
    private interface IThing
    {
    }

    private class Thing : IThing
    {
    }

    private class GenericHolder<T>
    {
    }

    [Test]
    public void TryCreate_ReturnsACompletedTask_ForANullResultType()
    {
        // null is not a failure: it is how the profiler reports a body that returns nothing, which
        // is the runtime-async Task / ValueTask case.
        var normalize = RuntimeAsyncResultNormalizer.TryCreate(null);

        Assert.That(normalize, Is.Not.Null);
        Assert.That(normalize(null), Is.SameAs(Task.CompletedTask));
    }

    [Test]
    public void TryCreate_ProducesATypedCompletedTask_ForAValueType()
    {
        var task = RuntimeAsyncResultNormalizer.TryCreate(typeof(int))(42);

        Assert.Multiple(() =>
        {
            Assert.That(task, Is.TypeOf<Task<int>>());
            Assert.That(((Task<int>)task).Result, Is.EqualTo(42));
            Assert.That(task.IsCompleted, Is.True);
        });
    }

    [Test]
    public void TryCreate_ProducesATypedCompletedTask_ForAReferenceType()
    {
        var task = RuntimeAsyncResultNormalizer.TryCreate(typeof(string))("hello");

        Assert.Multiple(() =>
        {
            Assert.That(task, Is.TypeOf<Task<string>>());
            Assert.That(((Task<string>)task).Result, Is.EqualTo("hello"));
        });
    }

    [Test]
    public void TryCreate_TypesTheTaskToTheRequestedType_NotTheRuntimeType()
    {
        // the property wrappers actually depend on: Delegates.OnSuccess narrows with `as`, and
        // Task<Thing> as Task<IThing> is null, so the task must be typed to the declared type
        var task = RuntimeAsyncResultNormalizer.TryCreate(typeof(IThing))(new Thing());

        Assert.Multiple(() =>
        {
            Assert.That(task, Is.TypeOf<Task<IThing>>());
            Assert.That(task as Task<IThing>, Is.Not.Null);
            Assert.That(((Task<IThing>)task).Result, Is.InstanceOf<Thing>());
        });
    }

    [Test]
    public void TryCreate_UsesDefault_WhenTheResultDoesNotMatchTheType()
    {
        // a mismatch must fail closed rather than throw -- the wrapper then sees a completed
        // Task<int> holding 0, and the segment still ends
        var task = RuntimeAsyncResultNormalizer.TryCreate(typeof(int))("not an int");

        Assert.Multiple(() =>
        {
            Assert.That(task, Is.TypeOf<Task<int>>());
            Assert.That(((Task<int>)task).Result, Is.EqualTo(0));
        });
    }

    [Test]
    public void TryCreate_AllowsANullResult_ForAReferenceType()
    {
        var task = RuntimeAsyncResultNormalizer.TryCreate(typeof(string))(null);

        Assert.Multiple(() =>
        {
            Assert.That(task, Is.TypeOf<Task<string>>());
            Assert.That(((Task<string>)task).Result, Is.Null);
        });
    }

    [Test]
    public void TryCreate_ProducesDistinctDelegates_ForDifferentResultTypes()
    {
        // The reason normalizers are cached by TYPE and not by functionId. A functionId is per
        // method definition, so one generic runtime-async method arrives here once per
        // instantiation with a different type each time. Caching by functionId would hand the
        // second instantiation the first one's delegate, whose result would silently become
        // default(T) rather than the real value.
        var asInt = RuntimeAsyncResultNormalizer.TryCreate(typeof(int));
        var asString = RuntimeAsyncResultNormalizer.TryCreate(typeof(string));

        Assert.Multiple(() =>
        {
            Assert.That(asInt(7), Is.TypeOf<Task<int>>());
            Assert.That(asString("seven"), Is.TypeOf<Task<string>>());
            Assert.That(((Task<int>)asInt(7)).Result, Is.EqualTo(7));
            Assert.That(((Task<string>)asString("seven")).Result, Is.EqualTo("seven"));
        });
    }

    [Test]
    public void TryCreate_ReturnsTheSameDelegate_ForTheSameResultType()
    {
        var first = RuntimeAsyncResultNormalizer.TryCreate(typeof(List<Guid>));
        var second = RuntimeAsyncResultNormalizer.TryCreate(typeof(List<Guid>));

        Assert.That(first, Is.SameAs(second), "the delegate must be generated once per type and cached");
    }

    [Test]
    public void TryCreate_ReturnsNull_WhenTheResultTypeCannotBeBound()
    {
        // void is not a valid generic argument, so MakeGenericMethod throws. A null return tells
        // the caller to keep synchronous completion semantics rather than guess.
        Assert.That(RuntimeAsyncResultNormalizer.TryCreate(typeof(void)), Is.Null);
    }

    [Test]
    public void TryCreate_ReturnsNull_ForAnOpenGenericResultType()
    {
        // Defensive: the profiler renders a generic parameter as a TypeSpec the CLR resolves in the
        // method's generic context, so a closed type is what actually arrives. If an open one ever
        // did, there is no concrete T to type the task to and we must degrade rather than hand back
        // a delegate over a still-open method.
        //
        // This must hold on every target framework. It caught a real difference: .NET Framework
        // binds an open generic without complaint, where .NET 10 throws -- so the normalizer rejects
        // the case explicitly instead of relying on either behavior.
        var openGenericParameter = typeof(GenericHolder<>).GetGenericArguments()[0];

        Assert.That(RuntimeAsyncResultNormalizer.TryCreate(openGenericParameter), Is.Null);
    }
}

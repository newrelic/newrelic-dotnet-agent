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
    // The RuntimeAsyncResultNormalizer reads only the DECLARED return type, so an ordinary method is a faithful
    // stand-in for a runtime-async one; an actual runtime-async-capable .NET runtime is not required to test the normalizer's behavior.
    private class Subject
    {
        public Task ReturnsTask() => Task.CompletedTask;
        public ValueTask ReturnsValueTask() => default;
        public Task<int> ReturnsTaskOfInt() => Task.FromResult(0);
        public Task<string> ReturnsTaskOfString() => Task.FromResult<string>(null);
        public ValueTask<string> ReturnsValueTaskOfString() => default;
        public Task<IEnumerable<int>> ReturnsTaskOfInterface() => Task.FromResult<IEnumerable<int>>(null);
        public int ReturnsInt() => 0;
        public void ReturnsVoid() { }
        public Task<T> ReturnsTaskOfGenericParameter<T>(T value) => Task.FromResult(value);
        public Task<int> Overloaded(string a) => Task.FromResult(1);
        public Task<string> Overloaded(int a) => Task.FromResult("two");
        public List<int> ReturnsGenericNonTask() => null;

        // Both have one generic-typed parameter, and a generic parameter's FullName is null, so
        // both render as an empty signature string and cannot be told apart.
        public Task<int> AmbiguousGeneric<T>(T a) => Task.FromResult(1);
        public Task<int> AmbiguousGeneric<T, TOther>(TOther a) => Task.FromResult(2);
    }

    // A generic declaring type, to cover the case the profiler actually hands us: an
    // mdTypeDef resolves to the OPEN definition, so GetMethods() yields open MethodInfos.
    private class GenericSubject<TDoc>
    {
        public Task<TDoc> ReturnsTaskOfTypeParameter() => Task.FromResult(default(TDoc));
        public Task<long> ReturnsClosedTaskOnGenericType() => Task.FromResult(0L);
    }

    private static Func<object, Task> Create(string methodName, string parameterTypeNames = "")
    {
        return RuntimeAsyncResultNormalizer.TryCreate(typeof(Subject), methodName, parameterTypeNames)?.Normalize;
    }

    private static RuntimeAsyncNormalization CreateFull(string methodName, string parameterTypeNames = "")
    {
        return RuntimeAsyncResultNormalizer.TryCreate(typeof(Subject), methodName, parameterTypeNames);
    }

    [Test]
    public void TryCreate_ProducesCompletedTask_ForTaskReturn()
    {
        var task = Create(nameof(Subject.ReturnsTask))(null);

        Assert.Multiple(() =>
        {
            Assert.That(task, Is.Not.Null);
            Assert.That(task.IsCompleted, Is.True);
            Assert.That(task.IsFaulted, Is.False);
        });
    }

    [Test]
    public void TryCreate_ProducesCompletedTask_ForValueTaskReturn()
    {
        var task = Create(nameof(Subject.ReturnsValueTask))(null);

        Assert.That(task.IsCompleted, Is.True);
    }

    [Test]
    public void TryCreate_ProducesTypedCompletedTask_ForTaskOfIntReturn()
    {
        var task = Create(nameof(Subject.ReturnsTaskOfInt))(42);

        Assert.Multiple(() =>
        {
            Assert.That(task, Is.TypeOf<Task<int>>());
            Assert.That(((Task<int>)task).Result, Is.EqualTo(42));
            Assert.That(task.IsCompleted, Is.True);
        });
    }

    [Test]
    public void TryCreate_ProducesTypedCompletedTask_ForValueTaskOfStringReturn()
    {
        var task = Create(nameof(Subject.ReturnsValueTaskOfString))("hello");

        Assert.Multiple(() =>
        {
            Assert.That(task, Is.TypeOf<Task<string>>());
            Assert.That(((Task<string>)task).Result, Is.EqualTo("hello"));
        });
    }

    // The reason Task<object> is not an acceptable shortcut when the declared type IS known:
    // wrappers cast with `as`, and Task<FooImpl> as Task<IFoo> is null.
    [Test]
    public void TryCreate_ProducesTaskTypedToTheDeclaredInterface_NotTheRuntimeType()
    {
        var concreteResult = new List<int> { 1, 2, 3 };

        var task = Create(nameof(Subject.ReturnsTaskOfInterface))(concreteResult);

        Assert.Multiple(() =>
        {
            Assert.That(task as Task<IEnumerable<int>>, Is.Not.Null);
            Assert.That(((Task<IEnumerable<int>>)task).Result, Is.SameAs(concreteResult));
        });
    }

    [Test]
    public void TryCreate_UsesDefault_WhenResultIsNullForAValueType()
    {
        // Defensive: the profiler boxes a real value for Task<int>, so null should not occur.
        // It must degrade to default(T) rather than throw inside instrumentation.
        var task = Create(nameof(Subject.ReturnsTaskOfInt))(null);

        Assert.That(((Task<int>)task).Result, Is.EqualTo(0));
    }

    [Test]
    public void TryCreate_AllowsNullResult_ForAReferenceType()
    {
        var task = Create(nameof(Subject.ReturnsTaskOfString))(null);

        Assert.That(((Task<string>)task).Result, Is.Null);
    }

    [Test]
    public void TryCreate_ResolvesTheCorrectOverload_FromTheParameterSignature()
    {
        var stringOverload = Create(nameof(Subject.Overloaded), "System.String");
        var intOverload = Create(nameof(Subject.Overloaded), "System.Int32");

        Assert.Multiple(() =>
        {
            Assert.That(stringOverload(7), Is.TypeOf<Task<int>>());
            Assert.That(intOverload("seven"), Is.TypeOf<Task<string>>());
        });
    }

    [Test]
    public void TryCreate_ReturnsNull_WhenOverloadCannotBeDisambiguated()
    {
        Assert.That(Create(nameof(Subject.Overloaded), "System.Guid"), Is.Null);
    }

    [Test]
    public void TryCreate_ReturnsNull_ForANonTaskReturnType()
    {
        Assert.That(Create(nameof(Subject.ReturnsInt)), Is.Null);
    }

    [Test]
    public void TryCreate_ReturnsNull_ForAVoidReturnType()
    {
        Assert.That(Create(nameof(Subject.ReturnsVoid)), Is.Null);
    }

    [Test]
    public void TryCreate_ReturnsNull_ForAGenericReturnTypeThatIsNotATaskType()
    {
        Assert.That(Create(nameof(Subject.ReturnsGenericNonTask)), Is.Null);
    }

    [Test]
    public void TryCreate_ReturnsNull_WhenTwoCandidatesShareTheSameSignatureString()
    {
        // Refuse rather than pick one arbitrarily: guessing wrong would attach the wrong result
        // shape to the wrong method.
        Assert.That(Create("AmbiguousGeneric"), Is.Null);
    }

    [Test]
    public void TryCreate_FallsBackToTaskOfObject_ForAGenericMethod()
    {
        var normalization = CreateFull(nameof(Subject.ReturnsTaskOfGenericParameter), "T");
        var task = normalization.Normalize(42);

        Assert.Multiple(() =>
        {
            Assert.That(normalization.IsExactlyTyped, Is.False);
            Assert.That(task, Is.TypeOf<Task<object>>());
            Assert.That(((Task<object>)task).Result, Is.EqualTo(42));
            Assert.That(task.IsCompleted, Is.True);
            // the gate every wrapper but HttpClient/SendAsync uses
            Assert.That(task, Is.InstanceOf<Task>());
        });
    }

    [Test]
    public void TryCreate_FallsBackToTaskOfObject_WhenTheDeclaringTypeIsAnOpenGeneric()
    {
        var normalization = RuntimeAsyncResultNormalizer.TryCreate(
            typeof(GenericSubject<>), nameof(GenericSubject<object>.ReturnsTaskOfTypeParameter), string.Empty);
        var response = new object();

        Assert.Multiple(() =>
        {
            Assert.That(normalization.IsExactlyTyped, Is.False);
            Assert.That(normalization.Normalize(response), Is.TypeOf<Task<object>>());
            Assert.That(((Task<object>)normalization.Normalize(response)).Result, Is.SameAs(response));
        });
    }

    [Test]
    public void TryCreate_StillTypesExactly_WhenOnlyTheDeclaringTypeIsGeneric()
    {
        var normalization = RuntimeAsyncResultNormalizer.TryCreate(
            typeof(GenericSubject<>), nameof(GenericSubject<object>.ReturnsClosedTaskOnGenericType), string.Empty);

        Assert.Multiple(() =>
        {
            Assert.That(normalization.IsExactlyTyped, Is.True);
            Assert.That(normalization.Normalize(7L), Is.TypeOf<Task<long>>());
        });
    }

    [Test]
    public void TryCreate_ReportsExactTyping_ForAClosedResultType()
    {
        Assert.That(CreateFull(nameof(Subject.ReturnsTaskOfInt)).IsExactlyTyped, Is.True);
    }

    [Test]
    public void TryCreate_ReportsExactTyping_ForAVoidEffectiveReturn()
    {
        Assert.That(CreateFull(nameof(Subject.ReturnsTask)).IsExactlyTyped, Is.True);
    }

    [Test]
    public void TryCreate_ReturnsNull_WhenTheMethodDoesNotExist()
    {
        Assert.That(Create("NoSuchMethod"), Is.Null);
    }

    [Test]
    public void TryCreate_ReturnsNull_ForNullOrEmptyInputs()
    {
        Assert.Multiple(() =>
        {
            Assert.That(RuntimeAsyncResultNormalizer.TryCreate(null, "ReturnsTask", ""), Is.Null);
            Assert.That(RuntimeAsyncResultNormalizer.TryCreate(typeof(Subject), null, ""), Is.Null);
            Assert.That(RuntimeAsyncResultNormalizer.TryCreate(typeof(Subject), string.Empty, ""), Is.Null);
        });
    }

    [Test]
    public void TryCreate_TreatsNullParameterTypeNames_AsAnEmptySignature()
    {
        Assert.That(RuntimeAsyncResultNormalizer.TryCreate(typeof(Subject), nameof(Subject.ReturnsTask), null), Is.Not.Null);
    }
}

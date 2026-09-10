// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
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

        // Both have one generic-typed parameter. They render as !!0 and !!1, neither of which is
        // what a caller passing an empty signature is looking for -- but both return Task<int>, so
        // the result-shape fallback can still answer.
        public Task<int> AmbiguousGeneric<T>(T a) => Task.FromResult(1);
        public Task<int> AmbiguousGeneric<T, TOther>(TOther a) => Task.FromResult(2);

        // Overload groups exercising one parameter-type rendering each. Every group pairs a
        // non-trivially-rendered overload with a plain string one, so resolution has to pick
        // correctly rather than fall through to the result-shape agreement path -- the two
        // overloads in each group deliberately return DIFFERENT task types so a wrong pick shows.
        public Task<int> Rendered(List<int> a) => Task.FromResult(1);
        public Task<string> Rendered(string a) => Task.FromResult("s");

        public Task<int> Nested(Dictionary<string, int> a) => Task.FromResult(1);
        public Task<string> Nested(string a) => Task.FromResult("s");

        public Task<int> WithArray(int[] a) => Task.FromResult(1);
        public Task<string> WithArray(string a) => Task.FromResult("s");

        public Task<int> WithByRef(ref int a) => Task.FromResult(1);
        public Task<string> WithByRef(string a) => Task.FromResult("s");

        public Task<int> WithMethodGeneric<T>(T a) => Task.FromResult(1);
        public Task<string> WithMethodGeneric(string a) => Task.FromResult("s");

        public Task<int> GenericArray(List<int>[] a) => Task.FromResult(1);
        public Task<string> GenericArray(string a) => Task.FromResult("s");

        // Same result shape on both overloads, so the agreement fallback can answer even when the
        // signature matches neither.
        public Task<int> Agreeing(List<int> a) => Task.FromResult(1);
        public Task<int> Agreeing(string a) => Task.FromResult(2);

        // One overload is not a task type at all, so the shapes disagree.
        public Task<int> MixedTaskAndNot(List<int> a) => Task.FromResult(1);
        public int MixedTaskAndNot(string a) => 2;
    }

    // Overloads on a GENERIC declaring type, so a parameter can be the type's own generic
    // parameter -- rendered !0 rather than a method's !!0.
    private class GenericOverloadSubject<TDoc>
    {
        public Task<int> Save(TDoc a) => Task.FromResult(1);
        public Task<string> Save(string a) => Task.FromResult("s");
    }

    // A generic declaring type, to cover the case the profiler actually hands us: an
    // mdTypeDef resolves to the OPEN definition, so GetMethods() yields open MethodInfos.
    private class GenericSubject<TDoc>
    {
        public Task<TDoc> ReturnsTaskOfTypeParameter() => Task.FromResult(default(TDoc));
        public Task<long> ReturnsClosedTaskOnGenericType() => Task.FromResult(0L);
    }

    // A real Type whose member reflection throws, the way the CLR does when a member signature
    // references an assembly that cannot be loaded. TypeDelegator forwards everything else to the
    // wrapped type, so this fails exactly where a customer type with a missing optional dependency
    // would, and nowhere else.
    private class ReflectionHostileType : TypeDelegator
    {
        private readonly Exception _toThrow;

        public ReflectionHostileType(Exception toThrow) : base(typeof(Subject))
        {
            _toThrow = toThrow;
        }

        public override MethodInfo[] GetMethods(BindingFlags bindingAttr)
        {
            throw _toThrow;
        }
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
        // Nothing renders as System.Guid, and the two Overloaded candidates return Task<int> and
        // Task<string>, so the result-shape fallback cannot answer either.
        Assert.That(Create(nameof(Subject.Overloaded), "System.Guid"), Is.Null);
    }

    // The profiler renders parameter types via MethodSignature::ToString in
    // SignatureParser/Types.h, NOT the way Type.FullName does. The two agree for primitives,
    // arrays and by-ref, and disagree for anything generic: Types.h emits List`1[System.Int32]
    // while FullName emits List`1[[System.Int32, <assembly>, Version=...]]. These cases pin each
    // rendering that the resolver has to reproduce to identify an overload at all.

    [Test]
    public void TryCreate_MatchesAClosedGenericParameterType()
    {
        var normalization = CreateFull(nameof(Subject.Rendered), "System.Collections.Generic.List`1[System.Int32]");

        Assert.Multiple(() =>
        {
            Assert.That(normalization, Is.Not.Null, "the List<int> overload should have been identified");
            Assert.That(normalization.Normalize(1), Is.TypeOf<Task<int>>(), "picked the wrong overload");
        });
    }

    [Test]
    public void TryCreate_MatchesTheSiblingOfAGenericParameterType()
    {
        // The other half of the pair: the plainly-rendered overload must still resolve to itself.
        var normalization = CreateFull(nameof(Subject.Rendered), "System.String");

        Assert.That(normalization.Normalize("s"), Is.TypeOf<Task<string>>());
    }

    [Test]
    public void TryCreate_MatchesANestedGenericParameterType()
    {
        var normalization = CreateFull(nameof(Subject.Nested),
            "System.Collections.Generic.Dictionary`2[System.String,System.Int32]");

        Assert.That(normalization.Normalize(1), Is.TypeOf<Task<int>>());
    }

    [Test]
    public void TryCreate_MatchesAnArrayParameterType()
    {
        var normalization = CreateFull(nameof(Subject.WithArray), "System.Int32[]");

        Assert.That(normalization.Normalize(1), Is.TypeOf<Task<int>>());
    }

    [Test]
    public void TryCreate_MatchesAByRefParameterType()
    {
        var normalization = CreateFull(nameof(Subject.WithByRef), "System.Int32&");

        Assert.That(normalization.Normalize(1), Is.TypeOf<Task<int>>());
    }

    [Test]
    public void TryCreate_MatchesAnArrayOfAClosedGenericParameterType()
    {
        var normalization = CreateFull(nameof(Subject.GenericArray),
            "System.Collections.Generic.List`1[System.Int32][]");

        Assert.That(normalization.Normalize(1), Is.TypeOf<Task<int>>());
    }

    [Test]
    public void TryCreate_MatchesAMethodsOwnGenericParameter()
    {
        // MvarType renders as !!N.
        var normalization = CreateFull(nameof(Subject.WithMethodGeneric), "!!0");

        Assert.That(normalization.Normalize(1), Is.TypeOf<Task<int>>());
    }

    [Test]
    public void TryCreate_MatchesTheDeclaringTypesGenericParameter()
    {
        // VarType renders as !N -- one bang, not two.
        var normalization = RuntimeAsyncResultNormalizer.TryCreate(typeof(GenericOverloadSubject<>), "Save", "!0");

        Assert.Multiple(() =>
        {
            Assert.That(normalization, Is.Not.Null);
            Assert.That(normalization.Normalize(1), Is.TypeOf<Task<int>>());
        });
    }

    [Test]
    public void TryCreate_UsesResultShapeAgreement_WhenNoCandidateMatchesTheSignature()
    {
        // Both Agreeing overloads return Task<int>, so which one this is cannot change the
        // normalizer. Refusing here would drop a genuinely async method to synchronous completion
        // semantics for no reason.
        var normalization = CreateFull(nameof(Subject.Agreeing), "System.Guid");

        Assert.Multiple(() =>
        {
            Assert.That(normalization, Is.Not.Null);
            Assert.That(normalization.Normalize(7), Is.TypeOf<Task<int>>());
            Assert.That(normalization.IsExactlyTyped, Is.True);
        });
    }

    [Test]
    public void TryCreate_UsesResultShapeAgreement_ForIndistinguishableGenericOverloads()
    {
        // These two are the case that used to be refused outright.
        var normalization = CreateFull("AmbiguousGeneric");

        Assert.Multiple(() =>
        {
            Assert.That(normalization, Is.Not.Null);
            Assert.That(normalization.Normalize(7), Is.TypeOf<Task<int>>());
        });
    }

    [Test]
    public void TryCreate_ReturnsNull_WhenOneCandidateIsNotATaskTypeAtAll()
    {
        // Agreement has to mean agreement: a non-task sibling is a disagreement, not something to
        // average over.
        Assert.That(Create(nameof(Subject.MixedTaskAndNot), "System.Guid"), Is.Null);
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

    // The reflection here runs outside the try in WrapperService.BeforeWrappedMethod, so an escaping
    // exception would reach AgentShim.GetTracer, which swallows it and returns a null tracer --
    // leaving the functionId uncached and re-reflecting on every subsequent call.
    [Test]
    public void TryCreate_ReturnsNull_WhenMemberReflectionThrows()
    {
        var declaringType = new ReflectionHostileType(new TypeLoadException("could not load a parameter type"));

        RuntimeAsyncNormalization normalization = null;
        Assert.DoesNotThrow(() => normalization = RuntimeAsyncResultNormalizer.TryCreate(declaringType, nameof(Subject.ReturnsTaskOfInt), string.Empty));
        Assert.That(normalization, Is.Null);
    }

    [Test]
    public void TryCreate_ReturnsNull_WhenAnAssemblyCannotBeLoadedDuringReflection()
    {
        // Not filtered by exception type: a missing optional dependency surfaces as
        // FileNotFoundException rather than TypeLoadException, and must degrade the same way.
        var declaringType = new ReflectionHostileType(new FileNotFoundException("optional dependency is not present"));

        RuntimeAsyncNormalization normalization = null;
        Assert.DoesNotThrow(() => normalization = RuntimeAsyncResultNormalizer.TryCreate(declaringType, nameof(Subject.ReturnsTaskOfInt), string.Empty));
        Assert.That(normalization, Is.Null);
    }
}

// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using System.Threading.Tasks;

namespace NewRelic.Agent.Core.OtelDistro;

// Fixture types for DistroMethodResolverTests and DistroSignatureFormatterTests. The method bodies are never run;
// only their metadata (name, parameters, AsyncStateMachineAttribute) matters.

public class ResolverSingleAsync
{
    public async Task Run(int value) => await Task.Yield();
}

public class ResolverTaskReturningNonAsync
{
    public Task Run(int value) => Task.CompletedTask;
}

public class ResolverSameArity
{
    public async Task M(CancellationToken token) => await Task.Yield();

    public Task M(int value) => Task.CompletedTask;

    public void M() { }

    public void M(int a, int b) { }

    public void Other(int value) { }
}

public class ResolverGenericMethod
{
    public async Task Insert<T>(string id, T content) => await Task.Yield();
}

public class ResolverByRef
{
    public Task Read(ref int value) => Task.CompletedTask;
}

public ref struct ResolverRefStruct
{
    public int Value;
}

public class ResolverRefStructParameter
{
    public Task Write(ResolverRefStruct value) => Task.CompletedTask;
}

public class ResolverBclRefStructParameter
{
    public Task Write(ReadOnlySpan<byte> data) => Task.CompletedTask;
}

public class ResolverRefToRefStructParameter
{
    public Task Write(ref ResolverRefStruct value) => Task.CompletedTask;
}

public static class ResolverStatic
{
    public static async Task Run(string value) => await Task.Yield();
}

public struct ResolverValueType
{
    public async Task Run(int value) => await Task.Yield();
}

public class ResolverGeneric<T>
{
    public async Task M(T value) => await Task.Yield();
}

public class ResolverNonGenericBase
{
}

public class ResolverGenericDerived<T> : ResolverNonGenericBase
{
    public Task M(T value) => Task.CompletedTask;
}

public class ResolverOuter<T>
{
    public class Inner
    {
        public async Task M(T value) => await Task.Yield();
    }
}

public class ResolverAssemblyCopyArgument
{
}

public class ResolverAssemblyCopy
{
    public async Task M(ResolverAssemblyCopyArgument argument) => await Task.Yield();
}

public class ResolverAmbiguousAgreeing
{
    public async Task M(int value) => await Task.Yield();

    public async Task M<T>(T value) => await Task.Yield();
}

public class ResolverAmbiguousDisagreeing
{
    public async Task M(int value) => await Task.Yield();

    public Task M(ref int value) => Task.CompletedTask;
}

public class ResolverParameterless
{
    public async Task M() => await Task.Yield();
}

public class FormatterFixture<TClass>
{
    public class Nested
    {
    }

    public class NestedGeneric<TInner>
    {
    }

    public void ClassGeneric(TClass value, IList<TClass> list) { }

    public void MethodGeneric<TMethod>(string id, TMethod content, InsertOptionsFixture options) { }

    public void NestedTypes(Nested nested, NestedGeneric<int> nestedGeneric) { }
}

public class InsertOptionsFixture
{
}

public class FormatterShapes
{
    public void Shipped(IEnumerable<long> ids, CancellationToken token) { }

    public void Dictionary(IDictionary<string, object> values) { }

    public void Arrays(object[] values, int[,] grid, string[][] jagged) { }

    public void ByRef(ref int value, out string text) { text = null; }

    public void Primitives(bool a, char b, byte c, double d, object e, string f) { }

    public void Nullable(int? value, KeyValuePair<string, int?> pair) { }

    public void None() { }
}

/// <summary>Builds dynamic assemblies used to test assembly-copy choice, by-name attribute matching and odd types.</summary>
public static class ResolverDynamicAssemblies
{
    public const string RefStructFullName = "OpenTelemetry.AutoInstrumentation.CallTarget.CallTargetRefStruct";

    private static readonly Lazy<Type> _callTargetRefStruct = new Lazy<Type>(() =>
    {
        var module = DefineModule("ResolverDynamic.CallTarget");
        return module.DefineType(RefStructFullName, TypeAttributes.Public | TypeAttributes.Sealed, typeof(ValueType)).CreateType();
    });

    private static readonly Lazy<Type> _globalNamespaceType = new Lazy<Type>(() =>
    {
        var module = DefineModule("ResolverDynamic.Global");
        return module.DefineType("GlobalNamespaceFixture", TypeAttributes.Public).CreateType();
    });

    /// <summary>A stand-in for OTel's CallTargetRefStruct: same full name, different assembly (Core matches by name).</summary>
    public static Type CallTargetRefStruct => _callTargetRefStruct.Value;

    public static Type GlobalNamespaceType => _globalNamespaceType.Value;

    /// <summary>
    /// An assembly with the given simple name containing <paramref name="typeName"/> with a non-async instance method
    /// <paramref name="methodName"/>(object). Used as a decoy copy of a real assembly.
    /// </summary>
    public static Assembly DefineDecoy(string assemblySimpleName, string typeName, string methodName)
    {
        var module = DefineModule(assemblySimpleName);
        var type = module.DefineType(typeName, TypeAttributes.Public);
        var method = type.DefineMethod(methodName, MethodAttributes.Public, typeof(void), new[] { typeof(object) });
        method.GetILGenerator().Emit(OpCodes.Ret);
        type.CreateType();
        return module.Assembly;
    }

    /// <summary>
    /// An assembly that declares its OWN System.Runtime.CompilerServices.AsyncStateMachineAttribute type and a type
    /// <paramref name="typeName"/> whose parameterless method <paramref name="methodName"/> carries it.
    /// </summary>
    public static Type DefineWithOwnAsyncStateMachineAttribute(string assemblySimpleName, string typeName, string methodName)
    {
        var module = DefineModule(assemblySimpleName);

        var attributeType = module.DefineType("System.Runtime.CompilerServices.AsyncStateMachineAttribute", TypeAttributes.Public | TypeAttributes.Sealed, typeof(Attribute));
        var attributeCtor = attributeType.DefineDefaultConstructor(MethodAttributes.Public);
        attributeType.CreateType();

        var type = module.DefineType(typeName, TypeAttributes.Public);
        var method = type.DefineMethod(methodName, MethodAttributes.Public, typeof(void), Type.EmptyTypes);
        method.GetILGenerator().Emit(OpCodes.Ret);
        method.SetCustomAttribute(new CustomAttributeBuilder(attributeCtor, Array.Empty<object>()));
        return type.CreateType();
    }

    private static ModuleBuilder DefineModule(string assemblySimpleName)
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(assemblySimpleName), AssemblyBuilderAccess.Run);
        return assembly.DefineDynamicModule(assemblySimpleName);
    }
}

/// <summary>An assembly whose type lookup throws, to prove Resolve never lets a reflection failure escape.</summary>
public class ThrowingAssembly : Assembly
{
    private readonly string _simpleName;

    public ThrowingAssembly(string simpleName)
    {
        _simpleName = simpleName;
    }

    public override AssemblyName GetName() => new AssemblyName(_simpleName);

    public override AssemblyName GetName(bool copiedName) => GetName();

    public override Type GetType(string name, bool throwOnError, bool ignoreCase) => throw new BadImageFormatException("boom");
}

/// <summary>A parameter whose type is chosen by the test, for shapes C# cannot declare without unsafe code.</summary>
public class FakeParameter : ParameterInfo
{
    private readonly Type _type;

    public FakeParameter(Type type)
    {
        _type = type;
    }

    public override Type ParameterType => _type;
}

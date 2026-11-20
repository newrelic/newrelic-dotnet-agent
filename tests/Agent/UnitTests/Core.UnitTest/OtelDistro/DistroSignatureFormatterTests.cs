// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Reflection;
using NUnit.Framework;

namespace NewRelic.Agent.Core.OtelDistro;

[TestFixture]
public class DistroSignatureFormatterTests
{
    private static ParameterInfo[] Parameters(Type type, string methodName) => type.GetMethod(methodName).GetParameters();

    private static string Format(params Type[] types)
    {
        var parameters = new ParameterInfo[types.Length];
        for (var i = 0; i < types.Length; i++)
        {
            parameters[i] = new FakeParameter(types[i]);
        }

        return DistroSignatureFormatter.Format(parameters);
    }

    [Test]
    public void Format_NoParameters_IsEmpty()
    {
        Assert.That(DistroSignatureFormatter.Format(Parameters(typeof(FormatterShapes), nameof(FormatterShapes.None))), Is.EqualTo(string.Empty));
    }

    [Test]
    public void Format_Primitives_UseNamespaceAndName()
    {
        Assert.That(DistroSignatureFormatter.Format(Parameters(typeof(FormatterShapes), nameof(FormatterShapes.Primitives))),
            Is.EqualTo("System.Boolean,System.Char,System.Byte,System.Double,System.Object,System.String"));
    }

    [Test]
    public void Format_ShippedCouchbaseShape_MethodGenericParameterIsBangBang()
    {
        Assert.That(DistroSignatureFormatter.Format(Parameters(typeof(FormatterFixture<int>), nameof(FormatterFixture<int>.MethodGeneric))),
            Is.EqualTo("System.String,!!0,NewRelic.Agent.Core.OtelDistro.InsertOptionsFixture"));
    }

    [Test]
    public void Format_ShippedEnumerableAndCancellationToken()
    {
        Assert.That(DistroSignatureFormatter.Format(Parameters(typeof(FormatterShapes), nameof(FormatterShapes.Shipped))),
            Is.EqualTo("System.Collections.Generic.IEnumerable`1[System.Int64],System.Threading.CancellationToken"));
    }

    [Test]
    public void Format_ShippedDictionary_GenericInstantiation()
    {
        Assert.That(DistroSignatureFormatter.Format(Parameters(typeof(FormatterShapes), nameof(FormatterShapes.Dictionary))),
            Is.EqualTo("System.Collections.Generic.IDictionary`2[System.String,System.Object]"));
    }

    [Test]
    public void Format_NestedGenericInstantiation()
    {
        Assert.That(DistroSignatureFormatter.Format(Parameters(typeof(FormatterShapes), nameof(FormatterShapes.Nullable))),
            Is.EqualTo("System.Nullable`1[System.Int32],System.Collections.Generic.KeyValuePair`2[System.String,System.Nullable`1[System.Int32]]"));
    }

    [Test]
    public void Format_ClassGenericParameter_IsBang()
    {
        var parameters = typeof(FormatterFixture<>).GetMethod("ClassGeneric").GetParameters();

        Assert.That(DistroSignatureFormatter.Format(parameters), Is.EqualTo("!0,System.Collections.Generic.IList`1[!0]"));
    }

    [Test]
    public void Format_Arrays_SingleMultiAndJagged()
    {
        Assert.That(DistroSignatureFormatter.Format(Parameters(typeof(FormatterShapes), nameof(FormatterShapes.Arrays))),
            Is.EqualTo("System.Object[],System.Int32[,],System.String[][]"));
    }

    [Test]
    public void Format_ByRefAndOut_AppendAmpersand()
    {
        Assert.That(DistroSignatureFormatter.Format(Parameters(typeof(FormatterShapes), nameof(FormatterShapes.ByRef))),
            Is.EqualTo("System.Int32&,System.String&"));
    }

    [Test]
    public void Format_ByRefOfGenericParameter()
    {
        Assert.That(Format(typeof(ResolverGeneric<>).GetGenericArguments()[0].MakeByRefType()), Is.EqualTo("!0&"));
    }

    [Test]
    public void Format_Pointers_AppendStar_VoidIsLowercase()
    {
        Assert.That(Format(typeof(int).MakePointerType(), typeof(void).MakePointerType()), Is.EqualTo("System.Int32*,void*"));
    }

    [Test]
    public void Format_NestedTypes_UseOwnNameOnly_AsTheProfilerMetadataNameDoes()
    {
        // The profiler formats a parameter type from its TypeDef/TypeRef name, which for a nested type is only the
        // nested type's own name (no namespace, no enclosing type). A type nested in a generic class carries the
        // enclosing class's type parameters as its own generic arguments.
        var parameters = typeof(FormatterFixture<>).GetMethod("NestedTypes").GetParameters();

        Assert.That(DistroSignatureFormatter.Format(parameters), Is.EqualTo("Nested[!0],NestedGeneric`1[!0,System.Int32]"));
    }

    [Test]
    public void Format_GlobalNamespaceType_IsNameOnly()
    {
        Assert.That(Format(ResolverDynamicAssemblies.GlobalNamespaceType), Is.EqualTo("GlobalNamespaceFixture"));
    }
}

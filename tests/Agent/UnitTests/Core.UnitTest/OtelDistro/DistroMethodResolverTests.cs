// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using NewRelic.Agent.Extensions.Logging;
using NUnit.Framework;

namespace NewRelic.Agent.Core.OtelDistro;

[TestFixture]
public class DistroMethodResolverTests
{
    private const ulong SiteFunctionId = 0x1234_5678_9ABC_DEF0ul;

    private DistroRecordingLogger _logger;

    [SetUp]
    public void SetUp()
    {
        DistroMethodResolutionDiagnostics.ResetForTesting();
        DistroConfigMetrics.ResetForTesting();
        _logger = new DistroRecordingLogger();
        Log.Initialize(_logger);
    }

    [TearDown]
    public void TearDown()
    {
        Log.Initialize(new NoOpLogger());
        DistroMethodResolutionDiagnostics.ResetForTesting();
        DistroConfigMetrics.ResetForTesting();
    }

    private static readonly string TestAssemblyName = typeof(DistroMethodResolverTests).Assembly.GetName().Name;

    private static DistroInstrumentationSite Site(Type type, string methodName, string argumentSignature = "", string assemblyName = null) =>
        Site(type.FullName, methodName, argumentSignature, assemblyName ?? type.Assembly.GetName().Name);

    private static DistroInstrumentationSite Site(string typeName, string methodName, string argumentSignature, string assemblyName) =>
        new DistroInstrumentationSite("Factory", "Metric", 0u, assemblyName, typeName, methodName, argumentSignature, SiteFunctionId);

    private static MethodInfo Method(Type type, string name, params Type[] parameterTypes) =>
        type.GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
            .Single(m => m.Name == name && m.GetParameters().Select(p => p.ParameterType).SequenceEqual(parameterTypes));

    private static ulong ExpectedFunctionId(MethodInfo method) =>
        DistroMethodResolver.ComputeFunctionId(SiteFunctionId, method.MetadataToken, method.Module.ModuleVersionId);

    private static void AssertResolved(DistroResolvedMethod result, MethodInfo method, bool isAsync, string signature)
    {
        Assert.Multiple(() =>
        {
            Assert.That(result.Outcome, Is.EqualTo(DistroResolutionOutcome.Resolved));
            Assert.That(result.IsResolved, Is.True);
            Assert.That(result.IsAsync, Is.EqualTo(isAsync));
            Assert.That(result.DeclaringType, Is.EqualTo(method.DeclaringType));
            Assert.That(result.ArgumentSignature, Is.EqualTo(signature));
            Assert.That(result.FunctionId, Is.EqualTo(ExpectedFunctionId(method)));
            Assert.That(result.FunctionId, Is.Not.EqualTo(SiteFunctionId));
        });
    }

    private static void AssertUnresolved(DistroResolvedMethod result, DistroResolutionOutcome outcome, Type targetType, string signature)
    {
        Assert.Multiple(() =>
        {
            Assert.That(result.Outcome, Is.EqualTo(outcome));
            Assert.That(result.IsResolved, Is.False);
            Assert.That(result.IsAsync, Is.False);
            Assert.That(result.DeclaringType, Is.EqualTo(targetType));
            Assert.That(result.ArgumentSignature, Is.EqualTo(signature));
            Assert.That(result.FunctionId, Is.EqualTo(SiteFunctionId));
        });
    }

    [Test]
    public void Resolve_SingleAsyncMethod_IsAsyncTrue()
    {
        var type = typeof(ResolverSingleAsync);

        var result = DistroMethodResolver.Resolve(Site(type, "Run"), type, new[] { typeof(int) });

        AssertResolved(result, Method(type, "Run", typeof(int)), true, "System.Int32");
    }

    [Test]
    public void Resolve_TaskReturningNonAsync_IsAsyncFalse()
    {
        var type = typeof(ResolverTaskReturningNonAsync);

        var result = DistroMethodResolver.Resolve(Site(type, "Run"), type, new[] { typeof(int) });

        AssertResolved(result, Method(type, "Run", typeof(int)), false, "System.Int32");
    }

    [Test]
    public void Resolve_SameArityOverloads_EachGetsItsOwnFlagAndFunctionId()
    {
        var type = typeof(ResolverSameArity);
        var site = Site(type, "M");

        var asyncResult = DistroMethodResolver.Resolve(site, type, new[] { typeof(CancellationToken) });
        var syncResult = DistroMethodResolver.Resolve(site, type, new[] { typeof(int) });

        AssertResolved(asyncResult, Method(type, "M", typeof(CancellationToken)), true, "System.Threading.CancellationToken");
        AssertResolved(syncResult, Method(type, "M", typeof(int)), false, "System.Int32");
        Assert.That(asyncResult.FunctionId, Is.Not.EqualTo(syncResult.FunctionId));
    }

    [Test]
    public void Resolve_SameOverloadTwice_SameFunctionId()
    {
        var type = typeof(ResolverSameArity);
        var site = Site(type, "M");

        var first = DistroMethodResolver.Resolve(site, type, new[] { typeof(int) });
        var second = DistroMethodResolver.Resolve(site, type, new[] { typeof(int) });

        Assert.That(second.FunctionId, Is.EqualTo(first.FunctionId));
    }

    [TestCase("System.Int32")]
    [TestCase(" system.int32 ")]
    public void Resolve_ExplicitSite_XmlSignatureMustAgree(string xmlSignature)
    {
        var type = typeof(ResolverSameArity);

        var result = DistroMethodResolver.Resolve(Site(type, "M", xmlSignature), type, new[] { typeof(int) });

        AssertResolved(result, Method(type, "M", typeof(int)), false, "System.Int32");
    }

    [Test]
    public void Resolve_ExplicitSite_MismatchingXmlSignature_NotFound()
    {
        var type = typeof(ResolverSameArity);

        var result = DistroMethodResolver.Resolve(Site(type, "M", "System.String"), type, new[] { typeof(int) });

        AssertUnresolved(result, DistroResolutionOutcome.NotFound, type, "System.String");
    }

    [Test]
    public void Resolve_ExplicitVoidSite_MatchesParameterlessMethod()
    {
        var type = typeof(ResolverParameterless);

        var result = DistroMethodResolver.Resolve(Site(type, "M", "void"), type, Type.EmptyTypes);

        AssertResolved(result, Method(type, "M"), true, string.Empty);
    }

    [Test]
    public void Resolve_OverloadWideSite_ReturnsFormattedSignature()
    {
        var type = typeof(ResolverSameArity);

        var result = DistroMethodResolver.Resolve(Site(type, "M"), type, new[] { typeof(CancellationToken) });

        Assert.That(result.ArgumentSignature, Is.EqualTo("System.Threading.CancellationToken"));
    }

    [Test]
    public void Resolve_GenericMethodParameter_MatchesAnyArgument()
    {
        var type = typeof(ResolverGenericMethod);

        var result = DistroMethodResolver.Resolve(Site(type, "Insert"), type, new[] { typeof(string), typeof(int) });

        var method = type.GetMethod("Insert");
        AssertResolved(result, method, true, "System.String,!!0");
    }

    [Test]
    public void Resolve_ByRefParameter_MatchesElementType()
    {
        var type = typeof(ResolverByRef);

        var result = DistroMethodResolver.Resolve(Site(type, "Read"), type, new[] { typeof(int) });

        AssertResolved(result, Method(type, "Read", typeof(int).MakeByRefType()), false, "System.Int32&");
    }

    [Test]
    public void Resolve_RefStructParameter_MatchesCallTargetRefStruct()
    {
        var type = typeof(ResolverRefStructParameter);

        var result = DistroMethodResolver.Resolve(Site(type, "Write"), type, new[] { ResolverDynamicAssemblies.CallTargetRefStruct });

        AssertResolved(result, Method(type, "Write", typeof(ResolverRefStruct)), false, "NewRelic.Agent.Core.OtelDistro.ResolverRefStruct");
    }

    [Test]
    public void Resolve_BclRefStructParameter_MatchesCallTargetRefStruct()
    {
        var type = typeof(ResolverBclRefStructParameter);
        var method = Method(type, "Write", typeof(ReadOnlySpan<byte>));

        var result = DistroMethodResolver.Resolve(Site(type, "Write"), type, new[] { ResolverDynamicAssemblies.CallTargetRefStruct });

        AssertResolved(result, method, false, "System.ReadOnlySpan`1[System.Byte]");
    }

    [Test]
    public void Resolve_ByRefToRefStructParameter_MatchesCallTargetRefStruct()
    {
        var type = typeof(ResolverRefToRefStructParameter);

        var result = DistroMethodResolver.Resolve(Site(type, "Write"), type, new[] { ResolverDynamicAssemblies.CallTargetRefStruct });

        AssertResolved(result, Method(type, "Write", typeof(ResolverRefStruct).MakeByRefType()), false, "NewRelic.Agent.Core.OtelDistro.ResolverRefStruct&");
    }

    [Test]
    public void Resolve_NonRefStructParameter_DoesNotMatchCallTargetRefStruct()
    {
        var type = typeof(ResolverSingleAsync);

        var result = DistroMethodResolver.Resolve(Site(type, "Run"), type, new[] { ResolverDynamicAssemblies.CallTargetRefStruct });

        AssertUnresolved(result, DistroResolutionOutcome.NotFound, type, string.Empty);
    }

    [Test]
    public void Resolve_RefStructParameter_OtherArgumentType_NotFound()
    {
        var type = typeof(ResolverRefStructParameter);

        var result = DistroMethodResolver.Resolve(Site(type, "Write"), type, new[] { typeof(int) });

        AssertUnresolved(result, DistroResolutionOutcome.NotFound, type, string.Empty);
    }

    [Test]
    public void Resolve_StaticMethod_TargetIsDeclaringType()
    {
        var type = typeof(ResolverStatic);

        var result = DistroMethodResolver.Resolve(Site(type, "Run"), type, new[] { typeof(string) });

        AssertResolved(result, Method(type, "Run", typeof(string)), true, "System.String");
    }

    [Test]
    public void Resolve_ValueTypeInstanceMethod()
    {
        var type = typeof(ResolverValueType);

        var result = DistroMethodResolver.Resolve(Site(type, "Run"), type, new[] { typeof(int) });

        AssertResolved(result, Method(type, "Run", typeof(int)), true, "System.Int32");
    }

    [Test]
    public void Resolve_GenericClass_TargetIsObject_ResolvesByNameWithRealType()
    {
        var definition = typeof(ResolverGeneric<>);

        var result = DistroMethodResolver.Resolve(Site(definition, "M"), typeof(object), new[] { typeof(int) }, new[] { definition.Assembly });

        AssertResolved(result, definition.GetMethod("M"), true, "!0");
        Assert.That(result.DeclaringType, Is.EqualTo(definition));
    }

    [Test]
    public void Resolve_ByNameLookup_LogsTheAssemblyCopyUsedAtFinest()
    {
        var definition = typeof(ResolverGeneric<>);

        DistroMethodResolver.Resolve(Site(definition, "M"), typeof(object), new[] { typeof(int) }, new[] { definition.Assembly });

        Assert.That(_logger.Messages(Level.Finest), Is.EqualTo(new[]
        {
            $"OTel distro: looking up {definition.FullName} by name; using assembly {definition.Assembly.FullName}."
        }));
    }

    [Test]
    public void Resolve_ByNameLookup_NoAssemblyLoaded_LogsNone()
    {
        var site = Site("Some.Type", "M", string.Empty, "No.Such.Assembly");

        DistroMethodResolver.Resolve(site, typeof(object), new[] { typeof(int) }, Array.Empty<Assembly>());

        Assert.That(_logger.Messages(Level.Finest), Is.EqualTo(new[] { "OTel distro: looking up Some.Type by name; using assembly (none loaded)." }));
    }

    [Test]
    public void Resolve_TargetIsTheDeclaringType_DoesNotLookUpByName()
    {
        var type = typeof(ResolverSingleAsync);

        DistroMethodResolver.Resolve(Site(type, "Run"), type, new[] { typeof(int) });

        Assert.That(_logger.Messages(Level.Finest), Is.Empty);
    }

    [Test]
    public void Resolve_NotFoundAndAmbiguousDisagreeing_ReportDiagnostics()
    {
        DistroMethodResolutionDiagnostics.SetLoggerReady(true);
        var ambiguous = typeof(ResolverAmbiguousDisagreeing);
        var sameArity = typeof(ResolverSameArity);

        DistroMethodResolver.Resolve(Site(ambiguous, "M"), ambiguous, new[] { typeof(int) });
        DistroMethodResolver.Resolve(Site(sameArity, "Missing"), sameArity, new[] { typeof(int) });
        DistroMethodResolver.Resolve(Site(sameArity, "Other"), sameArity, new[] { typeof(int) });

        Assert.That(_logger.Messages(Level.Info), Is.EqualTo(new[]
        {
            $"OTel distro: cannot tell overloads of {ambiguous.FullName}.M with 1 parameters apart and they differ in async-ness; treating the call as not async (deliberate fallback).",
            $"OTel distro: could not identify the method {sameArity.FullName}.Missing with 1 parameters that fired; treating the call as not async."
        }));
    }

    [Test]
    public void Resolve_NullArgumentTypes_ReturnsUnresolvedWithoutThrowing()
    {
        var type = typeof(ResolverSingleAsync);

        DistroResolvedMethod result = null;
        Assert.DoesNotThrow(() => result = DistroMethodResolver.Resolve(Site(type, "Run"), type, null));

        AssertUnresolved(result, DistroResolutionOutcome.NotFound, type, string.Empty);
    }

    [Test]
    public void Resolve_GenericClass_TargetIsNonGenericBase_UsesTheTargetsAssembly()
    {
        var definition = typeof(ResolverGenericDerived<>);

        // No loaded assemblies: the declaring type can only come from the target's (base class's) assembly.
        var result = DistroMethodResolver.Resolve(Site(definition, "M"), typeof(ResolverNonGenericBase), new[] { typeof(int) }, Array.Empty<Assembly>());

        AssertResolved(result, definition.GetMethod("M"), false, "!0");
    }

    [Test]
    public void Resolve_NestedInGenericClass_TargetIsObject()
    {
        var definition = typeof(ResolverOuter<>.Inner);

        var result = DistroMethodResolver.Resolve(Site(definition, "M"), typeof(object), new[] { typeof(string) }, new[] { definition.Assembly });

        AssertResolved(result, definition.GetMethod("M"), true, "!0");
    }

    [Test]
    public void Resolve_AssemblyCopy_PrefersTargetOrArgumentAssembly()
    {
        var type = typeof(ResolverAssemblyCopy);
        var decoy = ResolverDynamicAssemblies.DefineDecoy(TestAssemblyName, type.FullName, "M");
        var loaded = new[] { decoy, type.Assembly };

        var result = DistroMethodResolver.Resolve(Site(type, "M"), typeof(object), new[] { typeof(ResolverAssemblyCopyArgument) }, loaded);

        AssertResolved(result, Method(type, "M", typeof(ResolverAssemblyCopyArgument)), true, "NewRelic.Agent.Core.OtelDistro.ResolverAssemblyCopyArgument");
        Assert.That(result.DeclaringType.Assembly, Is.SameAs(type.Assembly));
    }

    [Test]
    public void Resolve_AssemblyCopy_FallsBackToFirstLoadedAssemblyWithTheName()
    {
        var type = typeof(ResolverAssemblyCopy);
        var decoy = ResolverDynamicAssemblies.DefineDecoy(TestAssemblyName, type.FullName, "M");
        var loaded = new[] { typeof(object).Assembly, decoy, type.Assembly };

        var result = DistroMethodResolver.Resolve(Site(type, "M"), typeof(object), new[] { typeof(object) }, loaded);

        Assert.Multiple(() =>
        {
            Assert.That(result.Outcome, Is.EqualTo(DistroResolutionOutcome.Resolved));
            Assert.That(result.IsAsync, Is.False);
            Assert.That(result.DeclaringType, Is.EqualTo(decoy.GetType(type.FullName)));
            Assert.That(result.DeclaringType.Assembly, Is.Not.SameAs(type.Assembly));
        });
    }

    [Test]
    public void Resolve_AttributeMatchedByName()
    {
        var type = ResolverDynamicAssemblies.DefineWithOwnAsyncStateMachineAttribute("ResolverDynamic.OwnAttribute", "Legacy.Client", "Run");

        var result = DistroMethodResolver.Resolve(Site(type, "Run"), type, Type.EmptyTypes, new[] { type.Assembly });

        Assert.Multiple(() =>
        {
            Assert.That(result.Outcome, Is.EqualTo(DistroResolutionOutcome.Resolved));
            Assert.That(result.IsAsync, Is.True);
            Assert.That(result.DeclaringType, Is.EqualTo(type));
        });
    }

    [Test]
    public void Resolve_AmbiguousAgreeing_UsesFirstCandidate()
    {
        var type = typeof(ResolverAmbiguousAgreeing);
        var first = type.GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
            .First(m => m.Name == "M");

        var result = DistroMethodResolver.Resolve(Site(type, "M"), type, new[] { typeof(int) });

        Assert.Multiple(() =>
        {
            Assert.That(result.Outcome, Is.EqualTo(DistroResolutionOutcome.AmbiguousAgreeing));
            Assert.That(result.IsResolved, Is.True);
            Assert.That(result.IsAsync, Is.True);
            Assert.That(result.DeclaringType, Is.EqualTo(type));
            Assert.That(result.FunctionId, Is.EqualTo(ExpectedFunctionId(first)));
            Assert.That(result.ArgumentSignature, Is.EqualTo(DistroSignatureFormatter.Format(first.GetParameters())));
        });
    }

    [Test]
    public void Resolve_AmbiguousDisagreeing_NotAsyncWithSiteIdentity()
    {
        var type = typeof(ResolverAmbiguousDisagreeing);

        var result = DistroMethodResolver.Resolve(Site(type, "M"), type, new[] { typeof(int) });

        AssertUnresolved(result, DistroResolutionOutcome.AmbiguousDisagreeing, type, string.Empty);
    }

    [Test]
    public void Resolve_MethodNotFound_Unresolved()
    {
        var type = typeof(ResolverSameArity);

        var result = DistroMethodResolver.Resolve(Site(type, "Missing"), type, new[] { typeof(int) });

        AssertUnresolved(result, DistroResolutionOutcome.NotFound, type, string.Empty);
    }

    [Test]
    public void Resolve_TypeNotFound_Unresolved()
    {
        var site = Site("NewRelic.Agent.Core.OtelDistro.NoSuchType", "M", string.Empty, TestAssemblyName);

        var result = DistroMethodResolver.Resolve(site, typeof(object), new[] { typeof(int) }, new[] { typeof(DistroMethodResolverTests).Assembly });

        AssertUnresolved(result, DistroResolutionOutcome.NotFound, typeof(object), string.Empty);
    }

    [Test]
    public void Resolve_AssemblyNotLoaded_Unresolved()
    {
        var site = Site("Some.Type", "M", string.Empty, "No.Such.Assembly");

        var result = DistroMethodResolver.Resolve(site, typeof(object), new[] { typeof(int) }, new[] { typeof(object).Assembly });

        AssertUnresolved(result, DistroResolutionOutcome.NotFound, typeof(object), string.Empty);
    }

    [Test]
    public void Resolve_ReflectionThrows_ReturnsUnresolved()
    {
        var site = Site("Some.Type", "M", "System.Int32", "Throwing.Assembly");

        var result = DistroMethodResolver.Resolve(site, typeof(object), new[] { typeof(int) }, new Assembly[] { new ThrowingAssembly("Throwing.Assembly") });

        AssertUnresolved(result, DistroResolutionOutcome.NotFound, typeof(object), "System.Int32");
    }

    [Test]
    public void Resolve_NullSite_ReturnsUnresolvedWithoutThrowing()
    {
        var result = DistroMethodResolver.Resolve(null, typeof(object), new[] { typeof(int) });

        Assert.Multiple(() =>
        {
            Assert.That(result.Outcome, Is.EqualTo(DistroResolutionOutcome.NotFound));
            Assert.That(result.IsAsync, Is.False);
            Assert.That(result.FunctionId, Is.EqualTo(0ul));
            Assert.That(result.DeclaringType, Is.EqualTo(typeof(object)));
            Assert.That(result.ArgumentSignature, Is.EqualTo(string.Empty));
        });
    }

    [Test]
    public void ComputeFunctionId_IsFnv1a64OverSiteIdTokenAndModuleVersionId()
    {
        var mvid = new Guid("00112233-4455-6677-8899-aabbccddeeff");
        var bytes = BitConverter.GetBytes(SiteFunctionId).Concat(BitConverter.GetBytes(0x06000001)).Concat(mvid.ToByteArray());
        var expected = 14695981039346656037ul;
        foreach (var b in bytes)
        {
            expected ^= b;
            expected *= 1099511628211ul;
        }

        Assert.That(DistroMethodResolver.ComputeFunctionId(SiteFunctionId, 0x06000001, mvid), Is.EqualTo(expected));
    }

    [TestCase(DistroResolutionOutcome.Resolved, true)]
    [TestCase(DistroResolutionOutcome.AmbiguousAgreeing, true)]
    [TestCase(DistroResolutionOutcome.AmbiguousDisagreeing, false)]
    [TestCase(DistroResolutionOutcome.NotFound, false)]
    public void ResolvedMethod_IsResolved_FollowsOutcome(DistroResolutionOutcome outcome, bool expected)
    {
        var resolved = new DistroResolvedMethod(outcome, true, 7ul, typeof(int), "Sig");

        Assert.Multiple(() =>
        {
            Assert.That(resolved.IsResolved, Is.EqualTo(expected));
            Assert.That(resolved.Outcome, Is.EqualTo(outcome));
            Assert.That(resolved.IsAsync, Is.True);
            Assert.That(resolved.FunctionId, Is.EqualTo(7ul));
            Assert.That(resolved.DeclaringType, Is.EqualTo(typeof(int)));
            Assert.That(resolved.ArgumentSignature, Is.EqualTo("Sig"));
        });
    }
}

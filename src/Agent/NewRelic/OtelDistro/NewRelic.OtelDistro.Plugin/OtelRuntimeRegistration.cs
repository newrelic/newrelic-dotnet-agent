// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using NewRelic.Agent.Core.OtelDistro;
using OpenTelemetry.AutoInstrumentation.CallTarget;

namespace NewRelic.OtelDistro.Plugin;

/// <summary>
/// The reflection adapter onto OTel's run-time call-target registration (pinned to OTel 1.18.0): registers definitions
/// after start-up, for sites the on-disk XML turns on that the start-up payload did not include. Same shape as
/// <see cref="OtelConfigProbe"/>: the only reach into OTel's <c>NativeMethods</c>, and it never throws.
/// The call blocks on native ReJIT: callers must not hold a lock or run in an assembly-load handler or a CLR callback.
/// </summary>
/// <remarks>
/// This adapter owns the unmanaged strings it hands to native code. OTel's string constructor of
/// <c>NativeCallTargetDefinition2</c> writes into <c>NativeCallTargetUnmanagedMemoryHelper</c>, a static bump allocator
/// with no synchronization that OTel frees as a whole on exit; using it from Core's watcher thread would race OTel's own
/// payload builds and that exit-time free, and leak until exit. So each string is allocated here
/// (<see cref="Marshal.StringToHGlobalUni"/>), the entries are built with the IntPtr constructor, and everything is freed
/// once the native call returns: native copies every string it keeps (cor_profiler.cpp:1328-1342 builds WSTRINGs from
/// them) and reads none of them afterwards.
/// </remarks>
internal static class OtelRuntimeRegistration
{
    // OTel internals (src/OpenTelemetry.AutoInstrumentation, v1.18.0):
    // NativeMethods.cs:14 - internal static class NativeMethods; :18 - public static int RegisterCallTargetDefinitions(
    // string id, NativeCallTargetDefinition2[] methodArrays, uint enabledCategories), 0 for a null or empty array, else the
    // native count of definitions taken (0 when the profiler is not initialized or the id was already processed:
    // cor_profiler.cpp:1288-1305; fewer than passed when native skips an invalid entry: :1316-1326).
    private const string NativeMethodsTypeName = "OpenTelemetry.AutoInstrumentation.NativeMethods";
    private const string RegisterMethodName = "RegisterCallTargetDefinitions";

    // NativeCallTargetDefinition2.cs:11 - internal struct, Sequential/Unicode; :43 - the IntPtr constructor (15 parameters):
    // targetAssembly, targetType, targetMethod, targetSignatureTypes (IntPtr to an array of string pointers),
    // targetSignatureTypesLength (ushort), min major/minor/patch, max major/minor/patch (ushort x6), integrationAssembly,
    // integrationType (IntPtr), kind (byte), categories (uint).
    private const string NativeDefinitionTypeName = "OpenTelemetry.AutoInstrumentation.NativeCallTargetDefinition2";

    // Same values as DistroDefinitionsPayload: CallTargetKind.Default (direct) and the tracing category.
    private const byte DirectCallTargetKind = 0;
    private const uint TracingCategory = 1;

    // Instrumentation.cs:276 - upstream registers its own and the plugins' shared definitions with every category enabled.
    private const uint EnabledCategories = uint.MaxValue;

    private const BindingFlags StaticMembers = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    // Resolved on first use; a race only resolves it twice (same result). Never mutated after it is published.
    private static Binding _binding;

    /// <summary>
    /// Registers <paramref name="definitions"/> with OTel under <paramref name="definitionsId"/> (which must differ from
    /// every id registered before, or OTel ignores the call). True when OTel took at least one definition, with the native
    /// count in <paramref name="registered"/> (it can be less than the definitions passed when native skips an invalid one);
    /// otherwise false with the reason in <paramref name="failure"/> (null on success). Never throws.
    /// </summary>
    internal static bool TryRegister(IReadOnlyList<DistroCallTargetDefinition> definitions, string integrationAssemblyFullName, string definitionsId, out int registered, out string failure)
    {
        registered = 0;
        try
        {
            if (definitions == null || definitions.Count == 0 || string.IsNullOrEmpty(definitionsId))
            {
                failure = "no definitions or no definitions id";
                return false;
            }

            var binding = _binding ?? (_binding = Bind());
            var allocations = new List<IntPtr>(definitions.Count * 8);
            try
            {
                var nativeArray = BuildNativeArray(binding, definitions, integrationAssemblyFullName, allocations);
                registered = (int)binding.Register.Invoke(null, new object[] { definitionsId, nativeArray, EnabledCategories });
            }
            finally
            {
                foreach (var allocation in allocations)
                {
                    Marshal.FreeHGlobal(allocation);
                }
            }

            if (registered <= 0)
            {
                failure = "OTel registered 0 of " + definitions.Count + " definitions (native profiler not initialized, id " + definitionsId + " already processed, or every definition invalid)";
                return false;
            }

            failure = null;
            return true;
        }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            failure = Describe(ex.InnerException);
            return false;
        }
        catch (Exception ex)
        {
            failure = Describe(ex);
            return false;
        }
    }

    // Every pointer allocated goes into allocations before it is used, so the caller's finally frees all of them whatever
    // throws. StringToHGlobalUni(null) is IntPtr.Zero, as upstream's helper gives for null; FreeHGlobal(Zero) is a no-op.
    private static Array BuildNativeArray(Binding binding, IReadOnlyList<DistroCallTargetDefinition> definitions, string integrationAssemblyFullName, List<IntPtr> allocations)
    {
        var nativeArray = Array.CreateInstance(binding.NativeType, definitions.Count);
        var integrationAssembly = Allocate(integrationAssemblyFullName, allocations);
        for (var i = 0; i < definitions.Count; i++)
        {
            var d = definitions[i];
            var signatureTypes = d.TargetSignatureTypes;
            var signatureCount = signatureTypes?.Length ?? 0;
            if (signatureCount > ushort.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(definitions), signatureCount, "too many signature types for " + d.TargetType + "." + d.TargetMethod);
            }

            var signatureBlock = IntPtr.Zero;
            if (signatureCount > 0)
            {
                signatureBlock = Marshal.AllocHGlobal(signatureCount * IntPtr.Size);
                allocations.Add(signatureBlock);
                for (var s = 0; s < signatureCount; s++)
                {
                    Marshal.WriteIntPtr(signatureBlock, s * IntPtr.Size, Allocate(signatureTypes[s], allocations));
                }
            }

            binding.Construct(
                nativeArray, i,
                Allocate(d.TargetAssembly, allocations),
                Allocate(d.TargetType, allocations),
                Allocate(d.TargetMethod, allocations),
                signatureBlock,
                (ushort)signatureCount,
                d.MinMajor, d.MinMinor, d.MinPatch,
                d.MaxMajor, d.MaxMinor, d.MaxPatch,
                integrationAssembly,
                Allocate(d.IntegrationTypeName, allocations),
                DirectCallTargetKind,
                TracingCategory);
        }

        return nativeArray;
    }

    private static IntPtr Allocate(string value, List<IntPtr> allocations)
    {
        var pointer = Marshal.StringToHGlobalUni(value);
        if (pointer != IntPtr.Zero)
        {
            allocations.Add(pointer);
        }

        return pointer;
    }

    private static Binding Bind()
    {
        // A public OTel type of the managed assembly OTel loaded, the one the plugin is bound to.
        var otelAssembly = typeof(CallTargetState).Assembly;
        var nativeType = otelAssembly.GetType(NativeDefinitionTypeName, true);
        var nativeCtor = nativeType.GetConstructor(new[]
        {
            typeof(IntPtr), typeof(IntPtr), typeof(IntPtr), typeof(IntPtr), typeof(ushort),
            typeof(ushort), typeof(ushort), typeof(ushort), typeof(ushort), typeof(ushort), typeof(ushort),
            typeof(IntPtr), typeof(IntPtr), typeof(byte), typeof(uint)
        });
        if (nativeCtor == null)
        {
            throw new MissingMethodException(NativeDefinitionTypeName, ".ctor(IntPtr x4, ushort x7, IntPtr x2, byte, uint)");
        }

        var nativeMethods = otelAssembly.GetType(NativeMethodsTypeName, true);
        var register = nativeMethods.GetMethod(RegisterMethodName, StaticMembers, null, new[] { typeof(string), nativeType.MakeArrayType(), typeof(uint) }, null);
        if (register == null || register.ReturnType != typeof(int))
        {
            throw new MissingMethodException(NativeMethodsTypeName, RegisterMethodName + "(string, NativeCallTargetDefinition2[], uint) returning int");
        }

        return new Binding(nativeType, BuildConstructor(nativeType, nativeCtor), register);
    }

    // The struct is internal to OTel, so it cannot be named in source: a DynamicMethod runs the IntPtr constructor in place
    // on an array slot (as DistroDefinitionsPayload does for the string constructor). It is called once here on a scratch
    // slot with zero pointers (the constructor only stores its arguments), so a defective one fails now, while JIT-compiled,
    // and never during a registration. If it cannot be built or that call fails, reflection (one object[] and boxed
    // arguments per entry; run-time sets are small) does the same.
    private static NativeDefinitionConstructor BuildConstructor(Type nativeType, ConstructorInfo nativeCtor)
    {
        try
        {
            var compiled = CompileConstructor(nativeType, nativeCtor);
            compiled(Array.CreateInstance(nativeType, 1), 0, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0, 0, 0, 0, 0, 0, 0, IntPtr.Zero, IntPtr.Zero, DirectCallTargetKind, TracingCategory);
            return compiled;
        }
        catch (Exception)
        {
            return (target, index, targetAssembly, targetType, targetMethod, signatureTypes, signatureTypesLength, minMajor, minMinor, minPatch, maxMajor, maxMinor, maxPatch, integrationAssembly, integrationType, kind, categories) =>
                target.SetValue(nativeCtor.Invoke(new object[] { targetAssembly, targetType, targetMethod, signatureTypes, signatureTypesLength, minMajor, minMinor, minPatch, maxMajor, maxMinor, maxPatch, integrationAssembly, integrationType, kind, categories }), index);
        }
    }

    private static NativeDefinitionConstructor CompileConstructor(Type nativeType, ConstructorInfo nativeCtor)
    {
        var ctorParameters = nativeCtor.GetParameters();
        var parameterTypes = new Type[2 + ctorParameters.Length];
        parameterTypes[0] = typeof(Array);
        parameterTypes[1] = typeof(int);
        for (var p = 0; p < ctorParameters.Length; p++)
        {
            parameterTypes[2 + p] = ctorParameters[p].ParameterType;
        }

        var method = new DynamicMethod("DistroConstructRuntimeNativeDefinition", typeof(void), parameterTypes, typeof(OtelRuntimeRegistration), skipVisibility: true);
        var il = method.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Castclass, nativeType.MakeArrayType());
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Ldelema, nativeType);
        for (var a = 0; a < ctorParameters.Length; a++)
        {
            il.Emit(OpCodes.Ldarg, (short)(2 + a));
        }

        il.Emit(OpCodes.Call, nativeCtor);
        il.Emit(OpCodes.Ret);
        return (NativeDefinitionConstructor)method.CreateDelegate(typeof(NativeDefinitionConstructor));
    }

    private static string Describe(Exception ex)
    {
        return ex.GetType().Name + ": " + ex.Message;
    }

    // Mirrors the 15 IntPtr-constructor parameters, preceded by the target array and slot index.
    private delegate void NativeDefinitionConstructor(
        Array target, int index,
        IntPtr targetAssembly, IntPtr targetType, IntPtr targetMethod, IntPtr targetSignatureTypes, ushort targetSignatureTypesLength,
        ushort minMajor, ushort minMinor, ushort minPatch, ushort maxMajor, ushort maxMinor, ushort maxPatch,
        IntPtr integrationAssembly, IntPtr integrationType, byte kind, uint categories);

    private sealed class Binding
    {
        public Binding(Type nativeType, NativeDefinitionConstructor construct, MethodInfo register)
        {
            NativeType = nativeType;
            Construct = construct;
            Register = register;
        }

        public Type NativeType { get; }

        public NativeDefinitionConstructor Construct { get; }

        public MethodInfo Register { get; }
    }
}

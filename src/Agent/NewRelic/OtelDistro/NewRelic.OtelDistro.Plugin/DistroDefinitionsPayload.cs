// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using NewRelic.Agent.Core.OtelDistro;
using OpenTelemetry.AutoInstrumentation.CallTarget;

namespace NewRelic.OtelDistro.Plugin;

/// <summary>
/// Builds the <c>InstrumentationDefinitions.SharedPayload</c> the upstream OTel auto-instrumentation expects back from
/// a plugin's <c>GetAllDefinitionsPayload</c> hook. Both the payload struct and its <c>NativeCallTargetDefinition2</c>
/// element type are internal to <c>OpenTelemetry.AutoInstrumentation.dll</c>, so this builds them by reflection over
/// that assembly (the one that owns <see cref="CallTargetState"/>). Each definition points the native profiler at a
/// target method (assembly / type / method / signature + version range) and the generated integration type that
/// wraps it.
/// </summary>
internal static class DistroDefinitionsPayload
{
    private const byte DirectCallTargetKind = 0;
    private const uint TracingCategory = 1;

    private const string SharedPayloadTypeName = "OpenTelemetry.AutoInstrumentation.InstrumentationDefinitions+SharedPayload";
    internal const string NativeDefinitionTypeName = "OpenTelemetry.AutoInstrumentation.NativeCallTargetDefinition2";

    internal static object Build(IReadOnlyList<DistroCallTargetDefinition> definitions, string integrationAssemblyFullName, string definitionsId)
    {
        if (definitions == null || definitions.Count == 0)
        {
            return null;
        }

        var otelAssembly = typeof(CallTargetState).Assembly;

        var nativeType = otelAssembly.GetType(NativeDefinitionTypeName, throwOnError: true);
        var sharedPayloadType = otelAssembly.GetType(SharedPayloadTypeName, throwOnError: true);

        // The 14 NativeCallTargetDefinition2 ctor parameters, in order:
        //  1 targetAssembly, 2 targetType, 3 targetMethod, 4 targetSignatureTypes,
        //  5 minMajor, 6 minMinor, 7 minPatch, 8 maxMajor, 9 maxMinor, 10 maxPatch,
        //  11 integrationAssembly, 12 integrationType, 13 callTargetKind (byte), 14 category (uint).
        var nativeCtor = nativeType.GetConstructor(new[]
        {
            typeof(string), typeof(string), typeof(string), typeof(string[]),
            typeof(ushort), typeof(ushort), typeof(ushort), typeof(ushort), typeof(ushort), typeof(ushort),
            typeof(string), typeof(string), typeof(byte), typeof(uint)
        });

        if (nativeCtor == null)
        {
            throw new MissingMethodException(NativeDefinitionTypeName, ".ctor(string,string,string,string[],ushort x6,string,string,byte,uint)");
        }

        var nativeArray = Array.CreateInstance(nativeType, definitions.Count);

        // Slot 0 is the compiled constructor's first call, which is when the DynamicMethod is JIT-compiled, so a
        // defective one fails here and not in TryBuildConstructor. If it does, log it once and use reflection for
        // every slot, slot 0 included.
        var construct = TryBuildConstructor(nativeType, nativeCtor);
        if (construct != null)
        {
            try
            {
                ConstructCompiled(construct, nativeArray, 0, definitions[0], integrationAssemblyFullName);
            }
            catch (Exception ex)
            {
                LogFallback(ex);
                construct = null;
            }
        }

        if (construct == null)
        {
            ConstructByReflection(nativeCtor, nativeArray, 0, definitions[0], integrationAssemblyFullName);
        }

        for (var i = 1; i < definitions.Count; i++)
        {
            if (construct != null)
            {
                ConstructCompiled(construct, nativeArray, i, definitions[i], integrationAssemblyFullName);
            }
            else
            {
                ConstructByReflection(nativeCtor, nativeArray, i, definitions[i], integrationAssemblyFullName);
            }
        }

        // SharedPayload is a struct: box it, set its properties on the boxed instance, return the boxed object.
        // Upstream's PluginManager unboxes it via an "is SharedPayload" pattern.
        var payload = Activator.CreateInstance(sharedPayloadType);

        var definitionsIdProperty = sharedPayloadType.GetProperty("DefinitionsId", BindingFlags.Public | BindingFlags.Instance);
        if (definitionsIdProperty == null)
        {
            throw new MissingMemberException(SharedPayloadTypeName, "DefinitionsId");
        }

        var definitionsProperty = sharedPayloadType.GetProperty("Definitions", BindingFlags.Public | BindingFlags.Instance);
        if (definitionsProperty == null)
        {
            throw new MissingMemberException(SharedPayloadTypeName, "Definitions");
        }

        definitionsIdProperty.SetValue(payload, definitionsId);
        definitionsProperty.SetValue(payload, nativeArray);

        return payload;
    }

    // Builds, once per Build call, a delegate that runs the NativeCallTargetDefinition2 constructor directly on an
    // array slot. NativeCallTargetDefinition2 is an internal OTel struct, so it cannot be named in source (same reason
    // DistroActivityListener builds its trace-id generator as a DynamicMethod). ConstructorInfo.Invoke would cost an
    // object[] plus eight boxed arguments (six ushort, byte, uint) and a boxed result per definition (thousands of
    // them); a DynamicMethod takes typed arguments, so none of that is allocated.
    // The reflection fallback covers two failures, and only one of them can happen per Build call, so the fallback line
    // is logged at most once: building the DynamicMethod throws (this method logs and returns null; the caller then
    // goes straight to reflection), or its first call (slot 0, where it is JIT-compiled) throws any exception (the
    // caller logs, redoes slot 0 by reflection and uses reflection for the rest). A failure on a later slot is not a
    // fallback case and propagates to GetAllDefinitionsPayload, which registers nothing.
    private static NativeDefinitionConstructor TryBuildConstructor(Type nativeType, ConstructorInfo nativeCtor)
    {
        try
        {
            var arrayType = nativeType.MakeArrayType();

            // (Array target, int index, <the 14 constructor arguments>)
            var parameterTypes = new Type[2 + 14];
            parameterTypes[0] = typeof(Array);
            parameterTypes[1] = typeof(int);
            var ctorParameters = nativeCtor.GetParameters();
            for (var p = 0; p < ctorParameters.Length; p++)
            {
                parameterTypes[2 + p] = ctorParameters[p].ParameterType;
            }

            var method = new DynamicMethod("DistroConstructNativeDefinition", typeof(void), parameterTypes, typeof(DistroDefinitionsPayload), skipVisibility: true);
            var il = method.GetILGenerator();

            // target[index].ctor(args...): take the slot's address and run the struct constructor in place.
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Castclass, arrayType);      // Array -> NativeCallTargetDefinition2[]
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Ldelema, nativeType);       // &target[index]
            for (var a = 0; a < ctorParameters.Length; a++)
            {
                il.Emit(OpCodes.Ldarg, (short)(2 + a)); // each constructor argument, as passed
            }

            il.Emit(OpCodes.Call, nativeCtor);
            il.Emit(OpCodes.Ret);

            return (NativeDefinitionConstructor)method.CreateDelegate(typeof(NativeDefinitionConstructor));
        }
        catch (Exception ex)
        {
            LogFallback(ex);
            return null;
        }
    }

    private static void LogFallback(Exception ex)
    {
        Console.Error.WriteLine($"[NewRelic.OtelDistro.Plugin] Compiled native definition constructor unavailable, using reflection: {ex.GetType().Name}: {ex.Message}");
    }

    // Typed arguments straight into the array slot: no object[] and no boxing.
    private static void ConstructCompiled(NativeDefinitionConstructor construct, Array nativeArray, int index, DistroCallTargetDefinition d, string integrationAssemblyFullName)
    {
        construct(
            nativeArray, index,
            d.TargetAssembly,
            d.TargetType,
            d.TargetMethod,
            d.TargetSignatureTypes,
            d.MinMajor, d.MinMinor, d.MinPatch,
            d.MaxMajor, d.MaxMinor, d.MaxPatch,
            integrationAssemblyFullName,
            d.IntegrationTypeName,
            DirectCallTargetKind,
            TracingCategory);
    }

    // Fallback: reflection. One object[] plus eight boxed arguments, and a boxed result, per definition.
    private static void ConstructByReflection(ConstructorInfo nativeCtor, Array nativeArray, int index, DistroCallTargetDefinition d, string integrationAssemblyFullName)
    {
        var nativeDefinition = nativeCtor.Invoke(new object[]
        {
            d.TargetAssembly,
            d.TargetType,
            d.TargetMethod,
            d.TargetSignatureTypes,
            d.MinMajor, d.MinMinor, d.MinPatch,
            d.MaxMajor, d.MaxMinor, d.MaxPatch,
            integrationAssemblyFullName,
            d.IntegrationTypeName,
            DirectCallTargetKind,
            TracingCategory
        });
        nativeArray.SetValue(nativeDefinition, index);
    }

    // Mirrors the 14 NativeCallTargetDefinition2 constructor parameters, preceded by the target array and slot index.
    private delegate void NativeDefinitionConstructor(
        Array target, int index,
        string targetAssembly, string targetType, string targetMethod, string[] targetSignatureTypes,
        ushort minMajor, ushort minMinor, ushort minPatch, ushort maxMajor, ushort maxMinor, ushort maxPatch,
        string integrationAssembly, string integrationType, byte callTargetKind, uint category);
}

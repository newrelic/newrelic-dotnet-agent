// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;

namespace NewRelic.OtelDistro.Coordinator;

/// <summary>
/// Sends definitions straight to OTel native <c>RegisterCallTargetDefinitions</c> (the same export the OTel managed
/// loader calls). Marshals each definition into unmanaged memory and frees all of it before returning.
/// Exempt from unit tests: it needs the OTel native profiler loaded, so live runs prove it.
/// </summary>
public sealed class OtelNativeRegistrar : INativeRegistrar
{
    /// <summary>
    /// The layout signature (<see cref="DefinitionLayout.Describe"/>) of this class's native definition struct. The
    /// plugin compares it with OTel's <c>NativeCallTargetDefinition2</c> before the coordinator is used.
    /// </summary>
    public static string LayoutSignature => DefinitionLayout.Describe(typeof(NativeDefinition));

    /// <summary>
    /// The signature (<see cref="DefinitionLayout.DescribeMethod"/>) of this class's <c>RegisterCallTargetDefinitions</c>
    /// P/Invoke. The plugin compares it with OTel's declaration before the coordinator is used.
    /// </summary>
    public static string RegisterSignature => DefinitionLayout.DescribeMethod(
        typeof(OtelNativeRegistrar).GetMethod(nameof(RegisterCallTargetDefinitions), BindingFlags.NonPublic | BindingFlags.Static));

    /// <inheritdoc />
    public int Register(string id, CoordinatorDefinition[] definitions, uint enabledCategories)
    {
        var allocations = new List<IntPtr>();
        try
        {
            var items = new NativeDefinition[definitions.Length];
            for (var i = 0; i < definitions.Length; i++)
            {
                items[i] = ToNative(definitions[i], allocations);
            }

            return RegisterCallTargetDefinitions(id, items, items.Length, enabledCategories);
        }
        finally
        {
            foreach (var allocation in allocations)
            {
                Marshal.FreeHGlobal(allocation);
            }
        }
    }

    private static NativeDefinition ToNative(CoordinatorDefinition definition, List<IntPtr> allocations)
    {
        var signatureTypes = definition.SignatureTypes;
        var signaturePointer = IntPtr.Zero;
        if (signatureTypes != null && signatureTypes.Length > 0)
        {
            signaturePointer = Marshal.AllocHGlobal(signatureTypes.Length * IntPtr.Size);
            allocations.Add(signaturePointer);
            for (var i = 0; i < signatureTypes.Length; i++)
            {
                Marshal.WriteIntPtr(signaturePointer, i * IntPtr.Size, AllocateString(signatureTypes[i], allocations));
            }
        }

        return new NativeDefinition
        {
            TargetAssembly = AllocateString(definition.TargetAssembly, allocations),
            TargetType = AllocateString(definition.TargetType, allocations),
            TargetMethod = AllocateString(definition.TargetMethod, allocations),
            TargetSignatureTypes = signaturePointer,
            TargetSignatureTypesLength = (ushort)(signatureTypes?.Length ?? 0),
            TargetMinimumMajor = definition.MinMajor,
            TargetMinimumMinor = definition.MinMinor,
            TargetMinimumPatch = definition.MinPatch,
            TargetMaximumMajor = definition.MaxMajor,
            TargetMaximumMinor = definition.MaxMinor,
            TargetMaximumPatch = definition.MaxPatch,
            IntegrationAssembly = AllocateString(definition.IntegrationAssembly, allocations),
            IntegrationType = AllocateString(definition.IntegrationType, allocations),
            Kind = definition.Kind,
            Categories = definition.Categories
        };
    }

    // A null string marshals to IntPtr.Zero, which tracks nothing to free.
    private static IntPtr AllocateString(string value, List<IntPtr> allocations)
    {
        var pointer = Marshal.StringToHGlobalUni(value);
        if (pointer != IntPtr.Zero)
        {
            allocations.Add(pointer);
        }

        return pointer;
    }

    // "the dll extension is required on .NET Framework and optional on .NET Core" (OTel NativeMethods.cs:281-283).
    // Declaration mirrors OTel NativeMethods.cs:283 (Windows.RegisterCallTargetDefinitions); native export is
    // OTel interop.cpp:77.
    [DllImport("OpenTelemetry.AutoInstrumentation.Native.dll", CharSet = CharSet.Unicode)]
    private static extern int RegisterCallTargetDefinitions(string id, [In] NativeDefinition[] items, int size, uint enabledCategories);

    // Layout compared field by field with OTel NativeCallTargetDefinition2
    // (src/OpenTelemetry.AutoInstrumentation/NativeCallTargetDefinition2.cs:10-36), which mirrors native
    // CallTargetDefinition2 (integration.h:362): three string pointers, signature array pointer, length (ushort),
    // min major/minor/patch, max major/minor/patch (ushort x6), two string pointers, kind (byte), categories (uint).
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NativeDefinition
    {
        public IntPtr TargetAssembly;
        public IntPtr TargetType;
        public IntPtr TargetMethod;
        public IntPtr TargetSignatureTypes;
        public ushort TargetSignatureTypesLength;
        public ushort TargetMinimumMajor;
        public ushort TargetMinimumMinor;
        public ushort TargetMinimumPatch;
        public ushort TargetMaximumMajor;
        public ushort TargetMaximumMinor;
        public ushort TargetMaximumPatch;
        public IntPtr IntegrationAssembly;
        public IntPtr IntegrationType;
        public byte Kind;
        public uint Categories;
    }
}

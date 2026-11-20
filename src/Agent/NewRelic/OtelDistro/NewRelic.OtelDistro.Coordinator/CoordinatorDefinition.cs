// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;

namespace NewRelic.OtelDistro.Coordinator;

/// <summary>One call-target definition, as plain serializable data (mirrors OTel CallTargetDefinition2).</summary>
[Serializable]
public sealed class CoordinatorDefinition
{
    private const string FieldSeparator = "\u001f";
    private const string SignatureSeparator = "\u001e";

    /// <summary>Creates a definition.</summary>
    public CoordinatorDefinition(string targetAssembly, string targetType, string targetMethod, string[] signatureTypes,
        ushort minMajor, ushort minMinor, ushort minPatch, ushort maxMajor, ushort maxMinor, ushort maxPatch,
        string integrationAssembly, string integrationType, byte kind, uint categories)
    {
        TargetAssembly = targetAssembly;
        TargetType = targetType;
        TargetMethod = targetMethod;
        SignatureTypes = signatureTypes;
        MinMajor = minMajor;
        MinMinor = minMinor;
        MinPatch = minPatch;
        MaxMajor = maxMajor;
        MaxMinor = maxMinor;
        MaxPatch = maxPatch;
        IntegrationAssembly = integrationAssembly;
        IntegrationType = integrationType;
        Kind = kind;
        Categories = categories;

        Key = string.Join(FieldSeparator,
            targetAssembly, targetType, targetMethod,
            string.Join(SignatureSeparator, signatureTypes ?? new string[0]),
            minMajor, minMinor, minPatch, maxMajor, maxMinor, maxPatch,
            integrationAssembly, integrationType, kind, categories);
    }

    /// <summary>Assembly holding the method to instrument.</summary>
    public string TargetAssembly { get; }

    /// <summary>Type holding the method to instrument.</summary>
    public string TargetType { get; }

    /// <summary>Method to instrument.</summary>
    public string TargetMethod { get; }

    /// <summary>Return and parameter type names, as OTel expects.</summary>
    public string[] SignatureTypes { get; }

    /// <summary>Minimum assembly version, major.</summary>
    public ushort MinMajor { get; }

    /// <summary>Minimum assembly version, minor.</summary>
    public ushort MinMinor { get; }

    /// <summary>Minimum assembly version, patch.</summary>
    public ushort MinPatch { get; }

    /// <summary>Maximum assembly version, major.</summary>
    public ushort MaxMajor { get; }

    /// <summary>Maximum assembly version, minor.</summary>
    public ushort MaxMinor { get; }

    /// <summary>Maximum assembly version, patch.</summary>
    public ushort MaxPatch { get; }

    /// <summary>Assembly holding the integration (call target) type.</summary>
    public string IntegrationAssembly { get; }

    /// <summary>The integration (call target) type.</summary>
    public string IntegrationType { get; }

    /// <summary>OTel call target kind.</summary>
    public byte Kind { get; }

    /// <summary>OTel instrumentation categories bit mask.</summary>
    public uint Categories { get; }

    /// <summary>Identity of the definition: every field joined, used to de-duplicate sends.</summary>
    public string Key { get; }
}

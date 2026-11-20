// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;

namespace NewRelic.Agent.Core.OtelDistro;

/// <summary>
/// Describes a single generated OTel distro CallTarget integration (target method and the supported
/// version range). Instances are constructed positionally by InstrumentationDefinitionsTable, which decodes the
/// generated definitions table, so the constructor parameter order is a contract with that reader -- do not reorder it.
/// </summary>
public sealed class DistroCallTargetDefinition
{
    public string TargetAssembly { get; }

    public string TargetType { get; }

    public string TargetMethod { get; }

    public string[] TargetSignatureTypes { get; }

    public string IntegrationTypeName { get; }

    public ushort MinMajor { get; }

    public ushort MinMinor { get; }

    public ushort MinPatch { get; }

    public ushort MaxMajor { get; }

    public ushort MaxMinor { get; }

    public ushort MaxPatch { get; }

    public DistroCallTargetDefinition(
        string targetAssembly,
        string targetType,
        string targetMethod,
        string[] targetSignatureTypes,
        string integrationTypeName)
        : this(targetAssembly, targetType, targetMethod, targetSignatureTypes, integrationTypeName, 0, 0, 0, 65535, 65535, 65535)
    {
    }

    public DistroCallTargetDefinition(
        string targetAssembly,
        string targetType,
        string targetMethod,
        string[] targetSignatureTypes,
        string integrationTypeName,
        ushort minMajor,
        ushort minMinor,
        ushort minPatch,
        ushort maxMajor,
        ushort maxMinor,
        ushort maxPatch)
    {
        if (string.IsNullOrEmpty(targetAssembly))
        {
            throw new ArgumentException("targetAssembly must not be null or empty.", nameof(targetAssembly));
        }

        if (string.IsNullOrEmpty(targetType))
        {
            throw new ArgumentException("targetType must not be null or empty.", nameof(targetType));
        }

        if (string.IsNullOrEmpty(targetMethod))
        {
            throw new ArgumentException("targetMethod must not be null or empty.", nameof(targetMethod));
        }

        if (targetSignatureTypes == null || targetSignatureTypes.Length < 1)
        {
            throw new ArgumentException("targetSignatureTypes must not be null and must have at least one element.", nameof(targetSignatureTypes));
        }

        if (string.IsNullOrEmpty(integrationTypeName))
        {
            throw new ArgumentException("integrationTypeName must not be null or empty.", nameof(integrationTypeName));
        }

        TargetAssembly = targetAssembly;
        TargetType = targetType;
        TargetMethod = targetMethod;
        TargetSignatureTypes = targetSignatureTypes;
        IntegrationTypeName = integrationTypeName;
        MinMajor = minMajor;
        MinMinor = minMinor;
        MinPatch = minPatch;
        MaxMajor = maxMajor;
        MaxMinor = maxMinor;
        MaxPatch = maxPatch;
    }
}

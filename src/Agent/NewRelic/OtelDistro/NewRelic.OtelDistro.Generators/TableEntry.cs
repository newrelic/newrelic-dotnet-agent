// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;

namespace NewRelic.OtelDistro.Generators;

/// <summary>
/// The record-flag bits a table entry can carry, so the two independent flags travel as one argument rather than two
/// adjacent optional bools (a positional <c>true</c> could bind to the wrong one).
/// </summary>
[Flags]
public enum TableEntryFlags
{
    None = 0,

    /// <summary>The shipped XML disables the point (enabled="false"): the site starts off.</summary>
    DefaultOff = 1,

    /// <summary>The point is one of NR's log-forwarding wrappers (it records a log line).</summary>
    LogForwarding = 2,
}

/// <summary>
/// One CallTarget definition to write into the instrumentation definitions table: the target method, its signature as
/// OTel wants it ("_" return type first, then the parameter types), the generated integration type, the site data the
/// integration runs with (tracer factory, metric name, argument signature, tracer arguments, function id), the
/// supported assembly version range, and what turns the point off and on from the XML on disk (point key, source file,
/// default-off).
/// </summary>
public sealed class TableEntry
{
    public string Assembly { get; }

    public string Type { get; }

    public string Method { get; }

    public IReadOnlyList<string> Signature { get; }

    public string IntegrationType { get; }

    public string TracerFactoryName { get; }

    /// <summary>Null when the XML names no metric; written as NoString, which is not the same as "".</summary>
    public string MetricName { get; }

    /// <summary>The comma-joined XML parameter types; "" for an overload-wide match.</summary>
    public string ArgumentSignature { get; }

    public uint TracerArguments { get; }

    public ulong FunctionId { get; }

    public ushort MinMajor { get; }

    public ushort MinMinor { get; }

    public ushort MinPatch { get; }

    public ushort MaxMajor { get; }

    public ushort MaxMinor { get; }

    public ushort MaxPatch { get; }

    /// <summary>The PointKeyHasher value of the XML point; every entry of one point shares it.</summary>
    public ulong PointKey { get; }

    /// <summary>Index of the point's file in the writer's file list, or 0xFFFF for a point from the plugin's own XML.</summary>
    public ushort SourceFile { get; }

    /// <summary>True when the shipped XML disables the point (enabled="false"): the site starts off.</summary>
    public bool DefaultOff { get; }

    /// <summary>
    /// True when the point is one of NR's log-forwarding wrappers (it records a log line); written as the record's
    /// FlagLogForwarding bit so the plugin can bracket the call with its OTel/NR log dedupe counter.
    /// </summary>
    public bool LogForwarding { get; }

    /// <summary>An entry for every version (0.0.0 to 65535.65535.65535).</summary>
    public TableEntry(
        string assembly,
        string type,
        string method,
        IReadOnlyList<string> signature,
        string integrationType,
        string tracerFactoryName,
        string metricName,
        string argumentSignature,
        uint tracerArguments,
        ulong functionId,
        ulong pointKey = 0,
        ushort sourceFile = 0xFFFF,
        TableEntryFlags flags = TableEntryFlags.None)
        : this(assembly, type, method, signature, integrationType, tracerFactoryName, metricName, argumentSignature, tracerArguments, functionId, 0, 0, 0, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, pointKey, sourceFile, flags)
    {
    }

    public TableEntry(
        string assembly,
        string type,
        string method,
        IReadOnlyList<string> signature,
        string integrationType,
        string tracerFactoryName,
        string metricName,
        string argumentSignature,
        uint tracerArguments,
        ulong functionId,
        ushort minMajor,
        ushort minMinor,
        ushort minPatch,
        ushort maxMajor,
        ushort maxMinor,
        ushort maxPatch,
        ulong pointKey = 0,
        ushort sourceFile = 0xFFFF,
        TableEntryFlags flags = TableEntryFlags.None)
    {
        Assembly = assembly;
        Type = type;
        Method = method;
        Signature = signature;
        IntegrationType = integrationType;
        TracerFactoryName = tracerFactoryName;
        MetricName = metricName;
        ArgumentSignature = argumentSignature;
        TracerArguments = tracerArguments;
        FunctionId = functionId;
        MinMajor = minMajor;
        MinMinor = minMinor;
        MinPatch = minPatch;
        MaxMajor = maxMajor;
        MaxMinor = maxMinor;
        MaxPatch = maxPatch;
        PointKey = pointKey;
        SourceFile = sourceFile;
        DefaultOff = (flags & TableEntryFlags.DefaultOff) != 0;
        LogForwarding = (flags & TableEntryFlags.LogForwarding) != 0;
    }
}

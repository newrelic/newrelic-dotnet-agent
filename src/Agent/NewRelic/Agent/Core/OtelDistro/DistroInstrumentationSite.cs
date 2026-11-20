// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;

namespace NewRelic.Agent.Core.OtelDistro;

/// <summary>
/// Describes a single instrumented call point for an OTel distro generated integration. Instances are
/// constructed by <see cref="DistroInstrumentationSites"/> from a read NRIDT table, lazily on the first call that
/// needs one; generated code does not construct them.
/// </summary>
public sealed class DistroInstrumentationSite
{
    public string TracerFactoryName { get; }

    public string MetricName { get; }

    public uint TracerArguments { get; }

    public string AssemblyName { get; }

    public string TypeName { get; }

    public string MethodName { get; }

    public string ArgumentSignature { get; }

    public ulong FunctionId { get; }

    /// <summary>The PointKeyHasher value of the XML point this site came from; shared by every site of that point.</summary>
    public ulong PointKey { get; }

    /// <summary>Index of the shipped XML file the point came from (see <see cref="DistroShippedXmlManifest"/>); -1 for the plugin's own XML.</summary>
    public int SourceFile { get; }

    /// <summary>True when the shipped XML has the point disabled (enabled="false"), so the site starts off.</summary>
    public bool DefaultOff { get; }

    /// <summary>
    /// True when the point is one of NR's log-forwarding wrappers (it records a log line). The distro brackets such a
    /// call with its same-thread log-dedupe counter so OTel's log bridge does not forward the same line twice.
    /// </summary>
    public bool LogForwarding { get; }

    /// <summary>A point from the plugin's own XML: on, not default-off, not log-forwarding, no shipped-file source.</summary>
    public DistroInstrumentationSite(
        string tracerFactoryName,
        string metricName,
        uint tracerArguments,
        string assemblyName,
        string typeName,
        string methodName,
        string argumentSignature,
        ulong functionId)
        : this(tracerFactoryName, metricName, tracerArguments, assemblyName, typeName, methodName, argumentSignature, functionId, 0, -1, false, false)
    {
    }

    // Full form. defaultOff and logForwarding are required (not optional) so a positional true cannot bind to the wrong
    // flag; the reader passes them in order and tests name them.
    public DistroInstrumentationSite(
        string tracerFactoryName,
        string metricName,
        uint tracerArguments,
        string assemblyName,
        string typeName,
        string methodName,
        string argumentSignature,
        ulong functionId,
        ulong pointKey,
        int sourceFile,
        bool defaultOff,
        bool logForwarding)
    {
        if (string.IsNullOrEmpty(tracerFactoryName))
        {
            throw new ArgumentException("tracerFactoryName must not be null or empty.", nameof(tracerFactoryName));
        }

        if (string.IsNullOrEmpty(assemblyName))
        {
            throw new ArgumentException("assemblyName must not be null or empty.", nameof(assemblyName));
        }

        if (string.IsNullOrEmpty(typeName))
        {
            throw new ArgumentException("typeName must not be null or empty.", nameof(typeName));
        }

        if (string.IsNullOrEmpty(methodName))
        {
            throw new ArgumentException("methodName must not be null or empty.", nameof(methodName));
        }

        TracerFactoryName = tracerFactoryName;
        MetricName = metricName;
        TracerArguments = tracerArguments;
        AssemblyName = assemblyName;
        TypeName = typeName;
        MethodName = methodName;
        ArgumentSignature = argumentSignature ?? string.Empty;
        FunctionId = functionId;
        PointKey = pointKey;
        SourceFile = sourceFile;
        DefaultOff = defaultOff;
        LogForwarding = logForwarding;
    }
}

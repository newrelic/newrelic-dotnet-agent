// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;

// Linked into Core (see Core.csproj), which defines NR_OTELDISTRO_CORE; the generator build compiles the other branch.
#if NR_OTELDISTRO_CORE
namespace NewRelic.Agent.Core.OtelDistro.Xml;
#else
namespace NewRelic.OtelDistro.Generators;
#endif

/// <summary>
/// One <c>&lt;match&gt;</c>/<c>&lt;exactMethodMatcher&gt;</c> pair parsed out of NR instrumentation XML, plus the
/// enclosing <c>&lt;tracerFactory&gt;</c>'s name and metric name.
/// </summary>
public sealed class XmlInstrumentationPoint
{
    public string TracerFactoryName { get; }

    public string MetricName { get; }

    public string AssemblyName { get; }

    public string ClassName { get; }

    public string MethodName { get; }

    /// <summary>
    /// Null when the <c>parameters</c> attribute is absent from the <c>exactMethodMatcher</c>. An empty list when
    /// <c>parameters=""</c>, meaning the method takes zero parameters.
    /// </summary>
    public IReadOnlyList<string> ParameterTypes { get; }

    /// <summary>
    /// Raw text of the <c>&lt;match&gt;</c> element's <c>minVersion</c> attribute (inclusive lower bound), or null when
    /// the attribute is absent. Not interpreted here; see <see cref="VersionRangeConverter"/>.
    /// </summary>
    public string MinVersion { get; }

    /// <summary>
    /// Raw text of the <c>&lt;match&gt;</c> element's <c>maxVersion</c> attribute (exclusive upper bound), or null when
    /// the attribute is absent. Not interpreted here; see <see cref="VersionRangeConverter"/>.
    /// </summary>
    public string MaxVersion { get; }

    /// <summary>
    /// False when the enclosing <c>&lt;tracerFactory&gt;</c> carries <c>enabled="false"</c> (literal, case-insensitive).
    /// The point is still parsed so callers can decide what a disabled point means.
    /// </summary>
    public bool Enabled { get; }

    public XmlInstrumentationPoint(
        string tracerFactoryName,
        string metricName,
        string assemblyName,
        string className,
        string methodName,
        IReadOnlyList<string> parameterTypes,
        string minVersion = null,
        string maxVersion = null,
        bool enabled = true)
    {
        TracerFactoryName = tracerFactoryName;
        MetricName = metricName;
        AssemblyName = assemblyName;
        ClassName = className;
        MethodName = methodName;
        ParameterTypes = parameterTypes;
        MinVersion = minVersion;
        MaxVersion = maxVersion;
        Enabled = enabled;
    }
}

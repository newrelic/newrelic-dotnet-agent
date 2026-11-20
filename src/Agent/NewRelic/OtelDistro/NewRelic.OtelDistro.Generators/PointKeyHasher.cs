// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

// Linked into Core (see Core.csproj), which defines NR_OTELDISTRO_CORE; the generator build compiles the other branch.
#if NR_OTELDISTRO_CORE
namespace NewRelic.Agent.Core.OtelDistro.Xml;
#else
namespace NewRelic.OtelDistro.Generators;
#endif

/// <summary>
/// Identity of one instrumentation point for comparing a shipped XML file with an edited copy. FNV-1a 64 over the UTF-8
/// bytes of <c>assembly|class|method|params|rangeKey|tracerFactory|metricName</c>. Unlike <see cref="FunctionIdHasher"/>
/// it includes the tracer factory and metric name, so re-pointing a method at another factory counts as a change.
/// </summary>
public static class PointKeyHasher
{
    /// <param name="point">The parsed point.</param>
    /// <param name="className">The single class name (one segment of a comma-separated <c>className</c>).</param>
    /// <param name="rangeKey"><see cref="VersionRangeConversion.RangeKey"/> of the point's version range.</param>
    public static ulong Compute(XmlInstrumentationPoint point, string className, string rangeKey)
    {
        var parameters = point.ParameterTypes == null ? "*" : string.Join(",", point.ParameterTypes);
        var tracerFactory = point.TracerFactoryName ?? InstrumentationXmlParser.DefaultTracerFactoryName;

        var input = point.AssemblyName + "|" + className + "|" + point.MethodName + "|" + parameters + "|" + rangeKey + "|" + tracerFactory + "|" + point.MetricName;
        return Fnv1a64.AddUtf8(Fnv1a64.OffsetBasis, input);
    }
}

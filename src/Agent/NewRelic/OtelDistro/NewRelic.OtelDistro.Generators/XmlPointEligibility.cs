// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

// Linked into Core (see Core.csproj), which defines NR_OTELDISTRO_CORE; the generator build compiles the other branch.
#if NR_OTELDISTRO_CORE
namespace NewRelic.Agent.Core.OtelDistro.Xml;
#else
namespace NewRelic.OtelDistro.Generators;
#endif

/// <summary>Why the generator emits no record for a parsed point (see <see cref="XmlPointEligibility.Check"/>).</summary>
public enum XmlPointEligibilityResult
{
    /// <summary>The point gets records.</summary>
    Emittable,

    /// <summary>
    /// An empty or space-padded class name: the profiler keeps such segments of a comma split ("A, B," gives "A", " B"
    /// and ""), but a type name never has leading or trailing white space, so they can never match a type.
    /// </summary>
    BadClassName,

    /// <summary>More parameters than OTel's CallTarget begin methods take (<see cref="XmlPointEligibility.MaxParameters"/>).</summary>
    TooManyParameters,

    /// <summary>The version range is empty, so no assembly version can match.</summary>
    EmptyRange,
}

/// <summary>
/// The per-point rules that decide whether the generator emits records for a parsed XML point, shared by the generator
/// and by the distro's XML toggles so that a point the generator dropped is never reported as unsupported at run time.
/// Duplicate points (NRDG003) are decided across files and are not covered here.
/// </summary>
public static class XmlPointEligibility
{
    /// <summary>The most parameters an OTel CallTarget begin method takes.</summary>
    public const int MaxParameters = 8;

    /// <summary>
    /// Checks the point in the generator's order (class name, parameter count, version range). <paramref name="range"/> is
    /// the converted version range, or null when the class name or parameter count already decided.
    /// </summary>
    public static XmlPointEligibilityResult Check(XmlInstrumentationPoint point, out VersionRangeConversion range)
    {
        range = null;
        if (point.ClassName != null && (point.ClassName.Length == 0 || point.ClassName.Trim().Length != point.ClassName.Length))
        {
            return XmlPointEligibilityResult.BadClassName;
        }

        if (point.ParameterTypes != null && point.ParameterTypes.Count > MaxParameters)
        {
            return XmlPointEligibilityResult.TooManyParameters;
        }

        range = VersionRangeConverter.Convert(point.MinVersion, point.MaxVersion);
        return range.IsEmpty ? XmlPointEligibilityResult.EmptyRange : XmlPointEligibilityResult.Emittable;
    }
}

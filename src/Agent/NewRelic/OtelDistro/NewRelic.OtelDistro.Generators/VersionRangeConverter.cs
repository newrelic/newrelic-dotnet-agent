// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

// Linked into Core (see Core.csproj), which defines NR_OTELDISTRO_CORE; the generator build compiles the other branch.
#if NR_OTELDISTRO_CORE
namespace NewRelic.Agent.Core.OtelDistro.Xml;
#else
namespace NewRelic.OtelDistro.Generators;
#endif

/// <summary>What was lost or ignored when one XML version bound was converted to an OTel bound.</summary>
public enum VersionBoundIssue
{
    /// <summary>The bound was absent or converted exactly.</summary>
    None,

    /// <summary>The text is not a version the NR profiler accepts, so it means "no bound", as it does in agent mode.</summary>
    Invalid,

    /// <summary>The bound has a non-zero revision, which OTel cannot express; it was widened to the three-part bound.</summary>
    RevisionWidened
}

/// <summary>
/// An NR <c>[minVersion, maxVersion)</c> four-part range expressed as the inclusive three-part range OTel's CallTarget
/// definitions filter on. Immutable.
/// </summary>
public sealed class VersionRangeConversion
{
    public VersionRangeConversion(ushort minMajor, ushort minMinor, ushort minPatch, ushort maxMajor, ushort maxMinor, ushort maxPatch, VersionBoundIssue minIssue, VersionBoundIssue maxIssue)
    {
        MinMajor = minMajor;
        MinMinor = minMinor;
        MinPatch = minPatch;
        MaxMajor = maxMajor;
        MaxMinor = maxMinor;
        MaxPatch = maxPatch;
        MinIssue = minIssue;
        MaxIssue = maxIssue;
    }

    public ushort MinMajor { get; }

    public ushort MinMinor { get; }

    public ushort MinPatch { get; }

    public ushort MaxMajor { get; }

    public ushort MaxMinor { get; }

    public ushort MaxPatch { get; }

    public VersionBoundIssue MinIssue { get; }

    public VersionBoundIssue MaxIssue { get; }

    /// <summary>True when the range is 0.0.0 to 65535.65535.65535, i.e. neither bound constrains anything.</summary>
    public bool IsDefault =>
        MinMajor == 0 && MinMinor == 0 && MinPatch == 0 &&
        MaxMajor == ushort.MaxValue && MaxMinor == ushort.MaxValue && MaxPatch == ushort.MaxValue;

    /// <summary>True when the minimum is above the maximum, so no assembly version can match.</summary>
    public bool IsEmpty
    {
        get
        {
            if (MinMajor != MaxMajor)
            {
                return MinMajor > MaxMajor;
            }

            if (MinMinor != MaxMinor)
            {
                return MinMinor > MaxMinor;
            }

            return MinPatch > MaxPatch;
        }
    }

    /// <summary>"" for the default range, otherwise "min-max" as dotted three-part text, e.g. "1.0.0-5.65535.65535".</summary>
    public string RangeKey => IsDefault
        ? string.Empty
        : MinMajor + "." + MinMinor + "." + MinPatch + "-" + MaxMajor + "." + MaxMinor + "." + MaxPatch;
}

/// <summary>
/// Converts the NR instrumentation XML <c>minVersion</c>/<c>maxVersion</c> attributes (assembly version, min inclusive,
/// max exclusive, four parts) to OTel CallTarget bounds (assembly version, both inclusive, three parts, revision
/// ignored). Parsing mirrors the profiler's <c>AssemblyVersion::Create</c> so that a range means the same thing in
/// distro mode as in agent mode.
/// </summary>
public static class VersionRangeConverter
{
    private const ushort Max = ushort.MaxValue;

    private enum ParseOutcome
    {
        Absent,
        Invalid,
        NoBound,
        Version
    }

    public static VersionRangeConversion Convert(string minVersion, string maxVersion)
    {
        ushort minMajor = 0, minMinor = 0, minPatch = 0;
        var minIssue = VersionBoundIssue.None;
        var minOutcome = Parse(minVersion, out var min);
        if (minOutcome == ParseOutcome.Invalid)
        {
            minIssue = VersionBoundIssue.Invalid;
        }
        else if (minOutcome == ParseOutcome.Version)
        {
            // NR skips when found < min (four-part); OTel skips when found3 < min3, so dropping the revision only widens.
            minMajor = min.Major;
            minMinor = min.Minor;
            minPatch = min.Patch;
            minIssue = min.Revision != 0 ? VersionBoundIssue.RevisionWidened : VersionBoundIssue.None;
        }

        ushort maxMajor = Max, maxMinor = Max, maxPatch = Max;
        var maxIssue = VersionBoundIssue.None;
        var maxOutcome = Parse(maxVersion, out var max);
        if (maxOutcome == ParseOutcome.Invalid)
        {
            maxIssue = VersionBoundIssue.Invalid;
        }
        else if (maxOutcome == ParseOutcome.Version)
        {
            if (max.Revision != 0)
            {
                // NR skips when found >= max (four-part). With a revision, every found3 <= max3 can still pass (found
                // 1.2.3.0 < 1.2.3.4), so the inclusive three-part bound is max3 itself, wider than NR's.
                maxMajor = max.Major;
                maxMinor = max.Minor;
                maxPatch = max.Patch;
                maxIssue = VersionBoundIssue.RevisionWidened;
            }
            else
            {
                // OTel's max is inclusive and NR's is exclusive: the largest three-part version below max.
                if (max.Patch > 0)
                {
                    maxMajor = max.Major;
                    maxMinor = max.Minor;
                    maxPatch = (ushort)(max.Patch - 1);
                }
                else if (max.Minor > 0)
                {
                    maxMajor = max.Major;
                    maxMinor = (ushort)(max.Minor - 1);
                    maxPatch = Max;
                }
                else
                {
                    maxMajor = (ushort)(max.Major - 1);
                    maxMinor = Max;
                    maxPatch = Max;
                }
            }
        }

        return new VersionRangeConversion(minMajor, minMinor, minPatch, maxMajor, maxMinor, maxPatch, minIssue, maxIssue);
    }

    private static ParseOutcome Parse(string text, out ParsedVersion version)
    {
        version = default;

        // Create returns null at once for an empty string, with no warning; an absent attribute reads as "" in the
        // profiler (GetAttributeOrEmptyString), so null and "" are the same thing here. (AssemblyVersion.h:74-77)
        if (string.IsNullOrEmpty(text))
        {
            return ParseOutcome.Absent;
        }

        // Split on '.' keeps empty segments, and std::stoi of "" throws, so "1." and "1..2" are invalid. (:78, :85-89)
        var segments = text.Split('.');
        if (segments.Length > 4)
        {
            return ParseOutcome.Invalid;
        }

        var values = new int[4];
        for (var i = 0; i < segments.Length; i++)
        {
            // stoi failure (no digits, or beyond int) is caught and means invalid; so are negatives and over 65535.
            // (:91-103, :106-110, :112-118)
            if (!TryParseStoi(segments[i], out values[i]) || values[i] < 0 || values[i] > Max)
            {
                return ParseOutcome.Invalid;
            }
        }

        // 0.0.0.0 is "no bound", silently. (:120-124)
        if (values[0] == 0 && values[1] == 0 && values[2] == 0 && values[3] == 0)
        {
            return ParseOutcome.NoBound;
        }

        version = new ParsedVersion((ushort)values[0], (ushort)values[1], (ushort)values[2], (ushort)values[3]);
        return ParseOutcome.Version;
    }

    /// <summary>
    /// std::stoi(str, nullptr, 10) as AssemblyVersion::Create calls it through xstoi (xplat.h:98): skip leading C-locale
    /// whitespace, take an optional sign, read at least one digit, stop at the first non-digit and ignore the rest, and
    /// fail on no digits or a value outside int. "1x" reads as 1, "+2" as 2, "-0" as 0, "0xff" as 0.
    /// </summary>
    private static bool TryParseStoi(string text, out int value)
    {
        value = 0;
        var index = 0;
        while (index < text.Length && IsCSpace(text[index]))
        {
            index++;
        }

        var negative = false;
        if (index < text.Length && (text[index] == '+' || text[index] == '-'))
        {
            negative = text[index] == '-';
            index++;
        }

        if (index >= text.Length || text[index] < '0' || text[index] > '9')
        {
            return false;
        }

        long magnitude = 0;
        while (index < text.Length && text[index] >= '0' && text[index] <= '9')
        {
            magnitude = magnitude * 10 + (text[index] - '0');
            if (magnitude > (long)int.MaxValue + 1)
            {
                return false;
            }

            index++;
        }

        // int.MinValue is the one magnitude above int.MaxValue that stoi accepts, and only with a minus sign.
        if (magnitude > int.MaxValue && !negative)
        {
            return false;
        }

        value = (int)(negative ? -magnitude : magnitude);
        return true;
    }

    private static bool IsCSpace(char c) => c == ' ' || (c >= '\t' && c <= '\r');

    private readonly struct ParsedVersion
    {
        public ParsedVersion(ushort major, ushort minor, ushort patch, ushort revision)
        {
            Major = major;
            Minor = minor;
            Patch = patch;
            Revision = revision;
        }

        public ushort Major { get; }

        public ushort Minor { get; }

        public ushort Patch { get; }

        public ushort Revision { get; }
    }
}

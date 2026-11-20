// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using NewRelic.Agent.Extensions.Logging;

namespace NewRelic.Agent.Core.OtelDistro;

/// <summary>
/// Logs and counts what the NR instrumentation XML on disk did to the distro's compiled instrumentation (see
/// <see cref="DistroXmlEffectiveSet"/>). Each report records its supportability metrics first, through
/// <see cref="DistroConfigMetrics"/>, so a failing logger cannot drop them, then logs. Stateless: what was already
/// reported is tracked by the caller (<see cref="DistroXmlToggles"/>). Never throws.
/// </summary>
public static class DistroXmlToggleDiagnostics
{
    public const string PointsDisabledMetricName = "Supportability/Dotnet/OtelDistro/Xml/PointsDisabled";
    public const string PointsEnabledMetricName = "Supportability/Dotnet/OtelDistro/Xml/PointsEnabled";
    public const string UnsupportedMetricName = "Supportability/Dotnet/OtelDistro/Xml/Unsupported";
    public const string ParseErrorMetricName = "Supportability/Dotnet/OtelDistro/Xml/ParseError";
    public const string RegisterUnavailableMetricName = "Supportability/Dotnet/OtelDistro/Xml/RegisterUnavailable";
    public const string RegisteredAtRunTimeMetricName = "Supportability/Dotnet/OtelDistro/Xml/RegisteredAtRunTime";
    public const string UnreadableMetricName = "Supportability/Dotnet/OtelDistro/Xml/Unreadable";
    public const string FolderMissingMetricName = "Supportability/Dotnet/OtelDistro/Xml/FolderMissing";

    private const int MaxUnsupportedWarnings = 20;
    private const string OverloadWide = "*";

    /// <summary>
    /// Reports the new on-state <paramref name="on"/> against the previous one <paramref name="before"/> (index = site
    /// index). When sites changed: one count per point that changed, an Info summary and one Info per changed point (an
    /// overload-wide point once, its parameters shown as "(*)"). When nothing changed: one Info line, saying the XML matches
    /// the compiled instrumentation only when no file differs from what shipped and nothing is unsupported. Then a Warn
    /// per point in <paramref name="newUnsupported"/> (the first 20, then one line for the rest) with one count each; the
    /// summary counts every unsupported point of <paramref name="result"/>. <paramref name="changedFiles"/> names the files
    /// that differ from the last applied state.
    /// </summary>
    public static void ReportApplied(DistroInstrumentationSites sites, bool[] before, bool[] on, DistroXmlEffectiveSetResult result, IReadOnlyList<string> changedFiles, IReadOnlyList<DistroXmlUnsupportedPoint> newUnsupported)
    {
        try
        {
            // Plain loop first: the common case (nothing changed) builds no set.
            var turnedOff = new List<int>();
            var turnedOn = new List<int>();
            for (var i = 0; i < sites.Count; i++)
            {
                if (before[i] != on[i])
                {
                    (on[i] ? turnedOn : turnedOff).Add(i);
                }
            }

            var offPoints = CountPoints(sites, turnedOff, PointsDisabledMetricName);
            var onPoints = CountPoints(sites, turnedOn, PointsEnabledMetricName);
            foreach (var unused in newUnsupported)
            {
                DistroConfigMetrics.Record(UnsupportedMetricName);
            }

            var files = changedFiles.Count == 0 ? "none" : string.Join(", ", changedFiles);
            if (turnedOff.Count == 0 && turnedOn.Count == 0)
            {
                if (changedFiles.Count == 0 && result.ParsedFiles.Count == 0 && result.MissingFiles.Count == 0 && result.Unsupported.Count == 0)
                {
                    Log.Info("NR instrumentation XML matches the distro's compiled instrumentation ({0} shipped instrumentation files are not compiled into the distro yet; edits to them have no effect).", result.HashOnlyFileCount);
                }
                else
                {
                    Log.Info("NR instrumentation XML applied: no compiled points changed, {0} not supported, from files: {1}", result.Unsupported.Count, files);
                }
            }
            else
            {
                Log.Info("NR instrumentation XML applied: {0} points turned off, {1} turned on, {2} not supported, from files: {3}", offPoints, onPoints, result.Unsupported.Count, files);
                LogChangedPoints(sites, turnedOff, "off");
                LogChangedPoints(sites, turnedOn, "on");
            }

            LogUnsupported(newUnsupported);
        }
        catch (Exception)
        {
            // Diagnostics must never reach OTel start-up, the app, or a CLR callback.
        }
    }

    /// <summary>Reports a file that failed to parse on a live change; the previous state stays in force.</summary>
    public static void ReportRejected(DistroXmlEffectiveSetResult result)
    {
        try
        {
            DistroConfigMetrics.Record(ParseErrorMetricName);
            Log.Error("NR instrumentation XML {0} could not be parsed ({1}); instrumentation will not be refreshed.", result.RejectedFile, result.RejectedReason);
        }
        catch (Exception)
        {
        }
    }

    /// <summary>Reports a file that failed to parse at start-up and was skipped (agent mode's start-up rule).</summary>
    public static void ReportSkippedAtStartup(string file, string reason)
    {
        try
        {
            DistroConfigMetrics.Record(ParseErrorMetricName);
            Log.Error("NR instrumentation XML {0} could not be parsed ({1}); it was skipped (a shipped file keeps the distro's compiled defaults), and live instrumentation updates will not be applied until it is fixed.", file, reason);
        }
        catch (Exception)
        {
        }
    }

    /// <summary>Reports a file that could not be read at start-up; its shipped defaults are used.</summary>
    public static void ReportUnreadable(string file, string reason)
    {
        try
        {
            DistroConfigMetrics.Record(UnreadableMetricName);
            Log.Warn("NR instrumentation XML {0} could not be read ({1}); using the distro's compiled defaults for it.", file, reason);
        }
        catch (Exception)
        {
        }
    }

    /// <summary>Reports a file that could not be read on a live change; the current state is kept.</summary>
    public static void ReportUnreadableNotRefreshed(string file, string reason)
    {
        try
        {
            DistroConfigMetrics.Record(UnreadableMetricName);
            Log.Warn("NR instrumentation XML {0} could not be read ({1}); instrumentation was not refreshed, and live changes stay blocked until the file can be read again.", file, reason);
        }
        catch (Exception)
        {
        }
    }

    /// <summary>
    /// Reports that the extensions folder (null: there is none) does not exist; the current instrumentation state is kept
    /// (the compiled defaults at start-up).
    /// </summary>
    public static void ReportFolderMissing(string path)
    {
        try
        {
            DistroConfigMetrics.Record(FolderMissingMetricName);
            Log.Warn("NR instrumentation XML folder {0} was not found; using the distro's compiled instrumentation defaults.", path ?? "(no extensions directory)");
        }
        catch (Exception)
        {
        }
    }

    /// <summary>
    /// Reports that OTel's run-time registration of call-target definitions could not be used for <paramref name="pointCount"/>
    /// points, which stay off. Counted every time; logged as an Error the first time for a reason and as Debug when
    /// <paramref name="alreadyReported"/>.
    /// </summary>
    public static void ReportRegisterUnavailable(string failure, int pointCount, bool alreadyReported)
    {
        try
        {
            DistroConfigMetrics.Record(RegisterUnavailableMetricName);
            const string message = "NR instrumentation could not be turned on at run time: OTel run-time registration is unavailable ({0}); {1} points stay off.";
            if (alreadyReported)
            {
                Log.Debug(message, failure, pointCount);
            }
            else
            {
                Log.Error(message, failure, pointCount);
            }
        }
        catch (Exception)
        {
        }
    }

    /// <summary>
    /// Reports that <paramref name="pointCount"/> points the XML turned on are registered with OTel in this process at run
    /// time (they were not registered at start-up). Through the coordinator another AppDomain may have sent them already,
    /// so the wording does not claim this domain did; the coordinator's own line says which. The runtime may not
    /// instrument callers that already compiled or inlined those methods.
    /// </summary>
    public static void ReportRegisteredAtRunTime(int pointCount)
    {
        try
        {
            DistroConfigMetrics.Record(RegisteredAtRunTimeMetricName);
            Log.Info("NR instrumentation XML turned on {0} points that were not registered at start-up; they are now registered with OTel in this process. Methods already compiled into their callers may stay uninstrumented until the application restarts (restart recommended).", pointCount);
        }
        catch (Exception)
        {
        }
    }

    // Records one metric per distinct point key among the changed sites and returns that count.
    private static int CountPoints(DistroInstrumentationSites sites, List<int> changed, string metricName)
    {
        if (changed.Count == 0)
        {
            return 0;
        }

        var points = new HashSet<ulong>();
        foreach (var index in changed)
        {
            if (points.Add(sites.PointKey(index)))
            {
                DistroConfigMetrics.Record(metricName);
            }
        }

        return points.Count;
    }

    // One line per distinct point key, in first-seen order, named by its first changed site. A key with more than one record
    // in the table is an overload-wide point (one record per arity), shown with "(*)" even when only some of its records
    // changed.
    private static void LogChangedPoints(DistroInstrumentationSites sites, List<int> changed, string direction)
    {
        if (changed.Count == 0)
        {
            return;
        }

        var order = new List<ulong>();
        var groups = new Dictionary<ulong, (int First, int Total)>();
        foreach (var index in changed)
        {
            var key = sites.PointKey(index);
            if (!groups.ContainsKey(key))
            {
                groups.Add(key, (index, 0));
                order.Add(key);
            }
        }

        // Records per changed key over the whole table (a changed key's records need not all have changed).
        for (var i = 0; i < sites.Count; i++)
        {
            var key = sites.PointKey(i);
            if (groups.TryGetValue(key, out var group))
            {
                groups[key] = (group.First, group.Total + 1);
            }
        }

        foreach (var key in order)
        {
            var group = groups[key];

            // The index is in range, so TryGet always succeeds.
            sites.TryGet(group.First, out var site);
            var parameters = group.Total > 1 ? OverloadWide : site.ArgumentSignature;
            Log.Info("NR instrumentation XML turned {0} {1}.{2}({3}) [{4}].", direction, site.TypeName, site.MethodName, parameters, site.TracerFactoryName);
        }
    }

    private static void LogUnsupported(IReadOnlyList<DistroXmlUnsupportedPoint> unsupported)
    {
        for (var i = 0; i < unsupported.Count && i < MaxUnsupportedWarnings; i++)
        {
            var point = unsupported[i];
            Log.Warn("NR instrumentation XML {0} names {1}.{2}({3}) [{4}], which is not part of the distro's compiled instrumentation; it will not be instrumented (custom instrumentation is not supported in distro mode yet).", point.File, point.TypeName, point.MethodName, point.Parameters, point.TracerFactory);
        }

        if (unsupported.Count > MaxUnsupportedWarnings)
        {
            Log.Warn("... and {0} more NR instrumentation XML points are not supported in distro mode.", unsupported.Count - MaxUnsupportedWarnings);
        }
    }
}

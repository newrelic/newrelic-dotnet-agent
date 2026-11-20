// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace NewRelic.OtelDistro.Generators;

/// <summary>
/// Build-time generator that reads every additional file ending in <c>.xml</c> (NR instrumentation XML) and
/// emits one <c>GeneratedDistroIntegrations.g.cs</c> containing a CallTarget integration class per instrumented
/// method plus a <c>GeneratedDistroDefinitions.Table</c> (an NRIDT table) for the distro plugin's definitions payload.
/// A file with the <c>NrDistroHashOnly="true"</c> item metadata is not parsed: it is only listed, by home name and
/// content hash, in the table's shipped XML manifest. Points disabled in the XML (<c>enabled="false"</c>) are emitted
/// as default-off records.
/// </summary>
[Generator]
public sealed class DistroIntegrationGenerator : IIncrementalGenerator
{
    public const string GeneratedNamespace = "NewRelic.OtelDistro.Plugin.Generated";
    public const string GeneratedFileName = "GeneratedDistroIntegrations.g.cs";

    /// <summary>
    /// The analyzer config key of the <c>NrDistroHashOnly</c> AdditionalFiles item metadata (made visible to the generator
    /// by <c>CompilerVisibleItemMetadata</c> in the plugin csproj). "true" (ignoring case) marks a hash-only file.
    /// </summary>
    public const string HashOnlyMetadataKey = "build_metadata.AdditionalFiles.NrDistroHashOnly";

    /// <summary>
    /// The analyzer config key of the <c>TargetFrameworkIdentifier</c> MSBuild property (made visible to the generator by
    /// <c>CompilerVisibleProperty</c> in the plugin csproj). ".NETCoreApp" (ignoring case) selects the .NET (Core/5+) rows
    /// of <see cref="OtelInstrumentationMap"/>; anything else, or no value, the .NET Framework rows.
    /// </summary>
    public const string TargetFrameworkIdentifierKey = "build_property.TargetFrameworkIdentifier";

    private static readonly DiagnosticDescriptor MissingParametersDescriptor = new DiagnosticDescriptor(
        "NRDG001",
        "Instrumentation match without parameters",
        "Match {0}/{1}.{2} has no 'parameters'; instrumenting all overloads with 0-8 parameters",
        "NewRelic.OtelDistro.Generators",
        DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor TooManyParametersDescriptor = new DiagnosticDescriptor(
        "NRDG002",
        "Instrumentation match has too many parameters",
        "The match for {0}/{1}.{2} has {3} parameters, more than the supported maximum of 8; it was skipped",
        "NewRelic.OtelDistro.Generators",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor DuplicateMatchDescriptor = new DiagnosticDescriptor(
        "NRDG003",
        "Duplicate instrumentation match",
        "The match for {0}/{1}.{2} duplicates another match with the same assembly, class, method, parameters and version range; it was skipped (with the same tracer factory a default-on match wins over a default-off one, otherwise the first wins; with different factories, default-on matches keep the first)",
        "NewRelic.OtelDistro.Generators",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor MalformedXmlDescriptor = new DiagnosticDescriptor(
        "NRDG004",
        "Malformed instrumentation XML",
        "The instrumentation XML file '{0}' is malformed and was skipped",
        "NewRelic.OtelDistro.Generators",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor TableNotWrittenDescriptor = new DiagnosticDescriptor(
        "NRDG005",
        "Instrumentation definitions table cannot be written",
        "The instrumentation definitions table cannot be written: {0}. No source was generated.",
        "NewRelic.OtelDistro.Generators",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor InvalidVersionBoundDescriptor = new DiagnosticDescriptor(
        "NRDG006",
        "Invalid instrumentation version bound",
        "The {0} '{1}' of the match for {2} is not a version; it is treated as no bound, as agent mode does",
        "NewRelic.OtelDistro.Generators",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor RevisionWidenedDescriptor = new DiagnosticDescriptor(
        "NRDG007",
        "Instrumentation version bound widened",
        "The {0} '{1}' of the match for {2} has a non-zero revision, which OTel cannot express; it was widened to {3} (inclusive)",
        "NewRelic.OtelDistro.Generators",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor EmptyVersionRangeDescriptor = new DiagnosticDescriptor(
        "NRDG008",
        "Empty instrumentation version range",
        "The match for {0} has an empty version range (minVersion '{1}', maxVersion '{2}'); no version can match, so it was skipped",
        "NewRelic.OtelDistro.Generators",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor OverloadWideResidueDescriptor = new DiagnosticDescriptor(
        "NRDG009",
        "Overload-wide match in range where the explicit match is not",
        "The {6}overload-wide match for {0}/{1}.{2} (versions {3}) is in range for versions where no explicit match of the same method with {4} parameters is (explicit versions: {5}); agent mode instruments nothing there, the distro instruments the overload",
        "NewRelic.OtelDistro.Generators",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor OverlappingRangesDescriptor = new DiagnosticDescriptor(
        "NRDG010",
        "Overlapping instrumentation version ranges with different tracer factories",
        "The {5}matches for {0} have overlapping version ranges {1} ('{2}') and {3} ('{4}') with different tracer factories; the distro uses the first in XML order, agent mode's choice is unspecified",
        "NewRelic.OtelDistro.Generators",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor DefaultOffOnlyCoverageDescriptor = new DiagnosticDescriptor(
        "NRDG011",
        "OTel instrumentation stays on for a library NR covers only with default-off points",
        "OTel instrumentation for {0} stays on: NR covers it only with default-off points ({1})",
        "NewRelic.OtelDistro.Generators",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var xmlFiles = context.AdditionalTextsProvider
            .Where(static file => file.Path.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            .Combine(context.AnalyzerConfigOptionsProvider)
            .Select(static (pair, _) => new XmlFile(pair.Left, IsHashOnly(pair.Right.GetOptions(pair.Left))));

        var runtime = context.AnalyzerConfigOptionsProvider.Select(static (provider, _) => GetTargetRuntime(provider.GlobalOptions));

        context.RegisterSourceOutput(xmlFiles.Collect().Combine(runtime), static (productionContext, pair) => Execute(productionContext, pair.Left, pair.Right));
    }

    private static bool IsHashOnly(AnalyzerConfigOptions options) =>
        options.TryGetValue(HashOnlyMetadataKey, out var value) && string.Equals(value?.Trim(), "true", StringComparison.OrdinalIgnoreCase);

    private static DistroTargetRuntime GetTargetRuntime(AnalyzerConfigOptions globalOptions) =>
        globalOptions.TryGetValue(TargetFrameworkIdentifierKey, out var value) && string.Equals(value?.Trim(), ".NETCoreApp", StringComparison.OrdinalIgnoreCase)
            ? DistroTargetRuntime.NetCore
            : DistroTargetRuntime.NetFramework;

    private static void Execute(SourceProductionContext context, ImmutableArray<XmlFile> files, DistroTargetRuntime runtime)
    {
        var texts = files.Select(file => file.Text.GetText(context.CancellationToken)?.ToString() ?? string.Empty).ToList();
        var manifest = BuildManifest(files, texts, out var fileIndexByHomeName);
        var candidates = new List<Candidate>();

        for (var i = 0; i < files.Length; i++)
        {
            if (files[i].HashOnly)
            {
                continue;
            }

            var sourceFile = ShippedXmlFileNames.TryGetHomeName(files[i].Text.Path, out var homeName)
                ? (ushort)fileIndexByHomeName[homeName]
                : SourcedXmlPoint.PluginSourceFile;
            CollectCandidates(context, files[i].Text.Path, texts[i], sourceFile, candidates);
        }

        // Dedupe on the full key (assembly, class, method, parameters, range):
        //  - same tracer factory: a default-on point beats a default-off one whatever the file order (a disabled copy can
        //    never hide an enabled point); otherwise the first wins;
        //  - different tracer factories, both default-on: the first wins, as before;
        //  - different tracer factories, at least one default-off: both are kept, so turning either factory on in the XML
        //    finds its own point in the table. A kept point after the group's primary (its first default-on point, else
        //    its first point) folds its factory into its function id, so class names stay unique and the primary keeps
        //    the id it had before default-off points were emitted.
        var defaultOnFactoryKeys = new HashSet<string>(candidates.Where(c => c.Point.Enabled).Select(c => c.FactoryKey), StringComparer.Ordinal);
        var keptCandidates = new List<Candidate>();
        var keptByFullKey = new Dictionary<string, List<Candidate>>(StringComparer.Ordinal);
        var kept = new List<RangedPoint>();
        var keptByKey = new Dictionary<string, List<RangedPoint>>();

        foreach (var candidate in candidates)
        {
            var point = candidate.Point;
            keptByFullKey.TryGetValue(candidate.FullKey, out var sameKey);
            if ((!point.Enabled && defaultOnFactoryKeys.Contains(candidate.FactoryKey))
                || (sameKey != null && sameKey.Any(k => k.Factory == candidate.Factory || (point.Enabled && k.Point.Enabled))))
            {
                context.ReportDiagnostic(Diagnostic.Create(DuplicateMatchDescriptor, Location.None, point.AssemblyName, point.ClassName, point.MethodName));
                continue;
            }

            if (sameKey == null)
            {
                sameKey = new List<Candidate>();
                keptByFullKey.Add(candidate.FullKey, sameKey);
            }

            sameKey.Add(candidate);

            var rangedPoint = new RangedPoint(point, candidate.Range);
            if (!keptByKey.TryGetValue(candidate.Key, out var siblings))
            {
                siblings = new List<RangedPoint>();
                keptByKey.Add(candidate.Key, siblings);
            }

            foreach (var sibling in siblings)
            {
                // A sibling with the very same range is a kept same-key pair (above), not an overlap.
                if (sibling.Point.TracerFactoryName != point.TracerFactoryName && sibling.Range.RangeKey != candidate.Range.RangeKey && Overlaps(sibling, rangedPoint))
                {
                    context.ReportDiagnostic(Diagnostic.Create(OverlappingRangesDescriptor, Location.None, candidate.EntryName, FormatRange(sibling), sibling.Point.TracerFactoryName, FormatRange(rangedPoint), point.TracerFactoryName, DefaultOffQualifier(sibling.Point.Enabled || point.Enabled)));
                }
            }

            siblings.Add(rangedPoint);
            kept.Add(rangedPoint);
            keptCandidates.Add(candidate);
        }

        var primaries = new HashSet<Candidate>(keptByFullKey.Values.Select(group => group.FirstOrDefault(c => c.Point.Enabled) ?? group[0]));
        var points = keptCandidates.Select(c => new SourcedXmlPoint(c.Point, c.SourceFile, distinguishByTracerFactory: !primaries.Contains(c))).ToList();

        ReportOverloadWideResidues(context, kept);
        ReportDefaultOffOnlyCoverage(context, points.Select(p => p.Point).ToList(), runtime);

        if (!IntegrationSourceEmitter.TryEmit(points, manifest, GeneratedNamespace, runtime, out var source, out var tableFailure))
        {
            context.ReportDiagnostic(Diagnostic.Create(TableNotWrittenDescriptor, Location.None, tableFailure));
            return;
        }

        context.AddSource(GeneratedFileName, SourceText.From(source, Encoding.UTF8));
    }

    /// <summary>
    /// The shipped XML manifest: one file per home name (ignoring case), sorted by home name (ordinal, ignoring case) so
    /// file indexes do not depend on item order. A file listed both compiled and hash-only is compiled, with the compiled
    /// copy's hash; when two compiled files share a home name the first one's hash is kept and both feed that index.
    /// Files without a home name (the plugin's own XML) are not listed.
    /// </summary>
    private static IReadOnlyList<ShippedXmlFile> BuildManifest(ImmutableArray<XmlFile> files, List<string> texts, out Dictionary<string, int> fileIndexByHomeName)
    {
        var byHomeName = new Dictionary<string, ShippedXmlFile>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < files.Length; i++)
        {
            if (!ShippedXmlFileNames.TryGetHomeName(files[i].Text.Path, out var homeName))
            {
                continue;
            }

            var compiled = !files[i].HashOnly;
            if (!byHomeName.TryGetValue(homeName, out var existing) || (compiled && !existing.Compiled))
            {
                byHomeName[homeName] = new ShippedXmlFile(homeName, XmlContentHasher.Compute(texts[i]), compiled);
            }
        }

        var manifest = byHomeName.Values.OrderBy(file => file.HomeName, StringComparer.OrdinalIgnoreCase).ToList();
        fileIndexByHomeName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < manifest.Count; index++)
        {
            fileIndexByHomeName.Add(manifest[index].HomeName, index);
        }

        return manifest;
    }

    /// <summary>
    /// Parses one compiled file and adds every point that can be emitted, in XML order, reporting the per-point
    /// diagnostics (NRDG001, 002, 004, 006, 007, 008). Dedupe happens afterwards, across all files.
    /// </summary>
    private static void CollectCandidates(SourceProductionContext context, string path, string text, ushort sourceFile, List<Candidate> candidates)
    {
        var result = InstrumentationXmlParser.Parse(text);

        if (result.Errors.Count > 0)
        {
            context.ReportDiagnostic(Diagnostic.Create(MalformedXmlDescriptor, Location.None, path));
            return;
        }

        foreach (var point in result.Points)
        {
            // Shared with the run-time XML toggles (XmlPointEligibility), so a point dropped here is never reported there.
            var eligibility = XmlPointEligibility.Check(point, out var range);
            if (eligibility == XmlPointEligibilityResult.BadClassName)
            {
                continue;
            }

            string key;

            if (point.ParameterTypes == null)
            {
                context.ReportDiagnostic(Diagnostic.Create(MissingParametersDescriptor, Location.None, point.AssemblyName, point.ClassName, point.MethodName));
                key = point.AssemblyName + "|" + point.ClassName + "|" + point.MethodName + "|*";
            }
            else
            {
                if (eligibility == XmlPointEligibilityResult.TooManyParameters)
                {
                    context.ReportDiagnostic(Diagnostic.Create(TooManyParametersDescriptor, Location.None, point.AssemblyName, point.ClassName, point.MethodName, point.ParameterTypes.Count));
                    continue;
                }

                key = point.AssemblyName + "|" + point.ClassName + "|" + point.MethodName + "|" + string.Join(",", point.ParameterTypes);
            }

            var entryName = EntryName(point);
            ReportBoundIssue(context, "minVersion", point.MinVersion, range.MinIssue, entryName, FormatVersion(range.MinMajor, range.MinMinor, range.MinPatch));
            ReportBoundIssue(context, "maxVersion", point.MaxVersion, range.MaxIssue, entryName, FormatVersion(range.MaxMajor, range.MaxMinor, range.MaxPatch));

            if (eligibility == XmlPointEligibilityResult.EmptyRange)
            {
                context.ReportDiagnostic(Diagnostic.Create(EmptyVersionRangeDescriptor, Location.None, entryName, point.MinVersion ?? string.Empty, point.MaxVersion ?? string.Empty));
                continue;
            }

            // The range is part of the key, so version-split siblings (same method, different ranges) are all kept;
            // OTel native filters them by the loaded assembly's version, as the profiler does.
            candidates.Add(new Candidate(point, key, range, entryName, sourceFile));
        }
    }

    /// <summary>
    /// The OTel disable list counts default-on points only (see IntegrationSourceEmitter). Warn for each assembly whose
    /// default-off points would trigger a map row that the default-on points do not: OTel keeps instrumenting that
    /// library, and turning NR's points on later does not turn OTel's off.
    /// </summary>
    private static void ReportDefaultOffOnlyCoverage(SourceProductionContext context, List<XmlInstrumentationPoint> points, DistroTargetRuntime runtime)
    {
        var onAssemblies = new HashSet<string>(points.Where(p => p.Enabled).Select(p => p.AssemblyName), StringComparer.Ordinal);
        var offOnly = points.Where(p => !p.Enabled && !onAssemblies.Contains(p.AssemblyName)).GroupBy(p => p.AssemblyName, StringComparer.Ordinal).ToList();
        if (offOnly.Count == 0)
        {
            return;
        }

        var all = new HashSet<string>(onAssemblies.Concat(offOnly.Select(g => g.Key)), StringComparer.Ordinal);
        var rowsOnlyFromOff = OtelInstrumentationMap.Rows
            .Where(row => (row.OtelIds.Count > 0 || row.ExcludeSources.Count > 0) && row.IsTriggeredBy(all, runtime) && !row.IsTriggeredBy(onAssemblies, runtime))
            .ToList();

        foreach (var assembly in offOnly)
        {
            if (rowsOnlyFromOff.Any(row => row.NrTargetAssemblies.Contains(assembly.Key)))
            {
                var factories = string.Join(", ", assembly.Select(p => p.TracerFactoryName ?? InstrumentationXmlParser.DefaultTracerFactoryName).Distinct(StringComparer.Ordinal));
                context.ReportDiagnostic(Diagnostic.Create(DefaultOffOnlyCoverageDescriptor, Location.None, assembly.Key, factories));
            }
        }
    }

    private static void ReportBoundIssue(SourceProductionContext context, string attribute, string text, VersionBoundIssue issue, string entryName, string emittedBound)
    {
        if (issue == VersionBoundIssue.Invalid)
        {
            context.ReportDiagnostic(Diagnostic.Create(InvalidVersionBoundDescriptor, Location.None, attribute, text, entryName));
        }
        else if (issue == VersionBoundIssue.RevisionWidened)
        {
            context.ReportDiagnostic(Diagnostic.Create(RevisionWidenedDescriptor, Location.None, attribute, text, entryName, emittedBound));
        }
    }

    /// <summary>
    /// Agent mode looks up the exact-signature key first and falls back to the overload-wide entry only when the exact
    /// key has no entries at all, version aside. OTel native has no such rule: an overload-wide definition of the same
    /// arity also matches the explicit overload. So for an assembly version where the overload-wide entry is in range but
    /// no explicit entry of that arity is, the distro instruments the overload and agent mode instruments nothing. Warn
    /// when the overload-wide range minus the union of the explicit ranges (same assembly, type, method, arity) is
    /// non-empty; where the explicit entries cover it, the explicit definitions come first and win, as in agent mode.
    /// </summary>
    private static void ReportOverloadWideResidues(SourceProductionContext context, List<RangedPoint> kept)
    {
        foreach (var method in kept.GroupBy(p => p.Point.AssemblyName + "|" + p.Point.ClassName + "|" + p.Point.MethodName))
        {
            var explicitByArity = method.Where(p => p.Point.ParameterTypes != null).GroupBy(p => p.Point.ParameterTypes.Count).OrderBy(g => g.Key).ToList();

            foreach (var overloadWide in method.Where(p => p.Point.ParameterTypes == null))
            {
                foreach (var arity in explicitByArity)
                {
                    var explicitRanges = arity.OrderBy(p => p.Min).ToList();
                    if (!Covers(explicitRanges, overloadWide))
                    {
                        context.ReportDiagnostic(Diagnostic.Create(
                            OverloadWideResidueDescriptor,
                            Location.None,
                            overloadWide.Point.AssemblyName,
                            overloadWide.Point.ClassName,
                            overloadWide.Point.MethodName,
                            FormatRange(overloadWide),
                            arity.Key,
                            string.Join(", ", explicitRanges.Select(FormatRange)),
                            DefaultOffQualifier(overloadWide.Point.Enabled || explicitRanges.Any(p => p.Point.Enabled))));
                    }
                }
            }
        }
    }

    /// <summary>True when the ranges, sorted by minimum, together include every version of <paramref name="target"/>.</summary>
    private static bool Covers(List<RangedPoint> sortedRanges, RangedPoint target)
    {
        var firstUncovered = target.Min;
        foreach (var range in sortedRanges)
        {
            if (range.Min > firstUncovered)
            {
                return false;
            }

            if (range.Max >= firstUncovered)
            {
                firstUncovered = range.Max + 1;
            }

            if (firstUncovered > target.Max)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>"default-off " when a residue or overlap involves only default-off points, else "".</summary>
    private static string DefaultOffQualifier(bool anyDefaultOn) => anyDefaultOn ? string.Empty : "default-off ";

    private static bool Overlaps(RangedPoint a, RangedPoint b) => a.Min <= b.Max && b.Min <= a.Max;

    private static string EntryName(XmlInstrumentationPoint point) =>
        point.AssemblyName + "/" + point.ClassName + "." + point.MethodName + "(" + (point.ParameterTypes == null ? "any parameters" : string.Join(",", point.ParameterTypes)) + ")";

    private static string FormatRange(RangedPoint point) =>
        FormatVersion(point.Range.MinMajor, point.Range.MinMinor, point.Range.MinPatch) + "-" + FormatVersion(point.Range.MaxMajor, point.Range.MaxMinor, point.Range.MaxPatch);

    private static string FormatVersion(ushort major, ushort minor, ushort patch) =>
        major.ToString(CultureInfo.InvariantCulture) + "." + minor.ToString(CultureInfo.InvariantCulture) + "." + patch.ToString(CultureInfo.InvariantCulture);

    /// <summary>One additional XML file and whether it is hash-only (NrDistroHashOnly metadata).</summary>
    private sealed class XmlFile
    {
        public AdditionalText Text { get; }

        public bool HashOnly { get; }

        public XmlFile(AdditionalText text, bool hashOnly)
        {
            Text = text;
            HashOnly = hashOnly;
        }
    }

    /// <summary>A parsed point that passed the per-point checks, before dedupe.</summary>
    private sealed class Candidate
    {
        public XmlInstrumentationPoint Point { get; }

        /// <summary>assembly|class|method|parameters ("*" for overload-wide).</summary>
        public string Key { get; }

        /// <summary><see cref="Key"/> plus the range key: the dedupe key.</summary>
        public string FullKey { get; }

        /// <summary>The tracer factory, with the parser's default for an unnamed one.</summary>
        public string Factory { get; }

        /// <summary><see cref="FullKey"/> plus the tracer factory.</summary>
        public string FactoryKey { get; }

        public VersionRangeConversion Range { get; }

        public string EntryName { get; }

        public ushort SourceFile { get; }

        public Candidate(XmlInstrumentationPoint point, string key, VersionRangeConversion range, string entryName, ushort sourceFile)
        {
            Point = point;
            Key = key;
            Range = range;
            EntryName = entryName;
            SourceFile = sourceFile;
            FullKey = key + "|" + range.RangeKey;
            Factory = point.TracerFactoryName ?? InstrumentationXmlParser.DefaultTracerFactoryName;
            FactoryKey = FullKey + "|" + Factory;
        }
    }

    /// <summary>A kept point with its converted range, and the range's bounds as comparable numbers.</summary>
    private sealed class RangedPoint
    {
        public RangedPoint(XmlInstrumentationPoint point, VersionRangeConversion range)
        {
            Point = point;
            Range = range;
            Min = ((long)range.MinMajor << 32) | ((long)range.MinMinor << 16) | range.MinPatch;
            Max = ((long)range.MaxMajor << 32) | ((long)range.MaxMinor << 16) | range.MaxPatch;
        }

        public XmlInstrumentationPoint Point { get; }

        public VersionRangeConversion Range { get; }

        public long Min { get; }

        public long Max { get; }
    }
}

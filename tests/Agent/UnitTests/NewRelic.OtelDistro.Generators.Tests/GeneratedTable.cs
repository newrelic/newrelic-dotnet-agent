// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using NewRelic.Agent.Core.OtelDistro;
using NUnit.Framework;

namespace NewRelic.OtelDistro.Generators.Tests;

/// <summary>Reads the definitions table back out of generated source, through Core's production reader.</summary>
public static class GeneratedTable
{
    /// <summary>The bytes of <c>Table = new byte[] { 0x.., ... };</c> in generated source.</summary>
    public static byte[] Extract(string source)
    {
        var start = source.IndexOf("Table = new byte[]", StringComparison.Ordinal);
        Assert.That(start, Is.GreaterThanOrEqualTo(0), "generated source has no Table");
        var end = source.IndexOf("};", start, StringComparison.Ordinal);
        return Regex.Matches(source.Substring(start, end - start), "0x([0-9A-F]{2})")
            .Select(m => byte.Parse(m.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture))
            .ToArray();
    }

    /// <summary>The definitions in the generated source's table; fails the test if the reader rejects it.</summary>
    public static IReadOnlyList<DistroCallTargetDefinition> ReadDefinitions(string source) => Read(source).Definitions;

    /// <summary>
    /// The definitions and every site (site i belongs to record i) in the generated source's table; fails the test if
    /// the reader rejects it or a site is missing.
    /// </summary>
    public static (IReadOnlyList<DistroCallTargetDefinition> Definitions, IReadOnlyList<DistroInstrumentationSite> Sites) Read(string source)
    {
        var read = InstrumentationDefinitionsTable.TryRead(Extract(source), out var definitions, out var sites, out _, out var reason);
        Assert.That(read, Is.True, reason);
        Assert.That(sites.Count, Is.EqualTo(definitions.Count));
        var siteList = new List<DistroInstrumentationSite>();
        for (var i = 0; i < sites.Count; i++)
        {
            Assert.That(sites.TryGet(i, out var site), Is.True, "site " + i);
            siteList.Add(site);
        }

        return (definitions, siteList);
    }

    /// <summary>The shipped XML manifest in the generated source's table; fails the test if the reader rejects it.</summary>
    public static DistroShippedXmlManifest ReadManifest(string source)
    {
        var read = InstrumentationDefinitionsTable.TryRead(Extract(source), out _, out _, out var manifest, out var reason);
        Assert.That(read, Is.True, reason);
        return manifest;
    }

    /// <summary>
    /// Every generated integration class's <c>SiteIndex</c> constant, by full integration type name (namespace + class),
    /// parsed from the class's opening lines. Fails the test if a class has no <c>SiteIndex</c> right after its brace.
    /// </summary>
    public static IReadOnlyDictionary<string, int> SiteIndexByIntegrationType(string source, string generatedNamespace)
    {
        var classes = Regex.Matches(source, @"public abstract class (NrDistroIntegration_[0-9A-F]{16})").Count;
        var matches = Regex.Matches(source, @"public abstract class (NrDistroIntegration_[0-9A-F]{16})\r?\n\{\r?\n    public const int SiteIndex = (\d+);");
        Assert.That(matches.Count, Is.EqualTo(classes), "every integration class starts with a SiteIndex constant");
        return matches.Cast<Match>().ToDictionary(
            m => generatedNamespace + "." + m.Groups[1].Value,
            m => int.Parse(m.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture));
    }

    /// <summary>The string literals of <c>public static readonly string[] {fieldName} = ...;</c> in generated source, in order.</summary>
    public static string[] ReadStringArray(string source, string fieldName)
    {
        var start = source.IndexOf("string[] " + fieldName + " =", StringComparison.Ordinal);
        Assert.That(start, Is.GreaterThanOrEqualTo(0), "generated source has no " + fieldName);
        var end = source.IndexOf(";", start, StringComparison.Ordinal);
        return Regex.Matches(source.Substring(start, end - start), "\"([^\"]*)\"").Cast<Match>().Select(m => m.Groups[1].Value).ToArray();
    }

    /// <summary>One line per definition, every field, for whole-list comparisons with readable failures.</summary>
    public static string Describe(DistroCallTargetDefinition d) =>
        $"{d.TargetAssembly} | {d.TargetType} | {d.TargetMethod} | [{string.Join(",", d.TargetSignatureTypes)}] | {d.IntegrationTypeName} | "
        + $"{d.MinMajor}.{d.MinMinor}.{d.MinPatch}-{d.MaxMajor}.{d.MaxMinor}.{d.MaxPatch}";

    /// <summary>One line per site, every field; a null metric shows as "(null)" so it differs from an empty one.</summary>
    public static string Describe(DistroInstrumentationSite s) =>
        $"{s.TracerFactoryName} | {s.MetricName ?? "(null)"} | {s.TracerArguments} | {s.AssemblyName} | {s.TypeName} | {s.MethodName} | "
        + $"{s.ArgumentSignature} | 0x{s.FunctionId:X16}";
}

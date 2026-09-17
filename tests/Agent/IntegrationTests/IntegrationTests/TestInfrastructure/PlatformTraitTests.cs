// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using Xunit;

namespace NewRelic.Agent.IntegrationTests.TestInfrastructure;

/// <summary>
/// Carries no trait, so it runs on every lane that selects by exclusion.
/// </summary>
public class PlatformTraitTests
{
    [Fact]
    public void EveryTestClassIsClassifiableByTheResolver()
    {
        var offenders = Classify()
            .Where(c => !c.IsExempt && c.Lane == RuntimeLane.Unknown)
            .Select(c => c.FullName)
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            $"{offenders.Length} test class(es) cannot be classified. An unclassifiable Windows-only class " +
            "gets no trait, runs on the Linux lane, and fails there in a way that reads as a product bug. " +
            "Add a RuntimeTraitPolicy.ClassOverrides entry with a reason, or add the class to " +
            "RuntimeTraitPolicy.ExemptClasses if it launches no application.\n" + string.Join("\n", offenders));
    }

    [Fact]
    public void NoPortableClassDeclaresThePlatformTrait()
    {
        var offenders = Classify()
            .Where(c => c.IsExempt || (c.Lane != RuntimeLane.Unknown && !PlatformTrait.RequiresWindows(c.Lane)))
            .Where(c => c.Platform.Count > 0)
            .Select(c => c.FullName)
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            $"{offenders.Length} portable class(es) declare " +
            $"[Trait(\"{PlatformTrait.TraitName}\", ...)]. The trait is read with inherit: true, so a class also " +
            "picks it up from a base class, and it cannot be cancelled. Each one is excluded from the Linux lane " +
            "and silently loses that coverage. Re-run the trait tool, or give the base class no trait.\n" +
            string.Join("\n", offenders));
    }

    [Fact]
    public void EveryDeclaredPlatformTraitValueIsRecognised()
    {
        var offenders = Classify()
            .SelectMany(c => c.Platform.Select(v => new { c.FullName, Value = v }))
            .Where(x => !string.Equals(x.Value, PlatformTrait.WindowsOnlyValue, StringComparison.Ordinal))
            .Select(x => $"{x.FullName} -> \"{x.Value}\"")
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            $"Unrecognised {PlatformTrait.TraitName} trait value(s). \"{PlatformTrait.WindowsOnlyValue}\" is the " +
            "only value; anything else fails to exclude the class and it runs on Linux.\n" +
            string.Join("\n", offenders));
    }

    [Fact]
    public void EveryExemptionNamesAClassThatStillExists()
    {
        var present = new HashSet<string>(
            RuntimeTraitPolicy.EnumerateTestClasses(typeof(PlatformTraitTests).Assembly).Select(t => t.FullName),
            StringComparer.Ordinal);
        var stale = RuntimeTraitPolicy.ExemptClasses.Where(n => !present.Contains(n)).ToArray();

        Assert.True(
            stale.Length == 0,
            "Stale entries in RuntimeTraitPolicy.ExemptClasses. A renamed or deleted class leaves a silent hole.\n" +
            string.Join("\n", stale));
    }

    private static IReadOnlyList<Classification> Classify()
    {
        var resolver = new RuntimeLaneResolver(RuntimeTraitPolicy.ClassOverrides);
        var exempt = new HashSet<string>(RuntimeTraitPolicy.ExemptClasses, StringComparer.Ordinal);
        var result = new List<Classification>();

        foreach (var type in RuntimeTraitPolicy.EnumerateTestClasses(typeof(PlatformTraitTests).Assembly))
        {
            result.Add(new Classification(
                type.FullName,
                resolver.Resolve(type),
                exempt.Contains(type.FullName),
                RuntimeTraitPolicy.DeclaredPlatformTraits(type)));
        }

        return result;
    }

    private sealed record Classification(
        string FullName,
        RuntimeLane Lane,
        bool IsExempt,
        IReadOnlyCollection<string> Platform);
}

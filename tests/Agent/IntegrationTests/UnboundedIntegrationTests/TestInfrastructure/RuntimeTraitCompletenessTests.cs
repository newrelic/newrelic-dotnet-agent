// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using Xunit;

namespace NewRelic.Agent.UnboundedIntegrationTests.TestInfrastructure;

/// <summary>
/// Carries both trait values so it runs on every lane. Lane selection is by
/// inclusion, so a class with no Runtime trait runs nowhere and reports green.
/// This test is what makes that a failure instead.
/// </summary>
[Trait(RuntimeLaneResolver.TraitName, RuntimeLaneResolver.CoreValue)]
[Trait(RuntimeLaneResolver.TraitName, RuntimeLaneResolver.FrameworkValue)]
public class RuntimeTraitCompletenessTests
{
    [Fact]
    public void EveryTestClassDeclaresARuntimeTrait()
    {
        var exempt = new HashSet<string>(RuntimeTraitPolicy.ExemptClasses, StringComparer.Ordinal);
        var offenders = RuntimeTraitPolicy
            .EnumerateTestClasses(typeof(RuntimeTraitCompletenessTests).Assembly)
            .Where(t => !exempt.Contains(t.FullName))
            .Where(t => RuntimeTraitPolicy.DeclaredRuntimeTraits(t).Count == 0)
            .Select(t => t.FullName)
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            $"{offenders.Length} test class(es) declare no [Trait(\"{RuntimeLaneResolver.TraitName}\", ...)]. " +
            "Lane selection is by inclusion, so each of these runs on no CI lane and reports green. " +
            "Run .github/scripts/apply-runtime-traits.py, or add the class to RuntimeTraitPolicy.ExemptClasses " +
            "with a reason.\n" + string.Join("\n", offenders));
    }

    [Fact]
    public void EveryDeclaredRuntimeTraitValueIsRecognised()
    {
        var allowed = new[] { RuntimeLaneResolver.CoreValue, RuntimeLaneResolver.FrameworkValue };
        var offenders = RuntimeTraitPolicy
            .EnumerateTestClasses(typeof(RuntimeTraitCompletenessTests).Assembly)
            .SelectMany(t => RuntimeTraitPolicy.DeclaredRuntimeTraits(t).Select(v => new { Class = t.FullName, Value = v }))
            .Where(x => !allowed.Contains(x.Value, StringComparer.Ordinal))
            .Select(x => $"{x.Class} -> \"{x.Value}\"")
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            $"Unrecognised {RuntimeLaneResolver.TraitName} trait value(s). Only \"{RuntimeLaneResolver.CoreValue}\" " +
            $"and \"{RuntimeLaneResolver.FrameworkValue}\" select a lane; anything else runs nowhere.\n" +
            string.Join("\n", offenders));
    }

    [Fact]
    public void EveryExemptionNamesAClassThatStillExists()
    {
        var present = new HashSet<string>(
            RuntimeTraitPolicy.EnumerateTestClasses(typeof(RuntimeTraitCompletenessTests).Assembly).Select(t => t.FullName),
            StringComparer.Ordinal);
        var stale = RuntimeTraitPolicy.ExemptClasses.Where(n => !present.Contains(n)).ToArray();

        Assert.True(
            stale.Length == 0,
            "Stale entries in RuntimeTraitPolicy.ExemptClasses. A renamed or deleted class leaves a silent hole.\n" +
            string.Join("\n", stale));
    }
}

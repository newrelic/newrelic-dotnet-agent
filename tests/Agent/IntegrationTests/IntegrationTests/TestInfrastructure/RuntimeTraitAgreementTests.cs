// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using Xunit;

namespace NewRelic.Agent.IntegrationTests.TestInfrastructure;

[Trait(RuntimeLaneResolver.TraitName, RuntimeLaneResolver.CoreValue)]
[Trait(RuntimeLaneResolver.TraitName, RuntimeLaneResolver.FrameworkValue)]
public class RuntimeTraitAgreementTests
{
    [Fact]
    public void EveryCommittedRuntimeTraitMatchesWhatTheResolverComputes()
    {
        var resolver = new RuntimeLaneResolver(RuntimeTraitPolicy.ClassOverrides);
        var exempt = new HashSet<string>(RuntimeTraitPolicy.ExemptClasses, StringComparer.Ordinal);
        var mismatches = new List<string>();

        foreach (var type in RuntimeTraitPolicy.EnumerateTestClasses(typeof(RuntimeTraitAgreementTests).Assembly))
        {
            if (exempt.Contains(type.FullName))
            {
                continue;
            }

            var declared = RuntimeTraitPolicy.DeclaredRuntimeTraits(type);
            if (declared.Count != 1)
            {
                // Zero is the completeness guard's business; more than one is a
                // deliberate multi-lane class, which only the guards use.
                continue;
            }

            var computed = resolver.Resolve(type);
            if (computed == RuntimeLane.Unknown)
            {
                mismatches.Add($"{type.FullName}: declares \"{declared.Single()}\" but the resolver cannot classify it. " +
                               "Add a RuntimeTraitPolicy.ClassOverrides entry with a reason.");
                continue;
            }

            var expected = RuntimeLaneResolver.ToTraitValue(computed);
            if (!string.Equals(expected, declared.Single(), StringComparison.Ordinal))
            {
                mismatches.Add($"{type.FullName}: declares \"{declared.Single()}\", resolver says \"{expected}\".");
            }
        }

        Assert.True(
            mismatches.Count == 0,
            $"{mismatches.Count} class(es) carry a stale or hand-edited Runtime trait. Either re-run " +
            ".github/scripts/apply-runtime-traits.py, or record the deliberate exception in " +
            "RuntimeTraitPolicy.ClassOverrides.\n" + string.Join("\n", mismatches));
    }

    [Fact]
    public void EveryOverrideNamesAClassThatStillExists()
    {
        var present = new HashSet<string>(
            RuntimeTraitPolicy.EnumerateTestClasses(typeof(RuntimeTraitAgreementTests).Assembly).Select(t => t.FullName),
            StringComparer.Ordinal);
        var stale = RuntimeTraitPolicy.ClassOverrides.Keys.Where(n => !present.Contains(n)).ToArray();

        Assert.True(
            stale.Length == 0,
            "Stale entries in RuntimeTraitPolicy.ClassOverrides. A renamed or deleted class leaves a rule that " +
            "silently applies to nothing.\n" + string.Join("\n", stale));
    }
}

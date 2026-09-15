// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NewRelic.Agent.IntegrationTestHelpers;
using Xunit;

namespace NewRelic.Agent.UnboundedIntegrationTests.TestInfrastructure;

public static class RuntimeTraitPolicy
{
    /// <summary>
    /// Classes that genuinely are not runtime-specific. Every entry is a
    /// deliberate, reviewable line, not an accident. Add one only when the class
    /// launches no application.
    /// </summary>
    private static readonly string[] Exempt =
    {
        "NewRelic.Agent.UnboundedIntegrationTests.TestInfrastructure.RuntimeTraitCompletenessTests",
        "NewRelic.Agent.UnboundedIntegrationTests.TestInfrastructure.RuntimeTraitAgreementTests",
    };

    /// <summary>
    /// Tier 1 of the resolution order. An entry here overrides what the fixture
    /// and the class name say. Keep it empty until a real case forces an entry,
    /// and state the reason on the line.
    /// </summary>
    private static readonly Dictionary<string, RuntimeLane> Overrides = new Dictionary<string, RuntimeLane>();

    public static IReadOnlyCollection<string> ExemptClasses => Exempt;

    public static IReadOnlyDictionary<string, RuntimeLane> ClassOverrides => Overrides;

    /// <summary>
    /// Every concrete public class in the assembly that carries at least one
    /// xunit test method, public or inherited non-public (xunit v3 runs a
    /// protected [Fact] inherited from a base class). FactAttribute covers
    /// TheoryAttribute and the Skip*FactAttribute subclasses, because both
    /// derive from it.
    /// </summary>
    public static IEnumerable<Type> EnumerateTestClasses(Assembly assembly)
    {
        return assembly.GetExportedTypes()
            .Where(t => t.IsClass && !t.IsAbstract && !t.IsGenericTypeDefinition)
            .Where(t => t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Any(m => m.GetCustomAttributes<FactAttribute>(inherit: true).Any()))
            .OrderBy(t => t.FullName, StringComparer.Ordinal);
    }

    /// <summary>
    /// The Runtime trait values declared on a class. AttributeUsage on
    /// TraitAttribute is Inherited=True and AllowMultiple=True, so read the
    /// effective set with inherit: true.
    /// </summary>
    public static IReadOnlyCollection<string> DeclaredRuntimeTraits(Type testClass)
    {
        return testClass.GetCustomAttributes<TraitAttribute>(inherit: true)
            .Where(t => string.Equals(t.Name, RuntimeLaneResolver.TraitName, StringComparison.Ordinal))
            .Select(t => t.Value)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(v => v, StringComparer.Ordinal)
            .ToArray();
    }
}

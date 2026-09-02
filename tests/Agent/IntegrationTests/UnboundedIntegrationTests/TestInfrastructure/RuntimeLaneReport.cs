// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using NewRelic.Agent.IntegrationTestHelpers;
using Xunit;

namespace NewRelic.Agent.UnboundedIntegrationTests.TestInfrastructure;

/// <summary>
/// Runs only when NR_RUNTIME_LANE_REPORT names an output path.
/// </summary>
public sealed class LaneReportFactAttribute : FactAttribute
{
    public LaneReportFactAttribute(
        [CallerFilePath] string sourceFilePath = null,
        [CallerLineNumber] int sourceLineNumber = -1)
        : base(sourceFilePath, sourceLineNumber)
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(RuntimeLaneReport.OutputPathVariable)))
        {
            Skip = $"Set {RuntimeLaneReport.OutputPathVariable}=<path> to generate the lane report.";
        }
    }
}

[Trait(RuntimeLaneResolver.TraitName, RuntimeLaneResolver.CoreValue)]
[Trait(RuntimeLaneResolver.TraitName, RuntimeLaneResolver.FrameworkValue)]
public class RuntimeLaneReport
{
    public const string OutputPathVariable = "NR_RUNTIME_LANE_REPORT";

    [LaneReportFact]
    public void WriteReport()
    {
        var path = Environment.GetEnvironmentVariable(OutputPathVariable);
        var resolver = new RuntimeLaneResolver(RuntimeTraitPolicy.ClassOverrides);
        var builder = new StringBuilder();
        var unknown = 0;

        foreach (var type in RuntimeTraitPolicy.EnumerateTestClasses(typeof(RuntimeLaneReport).Assembly))
        {
            var lane = RuntimeTraitPolicy.ExemptClasses.Contains(type.FullName)
                ? RuntimeLane.Unknown
                : resolver.Resolve(type);

            if (lane == RuntimeLane.Unknown)
            {
                unknown++;
            }

            var fixtureType = RuntimeLaneResolver.GetFixtureType(type);
            builder.Append(type.FullName).Append('\t').Append(lane).Append('\t')
                   .Append(fixtureType == null ? "-" : fixtureType.FullName).Append('\n');
        }

        File.WriteAllText(path, builder.ToString(), new UTF8Encoding(false));
        Console.WriteLine($"Wrote {path}. Unknown: {unknown}.");
    }
}

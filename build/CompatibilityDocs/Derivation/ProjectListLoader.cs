// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace CompatibilityDocs.Derivation;

public class ProjectListLoader
{
    private const string DottyProjectInfo = "build/Dotty/projectInfo.json";
    private const string MfaHelpers =
        "tests/Agent/IntegrationTests/SharedApplications/Common/MultiFunctionApplicationHelpers/MultiFunctionApplicationHelpers.csproj";

    private sealed class Entry { public string projectFile { get; set; } = ""; }

    public virtual IReadOnlyList<string> GetProjectPaths(string repoRoot)
    {
        var json = File.ReadAllText(Path.Combine(repoRoot, DottyProjectInfo));
        var entries = JsonSerializer.Deserialize<List<Entry>>(json) ?? new List<Entry>();

        var relative = entries.Select(e => e.projectFile).ToList();
        relative.Add(MfaHelpers);

        return relative
            .Select(r => Path.GetFullPath(Path.Combine(repoRoot, r.Replace('/', Path.DirectorySeparatorChar))))
            .Distinct()
            .ToList();
    }
}

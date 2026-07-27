// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.IO;
using System.Linq;
using CompatibilityDocs.Derivation;
using NUnit.Framework;

namespace CompatibilityDocs.Tests;

[TestFixture]
public class ProjectListLoaderTests
{
    private string _repoRoot = null!;

    [SetUp]
    public void SetUp()
    {
        _repoRoot = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(Path.Combine(_repoRoot, "build", "Dotty"));
        File.WriteAllText(Path.Combine(_repoRoot, "build", "Dotty", "projectInfo.json"), """
[
  { "projectFile": "tests/Agent/IntegrationTests/SharedApplications/Common/MFALatestPackages/MFALatestPackages.csproj" },
  { "projectFile": "tests/Agent/IntegrationTests/ContainerApplications/KafkaTestApp/KafkaTestApp.csproj" }
]
""");
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_repoRoot, recursive: true);

    [Test]
    public void GetProjectPaths_IncludesDottyProjectsPlusMfaHelpers_AsAbsolutePaths()
    {
        var loader = new ProjectListLoader();
        var paths = loader.GetProjectPaths(_repoRoot);

        Assert.That(paths, Has.Some.Contains(Path.Combine("MFALatestPackages", "MFALatestPackages.csproj")));
        Assert.That(paths, Has.Some.Contains(Path.Combine("KafkaTestApp", "KafkaTestApp.csproj")));
        Assert.That(paths, Has.Some.Contains(Path.Combine("MultiFunctionApplicationHelpers", "MultiFunctionApplicationHelpers.csproj")));
        Assert.That(paths.All(Path.IsPathRooted), Is.True);
    }
}

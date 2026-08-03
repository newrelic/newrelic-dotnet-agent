// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.IO;
using System.Linq;
using CompatibilityDocs.Derivation;
using NUnit.Framework;

namespace CompatibilityDocs.Tests;

[TestFixture]
public class PackageReferenceScannerTests
{
    private string _csprojPath = null!;

    [SetUp]
    public void SetUp()
    {
        _csprojPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".csproj");
        File.WriteAllText(_csprojPath, """
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><TargetFrameworks>net481;net10.0</TargetFrameworks></PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Npgsql" Version="7.0.7" Condition="'$(TargetFramework)' == 'net10.0'" />
    <PackageReference Include="Npgsql" Version="4.0.0" Condition="'$(TargetFramework)' == 'net481'" />
    <PackageReference Include="Serilog" Version="4.3.1" />
  </ItemGroup>
</Project>
""");
    }

    [TearDown]
    public void TearDown() => File.Delete(_csprojPath);

    [Test]
    public void Scan_ReadsIncludeVersionAndTfm()
    {
        var scanner = new PackageReferenceScanner();
        var refs = scanner.Scan(_csprojPath);

        Assert.That(refs, Has.Count.EqualTo(3));
        var core = refs.Single(r => r.PackageId == "Npgsql" && r.Tfm == "net10.0");
        Assert.That(core.Version, Is.EqualTo("7.0.7"));
        var fw = refs.Single(r => r.PackageId == "Npgsql" && r.Tfm == "net481");
        Assert.That(fw.Version, Is.EqualTo("4.0.0"));
        var noCond = refs.Single(r => r.PackageId == "Serilog");
        Assert.That(noCond.Tfm, Is.Null);
    }
}

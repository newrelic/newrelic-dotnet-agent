// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace NewRelic.Agent.Core.OtelDistro;

[TestFixture]
public class DistroDependencyProbeTests
{
    private string _root;
    private string _appBase;
    private string _runtime;
    private string _windows;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), "DistroDependencyProbeTests", Guid.NewGuid().ToString("N"));
        _appBase = Dir("appbase");
        _runtime = Dir("runtime");
        _windows = Dir("windows");
    }

    [TearDown]
    public void TearDown()
    {
        Directory.Delete(_root, true);
    }

    private string Dir(params string[] parts)
    {
        var path = Path.Combine(new[] { _root }.Concat(parts).ToArray());
        Directory.CreateDirectory(path);
        return path;
    }

    private static void Touch(string directory, string file)
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, file), string.Empty);
    }

    private DistroDependencyProbe Probe(string privateBinPath = null) => new DistroDependencyProbe(_appBase, privateBinPath, _runtime, _windows);

    private static AssemblyName Ref(string name) => new AssemblyName(name);

    [TestCase(".NET Framework 4.8.9310.0", true)]
    [TestCase(".net framework 4.6.2", true)]
    [TestCase(".NET 11.0.0", false)]
    [TestCase(".NET Core 3.1.32", false)]
    [TestCase(null, false)]
    public void IsNetFramework_ByFrameworkDescription(string description, bool expected)
    {
        Assert.That(DistroDependencyProbe.IsNetFramework(description), Is.EqualTo(expected));
    }

    [Test]
    public void ForRuntime_ProbeOnlyOnNetFramework()
    {
        var probe = new DistroDependencyProbe(null, null, null, null);

        Assert.Multiple(() =>
        {
            Assert.That(DistroDependencyProbe.ForRuntime(() => ".NET Framework 4.8.1", () => probe), Is.SameAs(probe));
            Assert.That(DistroDependencyProbe.ForRuntime(() => ".NET 11.0.0", () => probe), Is.Null);
        });
    }

    [Test]
    public void ForCurrentRuntime_GatesOnlyOnNetFramework()
    {
#if NETFRAMEWORK
        Assert.That(DistroDependencyProbe.ForCurrentRuntime(), Is.InstanceOf<DistroDependencyProbe>());
#else
        Assert.That(DistroDependencyProbe.ForCurrentRuntime(), Is.Null);
#endif
    }

    [Test]
    public void ForRuntime_ProbeCreationThrowsOrReturnsNull_FailsClosed()
    {
        Assert.Multiple(() =>
        {
            Assert.That(DistroDependencyProbe.ForRuntime(() => ".NET Framework 4.8.1", () => throw new ArgumentException("Illegal characters in path.")), Is.InstanceOf<DistroFailClosedDependencyProbe>());
            Assert.That(DistroDependencyProbe.ForRuntime(() => ".NET Framework 4.8.1", () => null), Is.InstanceOf<DistroFailClosedDependencyProbe>());
        });
    }

    [Test]
    public void ForRuntime_RuntimeDescriptionThrows_FailsClosed()
    {
        Assert.That(DistroDependencyProbe.ForRuntime(() => throw new InvalidOperationException("description"), () => null), Is.InstanceOf<DistroFailClosedDependencyProbe>());
    }

    [Test]
    public void FailClosedProbe_NoReferenceHasAFile_LoadedNamesStillListed()
    {
        var testAssembly = typeof(DistroDependencyProbeTests).Assembly;
        var probe = new DistroFailClosedDependencyProbe();

        Assert.Multiple(() =>
        {
            Assert.That(probe.HasProbingFile(testAssembly.GetName(), testAssembly), Is.False, "the file is in the app base, but the fail-closed probe never says so");
            Assert.That(probe.GetLoadedAssemblyNames(), Does.Contain(testAssembly.GetName().Name));
        });
    }

#if NETFRAMEWORK
    [Test]
    public void Constructor_InvalidPrivateBinPathCharacters_Throws_SoForRuntimeFailsClosed()
    {
        // The real failure ForRuntime guards against: Path.Combine rejects invalid characters on .NET Framework.
        Assert.Multiple(() =>
        {
            Assert.Throws<ArgumentException>(() => new DistroDependencyProbe(_appBase, "bad|dir", _runtime, _windows));
            Assert.That(DistroDependencyProbe.ForRuntime(() => ".NET Framework 4.8.1", () => new DistroDependencyProbe(_appBase, "bad|dir", _runtime, _windows)), Is.InstanceOf<DistroFailClosedDependencyProbe>());
        });
    }
#endif

    [Test]
    public void GetLoadedAssemblyNames_ListsTheAppDomainsAssemblies()
    {
        var names = new DistroDependencyProbe().GetLoadedAssemblyNames().ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(names, Does.Contain(typeof(DistroDependencyProbeTests).Assembly.GetName().Name));
            Assert.That(names, Does.Contain(typeof(object).Assembly.GetName().Name));
        });
    }

    [Test]
    public void DefaultProbe_FindsAReferenceInTheAppBase_NotAMadeUpOne()
    {
        var probe = new DistroDependencyProbe();

        Assert.Multiple(() =>
        {
            Assert.That(probe.HasProbingFile(typeof(DistroDependencyProbeTests).Assembly.GetName(), null), Is.True);
            Assert.That(probe.HasProbingFile(Ref("No.Such.Assembly.Anywhere"), null), Is.False);
        });
    }

    [TestCase("Lib.dll")]
    [TestCase("Lib.exe")]
    [TestCase(@"Lib\Lib.dll")]
    [TestCase(@"Lib\Lib.exe")]
    public void AppBase_FileShapesAFusionProbeFinds(string relative)
    {
        Touch(Path.GetDirectoryName(Path.Combine(_appBase, relative)), Path.GetFileName(relative));

        Assert.Multiple(() =>
        {
            Assert.That(Probe().HasProbingFile(Ref("Lib"), null), Is.True);
            Assert.That(Probe().HasProbingFile(Ref("Other"), null), Is.False);
        });
    }

    [Test]
    public void PrivateBinPath_RelativeEntriesProbed_RootedAndEmptyEntriesIgnored()
    {
        // The probe takes privatePath entries relative to the app base only and treats a rooted entry as not probed
        // (conservative), so a file there must not count as one a bind would find.
        Touch(Path.Combine(_appBase, "bin"), "InBin.dll");
        var rooted = Dir("elsewhere");
        Touch(rooted, "Rooted.dll");

        var probe = Probe(" bin ;;" + rooted + "; ");

        Assert.Multiple(() =>
        {
            Assert.That(probe.HasProbingFile(Ref("InBin"), null), Is.True);
            Assert.That(probe.HasProbingFile(Ref("Rooted"), null), Is.False);
            Assert.That(Probe().HasProbingFile(Ref("InBin"), null), Is.False, "not probed without the private bin path");
        });
    }

    [Test]
    public void RuntimeDirectory_IsProbed()
    {
        Touch(_runtime, "Framework.Lib.dll");

        Assert.That(Probe().HasProbingFile(Ref("Framework.Lib"), null), Is.True);
    }

    [Test]
    public void ReferencingAssemblysOwnDirectory_IsProbed_ForTheLoadFromCase()
    {
        var plugin = Dir("plugin");
        Touch(plugin, "Neighbour.dll");

        Assert.Multiple(() =>
        {
            Assert.That(Probe().HasProbingFile(Ref("Neighbour"), new LocatedAssembly(Path.Combine(plugin, "Plugin.dll"))), Is.True);
            Assert.That(Probe().HasProbingFile(Ref("Neighbour"), null), Is.False);
        });
    }

    [Test]
    public void ReferencingAssemblyWithoutALocation_OnlyTheOtherDirectoriesAreProbed()
    {
        Touch(_appBase, "InAppBase.dll");

        Assert.Multiple(() =>
        {
            Assert.That(Probe().HasProbingFile(Ref("InAppBase"), new LocatedAssembly(string.Empty)), Is.True);
            Assert.That(Probe().HasProbingFile(Ref("InAppBase"), new LocatedAssembly(null)), Is.True, "a dynamic assembly's Location throws");
            Assert.That(Probe().HasProbingFile(Ref("Missing"), new LocatedAssembly(null)), Is.False);
        });
    }

    [TestCase(@"Microsoft.NET\assembly\GAC_MSIL")]
    [TestCase(@"Microsoft.NET\assembly\GAC_64")]
    [TestCase(@"Microsoft.NET\assembly\GAC_32")]
    [TestCase(@"assembly\GAC")]
    [TestCase(@"assembly\GAC_MSIL")]
    [TestCase(@"assembly\GAC_64")]
    [TestCase(@"assembly\GAC_32")]
    public void GacFolder_DirectoryOfTheNameIsEnough(string gac)
    {
        Dir("windows", gac, "Gac.Lib");

        Assert.That(Probe().HasProbingFile(Ref("Gac.Lib"), null), Is.True);
    }

    [Test]
    public void NullOrNamelessReference_HasNoFile()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Probe().HasProbingFile(null, null), Is.False);
            Assert.That(Probe().HasProbingFile(new AssemblyName(), null), Is.False);
        });
    }

    [Test]
    public void NullDirectories_AreSkipped()
    {
        var probe = new DistroDependencyProbe(null, "bin", null, null);

        Assert.That(probe.HasProbingFile(Ref("Anything"), null), Is.False);
    }

#if NETFRAMEWORK
    [Test]
    public void DefaultProbe_FindsAFrameworkAssemblyInTheGac()
    {
        Assert.That(new DistroDependencyProbe().HasProbingFile(Ref("System.Data"), null), Is.True);
    }
#endif

    private sealed class LocatedAssembly : Assembly
    {
        private readonly string _location;

        public LocatedAssembly(string location)
        {
            _location = location;
        }

        public override string Location => _location ?? throw new NotSupportedException("dynamic");
    }
}

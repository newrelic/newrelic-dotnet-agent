// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NewRelic.Agent.Core.OtelDistro.PrecedenceTestTypes;
using NewRelic.Agent.Extensions.Logging;
using NUnit.Framework;

namespace NewRelic.Agent.Core.OtelDistro;

// The .NET Framework reference gate of DistroOtelDerivedPrecedenceWatcher: an assembly whose references cannot all be
// bound is not resolved (no GetType) until they can, so the check never caches a bind failure for the AppDomain.
[TestFixture]
public class DistroOtelDerivedPrecedenceGateTests
{
    private const string Metric = "Supportability/Dotnet/OtelDistro/Precedence/OtelWins";
    private const string Ns = "NewRelic.Agent.Core.OtelDistro.PrecedenceTestTypes.";
    private const string DeferredPrefix = "OTel distro: deferring the Derived/Interface precedence check of ";

    private static readonly Assembly TestTypes = typeof(PrecedenceDerived).Assembly;
    private static readonly string TestAssembly = TestTypes.GetName().Name;

    private DistroRecordingLogger _logger;
    private List<string> _metrics;

    [SetUp]
    public void SetUp()
    {
        DistroOtelPrecedenceDiagnostics.ResetForTesting();
        DistroConfigMetrics.ResetForTesting();
        _metrics = new List<string>();
        DistroConfigMetrics.SetMetricSink(_metrics.Add);
        _logger = new DistroRecordingLogger();
        Log.Initialize(_logger);
    }

    [TearDown]
    public void TearDown()
    {
        Log.Initialize(new NoOpLogger());
        DistroConfigMetrics.ResetForTesting();
        DistroOtelPrecedenceDiagnostics.ResetForTesting();
    }

    private static OtelSite DerivedOtel() => new OtelSite
    {
        Id = "MYSQLDATA",
        Integration = "MySqlData",
        Assembly = TestAssembly,
        Type = Ns + "PrecedenceBase",
        Method = "Run",
        Signature = new[] { "System.String" },
        MatchesDerivedOrInterface = true,
    };

    private static NrSite NrOn(Type type) => new NrSite
    {
        TracerFactory = "SqlCommandTracerFactory",
        Assembly = TestAssembly,
        Type = type.FullName,
        Method = "Run",
        Signature = new[] { "System.String" },
    };

    private static DistroOtelDerivedPrecedenceWatcher Watcher(IDistroDependencyProbe probe, params Type[] nrTypes) =>
        new DistroOtelDerivedPrecedenceWatcher(new[] { DerivedOtel() }, (nrTypes.Length == 0 ? new[] { typeof(PrecedenceDerived) } : nrTypes).Select(NrOn).ToArray(), probe);

    private string[] Deferrals() => _logger.Messages(Level.Debug).Where(m => m.StartsWith(DeferredPrefix, StringComparison.Ordinal)).ToArray();

    [Test]
    public void AllReferencesLoadedOrOnDisk_CheckedNow()
    {
        var probe = new FakeProbe { Loaded = { "Loaded.Dep" }, OnDisk = { "Disk.Dep" } };
        var a = new GatedAssembly(TestTypes, "Loaded.Dep", "Disk.Dep");

        Watcher(probe).OnAssemblyLoaded(a);

        Assert.Multiple(() =>
        {
            Assert.That(a.GetTypeCalls, Is.EqualTo(1));
            Assert.That(_metrics, Is.EqualTo(new[] { Metric }));
            Assert.That(Deferrals(), Is.Empty);
            Assert.That(probe.ProbedNames, Is.EqualTo(new[] { "Disk.Dep" }), "a loaded reference needs no file check");
        });
    }

    [Test]
    public void ReferenceLoadedAfterTheSeed_CountsAsLoaded_SeedReadOnce()
    {
        var probe = new FakeProbe();
        var watcher = Watcher(probe);
        watcher.OnAssemblyLoaded(new GatedAssembly(TestTypes, "Disk.Dep") { References = new AssemblyName[0] });
        watcher.OnAssemblyLoaded(new NamedAssembly("Late.Dep"));
        var a = new GatedAssembly(TestTypes, "Late.Dep");

        watcher.OnAssemblyLoaded(a);
        watcher.OnAssemblyLoaded(new GatedAssembly(TestTypes, "Late.Dep"));

        Assert.Multiple(() =>
        {
            Assert.That(probe.SeedCalls, Is.EqualTo(1), "the loaded names are read from the AppDomain once, then kept from the loads");
            Assert.That(probe.ProbedNames, Is.Empty);
            Assert.That(a.GetTypeCalls, Is.EqualTo(1));
        });
    }

    [Test]
    public void OneReferenceMissing_DeferredWithoutGetType_LoggedOnceAtDebug()
    {
        var probe = new FakeProbe { OnDisk = { "Disk.Dep" } };
        var a = new GatedAssembly(TestTypes, "Disk.Dep", "Missing.Dep");
        var watcher = Watcher(probe);

        watcher.OnAssemblyLoaded(a);
        watcher.OnAssemblyLoaded(a);

        Assert.Multiple(() =>
        {
            Assert.That(a.GetTypeCalls, Is.EqualTo(0));
            Assert.That(_metrics, Is.Empty);
            Assert.That(Deferrals(), Is.EqualTo(new[] { DeferredPrefix + TestAssembly + ": Missing.Dep is neither loaded nor where a bind would look for it; checking again on a later assembly load." }));
            Assert.That(_logger.Messages(Level.Warn), Is.Empty);
        });
    }

    [Test]
    public void MissingReferenceAppearsOnDisk_CheckedOnALaterUnrelatedLoad()
    {
        var probe = new FakeProbe();
        var a = new GatedAssembly(TestTypes, "Missing.Dep");
        var watcher = Watcher(probe);
        watcher.OnAssemblyLoaded(a);

        watcher.OnAssemblyLoaded(new NamedAssembly("Unrelated.One"));
        var beforeAppears = a.GetTypeCalls;
        probe.OnDisk.Add("Missing.Dep");
        watcher.OnAssemblyLoaded(new NamedAssembly("Unrelated.Two"));
        watcher.OnAssemblyLoaded(new NamedAssembly("Unrelated.Three"));

        Assert.Multiple(() =>
        {
            Assert.That(beforeAppears, Is.EqualTo(0));
            Assert.That(a.GetTypeCalls, Is.EqualTo(1), "checked once, then no longer deferred");
            Assert.That(_metrics, Is.EqualTo(new[] { Metric }));
            Assert.That(_logger.Messages(Level.Debug), Does.Contain("OTel distro: running the deferred Derived/Interface precedence check of " + TestAssembly + "."));
        });
    }

    [Test]
    public void MissingReferenceLoadedElsewhere_CheckedOnALaterUnrelatedLoad()
    {
        var probe = new FakeProbe();
        var a = new GatedAssembly(TestTypes, "Resolved.Dep");
        var watcher = Watcher(probe);
        watcher.OnAssemblyLoaded(a);

        // Supplied by an AssemblyResolve handler from a directory the probe does not look in: its own load is skipped,
        // the next load checks.
        watcher.OnAssemblyLoaded(new NamedAssembly("Resolved.Dep"));
        var onOwnLoad = a.GetTypeCalls;
        watcher.OnAssemblyLoaded(new NamedAssembly("Unrelated"));

        Assert.Multiple(() =>
        {
            Assert.That(onOwnLoad, Is.EqualTo(0));
            Assert.That(a.GetTypeCalls, Is.EqualTo(1));
            Assert.That(_metrics, Is.EqualTo(new[] { Metric }));
        });
    }

    [Test]
    public void ReferenceNeverAppears_StaysDeferredWithoutError()
    {
        var probe = new FakeProbe();
        var a = new GatedAssembly(TestTypes, "Never.Dep");
        var watcher = Watcher(probe);
        watcher.OnAssemblyLoaded(a);

        for (var i = 0; i < 20; i++)
        {
            Assert.DoesNotThrow(() => watcher.OnAssemblyLoaded(new NamedAssembly("Unrelated." + i)));
        }

        Assert.Multiple(() =>
        {
            Assert.That(a.GetTypeCalls, Is.EqualTo(0));
            Assert.That(_metrics, Is.Empty);
            Assert.That(Deferrals(), Has.Length.EqualTo(1));
            // The gate probes once; with back-off the rechecks probe on the 1st, 2nd, 4th, 8th and 16th later load.
            Assert.That(probe.ProbedNames.Count(n => n == "Never.Dep"), Is.EqualTo(6), "only the missing reference is probed again, with back-off");
        });
    }

    [Test]
    public void LoadOfAReferenceOfTheDeferredAssembly_DoesNotTriggerTheRecheck()
    {
        var probe = new FakeProbe();
        var a = new GatedAssembly(TestTypes, "Missing.Dep", "Other.Dep");
        var watcher = Watcher(probe);
        watcher.OnAssemblyLoaded(a);
        probe.OnDisk.Add("Missing.Dep");
        probe.OnDisk.Add("Other.Dep");

        // Inside these loads the application may be loading the NR target type itself; resolving it then fails as a
        // recursive load, so the recheck waits for a load that is not one of the assembly's references.
        watcher.OnAssemblyLoaded(new NamedAssembly("missing.dep"));
        watcher.OnAssemblyLoaded(new NamedAssembly("Other.Dep"));
        var onReferenceLoads = a.GetTypeCalls;
        watcher.OnAssemblyLoaded(new NamedAssembly("Unrelated"));

        Assert.Multiple(() =>
        {
            Assert.That(onReferenceLoads, Is.EqualTo(0));
            Assert.That(a.GetTypeCalls, Is.EqualTo(1));
        });
    }

    [Test]
    public void LoadOfTheDeferredAssemblyItself_DoesNotTriggerTheRecheck()
    {
        var probe = new FakeProbe();
        var a = new GatedAssembly(TestTypes, "Missing.Dep");
        var watcher = Watcher(probe);
        watcher.OnAssemblyLoaded(a);
        probe.OnDisk.Add("Missing.Dep");

        watcher.OnAssemblyLoaded(a);

        Assert.That(a.GetTypeCalls, Is.EqualTo(0));
    }

    [Test]
    public void AssemblyWithoutReferences_CheckedNow()
    {
        var a = new GatedAssembly(TestTypes);

        Watcher(new FakeProbe()).OnAssemblyLoaded(a);

        Assert.That(_metrics, Is.EqualTo(new[] { Metric }));
    }

    [Test]
    public void NullReferenceEntries_AreIgnored()
    {
        var a = new GatedAssembly(TestTypes) { References = new AssemblyName[] { null, new AssemblyName() } };

        Watcher(new FakeProbe()).OnAssemblyLoaded(a);

        Assert.That(_metrics, Is.EqualTo(new[] { Metric }));
    }

    [Test]
    public void NetCorePath_NoProbe_ChecksNowWhateverTheReferences()
    {
        var a = new GatedAssembly(TestTypes, "Missing.Dep");

        Watcher(null).OnAssemblyLoaded(a);

        Assert.Multiple(() =>
        {
            Assert.That(a.GetTypeCalls, Is.EqualTo(1));
            Assert.That(a.GetReferencedAssembliesCalls, Is.EqualTo(0), "no reference is looked at on .NET");
            Assert.That(_metrics, Is.EqualTo(new[] { Metric }));
            Assert.That(Deferrals(), Is.Empty);
        });
    }

    [Test]
    public void DefaultConstructor_GatesOnlyOnNetFramework()
    {
        var a = new GatedAssembly(TestTypes, "Missing.Dep.That.Is.Nowhere");

        new DistroOtelDerivedPrecedenceWatcher(new[] { DerivedOtel() }, new[] { NrOn(typeof(PrecedenceDerived)) }).OnAssemblyLoaded(a);

#if NETFRAMEWORK
        Assert.Multiple(() =>
        {
            Assert.That(a.GetTypeCalls, Is.EqualTo(0));
            Assert.That(Deferrals(), Has.Length.EqualTo(1));
        });
#else
        Assert.Multiple(() =>
        {
            Assert.That(a.GetTypeCalls, Is.EqualTo(1));
            Assert.That(Deferrals(), Is.Empty);
        });
#endif
    }

    [TestCase("seed")]
    [TestCase("probe")]
    [TestCase("references")]
    public void SeamThrows_NeverThrows_TreatedAsDeferred(string what)
    {
        var probe = new FakeProbe { ThrowOnSeed = what == "seed", ThrowOnProbe = what == "probe" };
        var a = new GatedAssembly(TestTypes, "Some.Dep") { ThrowOnReferences = what == "references" };
        var watcher = Watcher(probe);

        Assert.DoesNotThrow(() => watcher.OnAssemblyLoaded(a));

        Assert.Multiple(() =>
        {
            Assert.That(a.GetTypeCalls, Is.EqualTo(0));
            Assert.That(_metrics, Is.Empty);
            Assert.That(Deferrals(), Has.Length.EqualTo(1));
        });
    }

    [Test]
    public void SeedThrowsOnce_RecheckSeedsAgainAndChecks()
    {
        var probe = new FakeProbe { ThrowOnSeed = true, OnDisk = { "Some.Dep" } };
        var a = new GatedAssembly(TestTypes, "Some.Dep");
        var watcher = Watcher(probe);
        watcher.OnAssemblyLoaded(a);

        probe.ThrowOnSeed = false;
        watcher.OnAssemblyLoaded(new NamedAssembly("Unrelated"));

        Assert.Multiple(() =>
        {
            Assert.That(a.GetTypeCalls, Is.EqualTo(1));
            Assert.That(_metrics, Is.EqualTo(new[] { Metric }));
        });
    }

    [Test]
    public void ProbeThrowsDuringRecheck_NeverThrows_StaysDeferred()
    {
        var probe = new FakeProbe();
        var a = new GatedAssembly(TestTypes, "Some.Dep");
        var watcher = Watcher(probe);
        watcher.OnAssemblyLoaded(a);
        probe.ThrowOnProbe = true;

        Assert.DoesNotThrow(() => watcher.OnAssemblyLoaded(new NamedAssembly("Unrelated.One")));
        probe.ThrowOnProbe = false;
        probe.OnDisk.Add("Some.Dep");
        watcher.OnAssemblyLoaded(new NamedAssembly("Unrelated.Two"));

        Assert.That(a.GetTypeCalls, Is.EqualTo(1));
    }

    [Test]
    public void LoggerThrowsOnDeferral_StillDeferred()
    {
        Log.Initialize(new ThrowingLogger());
        var a = new GatedAssembly(TestTypes, "Missing.Dep");
        var watcher = Watcher(new FakeProbe());

        Assert.DoesNotThrow(() => watcher.OnAssemblyLoaded(a));

        Assert.That(a.GetTypeCalls, Is.EqualTo(0));
    }

    [Test]
    public void RecheckedSiteThrows_RetriedOnLaterLoads_UpToTheCap()
    {
        var probe = new FakeProbe();
        var a = new GatedAssembly(TestTypes, "Missing.Dep") { ThrowingTypeName = typeof(PrecedenceDerived).FullName };
        var watcher = Watcher(probe);
        watcher.OnAssemblyLoaded(a);
        probe.OnDisk.Add("Missing.Dep");

        for (var i = 0; i < DistroOtelDerivedPrecedenceWatcher.MaxDeferredCheckAttempts + 3; i++)
        {
            watcher.OnAssemblyLoaded(new NamedAssembly("Unrelated." + i));
        }

        Assert.Multiple(() =>
        {
            Assert.That(a.GetTypeCalls, Is.EqualTo(DistroOtelDerivedPrecedenceWatcher.MaxDeferredCheckAttempts));
            Assert.That(_logger.Messages(Level.Debug), Does.Contain("OTel distro: giving up the deferred Derived/Interface precedence check of " + TestAssembly + " after " + DistroOtelDerivedPrecedenceWatcher.MaxDeferredCheckAttempts + " attempts."));
        });
    }

    [Test]
    public void RecheckedSiteThrowsOnce_RetriedOnlyForThatSite_ThenReported()
    {
        var probe = new FakeProbe();
        var a = new GatedAssembly(TestTypes, "Missing.Dep") { ThrowingTypeName = typeof(PrecedenceDerived).FullName, ThrowCount = 1 };
        var watcher = Watcher(probe, typeof(PrecedenceDerived), typeof(PrecedenceBoth));
        watcher.OnAssemblyLoaded(a);
        probe.OnDisk.Add("Missing.Dep");

        // First recheck: PrecedenceDerived throws (as a recursive type load would), PrecedenceBoth is checked.
        watcher.OnAssemblyLoaded(new NamedAssembly("Unrelated.One"));
        var afterFirst = a.GetTypeCallsFor(typeof(PrecedenceBoth).FullName);
        watcher.OnAssemblyLoaded(new NamedAssembly("Unrelated.Two"));
        watcher.OnAssemblyLoaded(new NamedAssembly("Unrelated.Three"));

        Assert.Multiple(() =>
        {
            Assert.That(afterFirst, Is.EqualTo(1));
            Assert.That(a.GetTypeCallsFor(typeof(PrecedenceBoth).FullName), Is.EqualTo(1), "a site already checked is not retried");
            Assert.That(a.GetTypeCallsFor(typeof(PrecedenceDerived).FullName), Is.EqualTo(2));
            Assert.That(_metrics, Has.Count.EqualTo(2));
        });
    }

    [Test]
    public void ImmediateCheck_SiteThrows_NotRetried()
    {
        var a = new GatedAssembly(TestTypes) { ThrowingTypeName = typeof(PrecedenceDerived).FullName };
        var watcher = Watcher(new FakeProbe());

        watcher.OnAssemblyLoaded(a);
        watcher.OnAssemblyLoaded(new NamedAssembly("Unrelated"));

        Assert.That(a.GetTypeCalls, Is.EqualTo(1));
    }

    [Test]
    public void TwoDeferredAssemblies_EachCheckedWhenItsReferenceAppears()
    {
        var probe = new FakeProbe();
        var watcher = new DistroOtelDerivedPrecedenceWatcher(new[] { DerivedOtel() }, new[] { NrOn(typeof(PrecedenceDerived)), new NrSite { Assembly = "Second", Type = "X", Method = "Run" } }, probe);
        var first = new GatedAssembly(TestTypes, "First.Dep");
        var second = new GatedAssembly(new NamedAssembly("Second"), "Second.Dep");
        watcher.OnAssemblyLoaded(first);
        watcher.OnAssemblyLoaded(second);

        probe.OnDisk.Add("Second.Dep");
        watcher.OnAssemblyLoaded(new NamedAssembly("Unrelated.One"));
        var firstAfterOne = first.GetTypeCalls;
        var secondAfterOne = second.GetTypeCalls;
        probe.OnDisk.Add("First.Dep");

        // The second assembly's load and Unrelated.One were the first's eligible loads 1 and 2; these are 3 and 4, and
        // 4 is the next probe load.
        watcher.OnAssemblyLoaded(new NamedAssembly("Unrelated.Two"));
        watcher.OnAssemblyLoaded(new NamedAssembly("Unrelated.Three"));

        Assert.Multiple(() =>
        {
            Assert.That(firstAfterOne, Is.EqualTo(0));
            Assert.That(secondAfterOne, Is.EqualTo(1));
            Assert.That(second.GetTypeCalls, Is.EqualTo(1));
            Assert.That(first.GetTypeCalls, Is.EqualTo(1));
            Assert.That(Deferrals(), Has.Length.EqualTo(2));
            Assert.That(_metrics, Is.EqualTo(new[] { Metric }));
        });
    }

    [Test]
    public void RecheckRacingAnotherLoad_TheTypeIsCheckedOnce()
    {
        var probe = new FakeProbe();
        var a = new GatedAssembly(TestTypes, "Missing.Dep");
        var watcher = Watcher(probe);
        watcher.OnAssemblyLoaded(a);
        probe.OnDisk.Add("Missing.Dep");

        // While the outer recheck probes, another load runs the same recheck to the end (as a load on another thread
        // could); the outer one then finds the assembly taken and stops.
        var nested = 0;
        probe.OnProbe = () =>
        {
            if (nested++ == 0)
            {
                watcher.OnAssemblyLoaded(new NamedAssembly("Unrelated.Inner"));
            }
        };
        watcher.OnAssemblyLoaded(new NamedAssembly("Unrelated.Outer"));

        Assert.Multiple(() =>
        {
            Assert.That(a.GetTypeCalls, Is.EqualTo(1));
            Assert.That(_metrics, Is.EqualTo(new[] { Metric }));
        });
    }

    [Test]
    public void DeferredAssemblyWithNullReferenceEntries_IgnoresThem()
    {
        var probe = new FakeProbe();
        var a = new GatedAssembly(TestTypes) { References = new[] { null, new AssemblyName(), new AssemblyName("Missing.Dep") } };
        var watcher = Watcher(probe);
        watcher.OnAssemblyLoaded(a);
        probe.OnDisk.Add("Missing.Dep");

        watcher.OnAssemblyLoaded(new NamedAssembly("Unrelated"));

        Assert.Multiple(() =>
        {
            Assert.That(Deferrals(), Is.EqualTo(new[] { DeferredPrefix + TestAssembly + ": Missing.Dep is neither loaded nor where a bind would look for it; checking again on a later assembly load." }));
            Assert.That(a.GetTypeCalls, Is.EqualTo(1));
        });
    }

    [Test]
    public void ThrowingLoadedAssembly_DoesNotStopLaterRechecks()
    {
        var probe = new FakeProbe();
        var a = new GatedAssembly(TestTypes, "Missing.Dep");
        var watcher = Watcher(probe);
        watcher.OnAssemblyLoaded(a);
        probe.OnDisk.Add("Missing.Dep");

        Assert.DoesNotThrow(() => watcher.OnAssemblyLoaded(new NamedAssembly(null)));
        watcher.OnAssemblyLoaded(new NamedAssembly("Unrelated"));

        Assert.That(a.GetTypeCalls, Is.EqualTo(1));
    }

    [Test]
    public void ReflectionOnlyLoadOfAReference_DoesNotCountAsLoaded()
    {
        var probe = new FakeProbe();
        var watcher = Watcher(probe);
        watcher.OnAssemblyLoaded(new NamedAssembly("Missing.Dep") { IsReflectionOnly = true });
        var a = new GatedAssembly(TestTypes, "Missing.Dep");

        watcher.OnAssemblyLoaded(a);

        Assert.Multiple(() =>
        {
            Assert.That(a.GetTypeCalls, Is.EqualTo(0), "a reflection-only copy does not make the execution-context bind succeed");
            Assert.That(Deferrals(), Has.Length.EqualTo(1));
        });
    }

    [Test]
    public void ReflectionOnlyLoadOfAReference_WhileDeferred_DoesNotTriggerTheCheck()
    {
        var probe = new FakeProbe();
        var a = new GatedAssembly(TestTypes, "Missing.Dep");
        var watcher = Watcher(probe);
        watcher.OnAssemblyLoaded(a);

        watcher.OnAssemblyLoaded(new NamedAssembly("Missing.Dep") { IsReflectionOnly = true });
        watcher.OnAssemblyLoaded(new NamedAssembly("Unrelated"));

        Assert.That(a.GetTypeCalls, Is.EqualTo(0));
    }

    [Test]
    public void ReflectionOnlySiteAssembly_IsIgnored()
    {
        var a = new GatedAssembly(TestTypes) { IsReflectionOnly = true };

        Watcher(new FakeProbe()).OnAssemblyLoaded(a);
        Watcher(null).OnAssemblyLoaded(a);

        Assert.Multiple(() =>
        {
            Assert.That(a.GetTypeCalls, Is.EqualTo(0));
            Assert.That(Deferrals(), Is.Empty);
        });
    }

    [Test]
    public void FailClosedProbe_DefersUntilTheReferenceIsLoaded_ThenChecks()
    {
        var a = new GatedAssembly(TestTypes, "Missing.Dep");
        var watcher = Watcher(new DistroFailClosedDependencyProbe());

        watcher.OnAssemblyLoaded(a);
        var beforeLoad = a.GetTypeCalls;
        watcher.OnAssemblyLoaded(new NamedAssembly("Missing.Dep"));
        watcher.OnAssemblyLoaded(new NamedAssembly("Unrelated"));

        Assert.Multiple(() =>
        {
            Assert.That(beforeLoad, Is.EqualTo(0));
            Assert.That(a.GetTypeCalls, Is.EqualTo(1));
            Assert.That(_metrics, Is.EqualTo(new[] { Metric }));
        });
    }

    [Test]
    public void DeferredProbeBackOff_ProbesOnLoads1_2_4_Doubling_ThenEveryMaxInterval()
    {
        var probe = new FakeProbe();
        var a = new GatedAssembly(TestTypes, "Never.Dep");
        var watcher = Watcher(probe);
        watcher.OnAssemblyLoaded(a);
        probe.ProbedNames.Clear();

        var probedOn = new List<int>();
        for (var load = 1; load <= 200; load++)
        {
            var before = probe.ProbedNames.Count;
            watcher.OnAssemblyLoaded(new NamedAssembly("Unrelated." + load));
            if (probe.ProbedNames.Count > before)
            {
                probedOn.Add(load);
            }
        }

        Assert.Multiple(() =>
        {
            Assert.That(DistroOtelDerivedPrecedenceWatcher.MaxDeferredProbeInterval, Is.EqualTo(64));
            Assert.That(probedOn, Is.EqualTo(new[] { 1, 2, 4, 8, 16, 32, 64, 128, 192 }));
        });
    }

    [Test]
    public void DeferredProbeBackOff_ReferenceAppearsLate_StillChecked()
    {
        var probe = new FakeProbe();
        var a = new GatedAssembly(TestTypes, "Late.Dep");
        var watcher = Watcher(probe);
        watcher.OnAssemblyLoaded(a);
        for (var load = 1; load <= 100; load++)
        {
            watcher.OnAssemblyLoaded(new NamedAssembly("Unrelated." + load));
        }

        probe.OnDisk.Add("Late.Dep");
        var loads = 0;
        while (a.GetTypeCalls == 0 && loads < DistroOtelDerivedPrecedenceWatcher.MaxDeferredProbeInterval)
        {
            watcher.OnAssemblyLoaded(new NamedAssembly("After." + loads++));
        }

        Assert.Multiple(() =>
        {
            Assert.That(a.GetTypeCalls, Is.EqualTo(1), "checked within one maximum interval of the file appearing");
            Assert.That(loads, Is.EqualTo(28), "load 128 is the next probe after load 100");
        });
    }

    [Test]
    public void DeferredProbeBackOff_ALoadedReferenceIsSeenOnTheNextLoad_WithoutWaitingForAProbeLoad()
    {
        var probe = new FakeProbe();
        var a = new GatedAssembly(TestTypes, "Missing.Dep");
        var watcher = Watcher(probe);
        watcher.OnAssemblyLoaded(a);
        for (var load = 1; load <= 4; load++)
        {
            watcher.OnAssemblyLoaded(new NamedAssembly("Unrelated." + load));
        }

        var probesBefore = probe.ProbedNames.Count;
        watcher.OnAssemblyLoaded(new NamedAssembly("Missing.Dep"));
        watcher.OnAssemblyLoaded(new NamedAssembly("Unrelated.5"));

        Assert.Multiple(() =>
        {
            Assert.That(a.GetTypeCalls, Is.EqualTo(1), "load 5 is not a probe load, but a loaded reference needs no probe");
            Assert.That(probe.ProbedNames.Count, Is.EqualTo(probesBefore));
        });
    }

    [Test]
    public void DeferredProbeBackOff_UnknownReferences_ReReadOnlyOnProbeLoads()
    {
        var probe = new FakeProbe { ThrowOnProbe = true, OnDisk = { "Some.Dep" } };
        var a = new GatedAssembly(TestTypes, "Some.Dep");
        var watcher = Watcher(probe);
        watcher.OnAssemblyLoaded(a);
        watcher.OnAssemblyLoaded(new NamedAssembly("Unrelated.1"));
        watcher.OnAssemblyLoaded(new NamedAssembly("Unrelated.2"));
        var readsAfterTwo = a.GetReferencedAssembliesCalls;

        probe.ThrowOnProbe = false;
        watcher.OnAssemblyLoaded(new NamedAssembly("Unrelated.3"));
        var readsAfterThree = a.GetReferencedAssembliesCalls;
        var checkedAfterThree = a.GetTypeCalls;
        watcher.OnAssemblyLoaded(new NamedAssembly("Unrelated.4"));

        Assert.Multiple(() =>
        {
            Assert.That(readsAfterThree, Is.EqualTo(readsAfterTwo), "load 3 is not a probe load: the references are not re-read");
            Assert.That(checkedAfterThree, Is.EqualTo(0));
            Assert.That(a.GetTypeCalls, Is.EqualTo(1), "load 4 re-reads and probes them");
        });
    }

    // ---- fakes ----

    private sealed class FakeProbe : IDistroDependencyProbe
    {
        public HashSet<string> Loaded { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> OnDisk { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public List<string> ProbedNames { get; } = new List<string>();
        public int SeedCalls { get; private set; }
        public bool ThrowOnSeed { get; set; }
        public bool ThrowOnProbe { get; set; }
        public Action OnProbe { get; set; }

        public IEnumerable<string> GetLoadedAssemblyNames()
        {
            SeedCalls++;
            if (ThrowOnSeed)
            {
                throw new InvalidOperationException("seed");
            }

            return Loaded.ToArray();
        }

        public bool HasProbingFile(AssemblyName reference, Assembly referencingAssembly)
        {
            if (ThrowOnProbe)
            {
                throw new InvalidOperationException("probe");
            }

            OnProbe?.Invoke();
            ProbedNames.Add(reference.Name);
            return OnDisk.Contains(reference.Name);
        }
    }

    // An assembly with a chosen simple name and nothing else (an unrelated load, or a dependency's own load).
    private sealed class NamedAssembly : Assembly
    {
        private readonly string _name;

        public NamedAssembly(string name)
        {
            _name = name;
        }

        public bool IsReflectionOnly { get; set; }

        public override bool ReflectionOnly => IsReflectionOnly;

        public override AssemblyName GetName() => _name == null ? throw new InvalidOperationException("name") : new AssemblyName(_name);

        public override AssemblyName[] GetReferencedAssemblies() => new AssemblyName[0];

        public override Type GetType(string name, bool throwOnError) => null;
    }

    // An NR-site assembly with chosen references; types resolve from the inner assembly, and GetType calls are counted.
    private sealed class GatedAssembly : Assembly
    {
        private readonly Assembly _inner;
        private readonly Dictionary<string, int> _calls = new Dictionary<string, int>();

        public GatedAssembly(Assembly inner, params string[] references)
        {
            _inner = inner;
            References = references.Select(r => new AssemblyName(r)).ToArray();
        }

        public AssemblyName[] References { get; set; }
        public bool ThrowOnReferences { get; set; }
        public string ThrowingTypeName { get; set; }
        public int ThrowCount { get; set; } = int.MaxValue;
        public int GetTypeCalls => _calls.Values.Sum();
        public int GetReferencedAssembliesCalls { get; private set; }

        public bool IsReflectionOnly { get; set; }

        public override bool ReflectionOnly => IsReflectionOnly;

        public int GetTypeCallsFor(string name) => _calls.TryGetValue(name, out var n) ? n : 0;

        public override AssemblyName GetName() => _inner.GetName();

        public override AssemblyName[] GetReferencedAssemblies()
        {
            GetReferencedAssembliesCalls++;
            return ThrowOnReferences ? throw new InvalidOperationException("references") : References;
        }

        public override Type GetType(string name, bool throwOnError)
        {
            _calls[name] = GetTypeCallsFor(name) + 1;
            if (name == ThrowingTypeName && ThrowCount-- > 0)
            {
                throw new TypeLoadException("recursive load");
            }

            return _inner.GetType(name, throwOnError);
        }
    }

    private sealed class ThrowingLogger : ILogger
    {
        public bool IsDebugEnabled => true;
        public bool IsErrorEnabled => true;
        public bool IsFinestEnabled => true;
        public bool IsInfoEnabled => true;
        public bool IsWarnEnabled => true;

        public void Debug(Exception exception, string message, params object[] args) => throw new InvalidOperationException("logger");
        public void Debug(string message, params object[] args) => throw new InvalidOperationException("logger");
        public void Error(Exception exception, string message, params object[] args) => throw new InvalidOperationException("logger");
        public void Error(string message, params object[] args) => throw new InvalidOperationException("logger");
        public void Finest(Exception exception, string message, params object[] args) => throw new InvalidOperationException("logger");
        public void Finest(string message, params object[] args) => throw new InvalidOperationException("logger");
        public void Info(Exception exception, string message, params object[] args) => throw new InvalidOperationException("logger");
        public void Info(string message, params object[] args) => throw new InvalidOperationException("logger");
        public void Warn(Exception exception, string message, params object[] args) => throw new InvalidOperationException("logger");
        public void Warn(string message, params object[] args) => throw new InvalidOperationException("logger");

        public bool IsEnabledFor(Level level) => true;
        public void Log(Level level, string message) => throw new InvalidOperationException("logger");
        public void Log(Level level, Exception ex, string message) => throw new InvalidOperationException("logger");
    }
}

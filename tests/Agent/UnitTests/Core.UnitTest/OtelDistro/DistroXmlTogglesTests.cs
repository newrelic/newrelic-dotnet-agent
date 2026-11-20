// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NewRelic.Agent.Core.OtelDistro.Xml;
using NewRelic.Agent.Extensions.Logging;
using NUnit.Framework;
using Telerik.JustMock;
using static NewRelic.Agent.Core.OtelDistro.DistroShippedXmlTestData;

namespace NewRelic.Agent.Core.OtelDistro;

[TestFixture]
public class DistroXmlTogglesTests
{
    private const string FileA = "A.xml";
    private const string FileB = "B.xml";
    private const string FileC = "C.xml";
    private const string Extra = "Extra.xml";
    private const string Root = "/nr/extensions";
    private const string WatcherDisabledMessage = "Live instrumentation updates due to instrumentation file changes will not be applied because they have been disabled by local configuration.";
    private const string RestartRecommended = "restart recommended";

    // A.xml: "Run" is on by default, "Read" is shipped enabled="false". B.xml: "Go" is on by default. C.xml: hash-only.
    private static readonly string TextA = File(
        Factory("F1", "M1", "Asm", "NS.Foo", "Run", "System.String"),
        Factory("Fdis", "Mdis", "Asm", "NS.Foo", "Read", "System.Int32", enabled: "false"));
    private static readonly string TextAReadOn = TextA.Replace("enabled=\"false\"", "enabled=\"true\"");
    private static readonly string TextB = File(Factory("F2", "M2", "Asm2", "NS.Bar", "Go", ""));
    private static readonly string TextC = File(Factory("F3", "M3", "Asm3", "NS.Baz", "Hash", ""));
    private static readonly string UnknownPoint = File(Factory("F9", "M9", "Custom", "NS.Custom", "Mine", ""));

    private Shipped _shipped;
    private IDistroXmlFileSource _files;
    private IDistroXmlChangeWatcher _watcher;
    private IReadOnlyList<DistroXmlFile> _onDisk;
    private int _readCount;
    private List<string> _fileReads;
    private string _rootDirectory;
    private bool _rootExists;
    private Action _watcherCallback;
    private Func<bool> _catchUp;
    private int _watcherStarts;
    private List<int[]> _registerCalls;
    private bool _registerResult;
    private string _registerFailure;
    private DistroRegisterSites _registerSites;
    private int _appliedCalls;
    private Action _onApplied;
    private DistroRecordingLogger _logger;
    private List<string> _metrics;
    private DistroXmlToggles _toggles;

    [SetUp]
    public void SetUp()
    {
        _shipped = new Shipped(new[] { (FileA, TextA, true), (FileB, TextB, true) });
        _onDisk = Unchanged();
        _readCount = 0;
        _fileReads = new List<string>();
        _rootDirectory = Root;
        _rootExists = true;
        _files = Mock.Create<IDistroXmlFileSource>();
        Mock.Arrange(() => _files.RootDirectory).Returns(() => _rootDirectory);
        Mock.Arrange(() => _files.RootExists).Returns(() => _rootExists);
        Mock.Arrange(() => _files.List()).Returns(() =>
        {
            Interlocked.Increment(ref _readCount);
            return Listing(_onDisk);
        });
        Mock.Arrange(() => _files.Read(Arg.IsAny<DistroXmlFileEntry>())).Returns((DistroXmlFileEntry entry) =>
        {
            lock (_fileReads)
            {
                _fileReads.Add(entry.HomeName);
            }

            return _onDisk.First(f => f.Path == entry.Path);
        });

        _watcherCallback = null;
        _catchUp = null;
        _watcherStarts = 0;
        _watcher = Mock.Create<IDistroXmlChangeWatcher>();
        Mock.Arrange(() => _watcher.Start(Arg.IsAny<Action>(), Arg.IsAny<Func<bool>>())).DoInstead((Action onChange, Func<bool> changedSinceListing) =>
        {
            _watcherStarts++;
            _watcherCallback = onChange;
            _catchUp = changedSinceListing;
        });

        _registerCalls = new List<int[]>();
        _registerResult = true;
        _registerFailure = "OTel 1.18.0.0: native said no";
        _registerSites = (IReadOnlyList<int> indices, out string failure) =>
        {
            lock (_registerCalls)
            {
                _registerCalls.Add(indices.ToArray());
            }

            failure = _registerResult ? null : _registerFailure;
            return _registerResult;
        };

        _appliedCalls = 0;
        _onApplied = () => _appliedCalls++;

        DistroConfigMetrics.ResetForTesting();
        _metrics = new List<string>();
        DistroConfigMetrics.SetMetricSink(_metrics.Add);
        _logger = new DistroRecordingLogger();
        Log.Initialize(_logger);
    }

    [TearDown]
    public void TearDown()
    {
        _toggles?.Dispose();
        _watcher?.Dispose();
        Log.Initialize(new NoOpLogger());
        DistroConfigMetrics.ResetForTesting();
    }

    private static DistroXmlFile Disk(string name, string text) => new DistroXmlFile(name, Root + "/" + name, Encoding.UTF8.GetBytes(text), null);

    private static DistroXmlFile Unreadable(string name, string error) => new DistroXmlFile(name, Root + "/" + name, null, error);

    private static DistroXmlFile[] Unchanged() => new[] { Disk(FileA, TextA), Disk(FileB, TextB) };

    // The listing a real source would give: the stamp follows the content, so an edit changes it.
    private static IReadOnlyList<DistroXmlFileEntry> Listing(IReadOnlyList<DistroXmlFile> files) =>
        files.Select(f => new DistroXmlFileEntry(
            f.HomeName,
            f.Path,
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddTicks((long)((f.Bytes == null ? XmlContentHasher.Compute(f.ReadError) : XmlContentHasher.Compute(f.Bytes)) & 0xFFFFFFF)),
            f.Bytes?.Length ?? 0)).ToArray();

    private DistroXmlToggles Create(bool watcherDisabled = false) =>
        _toggles = new DistroXmlToggles(_shipped.Sites, _shipped.Manifest, _files, _watcher, watcherDisabled, _registerSites, _onApplied);

    private int[] SitesOf(string method)
    {
        var indexes = new List<int>();
        for (var i = 0; i < _shipped.Sites.Count; i++)
        {
            _shipped.Sites.TryGet(i, out var site);
            if (site.MethodName == method)
            {
                indexes.Add(i);
            }
        }

        return indexes.ToArray();
    }

    private bool[] DefaultState()
    {
        var state = new bool[_shipped.Sites.Count];
        for (var i = 0; i < state.Length; i++)
        {
            state[i] = !_shipped.Sites.IsDefaultOff(i);
        }

        return state;
    }

    private bool[] CurrentState() => Enumerable.Range(0, _shipped.Sites.Count).Select(_shipped.Sites.IsOn).ToArray();

    private bool IsOn(string method) => SitesOf(method).All(_shipped.Sites.IsOn);

    private bool IsOff(string method) => !SitesOf(method).Any(_shipped.Sites.IsOn);

    private int Count(string metric) => _metrics.Count(m => m == metric);

    // ---- ApplyAtStartup

    [Test]
    public void ApplyAtStartup_Unchanged_ReturnsDefaultOnSitesAndStartsWatcher()
    {
        var toggles = Create();
        var timingsBefore = toggles.StartupTimings;

        var registerNow = toggles.ApplyAtStartup();

        Assert.Multiple(() =>
        {
            Assert.That(registerNow, Is.EqualTo(DefaultState()));
            Assert.That(registerNow.Count(on => on), Is.EqualTo(3), "plugin, Run and Go");
            Assert.That(CurrentState(), Is.EqualTo(DefaultState()));
            Assert.That(_watcherStarts, Is.EqualTo(1));
            Assert.That(_watcherCallback, Is.Not.Null);
            Assert.That(_catchUp != null, Is.True, "a catch-up check is handed to the watcher");
            Assert.That(_registerCalls, Is.Empty, "the plugin registers the start-up set itself");
            Assert.That(_logger.Messages(Level.Error), Is.Empty);
            Assert.That(_logger.Messages(Level.Warn), Is.Empty);
            Assert.That(_logger.Messages(Level.Info), Has.Length.EqualTo(1).And.Some.StartsWith("NR instrumentation XML matches the distro's compiled instrumentation"));
            Assert.That(_logger.Messages(Level.Debug), Does.Not.Contain(WatcherDisabledMessage));
            Assert.That(_appliedCalls, Is.EqualTo(0), "start-up has nothing cached to clear");
            Assert.That(timingsBefore, Is.Null);
            Assert.That(toggles.StartupTimings, Is.Not.Null);
            Assert.That(new[] { toggles.StartupTimings.ListMs, toggles.StartupTimings.ReadMs, toggles.StartupTimings.ComputeMs, toggles.StartupTimings.ReportMs, toggles.StartupTimings.WatcherMs }, Has.All.GreaterThanOrEqualTo(0.0));
        });
    }

    [Test]
    public void ApplyAtStartup_HashOnlyFile_IsListedButNeverRead()
    {
        _shipped = new Shipped(new[] { (FileA, TextA, true), (FileB, TextB, true), (FileC, TextC, false) });
        _onDisk = new[] { Disk(FileA, TextA), Disk(FileB, TextB), Disk(FileC, "not even xml") };

        var registerNow = Create().ApplyAtStartup();

        Assert.Multiple(() =>
        {
            Assert.That(_fileReads, Is.EquivalentTo(new[] { FileA, FileB }));
            Assert.That(registerNow, Is.EqualTo(DefaultState()));
            Assert.That(_logger.Messages(Level.Info), Has.Some.Contains("(1 shipped instrumentation files are not compiled into the distro yet; edits to them have no effect)"));
            Assert.That(_logger.Messages(Level.Error), Is.Empty);
        });
    }

    [Test]
    public void ApplyAtStartup_EnabledTrueEdit_ReturnsDefaultOffSitesToo()
    {
        _onDisk = new[] { Disk(FileA, TextAReadOn), Disk(FileB, TextB) };

        var registerNow = Create().ApplyAtStartup();

        Assert.Multiple(() =>
        {
            Assert.That(SitesOf("Read").All(i => registerNow[i]), Is.True);
            Assert.That(registerNow.Count(on => on), Is.EqualTo(4));
            Assert.That(IsOn("Read"), Is.True);
            Assert.That(Count(DistroXmlToggleDiagnostics.PointsEnabledMetricName), Is.EqualTo(1));
            Assert.That(_registerCalls, Is.Empty);
            Assert.That(_logger.Messages(Level.Info), Has.Some.EndsWith("from files: " + FileA));
            Assert.That(_logger.Messages(Level.Info), Has.None.Contains(RestartRecommended), "registered at start-up, not at run time");
        });
    }

    [Test]
    public void ApplyAtStartup_InvalidCompiledFile_SkippedAloneKeepsItsDefaultsOthersApply()
    {
        // A.xml is broken; a copy of it with Read turned on still applies (agent mode's start-up rule).
        _onDisk = new[] { Disk(FileA, "<extension><instrumentation>"), Disk(FileB, TextB), Disk(Extra, TextAReadOn) };

        var registerNow = Create().ApplyAtStartup();

        Assert.Multiple(() =>
        {
            Assert.That(IsOn("Run"), Is.True, "the broken shipped file keeps its defaults");
            Assert.That(IsOn("Read"), Is.True, "the other file's edit applies");
            Assert.That(IsOn("Go"), Is.True);
            Assert.That(SitesOf("Read").All(i => registerNow[i]), Is.True);
            Assert.That(_logger.Messages(Level.Error), Has.Length.EqualTo(1));
            Assert.That(_logger.Messages(Level.Error)[0], Does.StartWith("NR instrumentation XML " + FileA + " could not be parsed").And.Contain("live instrumentation updates will not be applied until it is fixed"));
            Assert.That(Count(DistroXmlToggleDiagnostics.ParseErrorMetricName), Is.EqualTo(1));
            Assert.That(_logger.Messages(Level.Info), Has.Some.Contains("from files: " + Extra + ", " + FileA + " (invalid)"));
            Assert.That(_watcherStarts, Is.EqualTo(1), "a later fix must still be picked up");
        });
    }

    [Test]
    public void ApplyAtStartup_InvalidExtraFile_ContributesNothing()
    {
        _onDisk = new[] { Disk(FileA, TextA), Disk(FileB, TextB), Disk(Extra, "<extension>") };

        var registerNow = Create().ApplyAtStartup();

        Assert.Multiple(() =>
        {
            Assert.That(registerNow, Is.EqualTo(DefaultState()));
            Assert.That(_logger.Messages(Level.Error), Has.Length.EqualTo(1));
            Assert.That(_logger.Messages(Level.Error)[0], Does.Contain(Extra));
            Assert.That(_logger.Messages(Level.Info), Has.Some.Contains("no compiled points changed, 0 not supported, from files: " + Extra + " (invalid)"));
        });
    }

    [Test]
    public void ApplyAtStartup_WatcherDisabled_DoesNotStartLogsDebug()
    {
        var registerNow = Create(watcherDisabled: true).ApplyAtStartup();

        Assert.Multiple(() =>
        {
            Assert.That(registerNow, Is.EqualTo(DefaultState()));
            Assert.That(_watcherStarts, Is.EqualTo(0));
            Assert.That(_logger.Messages(Level.Debug), Is.EqualTo(new[] { WatcherDisabledMessage }));
        });
    }

    [Test]
    public void ApplyAtStartup_Throws_DefaultsOneError()
    {
        Mock.Arrange(() => _files.List()).Throws(new InvalidOperationException("boom"));

        bool[] registerNow = null;
        Assert.DoesNotThrow(() => registerNow = Create().ApplyAtStartup());

        Assert.Multiple(() =>
        {
            Assert.That(registerNow, Is.EqualTo(DefaultState()));
            Assert.That(CurrentState(), Is.EqualTo(DefaultState()));
            Assert.That(_logger.Messages(Level.Error), Has.Length.EqualTo(1));
            Assert.That(_logger.Messages(Level.Error)[0], Does.Contain("InvalidOperationException: boom"));
            Assert.That(_watcherStarts, Is.EqualTo(1));
            Assert.That(_toggles.StartupTimings, Is.Not.Null);
        });
    }

    [Test]
    public void ApplyAtStartup_InconsistentTable_DefaultsOneError()
    {
        // A manifest with fewer files than the sites name: Compute throws on inconsistent input (carried note (a)).
        var other = new Shipped(new[] { (FileA, TextA, true) });
        _toggles = new DistroXmlToggles(_shipped.Sites, other.Manifest, _files, _watcher, false, _registerSites, _onApplied);
        _onDisk = new[] { Disk(FileA, TextAReadOn) };

        var registerNow = _toggles.ApplyAtStartup();

        Assert.Multiple(() =>
        {
            Assert.That(registerNow, Is.EqualTo(DefaultState()));
            Assert.That(_logger.Messages(Level.Error), Has.Length.EqualTo(1));
        });
    }

    [Test]
    public void ApplyAtStartup_UnreadableFile_WarnsAndUsesDefaults()
    {
        _onDisk = new[] { Unreadable(FileA, "The file is locked."), Disk(FileB, TextB) };

        var registerNow = Create().ApplyAtStartup();

        Assert.Multiple(() =>
        {
            Assert.That(registerNow, Is.EqualTo(DefaultState()));
            Assert.That(CurrentState(), Is.EqualTo(DefaultState()));
            Assert.That(_logger.Messages(Level.Warn), Has.Length.EqualTo(1));
            Assert.That(_logger.Messages(Level.Warn)[0], Does.Contain(FileA).And.Contain("The file is locked.").And.Contain("using the distro's compiled defaults for it"));
            Assert.That(_logger.Messages(Level.Error), Is.Empty);
            Assert.That(Count(DistroXmlToggleDiagnostics.UnreadableMetricName), Is.EqualTo(1));
            Assert.That(_logger.Messages(Level.Info), Has.Some.Contains("from files: " + FileA + " (unreadable)"));
        });
    }

    [TestCase(null, "(no extensions directory)")]
    [TestCase(Root, Root)]
    public void ApplyAtStartup_ExtensionsFolderMissing_KeepsDefaultsOneWarn(string rootDirectory, string named)
    {
        _rootDirectory = rootDirectory;
        _rootExists = false;
        _onDisk = new DistroXmlFile[0];

        var toggles = Create();
        var registerNow = toggles.ApplyAtStartup();

        Assert.Multiple(() =>
        {
            Assert.That(registerNow, Is.EqualTo(DefaultState()));
            Assert.That(CurrentState(), Is.EqualTo(DefaultState()));
            Assert.That(_readCount, Is.EqualTo(0), "nothing is listed or computed");
            Assert.That(_logger.Messages(Level.Warn), Is.EqualTo(new[] { "NR instrumentation XML folder " + named + " was not found; using the distro's compiled instrumentation defaults." }));
            Assert.That(Count(DistroXmlToggleDiagnostics.FolderMissingMetricName), Is.EqualTo(1));
            Assert.That(_logger.Messages(Level.Info), Is.Empty, "nothing applied, so nothing reported as applied");
            Assert.That(_logger.Messages(Level.Error), Is.Empty);
            Assert.That(toggles.ChangedSinceStartup(), Is.False, "still no folder: nothing to catch up");
        });
    }

    [Test]
    public void ApplyAtStartup_ExistingFolderWithShippedFilesDeleted_FollowsCompute()
    {
        // The folder exists but holds no shipped file: every shipped point is absent from disk, so only the plugin's own
        // sites stay on (the Compute rule; only a missing folder keeps the defaults).
        _onDisk = new DistroXmlFile[0];

        var registerNow = Create().ApplyAtStartup();

        Assert.Multiple(() =>
        {
            Assert.That(registerNow.Count(on => on), Is.EqualTo(1));
            Assert.That(Count(DistroXmlToggleDiagnostics.FolderMissingMetricName), Is.EqualTo(0));
            Assert.That(_logger.Messages(Level.Error), Is.Empty);
            Assert.That(_logger.Messages(Level.Info), Has.Some.Contains("from files: " + FileA + " (deleted), " + FileB + " (deleted)"));
        });
    }

    [Test]
    public void ApplyAtStartup_SecondCall_ReturnsTheCurrentStateAndDoesNotRestartWatcher()
    {
        _onDisk = new[] { Disk(FileA, TextAReadOn), Disk(FileB, TextB) };
        var toggles = Create();
        var first = toggles.ApplyAtStartup();

        var second = toggles.ApplyAtStartup();

        Assert.Multiple(() =>
        {
            Assert.That(second, Is.EqualTo(first));
            Assert.That(second, Is.Not.SameAs(first));
            Assert.That(_watcherStarts, Is.EqualTo(1));
            Assert.That(_readCount, Is.EqualTo(1));
        });
    }

    [Test]
    public void ApplyAtStartup_AfterDispose_DoesNotStartWatcher()
    {
        var toggles = Create();
        toggles.Dispose();

        var registerNow = toggles.ApplyAtStartup();

        Assert.Multiple(() =>
        {
            Assert.That(registerNow, Is.EqualTo(DefaultState()));
            Assert.That(_watcherStarts, Is.EqualTo(0));
        });
    }

    [Test]
    public void ApplyAtStartup_WatcherStartThrows_LogsErrorAndReturnsSet()
    {
        Mock.Arrange(() => _watcher.Start(Arg.IsAny<Action>(), Arg.IsAny<Func<bool>>())).Throws(new System.IO.IOException("no watch"));

        bool[] registerNow = null;
        Assert.DoesNotThrow(() => registerNow = Create().ApplyAtStartup());

        Assert.Multiple(() =>
        {
            Assert.That(registerNow, Is.EqualTo(DefaultState()));
            Assert.That(_logger.Messages(Level.Error), Has.Length.EqualTo(1));
            Assert.That(_logger.Messages(Level.Error)[0], Does.Contain("IOException: no watch"));
        });
    }

    [Test]
    public void ApplyAtStartup_LoggerThrows_DoesNotThrow()
    {
        Log.Initialize(Mock.Create<ILogger>(Behavior.Strict));
        Mock.Arrange(() => _files.List()).Throws(new InvalidOperationException("boom"));

        bool[] registerNow = null;
        Assert.DoesNotThrow(() => registerNow = Create(watcherDisabled: true).ApplyAtStartup());

        Assert.That(registerNow, Is.EqualTo(DefaultState()));
    }

    [Test]
    public void ApplyAtStartup_UnsupportedPoint_WarnedOncePerProcessEvenInCopies()
    {
        _onDisk = new[] { Disk(FileA, TextA), Disk(FileB, TextB), Disk(Extra, UnknownPoint) };
        Create().ApplyAtStartup();
        var warnsAtStartup = _logger.Messages(Level.Warn).Length;

        // A copy of the custom file plus an edit elsewhere: the same unsupported point must not warn again.
        _onDisk = new[] { Disk(FileA, TextAReadOn), Disk(FileB, TextB), Disk(Extra, UnknownPoint), Disk("Copy of Extra.xml", UnknownPoint) };
        _watcherCallback();

        Assert.Multiple(() =>
        {
            Assert.That(warnsAtStartup, Is.EqualTo(1));
            Assert.That(_logger.Messages(Level.Warn), Has.Length.EqualTo(1));
            Assert.That(Count(DistroXmlToggleDiagnostics.UnsupportedMetricName), Is.EqualTo(1));
            Assert.That(_logger.Messages(Level.Info), Has.Some.Contains("1 turned on, 2 not supported, from files: " + FileA + ", Copy of Extra.xml"));
            Assert.That(IsOn("Read"), Is.True);
        });
    }

    // ---- Watcher catch-up

    [Test]
    public void ChangedSinceStartup_SameListing_False()
    {
        var toggles = Create();
        toggles.ApplyAtStartup();

        Assert.That(_catchUp(), Is.False);
    }

    [Test]
    public void ChangedSinceStartup_EditBeforeTheWatcherIsArmed_TrueAndTheChangeRunAppliesIt()
    {
        Create().ApplyAtStartup();

        // The edit lands after start-up listed the files but before the watcher was armed: no file-system event for it.
        _onDisk = new[] { Disk(FileA, TextAReadOn), Disk(FileB, TextB) };
        var changed = _catchUp();

        // The real watcher signals itself when the catch-up says so; the change run follows.
        _watcherCallback();

        Assert.Multiple(() =>
        {
            Assert.That(changed, Is.True);
            Assert.That(IsOn("Read"), Is.True, "the edit is not lost");
            Assert.That(_registerCalls, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public void ChangedSinceStartup_FileAddedOrRemoved_True()
    {
        Create().ApplyAtStartup();

        _onDisk = new[] { Disk(FileA, TextA), Disk(FileB, TextB), Disk(Extra, UnknownPoint) };
        var added = _catchUp();
        _onDisk = new[] { Disk(FileA, TextA) };
        var removed = _catchUp();
        _onDisk = new[] { Disk(FileA, TextA), Disk(Extra, TextB) };
        var renamed = _catchUp();

        Assert.Multiple(() =>
        {
            Assert.That(added, Is.True);
            Assert.That(removed, Is.True);
            Assert.That(renamed, Is.True);
        });
    }

    [Test]
    public void ChangedSinceStartup_ListingFailsNowOrAtStartup_True()
    {
        Mock.Arrange(() => _files.List()).Throws(new InvalidOperationException("boom"));
        var toggles = Create();
        toggles.ApplyAtStartup();
        var whileFailing = toggles.ChangedSinceStartup();

        // Start-up never got a listing, so any listing now counts as a change.
        Mock.Arrange(() => _files.List()).Returns(() => Listing(_onDisk));

        Assert.Multiple(() =>
        {
            Assert.That(whileFailing, Is.True);
            Assert.That(toggles.ChangedSinceStartup(), Is.True);
        });
    }

    [Test]
    public void ChangedSinceStartup_FolderRemovedAfterStartup_True()
    {
        var toggles = Create();
        toggles.ApplyAtStartup();
        _rootExists = false;

        Assert.That(toggles.ChangedSinceStartup(), Is.True);
    }

    // ---- OnChange

    [Test]
    public void OnChange_TurnOnUnregistered_CallsRegisterOnceAndRecommendsRestart()
    {
        Create().ApplyAtStartup();
        _onDisk = new[] { Disk(FileA, TextAReadOn), Disk(FileB, TextB) };

        _watcherCallback();

        Assert.Multiple(() =>
        {
            Assert.That(_registerCalls, Has.Count.EqualTo(1));
            Assert.That(_registerCalls[0], Is.EqualTo(SitesOf("Read")));
            Assert.That(IsOn("Read"), Is.True);
            Assert.That(IsOn("Run"), Is.True);
            Assert.That(Count(DistroXmlToggleDiagnostics.PointsEnabledMetricName), Is.EqualTo(1));
            Assert.That(_logger.Messages(Level.Info), Has.Some.Contains("turned on NS.Foo.Read"));
            Assert.That(_logger.Messages(Level.Info).Where(m => m.Contains(RestartRecommended)), Is.EqualTo(new[]
            {
                "NR instrumentation XML turned on 1 points that were not registered at start-up; they are now registered with OTel in this process. Methods already compiled into their callers may stay uninstrumented until the application restarts (restart recommended).",
            }));
            Assert.That(Count(DistroXmlToggleDiagnostics.RegisteredAtRunTimeMetricName), Is.EqualTo(1));
            Assert.That(_logger.Messages(Level.Info), Has.Some.Contains("from files: " + FileA));
            Assert.That(_appliedCalls, Is.EqualTo(1), "the wrapper caches are cleared after a change");
        });
    }

    [Test]
    public void OnChange_TurnOnRegistered_GateOnly()
    {
        _onDisk = new[] { Disk(FileA, TextAReadOn), Disk(FileB, TextB) };
        Create().ApplyAtStartup();

        _onDisk = Unchanged();
        _watcherCallback();
        var offAfterFirst = IsOff("Read");

        _onDisk = new[] { Disk(FileA, TextAReadOn), Disk(FileB, TextB) };
        _watcherCallback();

        Assert.Multiple(() =>
        {
            Assert.That(offAfterFirst, Is.True);
            Assert.That(IsOn("Read"), Is.True);
            Assert.That(_registerCalls, Is.Empty, "registered at start-up, so the gate alone turns it back on");
            Assert.That(Count(DistroXmlToggleDiagnostics.PointsDisabledMetricName), Is.EqualTo(1));
            Assert.That(Count(DistroXmlToggleDiagnostics.PointsEnabledMetricName), Is.EqualTo(2), "on at start-up, then on again");
            Assert.That(_logger.Messages(Level.Info), Has.None.Contains(RestartRecommended), "a gate-only turn-on registers nothing");
            Assert.That(Count(DistroXmlToggleDiagnostics.RegisteredAtRunTimeMetricName), Is.EqualTo(0));
            Assert.That(_appliedCalls, Is.EqualTo(2));
        });
    }

    [Test]
    public void OnChange_NothingChanged_NoCacheClearAndNoFilesNamed()
    {
        Create().ApplyAtStartup();

        _watcherCallback();

        Assert.Multiple(() =>
        {
            Assert.That(_appliedCalls, Is.EqualTo(0));
            Assert.That(_logger.Messages(Level.Info).Last(), Does.StartWith("NR instrumentation XML matches the distro's compiled instrumentation"));
        });
    }

    [Test]
    public void OnChange_TouchedAfterAnEdit_NoPointsChangedNamesNoFiles()
    {
        _onDisk = new[] { Disk(FileA, TextAReadOn), Disk(FileB, TextB) };
        Create().ApplyAtStartup();

        _watcherCallback();

        Assert.That(_logger.Messages(Level.Info).Last(), Is.EqualTo("NR instrumentation XML applied: no compiled points changed, 0 not supported, from files: none"));
    }

    [Test]
    public void OnChange_OnAppliedThrows_LogsErrorAndKeepsTheAppliedState()
    {
        _onApplied = () => throw new InvalidOperationException("cache");
        _onDisk = new[] { Disk(FileA, TextAReadOn), Disk(FileB, TextB) };
        Create().ApplyAtStartup();
        _onDisk = Unchanged();

        Assert.DoesNotThrow(() => _watcherCallback());

        Assert.Multiple(() =>
        {
            Assert.That(IsOff("Read"), Is.True);
            Assert.That(_logger.Messages(Level.Error), Has.Length.EqualTo(1));
            Assert.That(_logger.Messages(Level.Error)[0], Does.Contain("wrapper caches could not be cleared").And.Contain("InvalidOperationException: cache"));
        });
    }

    [Test]
    public void OnChange_RestoreAndDelete_NameTheFilesThatChanged()
    {
        _onDisk = new[] { Disk(FileA, TextAReadOn), Disk(FileB, TextB), Disk(Extra, UnknownPoint) };
        Create().ApplyAtStartup();

        // A.xml restored to the shipped bytes (not parsed now) and Extra.xml deleted.
        _onDisk = Unchanged();
        _watcherCallback();

        Assert.That(_logger.Messages(Level.Info), Has.Some.EndsWith("from files: " + FileA + ", " + Extra + " (deleted)"));
    }

    [Test]
    public void OnChange_RegisterFails_SitesStayOffReasonLoggedOnceThenDebug()
    {
        Create().ApplyAtStartup();
        _onDisk = new[] { Disk(FileA, TextAReadOn), Disk(FileB, TextB) };
        _registerResult = false;

        _watcherCallback();
        var offAfterFailure = IsOff("Read");
        var errorsAfterFailure = _logger.Messages(Level.Error);
        var turnedOnLinesAfterFailure = _logger.Messages(Level.Info).Count(m => m.Contains("turned on"));

        // Same reason again: Debug only. Then a different reason: Error again.
        _watcherCallback();
        var errorsAfterRepeat = _logger.Messages(Level.Error).Length;
        var debugAfterRepeat = _logger.Messages(Level.Debug).Count(m => m.Contains("OTel run-time registration is unavailable"));
        _registerFailure = null;
        _watcherCallback();

        // The next change retries the registration (the sites were never registered).
        _registerResult = true;
        _watcherCallback();

        Assert.Multiple(() =>
        {
            Assert.That(offAfterFailure, Is.True);
            Assert.That(errorsAfterFailure, Is.EqualTo(new[]
            {
                "NR instrumentation could not be turned on at run time: OTel run-time registration is unavailable (OTel 1.18.0.0: native said no); 1 points stay off.",
            }));
            Assert.That(turnedOnLinesAfterFailure, Is.EqualTo(0));
            Assert.That(errorsAfterRepeat, Is.EqualTo(1));
            Assert.That(debugAfterRepeat, Is.EqualTo(1));
            Assert.That(_logger.Messages(Level.Error), Has.Length.EqualTo(2));
            Assert.That(_logger.Messages(Level.Error)[1], Does.Contain("(no reason given)"));
            Assert.That(Count(DistroXmlToggleDiagnostics.RegisterUnavailableMetricName), Is.EqualTo(3));
            Assert.That(Count(DistroXmlToggleDiagnostics.PointsEnabledMetricName), Is.EqualTo(1), "counted once, on the successful retry");
            Assert.That(_registerCalls, Has.Count.EqualTo(4));
            Assert.That(IsOn("Read"), Is.True);
        });
    }

    [Test]
    public void OnChange_RegisterThrows_LogsErrorKeepsState()
    {
        _registerSites = (IReadOnlyList<int> indices, out string failure) => throw new InvalidOperationException("native");
        Create().ApplyAtStartup();
        _onDisk = new[] { Disk(FileA, TextAReadOn), Disk(FileB, TextB) };

        Assert.DoesNotThrow(() => _watcherCallback());

        Assert.Multiple(() =>
        {
            Assert.That(IsOff("Read"), Is.True);
            Assert.That(_logger.Messages(Level.Error), Has.Length.EqualTo(1));
            Assert.That(_logger.Messages(Level.Error)[0], Does.Contain("InvalidOperationException: native"));
        });
    }

    [Test]
    public void OnChange_AfterDispose_NoOp()
    {
        var toggles = Create();
        toggles.ApplyAtStartup();
        _onDisk = new[] { Disk(FileA, TextAReadOn), Disk(FileB, TextB) };

        toggles.Dispose();
        _watcherCallback();

        Assert.Multiple(() =>
        {
            Assert.That(toggles.IsDisposed, Is.True);
            Assert.That(_readCount, Is.EqualTo(1));
            Assert.That(IsOff("Read"), Is.True);
            Assert.That(_registerCalls, Is.Empty);
        });
        Mock.Assert(() => _watcher.Dispose(), Occurs.Once());
    }

    [Test]
    public void OnChange_BeforeApplyAtStartup_NoOp()
    {
        var toggles = Create();
        _onDisk = new[] { Disk(FileA, TextAReadOn), Disk(FileB, TextB) };

        toggles.OnChange();

        Assert.Multiple(() =>
        {
            Assert.That(_readCount, Is.EqualTo(0));
            Assert.That(_registerCalls, Is.Empty);
            Assert.That(CurrentState(), Is.EqualTo(DefaultState()));
        });
    }

    [Test]
    public void OnChange_Concurrent_SecondSkippedThenRerunOnce()
    {
        using var entered = new ManualResetEventSlim(false);
        using var release = new ManualResetEventSlim(false);
        _registerSites = (IReadOnlyList<int> indices, out string failure) =>
        {
            lock (_registerCalls)
            {
                _registerCalls.Add(indices.ToArray());
            }

            entered.Set();
            release.Wait(TimeSpan.FromSeconds(30));
            failure = null;
            return true;
        };
        var toggles = Create();
        toggles.ApplyAtStartup();
        _onDisk = new[] { Disk(FileA, TextAReadOn), Disk(FileB, TextB) };

        var first = Task.Run(() => toggles.OnChange());
        Assert.That(entered.Wait(TimeSpan.FromSeconds(10)), Is.True, "the first run reached registerSites");

        // Two more changes arrive while the first run is blocked in registerSites: both return at once.
        var concurrent = Task.Run(() =>
        {
            toggles.OnChange();
            toggles.OnChange();
        });
        var skipped = concurrent.Wait(TimeSpan.FromSeconds(10));
        var readsWhileBlocked = _readCount;

        release.Set();
        Assert.That(first.Wait(TimeSpan.FromSeconds(10)), Is.True);

        Assert.Multiple(() =>
        {
            Assert.That(skipped, Is.True, "a concurrent OnChange must not block");
            Assert.That(readsWhileBlocked, Is.EqualTo(2), "start-up plus the blocked run");
            Assert.That(_readCount, Is.EqualTo(3), "exactly one re-run after the blocked run");
            Assert.That(_registerCalls, Has.Count.EqualTo(1), "the re-run finds the sites registered");
            Assert.That(IsOn("Read"), Is.True);
        });
    }

    [Test]
    public void OnChange_DisposedDuringRun_RerunIsNoOp()
    {
        DistroXmlToggles toggles = null;
        _registerSites = (IReadOnlyList<int> indices, out string failure) =>
        {
            _registerCalls.Add(indices.ToArray());

            // A change arriving during the run (same thread here) queues a re-run; the dispose then makes it a no-op.
            toggles.OnChange();
            toggles.Dispose();
            failure = null;
            return true;
        };
        toggles = Create();
        toggles.ApplyAtStartup();
        _onDisk = new[] { Disk(FileA, TextAReadOn), Disk(FileB, TextB) };

        toggles.OnChange();

        Assert.Multiple(() =>
        {
            Assert.That(_readCount, Is.EqualTo(2), "start-up and the first run; the re-run read nothing");
            Assert.That(_registerCalls, Has.Count.EqualTo(1));
            Assert.That(IsOn("Read"), Is.True, "the run in progress still applies what it registered");
        });
    }

    [Test]
    public void OnChange_ParseErrorThenFixed_KeepsThenApplies()
    {
        Create().ApplyAtStartup();

        // Half-written file: parse error, the whole previous state kept (agent mode's refresh rule), one Error.
        _onDisk = new[] { Disk(FileA, TextAReadOn.Substring(0, TextAReadOn.Length / 2)), Disk(FileB, TextB), Disk(Extra, UnknownPoint) };
        _watcherCallback();
        var stateAfterError = CurrentState();
        var errorsAfterError = _logger.Messages(Level.Error);

        // The next change event recovers.
        _onDisk = new[] { Disk(FileA, TextAReadOn), Disk(FileB, TextB) };
        _watcherCallback();

        Assert.Multiple(() =>
        {
            Assert.That(stateAfterError, Is.EqualTo(DefaultState()));
            Assert.That(errorsAfterError, Has.Length.EqualTo(1));
            Assert.That(errorsAfterError[0], Does.Contain(FileA).And.Contain("instrumentation will not be refreshed"));
            Assert.That(_logger.Messages(Level.Warn), Is.Empty, "a rejected run reports nothing else");
            Assert.That(_logger.Messages(Level.Error), Has.Length.EqualTo(1));
            Assert.That(Count(DistroXmlToggleDiagnostics.ParseErrorMetricName), Is.EqualTo(1));
            Assert.That(IsOn("Read"), Is.True);
            Assert.That(_registerCalls, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public void OnChange_DeleteThenRestore_GateOnlyNoRegister()
    {
        Create().ApplyAtStartup();

        _onDisk = new[] { Disk(FileB, TextB) };
        _watcherCallback();
        var runOffAfterDelete = IsOff("Run");

        _onDisk = Unchanged();
        _watcherCallback();

        Assert.Multiple(() =>
        {
            Assert.That(runOffAfterDelete, Is.True);
            Assert.That(IsOn("Run"), Is.True);
            Assert.That(IsOn("Go"), Is.True);
            Assert.That(CurrentState(), Is.EqualTo(DefaultState()));
            Assert.That(_registerCalls, Is.Empty);
        });
    }

    [Test]
    public void OnChange_FolderRemoved_KeepsCurrentStateWarnsOncePerChange()
    {
        _onDisk = new[] { Disk(FileA, TextAReadOn), Disk(FileB, TextB) };
        Create().ApplyAtStartup();
        var started = CurrentState();

        _rootExists = false;
        _onDisk = new DistroXmlFile[0];
        _watcherCallback();
        _watcherCallback();
        var stateWhileMissing = CurrentState();
        var warnsWhileMissing = _logger.Messages(Level.Warn);

        // The folder comes back without the edit: applied as usual.
        _rootExists = true;
        _onDisk = Unchanged();
        _watcherCallback();

        Assert.Multiple(() =>
        {
            Assert.That(stateWhileMissing, Is.EqualTo(started), "Read stays on: the current state is kept, not the defaults");
            Assert.That(warnsWhileMissing, Has.Length.EqualTo(2));
            Assert.That(warnsWhileMissing, Has.All.Contains("NR instrumentation XML folder " + Root + " was not found"));
            Assert.That(Count(DistroXmlToggleDiagnostics.FolderMissingMetricName), Is.EqualTo(2));
            Assert.That(_readCount, Is.EqualTo(2), "start-up and the run after the folder came back");
            Assert.That(CurrentState(), Is.EqualTo(DefaultState()));
            Assert.That(_registerCalls, Is.Empty);
        });
    }

    [Test]
    public void OnChange_DisposedBeforeRegistering_SkipsTheNativeCall()
    {
        var toggles = Create();
        toggles.ApplyAtStartup();
        Mock.Arrange(() => _files.List()).Returns(() =>
        {
            // Disposed after the run started but before it registers.
            toggles.Dispose();
            _onDisk = new[] { Disk(FileA, TextAReadOn), Disk(FileB, TextB) };
            return Listing(_onDisk);
        });

        toggles.OnChange();

        Assert.Multiple(() =>
        {
            Assert.That(_registerCalls, Is.Empty);
            Assert.That(CurrentState(), Is.EqualTo(DefaultState()));
            Assert.That(_logger.Messages(Level.Info).Count(m => m.Contains("applied")), Is.EqualTo(0));
        });
    }

    [Test]
    public void MarkUnregistered_FailedStartupRegistration_RegisteredOnTheNextChange()
    {
        var toggles = Create();
        var registerNow = toggles.ApplyAtStartup();
        Assert.That(SitesOf("Run").All(i => registerNow[i]), Is.True);

        // The plugin's start-up registration of Run failed.
        toggles.MarkUnregistered(SitesOf("Run"));
        _watcherCallback();

        Assert.Multiple(() =>
        {
            Assert.That(_registerCalls, Has.Count.EqualTo(1));
            Assert.That(_registerCalls[0], Is.EqualTo(SitesOf("Run")));
            Assert.That(IsOn("Run"), Is.True);
        });
    }

    [Test]
    public void MarkUnregistered_RegistrationStillFails_SitesTurnOff()
    {
        var toggles = Create();
        toggles.ApplyAtStartup();
        toggles.MarkUnregistered(SitesOf("Go"));
        _registerResult = false;

        _watcherCallback();

        Assert.Multiple(() =>
        {
            Assert.That(IsOff("Go"), Is.True, "nothing native is behind them, so the gate says off");
            Assert.That(Count(DistroXmlToggleDiagnostics.RegisterUnavailableMetricName), Is.EqualTo(1));
        });
    }

    [Test]
    public void MarkUnregistered_NullOutOfRangeAndBeforeStartup_Ignored()
    {
        var toggles = Create();

        Assert.DoesNotThrow(() =>
        {
            toggles.MarkUnregistered(SitesOf("Run"));
            toggles.ApplyAtStartup();
            toggles.MarkUnregistered(null);
            toggles.MarkUnregistered(new[] { -1, _shipped.Sites.Count });
        });
        _watcherCallback();

        Assert.That(_registerCalls, Is.Empty, "the call before start-up was ignored; start-up marked everything on as registered");
    }

    [Test]
    public void OnChange_UnreadableFileAfterAnEdit_KeepsTheEditedStateUntilReadable()
    {
        // Start from an edited state: Read is on. A later change finds A.xml locked; its edit must not revert to the
        // shipped defaults (Read off) for a moment.
        _onDisk = new[] { Disk(FileA, TextAReadOn), Disk(FileB, TextB) };
        Create().ApplyAtStartup();
        var started = CurrentState();

        _onDisk = new[] { Unreadable(FileA, "Access denied."), Disk(FileB, TextB) };
        _watcherCallback();
        var whileLocked = CurrentState();

        _onDisk = new[] { Disk(FileA, TextAReadOn), Disk(FileB, TextB) };
        _watcherCallback();

        Assert.Multiple(() =>
        {
            Assert.That(whileLocked, Is.EqualTo(started));
            Assert.That(_logger.Messages(Level.Warn), Has.Length.EqualTo(1));
            Assert.That(_logger.Messages(Level.Warn)[0], Does.Contain(FileA).And.Contain("Access denied.").And.Contain("instrumentation was not refreshed"));
            Assert.That(Count(DistroXmlToggleDiagnostics.UnreadableMetricName), Is.EqualTo(1));
            Assert.That(CurrentState(), Is.EqualTo(started));
        });
    }

    // ---- Dispose / construction

    [Test]
    public void Dispose_Twice_DisposesWatcherOnce()
    {
        var toggles = Create();

        toggles.Dispose();
        toggles.Dispose();

        Mock.Assert(() => _watcher.Dispose(), Occurs.Once());
    }

    [Test]
    public void Dispose_WatcherThrows_DoesNotThrowLogsError()
    {
        Mock.Arrange(() => _watcher.Dispose()).Throws(new InvalidOperationException("stuck"));
        var toggles = Create();

        Assert.DoesNotThrow(() => toggles.Dispose());

        Assert.Multiple(() =>
        {
            Assert.That(toggles.IsDisposed, Is.True);
            Assert.That(_logger.Messages(Level.Error), Has.Length.EqualTo(1));
        });
        Mock.Arrange(() => _watcher.Dispose()).DoNothing();
    }

    [TestCase(0, "sites")]
    [TestCase(1, "manifest")]
    [TestCase(2, "files")]
    [TestCase(3, "watcher")]
    [TestCase(4, "registerSites")]
    [TestCase(5, "onApplied")]
    public void Constructor_NullArgument_Throws(int nullArgument, string name)
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new DistroXmlToggles(
            nullArgument == 0 ? null : _shipped.Sites,
            nullArgument == 1 ? null : _shipped.Manifest,
            nullArgument == 2 ? null : _files,
            nullArgument == 3 ? null : _watcher,
            false,
            nullArgument == 4 ? null : _registerSites,
            nullArgument == 5 ? null : _onApplied));

        Assert.That(exception.ParamName, Is.EqualTo(name));
    }
}

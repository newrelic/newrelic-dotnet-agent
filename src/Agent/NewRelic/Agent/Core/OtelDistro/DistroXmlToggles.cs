// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using NewRelic.Agent.Extensions.Logging;

namespace NewRelic.Agent.Core.OtelDistro;

/// <summary>
/// Registers the given site indices with OTel's call-target registration. Returns false, with the reason in
/// <paramref name="failure"/>, when that is not possible. Blocks on native ReJIT.
/// </summary>
public delegate bool DistroRegisterSites(IReadOnlyList<int> sites, out string failure);

/// <summary>Where the start-up XML step spent its time (<see cref="DistroXmlToggles.ApplyAtStartup"/>), in milliseconds.</summary>
public sealed class DistroXmlStartupTimings
{
    /// <summary>Listing the extensions directories (no file opened).</summary>
    public double ListMs { get; internal set; }

    /// <summary>Reading the compiled and unknown files' bytes.</summary>
    public double ReadMs { get; internal set; }

    /// <summary>Hashing, and parsing what changed: <see cref="DistroXmlEffectiveSet.Compute"/>.</summary>
    public double ComputeMs { get; internal set; }

    /// <summary>Setting the gate and logging the outcome.</summary>
    public double ReportMs { get; internal set; }

    /// <summary>Handing the watcher start to its thread (the watcher itself is armed off the start-up path).</summary>
    public double WatcherMs { get; internal set; }

    internal static double Since(long start) => (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
}

/// <summary>
/// Applies the NR instrumentation XML on disk to the distro's compiled instrumentation sites, at start-up and whenever the
/// XML changes (see <see cref="DistroXmlEffectiveSet"/>). The per-site gate (<see cref="DistroInstrumentationSites.SetOn"/>)
/// turns sites off and back on; a site that was never registered with OTel's call-target registration is registered
/// through <c>registerSites</c> the first time it turns on. This class is the only writer of the gate: start-up runs
/// before the watcher starts, and change runs never overlap.
///
/// Start-up follows agent mode's start-up rules: a file that fails to parse is skipped alone, an unreadable shipped file
/// keeps its defaults. A live change follows agent mode's refresh rule: a file that fails to parse, or cannot be read,
/// keeps the whole current state. Hash-only shipped files are never read.
///
/// Nothing here throws into OTel start-up, the app, or the watcher's thread: failures keep the current state and log once.
/// </summary>
public sealed class DistroXmlToggles : IDisposable
{
    private const string WatcherDisabledMessage = "Live instrumentation updates due to instrumentation file changes will not be applied because they have been disabled by local configuration.";

    private readonly DistroInstrumentationSites _sites;
    private readonly DistroShippedXmlManifest _manifest;
    private readonly IDistroXmlFileSource _files;
    private readonly IDistroXmlChangeWatcher _watcher;
    private readonly bool _watcherDisabled;
    private readonly DistroRegisterSites _registerSites;
    private readonly Action _onApplied;

    // Unsupported points already warned about (per process, by key, so copies of a file warn once) and run-time
    // registration failures already logged as errors. Touched only by start-up and the single OnChange runner.
    private readonly HashSet<ulong> _warnedUnsupported = new HashSet<ulong>();
    private readonly HashSet<string> _reportedRegisterFailures = new HashSet<string>(StringComparer.Ordinal);

    // registered[i]: site i has been handed to OTel's registration (by the plugin at start-up, or by _registerSites).
    // Set by ApplyAtStartup before the watcher starts; afterwards written true only by the single OnChange runner and
    // false only by MarkUnregistered (the plugin's start-up feedback).
    private bool[] _registered;

    // The listing start-up computed from (for the watcher's catch-up), and the content hash per path of the last applied
    // run (to name the files a change touched). Written by start-up, then only by the single OnChange runner.
    private IReadOnlyList<DistroXmlFileEntry> _startupListing;
    private Dictionary<string, DistroXmlFileHash> _appliedHashes = new Dictionary<string, DistroXmlFileHash>(StringComparer.OrdinalIgnoreCase);

    private int _startCalled;
    private volatile bool _ready;
    private int _disposed;

    // OnChange requests not yet handled; the caller that moves it from 0 to 1 runs them, every other caller just adds one.
    private int _pendingChanges;

    public bool IsDisposed => Volatile.Read(ref _disposed) != 0;

    /// <summary>Where <see cref="ApplyAtStartup"/> spent its time; null before it ran.</summary>
    public DistroXmlStartupTimings StartupTimings { get; private set; }

    // registerSites registers the given site indices with OTel's call-target registration and returns false (with a reason)
    // when that is not possible. It blocks on native ReJIT, so it is only ever called from OnChange, never under a lock.
    // onApplied runs after a change turned sites off or on (the agent clears its wrapper caches, as agent mode does).
    public DistroXmlToggles(DistroInstrumentationSites sites, DistroShippedXmlManifest manifest, IDistroXmlFileSource files, IDistroXmlChangeWatcher watcher, bool watcherDisabled, DistroRegisterSites registerSites, Action onApplied)
    {
        _sites = sites ?? throw new ArgumentNullException(nameof(sites));
        _manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
        _files = files ?? throw new ArgumentNullException(nameof(files));
        _watcher = watcher ?? throw new ArgumentNullException(nameof(watcher));
        _watcherDisabled = watcherDisabled;
        _registerSites = registerSites ?? throw new ArgumentNullException(nameof(registerSites));
        _onApplied = onApplied ?? throw new ArgumentNullException(nameof(onApplied));
    }

    /// <summary>
    /// Applies the XML on disk to the gate and returns the sites to register now (on[i] for site i, the gate's new state);
    /// the caller registers them itself and they count as registered from here on. A missing folder or any failure keeps
    /// the compiled defaults; a file that fails to parse is skipped alone. Then starts the watcher, unless disabled by
    /// configuration or already disposed. Call once: a later call changes nothing and returns the gate's current state
    /// (registering the same set again under the same content id is ignored by OTel). Never throws.
    /// </summary>
    public bool[] ApplyAtStartup()
    {
        if (Interlocked.Exchange(ref _startCalled, 1) != 0)
        {
            return CurrentOn();
        }

        var timings = new DistroXmlStartupTimings();
        try
        {
            var result = ComputeAccepted(startup: true, timings);
            if (result != null)
            {
                var reportStart = Stopwatch.GetTimestamp();
                var before = CurrentOn();
                _sites.SetOn(result.On);
                foreach (var invalid in result.InvalidFiles)
                {
                    DistroXmlToggleDiagnostics.ReportSkippedAtStartup(invalid.HomeName, invalid.Reason);
                }

                DistroXmlToggleDiagnostics.ReportApplied(_sites, before, result.On, result, StartupChangedFiles(result), NewUnsupported(result));
                _appliedHashes = HashesByPath(result);
                timings.ReportMs = DistroXmlStartupTimings.Since(reportStart);
            }
        }
        catch (Exception ex)
        {
            TryLogError("NR instrumentation XML could not be applied at start-up ({0}); using the distro's compiled defaults.", ex);
        }

        var registerNow = CurrentOn();
        Volatile.Write(ref _registered, (bool[])registerNow.Clone());
        _ready = true;

        var watcherStart = Stopwatch.GetTimestamp();
        StartWatcher();
        timings.WatcherMs = DistroXmlStartupTimings.Since(watcherStart);
        StartupTimings = timings;
        return registerNow;
    }

    /// <summary>
    /// Start-up registration feedback: the caller could not register these sites (from <see cref="ApplyAtStartup"/>'s
    /// result), so they count as unregistered again and go through <c>registerSites</c> the next time a change finds them
    /// on. Ignored before <see cref="ApplyAtStartup"/>, for null, and for indices out of range. Never throws.
    /// </summary>
    public void MarkUnregistered(IReadOnlyList<int> sites)
    {
        var registered = Volatile.Read(ref _registered);
        if (registered == null || sites == null)
        {
            return;
        }

        foreach (var index in sites)
        {
            if ((uint)index < (uint)registered.Length)
            {
                Volatile.Write(ref registered[index], false);
            }
        }
    }

    /// <summary>
    /// Re-applies the XML on disk. Sites turning on that were never registered are registered first, in one call; when
    /// that fails they stay off (reported). A file that fails to parse or cannot be read, or any failure, keeps the
    /// current state. A call while another is running returns at once, and the running call then runs once more for all
    /// such calls. A no-op before <see cref="ApplyAtStartup"/> and after <see cref="Dispose"/>. Never throws.
    /// </summary>
    public void OnChange()
    {
        if (!_ready || IsDisposed)
        {
            return;
        }

        if (Interlocked.Increment(ref _pendingChanges) != 1)
        {
            return;
        }

        int handled;
        do
        {
            handled = Volatile.Read(ref _pendingChanges);
            ApplyChange();
        }
        while (Interlocked.Add(ref _pendingChanges, -handled) != 0);
    }

    /// <summary>
    /// The watcher's catch-up check: true when the files listed now differ (added, removed, or a different last write time
    /// or length) from the listing start-up computed from, so an edit made before the watcher was armed is applied. True
    /// when the listing fails (the change run then reports it). Never throws.
    /// </summary>
    public bool ChangedSinceStartup()
    {
        try
        {
            var before = _startupListing;
            var now = _files.RootExists ? _files.List() : new DistroXmlFileEntry[0];
            if (before == null || before.Count != now.Count)
            {
                return true;
            }

            for (var i = 0; i < now.Count; i++)
            {
                if (!now[i].SameAs(before[i]))
                {
                    return true;
                }
            }

            return false;
        }
        catch (Exception)
        {
            return true;
        }
    }

    /// <summary>Stops the watcher; later <see cref="OnChange"/> calls do nothing. Idempotent. Never throws.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        try
        {
            _watcher.Dispose();
        }
        catch (Exception ex)
        {
            TryLogError("NR instrumentation XML watcher could not be stopped ({0}).", ex);
        }
    }

    private void ApplyChange()
    {
        try
        {
            if (IsDisposed)
            {
                return;
            }

            var result = ComputeAccepted(startup: false, null);
            if (result == null)
            {
                return;
            }

            var on = (bool[])result.On.Clone();
            var toRegister = new List<int>();
            for (var i = 0; i < on.Length; i++)
            {
                if (on[i] && !_registered[i])
                {
                    toRegister.Add(i);
                }
            }

            if (toRegister.Count > 0)
            {
                // Disposed while this run was reading: do not start native work for an agent that is going away.
                if (IsDisposed)
                {
                    return;
                }

                // Not under any lock: this blocks on native ReJIT.
                var points = PointCount(toRegister);
                if (_registerSites(toRegister.ToArray(), out var failure))
                {
                    foreach (var index in toRegister)
                    {
                        _registered[index] = true;
                    }

                    DistroXmlToggleDiagnostics.ReportRegisteredAtRunTime(points);
                }
                else
                {
                    failure = failure ?? "no reason given";
                    DistroXmlToggleDiagnostics.ReportRegisterUnavailable(failure, points, !_reportedRegisterFailures.Add(failure));
                    foreach (var index in toRegister)
                    {
                        on[index] = false;
                    }
                }
            }

            var before = CurrentOn();
            _sites.SetOn(on);
            DistroXmlToggleDiagnostics.ReportApplied(_sites, before, on, result, ChangedFilesSinceApplied(result), NewUnsupported(result));
            _appliedHashes = HashesByPath(result);
            if (!SameState(before, on))
            {
                TryRun(_onApplied, "NR instrumentation XML was applied, but the wrapper caches could not be cleared ({0}).");
            }
        }
        catch (Exception ex)
        {
            TryLogError("NR instrumentation XML change could not be applied ({0}); instrumentation was not refreshed.", ex);
        }
    }

    // Lists, reads (never hash-only files) and computes. A missing folder, a rejected result (live) or an unreadable file
    // (live) is reported and gives null. At start-up invalid files are skipped and each unreadable file is reported. Compute
    // can throw on inconsistent input; callers catch.
    private DistroXmlEffectiveSetResult ComputeAccepted(bool startup, DistroXmlStartupTimings timings)
    {
        // No extensions folder (none configured, or it does not exist, or it was removed): keep the current state, which is
        // the compiled defaults at start-up. Only an existing folder's missing shipped files count as deleted.
        if (!_files.RootExists)
        {
            if (startup)
            {
                _startupListing = new DistroXmlFileEntry[0];
            }

            DistroXmlToggleDiagnostics.ReportFolderMissing(_files.RootDirectory);
            return null;
        }

        var start = Stopwatch.GetTimestamp();
        var listing = _files.List();
        if (startup)
        {
            _startupListing = listing;
            timings.ListMs = DistroXmlStartupTimings.Since(start);
            start = Stopwatch.GetTimestamp();
        }

        var files = ReadNeeded(listing);
        if (startup)
        {
            timings.ReadMs = DistroXmlStartupTimings.Since(start);
            start = Stopwatch.GetTimestamp();
        }
        else if (ReportUnreadableNotRefreshed(files))
        {
            return null;
        }

        var result = DistroXmlEffectiveSet.Compute(_sites, _manifest, files, skipInvalid: startup);
        if (startup)
        {
            timings.ComputeMs = DistroXmlStartupTimings.Since(start);
        }

        if (result.Rejected)
        {
            DistroXmlToggleDiagnostics.ReportRejected(result);
            return null;
        }

        foreach (var file in files)
        {
            if (file.ReadError != null)
            {
                DistroXmlToggleDiagnostics.ReportUnreadable(file.HomeName, file.ReadError);
            }
        }

        return result;
    }

    // Reads every listed file except hash-only shipped files, which nothing looks at.
    private List<DistroXmlFile> ReadNeeded(IReadOnlyList<DistroXmlFileEntry> listing)
    {
        var files = new List<DistroXmlFile>(listing.Count);
        foreach (var entry in listing)
        {
            var hashOnly = _manifest.TryFindFile(entry.HomeName, out var index) && !_manifest.IsCompiled(index);
            files.Add(hashOnly ? DistroXmlFile.NotRead(entry.HomeName, entry.Path) : _files.Read(entry));
        }

        return files;
    }

    // Live change: an unreadable file would drop its contribution (its edits revert to the defaults until the next change),
    // so the whole refresh waits for a readable state instead.
    private static bool ReportUnreadableNotRefreshed(List<DistroXmlFile> files)
    {
        var any = false;
        foreach (var file in files)
        {
            if (file.ReadError != null)
            {
                DistroXmlToggleDiagnostics.ReportUnreadableNotRefreshed(file.HomeName, file.ReadError);
                any = true;
            }
        }

        return any;
    }

    // Start-up: every file that differs from what shipped.
    private static List<string> StartupChangedFiles(DistroXmlEffectiveSetResult result)
    {
        var changed = new List<string>(result.ParsedFiles);
        foreach (var invalid in result.InvalidFiles)
        {
            changed.Add(invalid.HomeName + " (invalid)");
        }

        foreach (var name in result.UnreadableFiles)
        {
            changed.Add(name + " (unreadable)");
        }

        foreach (var name in result.MissingFiles)
        {
            changed.Add(name + " (deleted)");
        }

        return changed;
    }

    // Live change: every read file whose content differs from the last applied run (new, edited, restored), then every
    // file that was there and is gone.
    private List<string> ChangedFilesSinceApplied(DistroXmlEffectiveSetResult result)
    {
        var changed = new List<string>();
        var now = HashesByPath(result);
        foreach (var file in result.FileHashes)
        {
            if (!_appliedHashes.TryGetValue(file.Path, out var previous) || previous.Hash != file.Hash)
            {
                changed.Add(file.HomeName);
            }
        }

        foreach (var previous in _appliedHashes.Values)
        {
            if (!now.ContainsKey(previous.Path))
            {
                changed.Add(previous.HomeName + " (deleted)");
            }
        }

        return changed;
    }

    private static Dictionary<string, DistroXmlFileHash> HashesByPath(DistroXmlEffectiveSetResult result)
    {
        var hashes = new Dictionary<string, DistroXmlFileHash>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in result.FileHashes)
        {
            hashes[file.Path] = file;
        }

        return hashes;
    }

    // The unsupported points not warned about yet in this process.
    private List<DistroXmlUnsupportedPoint> NewUnsupported(DistroXmlEffectiveSetResult result)
    {
        var fresh = new List<DistroXmlUnsupportedPoint>();
        foreach (var point in result.Unsupported)
        {
            if (_warnedUnsupported.Add(point.PointKey))
            {
                fresh.Add(point);
            }
        }

        return fresh;
    }

    private int PointCount(List<int> siteIndices)
    {
        var keys = new HashSet<ulong>();
        foreach (var index in siteIndices)
        {
            keys.Add(_sites.PointKey(index));
        }

        return keys.Count;
    }

    private static bool SameState(bool[] left, bool[] right)
    {
        for (var i = 0; i < left.Length; i++)
        {
            if (left[i] != right[i])
            {
                return false;
            }
        }

        return true;
    }

    private void StartWatcher()
    {
        if (_watcherDisabled)
        {
            TryLog(() => Log.Debug(WatcherDisabledMessage));
            return;
        }

        if (IsDisposed)
        {
            return;
        }

        try
        {
            _watcher.Start(OnChange, ChangedSinceStartup);
        }
        catch (Exception ex)
        {
            TryLogError("NR instrumentation XML changes will not be applied live: the watcher could not be started ({0}).", ex);
        }
    }

    private bool[] CurrentOn()
    {
        var on = new bool[_sites.Count];
        for (var i = 0; i < on.Length; i++)
        {
            on[i] = _sites.IsOn(i);
        }

        return on;
    }

    private static void TryRun(Action action, string failureMessage)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            TryLogError(failureMessage, ex);
        }
    }

    private static void TryLogError(string message, Exception ex) =>
        TryLog(() => Log.Error(message, ex.GetType().Name + ": " + ex.Message));

    private static void TryLog(Action log)
    {
        try
        {
            log();
        }
        catch (Exception)
        {
            // Logging must never reach OTel start-up, the app, or the watcher's thread.
        }
    }
}

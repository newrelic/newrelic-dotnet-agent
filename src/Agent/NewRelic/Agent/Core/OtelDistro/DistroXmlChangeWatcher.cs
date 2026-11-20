// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Extensions.Logging;

namespace NewRelic.Agent.Core.OtelDistro;

/// <summary>
/// Distro mode's counterpart of agent mode's InstrumentationWatcher: a FileSystemWatcher on <c>*.xml</c> (last write and
/// file name changes) in each given directory that exists when the watcher is armed, coalesced through a
/// <see cref="SignalableAction"/> so a burst of changes within the delay runs the callback once, on the action's thread.
/// Arming (the action's thread, the FileSystemWatchers) runs on the thread pool, off the start-up path; once armed, the
/// start-up listing is checked once (catch-up) so an edit made before arming still runs the callback. Logs each change
/// with agent mode's text. Nothing it does on a file-system, pool or worker thread can throw.
/// </summary>
public sealed class DistroXmlChangeWatcher : IDistroXmlChangeWatcher
{
    /// <summary>Agent mode's InstrumentationWatcher delay.</summary>
    public const int ProductionDelayMilliseconds = 15000;

    private readonly IReadOnlyList<string> _directories;
    private readonly int _delayMilliseconds;
    private readonly Action<Action> _schedule;
    private readonly object _gate = new object();
    private readonly List<FileSystemWatcher> _watchers = new List<FileSystemWatcher>();

    private SignalableAction _action;
    private Action _onChange;
    private bool _started;
    private volatile bool _disposed;

    public DistroXmlChangeWatcher(IReadOnlyList<string> directories, int delayMilliseconds)
        : this(directories, delayMilliseconds, work => ThreadPool.QueueUserWorkItem(_ => work()))
    {
    }

    /// <param name="directories">The directories to watch.</param>
    /// <param name="delayMilliseconds">How long changes settle before the callback runs.</param>
    /// <param name="schedule">Runs the arming work; the production constructor queues it to the thread pool.</param>
    public DistroXmlChangeWatcher(IReadOnlyList<string> directories, int delayMilliseconds, Action<Action> schedule)
    {
        _directories = directories ?? throw new ArgumentNullException(nameof(directories));
        _delayMilliseconds = delayMilliseconds;
        _schedule = schedule ?? throw new ArgumentNullException(nameof(schedule));
    }

    public void Start(Action onChange, Func<bool> changedSinceListing)
    {
        if (onChange == null)
        {
            throw new ArgumentNullException(nameof(onChange));
        }

        if (changedSinceListing == null)
        {
            throw new ArgumentNullException(nameof(changedSinceListing));
        }

        lock (_gate)
        {
            if (_disposed || _started)
            {
                return;
            }

            _started = true;
            _onChange = onChange;
        }

        _schedule(() => Arm(changedSinceListing));
    }

    // Pool thread: builds the action's thread and the FileSystemWatchers, then the catch-up check. Never throws.
    private void Arm(Func<bool> changedSinceListing)
    {
        try
        {
            SignalableAction action;
            lock (_gate)
            {
                if (_disposed)
                {
                    return;
                }

                action = new SignalableAction(RunOnChange, _delayMilliseconds);
                _action = action;
                action.Start();

                foreach (var directory in _directories)
                {
                    if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
                    {
                        _watchers.Add(CreateWatcher(directory));
                    }
                }
            }

            // Armed: from here every change is seen. One that happened since the caller's listing is caught up now; a
            // failing check counts as a change (a needless re-run is cheap, a lost edit is not).
            bool changed;
            try
            {
                changed = changedSinceListing();
            }
            catch (Exception)
            {
                changed = true;
            }

            if (changed)
            {
                action.Signal();
            }
        }
        catch (Exception ex)
        {
            try
            {
                Log.Error("NR instrumentation XML changes will not be applied live: the watcher could not be started ({0}).", ex.GetType().Name + ": " + ex.Message);
            }
            catch (Exception)
            {
            }
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            foreach (var watcher in _watchers)
            {
                watcher.Dispose();
            }

            _watchers.Clear();

            // SignalableAction.Dispose only requests cancellation; its worker waits for a signal before it looks. One last
            // signal lets it wake, run RunOnChange (a no-op now) and exit instead of waiting forever.
            _action?.Dispose();
            _action?.Signal();
        }
    }

    // The only member that builds FileSystemWatchers (excluded from coverage; the handlers it attaches are covered).
    [NrExcludeFromCodeCoverage]
    private FileSystemWatcher CreateWatcher(string directory)
    {
        var watcher = new FileSystemWatcher(directory, "*.xml");
        watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName;
        watcher.Changed += OnChanged;
        watcher.Created += OnChanged;
        watcher.Deleted += OnChanged;
        watcher.Renamed += OnRenamed;
        watcher.EnableRaisingEvents = true;
        return watcher;
    }

    // File-system threads: signal first, so a failing logger cannot drop the change, and never throw.
    private void OnChanged(object sender, FileSystemEventArgs e)
    {
        _action.Signal();
        try
        {
            Log.Info("Instrumentation change detected: {0} - {1}", e.ChangeType, e.FullPath);
        }
        catch (Exception)
        {
        }
    }

    private void OnRenamed(object sender, RenamedEventArgs e)
    {
        _action.Signal();
        try
        {
            Log.Info("Instrumentation change detected: {0} - {1} -> {2}", e.ChangeType, e.OldFullPath, e.FullPath);
        }
        catch (Exception)
        {
        }
    }

    // The SignalableAction worker thread: an exception here would end the process, so none escapes.
    private void RunOnChange()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            _onChange();
        }
        catch (Exception ex)
        {
            try
            {
                Log.Error("NR instrumentation XML change handling failed: {0}", ex.GetType().Name + ": " + ex.Message);
            }
            catch (Exception)
            {
            }
        }
    }
}

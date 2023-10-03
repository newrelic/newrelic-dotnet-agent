// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Core.Wrapper;
using NewRelic.Core.Logging;
using System;
using System.Collections.Generic;
using System.IO;

namespace NewRelic.Agent.Core.Instrumentation
{
    public class InstrumentationWatcher : IDisposable
    {
        private const int RequestRejitDelayMilliseconds = 15000;

        private readonly IInstrumentationService _instrumentationService;
        private readonly IWrapperService _wrapperService;

        private List<FileSystemWatcher> _fileWatchers;
        private SignalableAction _action;

        public InstrumentationWatcher(IWrapperService wrapperService, IInstrumentationService instrumentationService)
        {
            _wrapperService = wrapperService;
            _instrumentationService = instrumentationService;
        }

        public void Start()
        {
            if (AgentInstallConfiguration.HomeExtensionsDirectory == null)
            {
                Log.Warn("Live instrumentation updates due to instrumentation file changes will not be applied because HomeExtensionsDirectory is null.");
                return;
            }

            _action = new SignalableAction(RequestRejit, RequestRejitDelayMilliseconds);
            _action.Start();

            SetupFileWatcherForDirectory(AgentInstallConfiguration.HomeExtensionsDirectory);
            SetupFileWatcherForDirectory(AgentInstallConfiguration.RuntimeHomeExtensionsDirectory);
        }

        private void SetupFileWatcherForDirectory(string path)
        {
            if (!Directory.Exists(path)) return;
            var watcher = new FileSystemWatcher(path, "*.xml");
            watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName;
            watcher.Changed += OnChanged;
            watcher.Created += OnChanged;
            watcher.Deleted += OnChanged;
            watcher.Renamed += OnRenamed;
            watcher.EnableRaisingEvents = true;
            if (_fileWatchers == null) _fileWatchers = new List<FileSystemWatcher>();
            _fileWatchers.Add(watcher);
        }

        private void RequestRejit()
        {
            Log.Info("Starting instrumentation refresh from InstrumentationWatcher");
            var result = _instrumentationService.InstrumentationRefresh();
            _wrapperService.ClearCaches();
            Log.Info("Completed instrumentation refresh from InstrumentationWatcher: {0}", result);
        }

        private void OnChanged(object sender, FileSystemEventArgs e)
        {
            Log.Info("Instrumentation change detected: {0} - {1}", e.ChangeType, e.FullPath);
            _action.Signal();
        }

        private void OnRenamed(object sender, RenamedEventArgs e)
        {
            Log.Info("Instrumentation change detected: {0} - {1} -> {2}", e.ChangeType, e.OldFullPath, e.FullPath);
            _action.Signal();
        }

        public void Dispose()
        {
            _action?.Dispose();
            if (_fileWatchers != null)
            {
                foreach (var watcher in _fileWatchers) watcher?.Dispose();
            }
        }
    }
}

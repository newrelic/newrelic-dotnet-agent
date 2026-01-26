// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Threading;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Extensions.Logging;

namespace NewRelic.Agent.Core.Config;

/// <summary>
/// The ConfigurationTracker starts a timer that runs every minute and checks the write timestamp of the
/// newrelic.config file.  If it is newer than the last time it was checked, the file is read into a new
/// AgentConfig and listeners are notified of the change.
/// </summary>
public class ConfigurationTracker : IDisposable
{
    private readonly Timer _timer;

    private DateTime _lastWriteTime;

    private readonly INativeMethods _nativeMethods;
    private readonly IFileWrapper _fileWrapper;

    public ConfigurationTracker(IConfigurationService configurationService, INativeMethods nativeMethods, IFileWrapper fileWrapper)
    {
        if (configurationService.Configuration.DisableFileSystemWatcher)
        {
            Log.Debug("Live updates to newrelic.config will not be applied because they have been disabled by local configuration.");
            return;
        }

        _nativeMethods = nativeMethods;
        _fileWrapper = fileWrapper;
        var fileName = configurationService.Configuration.NewRelicConfigFilePath;
        if (fileName == null)
            return;

        Log.Info("Reading configuration from \"{0}\"", fileName);

        _lastWriteTime = _fileWrapper.GetLastWriteTimeUtc(fileName);

        _timer = Scheduler.CreateExecuteEveryTimer(() =>
        {
            var lastWriteTime = _fileWrapper.GetLastWriteTimeUtc(fileName);
            if (lastWriteTime > _lastWriteTime)
            {
                Log.Debug("newrelic.config file changed, reloading.");
                ConfigurationLoader.Initialize(fileName);
                _lastWriteTime = lastWriteTime;
                _nativeMethods.ReloadConfiguration();
            }
        }, TimeSpan.FromMinutes(1));
    }

    public void Dispose()
    {
        if (_timer == null)
            return;

        _timer.Dispose();
    }
}

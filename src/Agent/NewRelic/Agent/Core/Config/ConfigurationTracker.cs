// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Time;
using NewRelic.Core.Logging;
using System;
using System.IO;
using System.Threading;

namespace NewRelic.Agent.Core.Config
{
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

        public ConfigurationTracker(IConfigurationService configurationService, INativeMethods nativeMethods)
        {
            _nativeMethods = nativeMethods;
            var fileName = configurationService.Configuration.NewRelicConfigFilePath;
            if (fileName == null)
                return;

            Log.Info("Reading configuration from \"{0}\"", fileName);

            _lastWriteTime = File.GetLastWriteTimeUtc(fileName);

            _timer = Scheduler.CreateExecuteEveryTimer(() =>
            {
                var lastWriteTime = File.GetLastWriteTimeUtc(fileName);
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
}

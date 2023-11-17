// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Diagnostics;

namespace NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures
{
    /// <summary>
    /// A remote fixture intended to be run against a console application
    /// that runs from begin to end without signaling test framework of it's start
    /// </summary>
    public class RemoteConsoleApplication : RemoteService
    {
        private TimeSpan _timeout = TimeSpan.FromSeconds(60);
        private bool _processHasBeenShutdown;

        protected override bool UsesSpecificPort => false;

        public RemoteConsoleApplication SetTimeout(TimeSpan timeSpan)
        {
            _timeout = timeSpan;
            return this;
        }


        public RemoteConsoleApplication(string applicationDirectoryName, string executableName, ApplicationType applicationType, bool isCoreApp = false, bool publishApp = false)
            : base(applicationDirectoryName, executableName, applicationType, false, isCoreApp, publishApp)
        {
        }

        public RemoteConsoleApplication(string applicationDirectoryName, string executableName, string targetFramework, ApplicationType applicationType, bool isCoreApp = false, bool publishApp = false)
            : base(applicationDirectoryName, executableName, targetFramework, applicationType, false, isCoreApp, publishApp)
        {
        }

        protected override void WaitForAppServerToStartListening(Process process, bool captureStandardOutput)
        {
            // The logic was removed here to simplify console app execution.
            // We may want to double-check that the process actually started before we try to 
            // perform operations against it (like writing to standard input).
            // This may cause us trouble in the future.
        }

        public override void Shutdown()
        {
            if (RemoteProcess is null)
            {
                // We might not have ever started the application, e.g. if the test is being skipped.
                return;
            }
            
            if (_processHasBeenShutdown)
            {
                RemoteProcess.WaitForExit();
                return;
            }

            _processHasBeenShutdown = true;

            try
            {
                if (!IsRunning || RemoteProcess.WaitForExit(Convert.ToInt32(_timeout.TotalMilliseconds)))
                {
                    // Allows for any asynchronous writing to complete
                    RemoteProcess.WaitForExit();
                    return;
                }

                if (IsRunning)
                {
                    RemoteProcess.Kill();
                }

                RemoteProcess.WaitForExit();
            }
            catch (Exception ex)
            {
                throw new Exception($"Remote Process \"{_executableName}\" did not complete within the expected duration of {_timeout.TotalSeconds} second(s)", ex);
            }
        }
    }
}

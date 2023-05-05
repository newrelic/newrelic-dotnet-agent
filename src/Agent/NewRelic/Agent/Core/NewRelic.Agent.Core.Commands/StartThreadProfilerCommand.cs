// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.ThreadProfiling;
using NewRelic.Core.Logging;
using System.Collections.Generic;

namespace NewRelic.Agent.Core.Commands
{
    public class StartThreadProfilerCommand : AbstractCommand
    {
        public IThreadProfilingSessionControl ThreadProfilingService { get; set; }

        public StartThreadProfilerCommand(IThreadProfilingSessionControl threadProfilingService)
        {
            Name = "start_profiler";
            ThreadProfilingService = threadProfilingService;
        }

        public override object Process(IDictionary<string, object> arguments)
        {
            var errorMessage = StartThreadProfilingSessions(arguments);
            if (errorMessage == null)
                return new Dictionary<string, object>();

            return new Dictionary<string, object>
            {
                {"error", errorMessage}
            };
        }

        private string StartThreadProfilingSessions(IDictionary<string, object> arguments)
        {
            if (arguments == null)
                return "No arguments sent with start_profiler command.";

            if (!AgentInstallConfiguration.IsWindows && !AgentInstallConfiguration.IsNetCore30OrAbove)
            {
                const string netCore30RequiredForLinuxThreadProfiling = "Cannot start a thread profiling session. Thread profiling support on Linux was introduced in .NET Core 3.0.";
                Log.Info(netCore30RequiredForLinuxThreadProfiling);
                return netCore30RequiredForLinuxThreadProfiling;
            }

            var startArgs = new ThreadProfilerCommandArgs(arguments, ThreadProfilingService.IgnoreMinMinimumSamplingDuration);
            if (startArgs.ProfileId == 0)
                return "A valid profile_id must be supplied to start a thread profiling session.";

            var startedNewSession = ThreadProfilingService.StartThreadProfilingSession(startArgs.ProfileId, startArgs.Frequency, startArgs.Duration);
            if (!startedNewSession)
                return "Cannot start a thread profiling session. Another session may already be in process.";

            return null;
        }
    }
}

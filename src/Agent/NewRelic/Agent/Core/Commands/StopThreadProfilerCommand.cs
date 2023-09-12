// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Core.ThreadProfiling;
using NewRelic.Core.Logging;
using System.Collections.Generic;

namespace NewRelic.Agent.Core.Commands
{
    public class StopThreadProfilerCommand : AbstractCommand
    {
        public IThreadProfilingSessionControl ThreadProfilingService { get; set; }

        public StopThreadProfilerCommand(IThreadProfilingSessionControl threadProfilingService)
        {
            Name = "stop_profiler";
            ThreadProfilingService = threadProfilingService;
        }

        public override object Process(IDictionary<string, object> arguments)
        {
            var errorMessage = StopThreadProfilingSessions(arguments);
            if (errorMessage == null)
                return new Dictionary<string, object>();

            return new Dictionary<string, object>
            {
                {"error", errorMessage}
            };
        }

        private string StopThreadProfilingSessions(IDictionary<string, object> arguments)
        {
            if (arguments == null)
                return "No arguments sent with stop_profiler command.";

            var stopArgs = new ThreadProfilerCommandArgs(arguments, ThreadProfilingService.IgnoreMinMinimumSamplingDuration);
            if (stopArgs.ProfileId == 0)
                return "A valid profile_id must be supplied to stop a thread profiling session.";

            try
            {
                var stoppedSession = ThreadProfilingService.StopThreadProfilingSession(stopArgs.ProfileId, stopArgs.ReportData);
                if (!stoppedSession)
                {
                    return "A thread profiling session is not running.";
                }
            }
            catch (InvalidProfileIdException e)
            {
                Log.Error(e, "StopThreadProfilingSessions() failed");
                return e.Message;
            }

            return null;
        }
    }
}

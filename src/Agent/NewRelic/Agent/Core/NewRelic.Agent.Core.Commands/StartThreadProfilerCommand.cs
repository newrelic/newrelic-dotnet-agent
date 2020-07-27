using System;
using System.Collections.Generic;
using NewRelic.Agent.Core.ThreadProfiling;

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

        public override Object Process(IDictionary<String, Object> arguments)
        {
            var errorMessage = StartThreadProfilingSessions(arguments);
            if (errorMessage == null)
                return new Dictionary<String, Object>();

            return new Dictionary<String, Object>
            {
                {"error", errorMessage}
            };
        }
        private String StartThreadProfilingSessions(IDictionary<String, Object> arguments)
        {
            if (arguments == null)
                return "No arguments sent with start_profiler command.";

            var startArgs = new ThreadProfilerCommandArgs(arguments);
            if (startArgs.ProfileId == 0)
                return "A valid profile_id must be supplied to start a thread profiling session.";

            var startedNewSession = ThreadProfilingService.StartThreadProfilingSession(startArgs.ProfileId, startArgs.Frequency, startArgs.Duration);
            if (!startedNewSession)
                return "Cannot start a profiler session. Another session may already be in process.";

            return null;
        }
    }
}

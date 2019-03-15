using NewRelic.Agent.Core.Logging;
using NewRelic.Agent.Core.ThreadProfiling;
using System;
using System.Collections.Generic;

namespace NewRelic.Agent.Core.Commands
{
	public class StartThreadProfilerCommand : AbstractCommand
	{
		private const string LinuxOperatingSystemFound = "Cannot start a profiler session. Thread profiling is currently only supported on Microsoft Windows.";
		private bool _isWindows;
		public IThreadProfilingSessionControl ThreadProfilingService { get; set; }

		public StartThreadProfilerCommand(IThreadProfilingSessionControl threadProfilingService, bool isWindows)
		{
			Name = "start_profiler";
			_isWindows = isWindows;
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

			if (!_isWindows)
			{
				Log.Finest(LinuxOperatingSystemFound);
				return LinuxOperatingSystemFound;
			}

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

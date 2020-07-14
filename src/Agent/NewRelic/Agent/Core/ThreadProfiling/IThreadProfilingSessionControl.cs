using System;

namespace NewRelic.Agent.Core.ThreadProfiling
{
	public interface IThreadProfilingSessionControl
	{
		Boolean StartThreadProfilingSession(Int32 profileSessionId, UInt32 frequencyInMsec, UInt32 durationInMsec);
		Boolean StopThreadProfilingSession(Int32 profileId, Boolean reportData = true);
	}
}

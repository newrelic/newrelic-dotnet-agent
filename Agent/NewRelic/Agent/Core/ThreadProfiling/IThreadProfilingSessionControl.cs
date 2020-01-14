namespace NewRelic.Agent.Core.ThreadProfiling
{
	public interface IThreadProfilingSessionControl
	{
		bool StartThreadProfilingSession(int profileSessionId, uint frequencyInMsec, uint durationInMsec);
		bool StopThreadProfilingSession(int profileId, bool reportData = true);
	}
}

namespace NewRelic.Agent.Core.ThreadProfiling
{
	public interface IThreadProfilingSampler
	{
		int NumberSamplesInSession { get; set; }

		bool Start(uint frequencyInMsec, uint durationInMsec);

		void Stop(bool reportData);
	}
}


using JetBrains.Annotations;

namespace NewRelic.Agent.Core.ThreadProfiling
{
	public interface IThreadProfilingSampler
	{
		bool Start(uint frequencyInMsec, uint durationInMsec, [NotNull]ISampleSink sampleSink, [NotNull] INativeMethods nativeMethods);

		void Stop();
	}
}


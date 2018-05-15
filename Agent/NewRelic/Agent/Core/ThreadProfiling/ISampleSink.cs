using JetBrains.Annotations;

namespace NewRelic.Agent.Core.ThreadProfiling
{
    public interface ISampleSink
    {
		void SampleAcquired([NotNull]ThreadSnapshot[] threadSnapshots);

		void SamplingComplete();
	}
}

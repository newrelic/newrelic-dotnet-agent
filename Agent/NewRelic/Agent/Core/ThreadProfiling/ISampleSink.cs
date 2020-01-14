namespace NewRelic.Agent.Core.ThreadProfiling
{
	public interface ISampleSink
	{
		void SampleAcquired(ThreadSnapshot[] threadSnapshots);
		void SamplingComplete();
	}
}

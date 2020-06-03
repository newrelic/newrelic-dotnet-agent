namespace NewRelic.Agent.Core.ThreadProfiling
{
    public interface IThreadProfilingSampler
    {
        bool Start(uint frequencyInMsec, uint durationInMsec, ISampleSink sampleSink, INativeMethods nativeMethods);
        void Stop();
    }
}

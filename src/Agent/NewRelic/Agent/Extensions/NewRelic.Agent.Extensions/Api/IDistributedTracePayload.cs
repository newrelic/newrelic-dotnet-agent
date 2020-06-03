namespace NewRelic.Agent.Api
{
    public interface IDistributedTracePayload
    {
        string HttpSafe();

        string Text();

        bool IsEmpty();
    }
}

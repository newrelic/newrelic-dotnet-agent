namespace NewRelic.Agent.Api
{
    public interface ITraceMetadata
    {
        string TraceId { get; }

        string SpanId { get; }

        bool IsSampled { get; }
    }
}

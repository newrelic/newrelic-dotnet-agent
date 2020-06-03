namespace NewRelic.Agent.Core
{
    public enum DataTransportResponseStatus
    {
        RequestSuccessful,
        Retain,
        ReduceSizeIfPossibleOtherwiseDiscard,
        Discard
    }
}

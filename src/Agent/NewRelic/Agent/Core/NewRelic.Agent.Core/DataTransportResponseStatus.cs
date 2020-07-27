namespace NewRelic.Agent.Core
{
    public enum DataTransportResponseStatus
    {
        RequestSuccessful,
        ConnectionError,
        ServiceUnavailableError,
        PostTooBigError,
        OtherError
    }
}

namespace NewRelic.Agent.Core.DataTransport
{
    public interface IConnectionManager
    {
        T SendDataRequest<T>(string method, params object[] data);

        void AttemptAutoStart();
    }
}

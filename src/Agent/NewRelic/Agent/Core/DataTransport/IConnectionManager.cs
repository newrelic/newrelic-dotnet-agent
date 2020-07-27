using System;

namespace NewRelic.Agent.Core.DataTransport
{
    public interface IConnectionManager
    {
        T SendDataRequest<T>(String method, params Object[] data);
    }
}

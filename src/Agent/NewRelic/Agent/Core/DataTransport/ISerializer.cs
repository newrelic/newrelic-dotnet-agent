using System;

namespace NewRelic.Agent.Core.DataTransport
{
    public interface ISerializer
    {
        String Serialize(Object[] parameters);
        T Deserialize<T>(String responseBody);
    }
}

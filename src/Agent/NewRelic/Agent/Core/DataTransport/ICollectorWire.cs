using System;

namespace NewRelic.Agent.Core.DataTransport
{
    public interface ICollectorWire
    {
        String SendData(String method, ConnectionInfo connectionInfo, String serializedData);
    }
}

﻿namespace NewRelic.Agent.Core.DataTransport
{
    public interface ICollectorWire
    {
        string SendData(string method, ConnectionInfo connectionInfo, string serializedData);
    }
}

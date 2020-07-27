using System;

namespace NewRelic.Agent.Core.Exceptions
{

    /// <summary>
    /// Thrown on a disconnect between the collector(RPM) and the Agent
    /// </summary>
    public class DisconnectedException : RPMException
    {
        public DisconnectedException(String message) : base(message)
        {
        }
    }
}

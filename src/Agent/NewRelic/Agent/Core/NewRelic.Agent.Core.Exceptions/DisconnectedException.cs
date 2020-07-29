namespace NewRelic.Agent.Core.Exceptions
{

    /// <summary>
    /// Thrown on a disconnect between the collector(RPM) and the Agent
    /// </summary>
    public class DisconnectedException : RPMException
    {
        public DisconnectedException(string message) : base(message)
        {
        }
    }
}

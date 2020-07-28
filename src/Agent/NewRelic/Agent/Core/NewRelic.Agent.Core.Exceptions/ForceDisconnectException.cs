namespace NewRelic.Agent.Core.Exceptions
{

    /// <summary>
    /// This exception is thrown when there has been a disconnection between the collector(RPM) and the Agent.
    /// </summary>
    public class ForceDisconnectException : InstructionException
    {
        public ForceDisconnectException(string message) : base(message)
        {
        }
    }
}

using System;

namespace NewRelic.Agent.Core.Exceptions
{
    /// <summary>
    /// This exception is thrown when the agent receives a service unavailable error
    /// </summary>
    public class ServiceUnavailableException : Exception
    {
        public ServiceUnavailableException(string message)
            : base(message)
        {
        }
    }
}

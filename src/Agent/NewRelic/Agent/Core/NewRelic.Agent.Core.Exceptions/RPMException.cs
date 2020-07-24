using System;

namespace NewRelic.Agent.Core.Exceptions
{

    /// <summary>
    /// A base clase for specific exceptions that appear to be thrown from the collector/RPM back to the Agent.
    /// </summary>
    public class RPMException : System.Exception
    {
        public RPMException(String message) : base(message)
        {
        }

        public RPMException(String message, Exception exception) : base(message, exception)
        {
        }
    }
}

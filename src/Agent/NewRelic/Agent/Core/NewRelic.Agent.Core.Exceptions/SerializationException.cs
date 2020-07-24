using System;

namespace NewRelic.Agent.Core.Exceptions
{
    public class SerializationException : RPMException
    {
        public SerializationException(String message)
            : base(message)
        {
        }
    }
}

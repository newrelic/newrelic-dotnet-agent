using System;

namespace NewRelic.Agent.Core.ThreadProfiling
{
    public class InvalidProfileIdException : Exception
    {
        public InvalidProfileIdException(string message)
            : base(message)
        { }
    }
}

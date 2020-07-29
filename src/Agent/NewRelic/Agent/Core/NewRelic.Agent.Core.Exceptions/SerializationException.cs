namespace NewRelic.Agent.Core.Exceptions
{
    public class SerializationException : RPMException
    {
        public SerializationException(string message)
            : base(message)
        {
        }
    }
}

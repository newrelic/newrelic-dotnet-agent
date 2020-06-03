namespace NewRelic.Api.Agent
{
    internal class NoOpSpan : ISpan
    {
        public ISpan AddCustomAttribute(string key, object value)
        {
            return this;
        }
    }
}

namespace NewRelic.Agent.Api
{
    /// <summary>
    /// This interface identifies functionality that is available to the API.
    /// Since the API refers to Spans, this object is named accordingly.
    /// </summary>
    public interface ISpan
    {
        ISpan AddCustomAttribute(string key, object value);
    }
}

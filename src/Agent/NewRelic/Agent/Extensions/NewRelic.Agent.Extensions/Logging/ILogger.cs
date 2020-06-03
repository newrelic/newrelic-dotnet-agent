namespace NewRelic.Agent.Extensions.Logging
{
    public enum Level
    {
        Finest,
        Debug,
        Info,
        Warn,
        Error
    }

    public interface ILogger
    {
        bool IsEnabledFor(Level level);
        void Log(Level level, object message);
    }
}

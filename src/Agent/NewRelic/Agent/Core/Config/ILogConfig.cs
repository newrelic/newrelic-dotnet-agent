namespace NewRelic.Agent.Core.Config
{
    public interface ILogConfig
    {
        string LogLevel { get; }

        string GetFullLogFileName();

        bool FileLockingModelSpecified { get; }
        configurationLogFileLockingModel FileLockingModel { get; }

        bool Console { get; }

        bool IsAuditLogEnabled { get; }
    }
}

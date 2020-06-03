namespace NewRelic.Agent.Core.Logging
{
    public static class AuditLog
    {
        private static readonly log4net.ILog Logger = log4net.LogManager.GetLogger(typeof(AuditLog));

        /// <summary>
        /// Logs <paramref name="message"/> at the AUDIT level, a custom log level that is not well-defined in popular logging providers like log4net. This log level should be used only as dictated by the security team to satisfy auditing requirements.
        /// </summary>
        public static void Log(string message)
        {
            var auditLogLevel = LoggerBootstrapper.GetAuditLevel();
            Logger.Logger.Log(typeof(AuditLog), auditLogLevel, message, null);
        }
    }
}

using System;
using JetBrains.Annotations;
using log4net.Core;

namespace NewRelic.Agent.Core.Logging
{
    public static class Log
    {
        [NotNull]
        private static readonly log4net.ILog Logger = log4net.LogManager.GetLogger(typeof(Log));

        #region Error

        /// <summary>
        /// True iff logging has been configured to include ERROR level logs.
        /// </summary>
        public static Boolean IsErrorEnabled => Logger.IsErrorEnabled;

        /// <summary>
        /// Logs <paramref name="message"/> at the ERROR level. This log level should be used for information regarding problems in the agent that will adversely affect the user in some way (data loss, performance problems, reduced agent functionality, etc). Do not use if logging that information will create a performance problem (say, due to excessive logging).
        /// </summary>
        public static void Error([NotNull] String message)
        {
            Logger.Error(message);
        }

        /// <summary>
        /// Logs <paramref name="exception"/> at the ERROR level by calling exception.ToString(). This log level should be used for information regarding problems in the agent that will adversely affect the user in some way (data loss, performance problems, reduced agent functionality, etc). Do not use if logging that information will create a performance problem (say, due to excessive logging).
        /// </summary>
        public static void Error([NotNull] Exception exception)
        {
            Logger.Error(exception.ToString());
        }

        /// <summary>
        /// Logs a message at the ERROR level by calling String.Format(<paramref name="format"/>, <paramref name="args"/>). This log level should be used for information regarding problems in the agent that will adversely affect the user in some way (data loss, performance problems, reduced agent functionality, etc). Do not use if logging that information will create a performance problem (say, due to excessive logging).
        /// </summary>
        public static void ErrorFormat([NotNull] String format, [NotNull] params Object[] args)
        {
            if (IsErrorEnabled)
            {
                Logger.Error(String.Format(format, args));
            }
        }

        #endregion Error

        #region Warn

        /// <summary>
        /// True iff logging has been configured to include WARN level logs.
        /// </summary>
        public static Boolean IsWarnEnabled => Logger.IsWarnEnabled;

        /// <summary>
        /// Logs <paramref name="message"/> at the WARN level. This log level should be used for information regarding *possible* problems in the agent that *might* adversely affect the user in some way (data loss, performance problems, reduced agent functionality, etc). Do not use if logging that information will create a performance problem (say, due to excessive logging).
        /// </summary>
        public static void Warn([NotNull] String message)
        {
            Logger.Warn(message);
        }

        /// <summary>
        /// Logs <paramref name="exception"/> at the WARN level by calling exception.ToString(). This log level should be used for information regarding *possible* problems in the agent that *might* adversely affect the user in some way (data loss, performance problems, reduced agent functionality, etc). Do not use if logging that information will create a performance problem (say, due to excessive logging).
        /// </summary>
        public static void Warn([NotNull] Exception exception)
        {
            Logger.Warn(exception.ToString());
        }

        /// <summary>
        /// Logs a message at the WARN level by calling String.Format(<paramref name="format"/>, <paramref name="args"/>). This log level should be used for information regarding *possible* problems in the agent that *might* adversely affect the user in some way (data loss, performance problems, reduced agent functionality, etc). Do not use if logging that information will create a performance problem (say, due to excessive logging).
        /// </summary>
        public static void WarnFormat([NotNull] String format, [NotNull] params Object[] args)
        {
            if (IsWarnEnabled)
            {
                Logger.Warn(String.Format(format, args));
            }
        }

        #endregion Warn

        #region Info

        /// <summary>
        /// True iff logging has been configured to include INFO level logs.
        /// </summary>
        public static Boolean IsInfoEnabled => Logger.IsInfoEnabled;

        /// <summary>
        /// Logs <paramref name="message"/> at the INFO level. This log level should be used for information for non-error information that may be of interest to the user, such as a the agent noticing a configuration change, or an infrequent "heartbeat". Do not use if logging that information will create a performance problem (say, due to excessive logging).
        /// </summary>
        public static void Info([NotNull] String message)
        {
            Logger.Info(message);
        }

        /// <summary>
        /// Logs <paramref name="exception"/> at the INFO level by calling exception.ToString(). This log level should be used for information for non-error information that may be of interest to the user, such as a the agent noticing a configuration change, or an infrequent "heartbeat". Do not use if logging that information will create a performance problem (say, due to excessive logging).
        /// </summary>
        public static void Info([NotNull] Exception exception)
        {
            Logger.Info(exception.ToString());
        }

        /// <summary>
        /// Logs a message at the INFO level by calling String.Format(<paramref name="format"/>, <paramref name="args"/>). This log level should be used for information for non-error information that may be of interest to the user, such as a the agent noticing a configuration change, or an infrequent "heartbeat". Do not use if logging that information will create a performance problem (say, due to excessive logging).
        /// </summary>
        public static void InfoFormat([NotNull] String format, [NotNull] params Object[] args)
        {
            if (IsInfoEnabled)
            {
                Logger.Info(String.Format(format, args));
            }
        }

        #endregion Info

        #region Debug

        /// <summary>
        /// True iff logging has been configured to include DEBUG level logs.
        /// </summary>
        public static Boolean IsDebugEnabled => Logger.IsDebugEnabled;

        /// <summary>
        /// Logs <paramref name="message"/> at the DEBUG level. This log level should be used for information that is non-critical and used mainly for troubleshooting common problems such as RUM injection or SQL explain plans. This level is not enabled by default so there is less concern about performance, but this level still should not be used for any logging that would cause significant performance, such as logging every transaction name for every transaction.
        /// </summary>
        public static void Debug([NotNull] String message)
        {
            Logger.Debug(message);
        }

        /// <summary>
        /// Logs <paramref name="exception"/> at the DEBUG level by calling exception.ToString(). This log level should be used for information that is non-critical and used mainly for troubleshooting common problems such as RUM injection or SQL explain plans. This level is not enabled by default so there is less concern about performance, but this level still should not be used for any logging that would cause significant performance, such as logging every transaction name for every transaction.
        /// </summary>
        public static void Debug([NotNull] Exception exception)
        {
            Logger.Debug(exception.ToString());
        }

        /// <summary>
        /// Logs a message at the DEBUG level by calling String.Format(<paramref name="format"/>, <paramref name="args"/>). This log level should be used for information that is non-critical and used mainly for troubleshooting common problems such as RUM injection or SQL explain plans. This level is not enabled by default so there is less concern about performance, but this level still should not be used for any logging that would cause significant performance, such as logging every transaction name for every transaction.
        /// </summary>
        public static void DebugFormat([NotNull] String format, [NotNull] params Object[] args)
        {
            if (Logger.IsDebugEnabled)
            {
                Logger.Debug(String.Format(format, args));
            }
        }

        #endregion Debug

        #region Finest

        /// <summary>
        /// True iff logging has been configured to include FINEST level logs.
        /// </summary>
        public static Boolean IsFinestEnabled => Logger.Logger.IsEnabledFor(Level.Finest);

        /// <summary>
        /// Logs <paramref name="message"/> at the FINEST level. This log level should be used as a last resort for information that would otherwise be too expensive or too noisy to log at DEBUG level, such as logging every transaction name for every transaction. Useful for troubleshooting subtle problems like WCF's dual transactions.
        /// </summary>
        public static void Finest([NotNull] String message)
        {
            Logger.Logger.Log(typeof(Log), Level.Finest, message, null);
        }

        /// <summary>
        /// Logs <paramref name="exception"/> at the FINEST level by calling exception.ToString(). This log level should be used as a last resort for information that would otherwise be too expensive or too noisy to log at DEBUG level, such as logging every transaction name for every transaction. Useful for troubleshooting subtle problems like WCF's dual transactions.
        /// </summary>
        public static void Finest([NotNull] Exception exception)
        {
            Logger.Logger.Log(typeof(Log), Level.Finest, exception.ToString(), null);
        }

        /// <summary>
        /// Logs a message at the FINEST level by calling String.Format(<paramref name="format"/>, <paramref name="args"/>). This log level should be used as a last resort for information that would otherwise be too expensive or too noisy to log at DEBUG level, such as logging every transaction name for every transaction. Useful for troubleshooting subtle problems like WCF's dual transactions.
        /// </summary>
        public static void FinestFormat([NotNull] String format, [NotNull] params Object[] args)
        {
            if (IsFinestEnabled)
            {
                var formattedMessage = String.Format(format, args);
                Logger.Logger.Log(typeof(Log), Level.Finest, formattedMessage, null);
            }
        }

        #endregion Finest

        #region Audit

        /// <summary>
        /// Logs <paramref name="message"/> at the AUDIT level, a custom log level that is not well-defined in popular logging providers like log4net. This log level should be used only as dictated by the security team to satisfy auditing requirements.
        /// </summary>
        public static void Audit([NotNull] String message)
        {
            var auditLogLevel = LoggerBootstrapper.GetAuditLevel();
            Logger.Logger.Log(typeof(Log), auditLogLevel, message, null);
        }

        #endregion
    }
}

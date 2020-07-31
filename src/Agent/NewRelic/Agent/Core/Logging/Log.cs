// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using log4net.Core;

namespace NewRelic.Agent.Core.Logging
{
    public static class Log
    {
        private static readonly log4net.ILog Logger = log4net.LogManager.GetLogger(typeof(Log));

        #region Error

        /// <summary>
        /// True iff logging has been configured to include ERROR level logs.
        /// </summary>
        public static bool IsErrorEnabled => Logger.IsErrorEnabled;

        /// <summary>
        /// Logs <paramref name="message"/> at the ERROR level. This log level should be used for information regarding problems in the agent that will adversely affect the user in some way (data loss, performance problems, reduced agent functionality, etc). Do not use if logging that information will create a performance problem (say, due to excessive logging).
        /// </summary>
        public static void Error(string message)
        {
            Logger.Error(message);
        }

        /// <summary>
        /// Logs <paramref name="exception"/> at the ERROR level by calling exception.ToString(). This log level should be used for information regarding problems in the agent that will adversely affect the user in some way (data loss, performance problems, reduced agent functionality, etc). Do not use if logging that information will create a performance problem (say, due to excessive logging).
        /// </summary>
        public static void Error(Exception exception)
        {
            Logger.Error(exception.ToString());
        }

        /// <summary>
        /// Logs a message at the ERROR level by calling String.Format(<paramref name="format"/>, <paramref name="args"/>). This log level should be used for information regarding problems in the agent that will adversely affect the user in some way (data loss, performance problems, reduced agent functionality, etc). Do not use if logging that information will create a performance problem (say, due to excessive logging).
        /// </summary>
        public static void ErrorFormat(string format, params object[] args)
        {
            if (IsErrorEnabled)
            {
                Logger.Error(string.Format(format, args));
            }
        }

        #endregion Error

        #region Warn

        /// <summary>
        /// True iff logging has been configured to include WARN level logs.
        /// </summary>
        public static bool IsWarnEnabled => Logger.IsWarnEnabled;

        /// <summary>
        /// Logs <paramref name="message"/> at the WARN level. This log level should be used for information regarding *possible* problems in the agent that *might* adversely affect the user in some way (data loss, performance problems, reduced agent functionality, etc). Do not use if logging that information will create a performance problem (say, due to excessive logging).
        /// </summary>
        public static void Warn(string message)
        {
            Logger.Warn(message);
        }

        /// <summary>
        /// Logs <paramref name="exception"/> at the WARN level by calling exception.ToString(). This log level should be used for information regarding *possible* problems in the agent that *might* adversely affect the user in some way (data loss, performance problems, reduced agent functionality, etc). Do not use if logging that information will create a performance problem (say, due to excessive logging).
        /// </summary>
        public static void Warn(Exception exception)
        {
            Logger.Warn(exception.ToString());
        }

        /// <summary>
        /// Logs a message at the WARN level by calling String.Format(<paramref name="format"/>, <paramref name="args"/>). This log level should be used for information regarding *possible* problems in the agent that *might* adversely affect the user in some way (data loss, performance problems, reduced agent functionality, etc). Do not use if logging that information will create a performance problem (say, due to excessive logging).
        /// </summary>
        public static void WarnFormat(string format, params object[] args)
        {
            if (IsWarnEnabled)
            {
                Logger.Warn(string.Format(format, args));
            }
        }

        #endregion Warn

        #region Info

        /// <summary>
        /// True iff logging has been configured to include INFO level logs.
        /// </summary>
        public static bool IsInfoEnabled => Logger.IsInfoEnabled;

        /// <summary>
        /// Logs <paramref name="message"/> at the INFO level. This log level should be used for information for non-error information that may be of interest to the user, such as a the agent noticing a configuration change, or an infrequent "heartbeat". Do not use if logging that information will create a performance problem (say, due to excessive logging).
        /// </summary>
        public static void Info(string message)
        {
            Logger.Info(message);
        }

        /// <summary>
        /// Logs <paramref name="exception"/> at the INFO level by calling exception.ToString(). This log level should be used for information for non-error information that may be of interest to the user, such as a the agent noticing a configuration change, or an infrequent "heartbeat". Do not use if logging that information will create a performance problem (say, due to excessive logging).
        /// </summary>
        public static void Info(Exception exception)
        {
            Logger.Info(exception.ToString());
        }

        /// <summary>
        /// Logs a message at the INFO level by calling String.Format(<paramref name="format"/>, <paramref name="args"/>). This log level should be used for information for non-error information that may be of interest to the user, such as a the agent noticing a configuration change, or an infrequent "heartbeat". Do not use if logging that information will create a performance problem (say, due to excessive logging).
        /// </summary>
        public static void InfoFormat(string format, params object[] args)
        {
            if (IsInfoEnabled)
            {
                Logger.Info(string.Format(format, args));
            }
        }

        #endregion Info

        #region Debug

        /// <summary>
        /// True iff logging has been configured to include DEBUG level logs.
        /// </summary>
        public static bool IsDebugEnabled => Logger.IsDebugEnabled;

        /// <summary>
        /// Logs <paramref name="message"/> at the DEBUG level. This log level should be used for information that is non-critical and used mainly for troubleshooting common problems such as RUM injection or SQL explain plans. This level is not enabled by default so there is less concern about performance, but this level still should not be used for any logging that would cause significant performance, such as logging every transaction name for every transaction.
        /// </summary>
        public static void Debug(string message)
        {
            Logger.Debug(message);
        }

        /// <summary>
        /// Logs <paramref name="exception"/> at the DEBUG level by calling exception.ToString(). This log level should be used for information that is non-critical and used mainly for troubleshooting common problems such as RUM injection or SQL explain plans. This level is not enabled by default so there is less concern about performance, but this level still should not be used for any logging that would cause significant performance, such as logging every transaction name for every transaction.
        /// </summary>
        public static void Debug(Exception exception)
        {
            Logger.Debug(exception.ToString());
        }

        /// <summary>
        /// Logs a message at the DEBUG level by calling String.Format(<paramref name="format"/>, <paramref name="args"/>). This log level should be used for information that is non-critical and used mainly for troubleshooting common problems such as RUM injection or SQL explain plans. This level is not enabled by default so there is less concern about performance, but this level still should not be used for any logging that would cause significant performance, such as logging every transaction name for every transaction.
        /// </summary>
        public static void DebugFormat(string format, params object[] args)
        {
            if (Logger.IsDebugEnabled)
            {
                Logger.Debug(string.Format(format, args));
            }
        }

        #endregion Debug

        #region Finest

        /// <summary>
        /// True iff logging has been configured to include FINEST level logs.
        /// </summary>
        public static bool IsFinestEnabled => Logger.Logger.IsEnabledFor(Level.Finest);

        /// <summary>
        /// Logs <paramref name="message"/> at the FINEST level. This log level should be used as a last resort for information that would otherwise be too expensive or too noisy to log at DEBUG level, such as logging every transaction name for every transaction. Useful for troubleshooting subtle problems like WCF's dual transactions.
        /// </summary>
        public static void Finest(string message)
        {
            Logger.Logger.Log(typeof(Log), Level.Finest, message, null);
        }

        /// <summary>
        /// Logs <paramref name="exception"/> at the FINEST level by calling exception.ToString(). This log level should be used as a last resort for information that would otherwise be too expensive or too noisy to log at DEBUG level, such as logging every transaction name for every transaction. Useful for troubleshooting subtle problems like WCF's dual transactions.
        /// </summary>
        public static void Finest(Exception exception)
        {
            Logger.Logger.Log(typeof(Log), Level.Finest, exception.ToString(), null);
        }

        /// <summary>
        /// Logs a message at the FINEST level by calling String.Format(<paramref name="format"/>, <paramref name="args"/>). This log level should be used as a last resort for information that would otherwise be too expensive or too noisy to log at DEBUG level, such as logging every transaction name for every transaction. Useful for troubleshooting subtle problems like WCF's dual transactions.
        /// </summary>
        public static void FinestFormat(string format, params object[] args)
        {
            if (IsFinestEnabled)
            {
                var formattedMessage = string.Format(format, args);
                Logger.Logger.Log(typeof(Log), Level.Finest, formattedMessage, null);
            }
        }

        #endregion Finest

        #region Audit

        /// <summary>
        /// Logs <paramref name="message"/> at the AUDIT level, a custom log level that is not well-defined in popular logging providers like log4net. This log level should be used only as dictated by the security team to satisfy auditing requirements.
        /// </summary>
        public static void Audit(string message)
        {
            var auditLogLevel = LoggerBootstrapper.GetAuditLevel();
            Logger.Logger.Log(typeof(Log), auditLogLevel, message, null);
        }

        #endregion
    }
}

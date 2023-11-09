// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;

namespace NewRelic.Core.Logging
{
    public enum LogLevel
    {
        Error,
        Warn,
        Info,
        Debug,
        Finest
    }

    public static class Log
    {
        private static ILogger Logger = new NoOpLogger();

        public static void Initialize(ILogger logger)
        {
            Logger = logger;
        }

        public static bool FileLoggingHasFailed { get; set; }

        #region Error

        /// <summary>
        /// True iff logging has been configured to include ERROR level logs.
        /// </summary>
        public static bool IsErrorEnabled => Logger.IsErrorEnabled;

        /// <summary>
        /// Logs <paramref name="message"/> at the ERROR level. This log level should be used for information regarding problems in the agent that will adversely affect the user in some way (data loss, performance problems, reduced agent functionality, etc). Do not use if logging that information will create a performance problem (say, due to excessive logging).
        /// </summary>
        public static void Error(Exception exception, string message, params object[] args) =>
            Logger.Error(exception, message, args);

        /// <summary>
        /// Logs <paramref name="exception"/> at the ERROR level by calling exception.ToString(). This log level should be used for information regarding problems in the agent that will adversely affect the user in some way (data loss, performance problems, reduced agent functionality, etc). Do not use if logging that information will create a performance problem (say, due to excessive logging).
        /// </summary>
        public static void Error(string message, params object[] args) =>
            Logger.Error(message, args);

        #endregion Error

        #region Warn

        /// <summary>
        /// True iff logging has been configured to include WARN level logs.
        /// </summary>
        public static bool IsWarnEnabled => Logger.IsWarnEnabled;

        /// <summary>
        /// Logs <paramref name="message"/> at the WARN level. This log level should be used for information regarding *possible* problems in the agent that *might* adversely affect the user in some way (data loss, performance problems, reduced agent functionality, etc). Do not use if logging that information will create a performance problem (say, due to excessive logging).
        /// </summary>
        public static void Warn(Exception exception, string message, params object[] args) =>
            Logger.Warn(exception, message, args);

        /// <summary>
        /// Logs <paramref name="exception"/> at the WARN level by calling exception.ToString(). This log level should be used for information regarding *possible* problems in the agent that *might* adversely affect the user in some way (data loss, performance problems, reduced agent functionality, etc). Do not use if logging that information will create a performance problem (say, due to excessive logging).
        /// </summary>
        public static void Warn(string message, params object[] args) =>
            Logger.Warn(message, args);

        #endregion Warn

        #region Info

        /// <summary>
        /// True iff logging has been configured to include INFO level logs.
        /// </summary>
        public static bool IsInfoEnabled => Logger.IsInfoEnabled;

        /// <summary>
        /// Logs <paramref name="message"/> at the INFO level. This log level should be used for information for non-error information that may be of interest to the user, such as a the agent noticing a configuration change, or an infrequent "heartbeat". Do not use if logging that information will create a performance problem (say, due to excessive logging).
        /// </summary>
        public static void Info(Exception exception, string message, params object[] args) =>
            Logger.Info(exception, message, args);

        /// <summary>
        /// Logs <paramref name="exception"/> at the INFO level by calling exception.ToString(). This log level should be used for information for non-error information that may be of interest to the user, such as a the agent noticing a configuration change, or an infrequent "heartbeat". Do not use if logging that information will create a performance problem (say, due to excessive logging).
        /// </summary>
        public static void Info(string message, params object[] args) =>
            Logger.Info(message, args);

        #endregion Info

        #region Debug

        /// <summary>
        /// True iff logging has been configured to include DEBUG level logs.
        /// </summary>
        public static bool IsDebugEnabled => Logger.IsDebugEnabled;

        /// <summary>
        /// Logs <paramref name="message"/> at the DEBUG level. This log level should be used for information that is non-critical and used mainly for troubleshooting common problems such as RUM injection or SQL explain plans. This level is not enabled by default so there is less concern about performance, but this level still should not be used for any logging that would cause significant performance, such as logging every transaction name for every transaction.
        /// </summary>
        public static void Debug(Exception exception, string message, params object[] args) =>
            Logger.Debug(exception, message, args);

        /// <summary>
        /// Logs <paramref name="exception"/> at the DEBUG level by calling exception.ToString(). This log level should be used for information that is non-critical and used mainly for troubleshooting common problems such as RUM injection or SQL explain plans. This level is not enabled by default so there is less concern about performance, but this level still should not be used for any logging that would cause significant performance, such as logging every transaction name for every transaction.
        /// </summary>
        public static void Debug(string message, params object[] args) =>
            Logger.Debug(message, args);

        #endregion Debug

        #region Finest

        /// <summary>
        /// True iff logging has been configured to include FINEST level logs.
        /// </summary>
        public static bool IsFinestEnabled => Logger.IsFinestEnabled;

        /// <summary>
        /// Logs <paramref name="message"/> at the FINEST level. This log level should be used as a last resort for information that would otherwise be too expensive or too noisy to log at DEBUG level, such as logging every transaction name for every transaction. Useful for troubleshooting subtle problems like WCF's dual transactions.
        /// </summary>
        public static void Finest(Exception exception, string message, params object[] args) =>
            Logger.Finest(exception, message, args);

        /// <summary>
        /// Logs <paramref name="exception"/> at the FINEST level by calling exception.ToString(). This log level should be used as a last resort for information that would otherwise be too expensive or too noisy to log at DEBUG level, such as logging every transaction name for every transaction. Useful for troubleshooting subtle problems like WCF's dual transactions.
        /// </summary>
        public static void Finest(string message, params object[] args) =>
            Logger.Finest(message, args);

        #endregion Finest

        public static bool IsEnabledFor(LogLevel level)
        {
            switch (level)
            {
                case LogLevel.Error:
                    return IsErrorEnabled;
                case LogLevel.Warn:
                    return IsWarnEnabled;
                case LogLevel.Info:
                    return IsInfoEnabled;
                case LogLevel.Debug:
                    return IsDebugEnabled;
                case LogLevel.Finest:
                    return IsFinestEnabled;
            }

            return false;
        }

        public static void LogMessage(LogLevel level, string message, params object[] args)
        {
            if (!IsEnabledFor(level))
            {
                return;
            }

            switch (level)
            {
                case LogLevel.Error:
                    Error(message, args);
                    break;
                case LogLevel.Warn:
                    Warn(message, args);
                    break;
                case LogLevel.Info:
                    Info(message, args);
                    break;
                case LogLevel.Debug:
                    Debug(message, args);
                    break;
                case LogLevel.Finest:
                    Finest(message, args);
                    break;
            }
        }

        public static void LogMessage(LogLevel level, Exception exception, string message, params object[] args)
        {
            if (!IsEnabledFor(level))
            {
                return;
            }

            switch (level)
            {
                case LogLevel.Error:
                    Error(exception, message, args);
                    break;
                case LogLevel.Warn:
                    Warn(exception, message, args);
                    break;
                case LogLevel.Info:
                    Info(exception, message, args);
                    break;
                case LogLevel.Debug:
                    Debug(exception, message, args);
                    break;
                case LogLevel.Finest:
                    Finest(exception, message, args);
                    break;
            }
        }
    }
}

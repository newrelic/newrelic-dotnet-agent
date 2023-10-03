// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Extensions.Logging;
using Serilog.Events;
using System;
using System.Threading;

namespace NewRelic.Agent.Core.Logging
{
    public class Logger : ILogger, global::NewRelic.Core.Logging.ILogger
    {
        private readonly Serilog.ILogger _logger = Serilog.Log.Logger;

        public bool IsEnabledFor(Level level)
        {
            switch (level)
            {
                case Level.Finest:
                    return _logger.IsEnabled(LogEventLevel.Verbose);
                case Level.Debug:
                    return _logger.IsEnabled(LogEventLevel.Debug);
                case Level.Info:
                    return _logger.IsEnabled(LogEventLevel.Information);
                case Level.Warn:
                    return _logger.IsEnabled(LogEventLevel.Warning);
                case Level.Error:
                    return _logger.IsEnabled(LogEventLevel.Error);
                default:
                    return false;
            }
        }

        public void Log(Level level, string message)
        {
            if (!IsEnabledFor(level)) return;
            var messageString = message.ToString();

            switch (level)
            {
                case Level.Finest:
                    _logger.Verbose(messageString);
                    break;
                case Level.Debug:
                    _logger.Debug(messageString);
                    break;
                case Level.Info:
                    _logger.Information(messageString);
                    break;
                case Level.Warn:
                    _logger.Warning(messageString);
                    break;
                case Level.Error:
                    _logger.Error(messageString);
                    break;
            }
        }

        #region Error

        /// <summary>
        /// True iff logging has been configured to include ERROR level logs.
        /// </summary>
        public bool IsErrorEnabled => _logger.IsEnabled(LogEventLevel.Error);

        /// <summary>
        /// Logs <paramref name="message"/> at the ERROR level. This log level should be used for information regarding problems in the agent that will adversely affect the user in some way (data loss, performance problems, reduced agent functionality, etc). Do not use if logging that information will create a performance problem (say, due to excessive logging).
        /// </summary>
        public void Error(Exception exception, string message, params object[] args) =>
            _logger.Error(exception, message, args);

        /// <summary>
        /// Logs <paramref name="exception"/> at the ERROR level by calling exception.ToString(). This log level should be used for information regarding problems in the agent that will adversely affect the user in some way (data loss, performance problems, reduced agent functionality, etc). Do not use if logging that information will create a performance problem (say, due to excessive logging).
        /// </summary>
        public void Error(string message, params object[] args) =>
            _logger.Error(message, args);

        #endregion Error

        #region Warn

        /// <summary>
        /// True iff logging has been configured to include WARN level logs.
        /// </summary>
        public bool IsWarnEnabled => _logger.IsEnabled(LogEventLevel.Warning);

        /// <summary>
        /// Logs <paramref name="message"/> at the WARN level. This log level should be used for information regarding *possible* problems in the agent that *might* adversely affect the user in some way (data loss, performance problems, reduced agent functionality, etc). Do not use if logging that information will create a performance problem (say, due to excessive logging).
        /// </summary>
        public void Warn(Exception exception, string message, params object[] args) =>
            _logger.Warning(exception, message, args);

        /// <summary>
        /// Logs <paramref name="exception"/> at the WARN level by calling exception.ToString(). This log level should be used for information regarding *possible* problems in the agent that *might* adversely affect the user in some way (data loss, performance problems, reduced agent functionality, etc). Do not use if logging that information will create a performance problem (say, due to excessive logging).
        /// </summary>
        public void Warn(string message, params object[] args) =>
            _logger.Warning(message, args);

        #endregion Warn

        #region Info

        /// <summary>
        /// True iff logging has been configured to include INFO level logs.
        /// </summary>
        public bool IsInfoEnabled => _logger.IsEnabled(LogEventLevel.Information);

        /// <summary>
        /// Logs <paramref name="message"/> at the INFO level. This log level should be used for information for non-error information that may be of interest to the user, such as a the agent noticing a configuration change, or an infrequent "heartbeat". Do not use if logging that information will create a performance problem (say, due to excessive logging).
        /// </summary>
        public void Info(Exception exception, string message, params object[] args) =>
            _logger.Information(exception, message, args);

        /// <summary>
        /// Logs a message at the INFO level by calling String.Format(<paramref name="format"/>, <paramref name="args"/>). This log level should be used for information for non-error information that may be of interest to the user, such as a the agent noticing a configuration change, or an infrequent "heartbeat". Do not use if logging that information will create a performance problem (say, due to excessive logging).
        /// </summary>
        public void Info(string message, params object[] args) =>
            _logger.Information(message, args);

        #endregion Info

        #region Debug

        /// <summary>
        /// True iff logging has been configured to include DEBUG level logs.
        /// </summary>
        public bool IsDebugEnabled => _logger.IsEnabled(LogEventLevel.Debug);

        /// <summary>
        /// Logs <paramref name="message"/> at the DEBUG level. This log level should be used for information that is non-critical and used mainly for troubleshooting common problems such as RUM injection or SQL explain plans. This level is not enabled by default so there is less concern about performance, but this level still should not be used for any logging that would cause significant performance, such as logging every transaction name for every transaction.
        /// </summary>
        public void Debug(Exception exception, string message, params object[] args) =>
            _logger.Debug(exception, message, args);

        /// <summary>
        /// Logs <paramref name="exception"/> at the DEBUG level by calling exception.ToString(). This log level should be used for information that is non-critical and used mainly for troubleshooting common problems such as RUM injection or SQL explain plans. This level is not enabled by default so there is less concern about performance, but this level still should not be used for any logging that would cause significant performance, such as logging every transaction name for every transaction.
        /// </summary>
        public void Debug(string message, params object[] args) =>
            _logger.Debug(message, args);

        #endregion Debug

        #region Finest

        /// <summary>
        /// True iff logging has been configured to include FINEST level logs.
        /// </summary>
        public bool IsFinestEnabled => _logger.IsEnabled(LogEventLevel.Verbose);

        /// <summary>
        /// Logs <paramref name="message"/> at the FINEST level. This log level should be used as a last resort for information that would otherwise be too expensive or too noisy to log at DEBUG level, such as logging every transaction name for every transaction. Useful for troubleshooting subtle problems like WCF's dual transactions.
        /// </summary>
        public void Finest(Exception exception, string message, params object[] args) =>
            _logger.Verbose(exception, message, args);

        /// <summary>
        /// Logs <paramref name="exception"/> at the FINEST level by calling exception.ToString(). This log level should be used as a last resort for information that would otherwise be too expensive or too noisy to log at DEBUG level, such as logging every transaction name for every transaction. Useful for troubleshooting subtle problems like WCF's dual transactions.
        /// </summary>
        public void Finest(string message, params object[] args) =>
            _logger.Verbose(message, args);

        #endregion Finest

    }
}

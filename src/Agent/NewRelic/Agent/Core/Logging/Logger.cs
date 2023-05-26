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

        public void Log(Level level, object message)
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
        public void Error(string message)
        {
            _logger.Error(message);
        }

        /// <summary>
        /// Logs <paramref name="exception"/> at the ERROR level by calling exception.ToString(). This log level should be used for information regarding problems in the agent that will adversely affect the user in some way (data loss, performance problems, reduced agent functionality, etc). Do not use if logging that information will create a performance problem (say, due to excessive logging).
        /// </summary>
        public void Error(Exception exception)
        {
            _logger.Error(exception, "");
        }

        /// <summary>
        /// Logs a message at the ERROR level by calling String.Format(<paramref name="format"/>, <paramref name="args"/>). This log level should be used for information regarding problems in the agent that will adversely affect the user in some way (data loss, performance problems, reduced agent functionality, etc). Do not use if logging that information will create a performance problem (say, due to excessive logging).
        /// </summary>
        public void ErrorFormat(string format, params object[] args)
        {
            if (IsErrorEnabled)
                _logger.Error(format, args);
        }

        #endregion Error

        #region Warn

        /// <summary>
        /// True iff logging has been configured to include WARN level logs.
        /// </summary>
        public bool IsWarnEnabled => _logger.IsEnabled(LogEventLevel.Warning);

        /// <summary>
        /// Logs <paramref name="message"/> at the WARN level. This log level should be used for information regarding *possible* problems in the agent that *might* adversely affect the user in some way (data loss, performance problems, reduced agent functionality, etc). Do not use if logging that information will create a performance problem (say, due to excessive logging).
        /// </summary>
        public void Warn(string message)
        {
            _logger.Warning(message);
        }

        /// <summary>
        /// Logs <paramref name="exception"/> at the WARN level by calling exception.ToString(). This log level should be used for information regarding *possible* problems in the agent that *might* adversely affect the user in some way (data loss, performance problems, reduced agent functionality, etc). Do not use if logging that information will create a performance problem (say, due to excessive logging).
        /// </summary>
        public void Warn(Exception exception)
        {
            _logger.Warning(exception, "");
        }

        /// <summary>
        /// Logs a message at the WARN level by calling String.Format(<paramref name="format"/>, <paramref name="args"/>). This log level should be used for information regarding *possible* problems in the agent that *might* adversely affect the user in some way (data loss, performance problems, reduced agent functionality, etc). Do not use if logging that information will create a performance problem (say, due to excessive logging).
        /// </summary>
        public void WarnFormat(string format, params object[] args)
        {
            if (IsWarnEnabled)
                _logger.Warning(format, args);
        }

        #endregion Warn

        #region Info

        /// <summary>
        /// True iff logging has been configured to include INFO level logs.
        /// </summary>
        public bool IsInfoEnabled => _logger.IsEnabled(LogEventLevel.Information);

        /// <summary>
        /// Logs <paramref name="message"/> at the INFO level. This log level should be used for information for non-error information that may be of interest to the user, such as a the agent noticing a configuration change, or an infrequent "heartbeat". Do not use if logging that information will create a performance problem (say, due to excessive logging).
        /// </summary>
        public void Info(string message)
        {
            _logger.Information(message);
        }

        /// <summary>
        /// Logs <paramref name="exception"/> at the INFO level by calling exception.ToString(). This log level should be used for information for non-error information that may be of interest to the user, such as a the agent noticing a configuration change, or an infrequent "heartbeat". Do not use if logging that information will create a performance problem (say, due to excessive logging).
        /// </summary>
        public void Info(Exception exception)
        {
            _logger.Information(exception, "");
        }

        /// <summary>
        /// Logs a message at the INFO level by calling String.Format(<paramref name="format"/>, <paramref name="args"/>). This log level should be used for information for non-error information that may be of interest to the user, such as a the agent noticing a configuration change, or an infrequent "heartbeat". Do not use if logging that information will create a performance problem (say, due to excessive logging).
        /// </summary>
        public void InfoFormat(string format, params object[] args)
        {
            if (IsInfoEnabled)
                _logger.Information(format, args);
        }

        #endregion Info

        #region Debug

        /// <summary>
        /// True iff logging has been configured to include DEBUG level logs.
        /// </summary>
        public bool IsDebugEnabled => _logger.IsEnabled(LogEventLevel.Debug);

        /// <summary>
        /// Logs <paramref name="message"/> at the DEBUG level. This log level should be used for information that is non-critical and used mainly for troubleshooting common problems such as RUM injection or SQL explain plans. This level is not enabled by default so there is less concern about performance, but this level still should not be used for any logging that would cause significant performance, such as logging every transaction name for every transaction.
        /// </summary>
        public void Debug(string message)
        {
            _logger.Debug(message);
        }

        /// <summary>
        /// Logs <paramref name="exception"/> at the DEBUG level by calling exception.ToString(). This log level should be used for information that is non-critical and used mainly for troubleshooting common problems such as RUM injection or SQL explain plans. This level is not enabled by default so there is less concern about performance, but this level still should not be used for any logging that would cause significant performance, such as logging every transaction name for every transaction.
        /// </summary>
        public void Debug(Exception exception)
        {
            _logger.Debug(exception, "");
        }

        /// <summary>
        /// Logs a message at the DEBUG level by calling String.Format(<paramref name="format"/>, <paramref name="args"/>). This log level should be used for information that is non-critical and used mainly for troubleshooting common problems such as RUM injection or SQL explain plans. This level is not enabled by default so there is less concern about performance, but this level still should not be used for any logging that would cause significant performance, such as logging every transaction name for every transaction.
        /// </summary>
        public void DebugFormat(string format, params object[] args)
        {
            if (IsDebugEnabled)
                _logger.Debug(format, args);
        }

        #endregion Debug

        #region Finest

        /// <summary>
        /// True iff logging has been configured to include FINEST level logs.
        /// </summary>
        public bool IsFinestEnabled => _logger.IsEnabled(LogEventLevel.Verbose);

        /// <summary>
        /// Logs <paramref name="message"/> at the FINEST level. This log level should be used as a last resort for information that would otherwise be too expensive or too noisy to log at DEBUG level, such as logging every transaction name for every transaction. Useful for troubleshooting subtle problems like WCF's dual transactions.
        /// </summary>
        public void Finest(string message)
        {
            _logger.Verbose(message);
        }

        /// <summary>
        /// Logs <paramref name="exception"/> at the FINEST level by calling exception.ToString(). This log level should be used as a last resort for information that would otherwise be too expensive or too noisy to log at DEBUG level, such as logging every transaction name for every transaction. Useful for troubleshooting subtle problems like WCF's dual transactions.
        /// </summary>
        public void Finest(Exception exception)
        {
            _logger.Verbose(exception, "");
        }

        /// <summary>
        /// Logs a message at the FINEST level by calling String.Format(<paramref name="format"/>, <paramref name="args"/>). This log level should be used as a last resort for information that would otherwise be too expensive or too noisy to log at DEBUG level, such as logging every transaction name for every transaction. Useful for troubleshooting subtle problems like WCF's dual transactions.
        /// </summary>
        public void FinestFormat(string format, params object[] args)
        {
            if (IsFinestEnabled)
                _logger.Verbose(format, args);
        }

        #endregion Finest

    }
}

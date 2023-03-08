using System;
using Serilog.Events;

namespace NewRelic.Agent.Core
{
    internal static class LogLevelExtensions
    {
        /// <summary>
        /// Map a configfile loglevel to the equivalent Serilog loglevel </summary>
        /// <param name="configLogLevel"></param>
        /// <returns></returns>
        public static LogEventLevel MapToSerilogLogLevel(this string configLogLevel)
        {
            switch (configLogLevel.ToUpper())
            {
                case "FINEST":
                    return LogEventLevel.Verbose;
                case "DEBUG":
                    return LogEventLevel.Debug;
                case "INFO":
                    return LogEventLevel.Information;
                case "WARN": 
                    return LogEventLevel.Warning;
                default:
                    // TODO: Add checking for deprecated log levels ??
                    Serilog.Log.Logger.Warning($"Invalid log level '{configLogLevel}' specified. Using log level 'Info' by default.");
                    return LogEventLevel.Information;
            }
        }

        /// <summary>
        /// Translates Serilog log level to the "legacy" Log4Net levels to ensure log file consistency
        /// </summary>
        /// <param name="logEventLevel"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public static string TranslateLogLevel(this LogEventLevel logEventLevel)
        {
            switch (logEventLevel)
            {
                case LogEventLevel.Verbose:
                    return "FINEST";
                case LogEventLevel.Debug:
                    return "DEBUG";
                case LogEventLevel.Information:
                    return "INFO";
                case LogEventLevel.Warning:
                    return "WARN";
                case LogEventLevel.Error:
                    return "ERR";
                default:
                    throw new ArgumentOutOfRangeException(nameof(logEventLevel), logEventLevel, null);
            }
        }

    }
}
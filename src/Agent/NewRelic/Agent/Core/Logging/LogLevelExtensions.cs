// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using Serilog.Events;

namespace NewRelic.Agent.Core
{
    public static class LogLevelExtensions
    {
        private static readonly List<string> DeprecatedLogLevels = new List<string>() { "Alert", "Critical", "Emergency", "Fatal", "Finer", "Trace", "Notice", "Severe", "Verbose", "Fine" };
        public static bool IsLogLevelDeprecated(this string level) => DeprecatedLogLevels.Any(l => l.Equals(level, StringComparison.InvariantCultureIgnoreCase));

        /// <summary>
        /// Gets a string identifying the Audit log level
        /// </summary>
        public const string AuditLevel = "Audit";

        /// <summary>
        /// Map a configfile loglevel to the equivalent Serilog loglevel. Includes mappings
        /// for all of the deprecated loglevels as well.</summary>
        /// <param name="configLogLevel"></param>
        /// <returns></returns>
        public static LogEventLevel MapToSerilogLogLevel(this string configLogLevel)
        {
            if (configLogLevel?.IsLogLevelDeprecated() ?? false)
            {
                Serilog.Log.Logger.Warning($"The log level, {configLogLevel}, set in your configuration file has been deprecated. The agent will still log correctly, but you should change to a supported logging level as described in newrelic.config or the online documentation.");
            }

            switch (configLogLevel?.ToUpper())
            {
                case "VERBOSE":
                case "FINE":
                case "FINER":
                case "FINEST":
                case "TRACE":
                case "ALL":
                    return LogEventLevel.Verbose;
                case "DEBUG":
                    return LogEventLevel.Debug;
                case "INFO":
                case "NOTICE":
                    return LogEventLevel.Information;
                case "WARN":
                case "ALERT":
                    return LogEventLevel.Warning;
                case "ERROR":
                case "CRITICAL":
                case "EMERGENCY":
                case "FATAL":
                case "SEVERE":
                    return LogEventLevel.Error;
                case "OFF":
                    // moderately hack-ish, but setting the level to something higher than Fatal disables logs as per https://stackoverflow.com/a/30864356/2078975
                    return (LogEventLevel)1 + (int)LogEventLevel.Fatal;
                case "AUDIT":
                    Serilog.Log.Logger.Warning("Log level was set to \"Audit\" which is not a valid log level. To enable audit logging, set the auditLog configuration option to true. Log level will be treated as INFO for this run.");
                    return LogEventLevel.Information;
                default:
                    Serilog.Log.Logger.Warning($"Invalid log level '{configLogLevel}' specified. Using log level 'Info' by default.");
                    return LogEventLevel.Information;
            }
        }

        /// <summary>
        /// Translates Serilog log level to the "legacy" Log4Net levels to ensure log file format consistency
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
                    return "ERROR";
                case LogEventLevel.Fatal: // Fatal is the level we use for Audit log messages
                    return AuditLevel;
                default:
                    throw new ArgumentOutOfRangeException(nameof(logEventLevel), logEventLevel, null);
            }
        }

    }
}

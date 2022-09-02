// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.IntegrationTests.Logging
{
    public enum LoggingFramework
    {
        Log4net,
        Serilog,
        MicrosoftLogging,
        SerilogWeb,
        NLog
    }

    public class LogUtils
    {
        public static string GetFrameworkName(LoggingFramework loggingFramework)
        {
            switch (loggingFramework)
            {
                case LoggingFramework.Log4net:
                    return "log4net";
                case LoggingFramework.MicrosoftLogging:
                    return "MicrosoftLogging";
                case LoggingFramework.SerilogWeb:
                case LoggingFramework.Serilog:
                    return "serilog";
                case LoggingFramework.NLog:
                    return "nlog";
                default:
                    return "unknown";
            }
        }

        public static string GetLevelName(LoggingFramework loggingFramework, string level)
        {
            switch (loggingFramework)
            {
                // log4net names are the same as our internal names
                case LoggingFramework.Log4net:
                    return level;
                case LoggingFramework.MicrosoftLogging:
                    switch (level)
                    {
                        case "DEBUG":
                            return "DEBUG";
                        case "INFO":
                            return "INFORMATION";
                        case "WARN":
                            return "WARNING";
                        case "ERROR":
                            return "ERROR";
                        case "FATAL":
                            return "CRITICAL";
                        default:
                            return level;
                    }
                case LoggingFramework.SerilogWeb:
                case LoggingFramework.Serilog:
                    switch (level)
                    {
                        case "DEBUG":
                            return "DEBUG";
                        case "INFO":
                            return "INFORMATION";
                        case "WARN":
                            return "WARNING";
                        case "ERROR":
                            return "ERROR";
                        case "FATAL":
                            return "FATAL";
                        default:
                            return level;
                    }
                case LoggingFramework.NLog:
                    return level;
            }

            return string.Empty;
        }
    }
}

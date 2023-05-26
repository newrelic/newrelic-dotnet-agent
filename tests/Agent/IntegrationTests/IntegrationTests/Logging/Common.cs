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
        NLog,
        DummyMEL,
        Sitecore
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
                case LoggingFramework.DummyMEL:
                    return "DummyMEL";
                case LoggingFramework.Sitecore:
                    return "sitecore";
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
                case LoggingFramework.Sitecore:
                    switch (level)
                    {
                        case "NOMESSAGE":
                            return "ERROR";
                        default:
                            return level;
                    }
                case LoggingFramework.MicrosoftLogging:
                case LoggingFramework.DummyMEL:
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
                        case "NOMESSAGE":
                            return "ERROR";
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
                        case "NOMESSAGE":
                            return "ERROR";
                        default:
                            return level;
                    }
                case LoggingFramework.NLog:
                    switch (level)
                    {
                        case "NOMESSAGE":
                            return "ERROR";
                        default:
                            return level;
                    }
            }

            return string.Empty;
        }
    }
}

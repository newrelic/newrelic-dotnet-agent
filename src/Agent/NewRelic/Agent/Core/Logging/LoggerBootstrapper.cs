// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Core.Config;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting;
using Logger = NewRelic.Agent.Core.Logging.Logger;

namespace NewRelic.Agent.Core
{
    public static class LoggerBootstrapper
    {

        /// <summary>
        /// The name of the event log to log to.
        /// </summary>
#pragma warning disable CS0414
        private static readonly string EventLogName = "Application";
#pragma warning restore CS0414

        /// <summary>
        /// The event source name.
        /// </summary>
#pragma warning disable CS0414
        private static readonly string EventLogSourceName = "New Relic .NET Agent";
#pragma warning restore CS0414

        ///// <summary>
        ///// The numeric level of the Audit log.
        ///// </summary>
        //private static int AuditLogLevel = 150000;

        /// <summary>
        /// The string name of the Audit log level.
        /// </summary>
        private static string AuditLogLevelName = "Audit";

        // Watch out!  If you change the time format that the agent puts into its log files, other log parsers may fail.
        //private static ILayout AuditLogLayout = new PatternLayout("%utcdate{yyyy-MM-dd HH:mm:ss,fff} NewRelic %level: %message\r\n");
        //private static ILayout FileLogLayout = new PatternLayout("%utcdate{yyyy-MM-dd HH:mm:ss,fff} NewRelic %6level: [pid: %property{pid}, tid: %property{threadid}] %message\r\n");


        private static LoggingLevelSwitch _loggingLevelSwitch = new LoggingLevelSwitch();

        public static void UpdateLoggingLevel(string newLogLevel)
        {
            _loggingLevelSwitch.MinimumLevel = newLogLevel.MapToSerilogLogLevel();
        }

        public static void Initialize()
        {
            var startupLoggerConfig = new LoggerConfiguration()
                .Enrich.With(new ThreadIdEnricher())
                .Enrich.With(new ProcessIdEnricher())
                .MinimumLevel.Information()
                .ConfigureInMemoryLogSink()
                // TODO: implement event log sink
                //.ConfigureEventLogSink()
                // TODO: Remove console log sink when in-memory sink is implemented
                .ConfigureConsoleSink();

            // set the global Serilog logger to our startup logger instance, this gets replaced when ConfigureLogger() is called
            Serilog.Log.Logger = startupLoggerConfig.CreateLogger();
        }

        /// <summary>
        /// Configures the agent logger.
        /// </summary>
        /// <remarks>This should only be called once, as soon as you have a valid config.</remarks>
        public static void ConfigureLogger(ILogConfig config)
        {
            SetupLogLevel(config);

            var loggerConfig = new LoggerConfiguration()
                .Enrich.With(new ThreadIdEnricher())
                .Enrich.With(new ProcessIdEnricher())
                .MinimumLevel.ControlledBy(_loggingLevelSwitch)
                .ConfigureFileSink(config)
                .ConfigureAuditLogSink(config)
                .ConfigureDebugSink();

            if (config.Console)
            {
                loggerConfig = loggerConfig.ConfigureConsoleSink();
            }

            var startupLogger = Serilog.Log.Logger;

            // configure the global singleton logger instance (which remains specific to the Agent by way of ILRepack)
            var configuredLogger = loggerConfig.CreateLogger();

            EchoInMemoryLogsToConfiguredLogger(configuredLogger);

            Serilog.Log.Logger = configuredLogger;

            NewRelic.Core.Logging.Log.Initialize(new Logger());
        }

        /// <summary>
        /// Gets a string identifying the Audit log level
        /// </summary>
        public static string GetAuditLevel() => AuditLogLevelName;

        private static void EchoInMemoryLogsToConfiguredLogger(Serilog.ILogger configuredLogger)
        {
            // TODO: copy logs from inMemory logger and emit them to Serilog.Log.Logger
            // possible example:
            //foreach (LogEvent logEvent in InMemorySink.Instance.LogEvents)
            //{
            //    configuredLogger.Write(logEvent.Level, logEvent.Exception, logEvent.MessageTemplate.Render(logEvent.Properties));
            //}
            //InMemorySink.Instance.Dispose();
        }

        /// <summary>
        /// Sets the log level for logger to either the level provided by the config or an public default.
        /// </summary>
        /// <param name="config">The LogConfig to look for the level setting in.</param>
        private static void SetupLogLevel(ILogConfig config)
        {
            _loggingLevelSwitch.MinimumLevel = config.LogLevel.MapToSerilogLogLevel();
        }

        // TODO: implement but don't use log4net log levels enum
        //private static bool IsLogLevelDeprecated(Level level)
        //{
        //    foreach (var l in DeprecatedLogLevels)
        //    {
        //        if (l.Name.Equals(level.Name, StringComparison.InvariantCultureIgnoreCase)) return true;
        //    }
        //    return false;
        //}

        private static LoggerConfiguration ConfigureInMemoryLogSink(this LoggerConfiguration loggerConfiguration)
        {
            // TODO Configure the (yet-to-be-implemented) in-memory sink

            return loggerConfiguration;
        }

        // TODO: Implement EventLog support, see commented package reference in Core.csproj
        ///// <summary>
        ///// Add the Event Log sink if running on Windows
        ///// </summary>
        ///// <param name="loggerConfiguration"></param>
        //private static LoggerConfiguration ConfigureEventLogSink(this LoggerConfiguration loggerConfiguration)
        //{
        //    if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        //        return loggerConfiguration;

        //    loggerConfiguration
        //        .WriteTo.Logger(configuration =>
        //        {
        //            ExcludeAuditLog(configuration);
        //            configuration
        //                .WriteTo.EventLog(
        //                    source: EventLogSourceName,
        //                    logName: EventLogName,
        //                    restrictedToMinimumLevel: LogEventLevel.Warning
        //                );
        //        });

        //    return loggerConfiguration;
        //}

        /// <summary>
        /// Configure the debug sink
        /// </summary>
        private static LoggerConfiguration ConfigureDebugSink(this LoggerConfiguration loggerConfiguration)
        {
#if DEBUG
            loggerConfiguration
                .WriteTo.Logger(configuration =>
                {
                    configuration
                        .ExcludeAuditLog()
                        .WriteTo.Debug(formatter: new CustomTextFormatter());
                });
#endif
            return loggerConfiguration;
        }

        /// <summary>
        /// Configure the console sink
        /// </summary>
        private static LoggerConfiguration ConfigureConsoleSink(this LoggerConfiguration loggerConfiguration)
        {
            return loggerConfiguration
                .WriteTo.Logger(configuration =>
                {
                    configuration
                        .ExcludeAuditLog()
                        .WriteTo.Console(formatter: new CustomTextFormatter());
                });
        }

        /// <summary>
        /// Configure the file log sink
        /// </summary>
        /// <param name="loggerConfiguration"></param>
        /// <param name="config">The configuration for the appender.</param>
        private static LoggerConfiguration ConfigureFileSink(this LoggerConfiguration loggerConfiguration, ILogConfig config)
        {
            string logFileName = config.GetFullLogFileName();

            try
            {
                loggerConfiguration
                    .WriteTo
                    .Logger(configuration =>
                    {
                        configuration
                            .ExcludeAuditLog()
                            .ConfigureRollingLogSink(logFileName, new CustomTextFormatter());
                    });
            }
            catch (Exception)
            {
                // TODO uncomment when EventLogSink is supported
                //// Fallback to the event log sink if we cannot setup a file logger.
                //loggerConfiguration.ConfigureEventLogSink();
            }

            return loggerConfiguration;
        }

        /// <summary>
        /// Setup the audit log file appender and attach it to a logger.
        /// </summary>
        private static LoggerConfiguration ConfigureAuditLogSink(this LoggerConfiguration loggerConfiguration, ILogConfig config)
        {
            if (!config.IsAuditLogEnabled) return loggerConfiguration;

            string logFileName = config.GetFullLogFileName().Replace(".log", "_audit.log");

            return loggerConfiguration
                .WriteTo
                .Logger(configuration =>
                {
                    configuration
                        .MinimumLevel.Fatal()
                        .IncludeOnlyAuditLog()
                        .ConfigureRollingLogSink(logFileName, new CustomAuditLogTextFormatter());
                });
        }

        /// <summary>
        /// Sets up a rolling file appender using defaults shared for all our rolling file appenders.
        /// </summary>
        /// <param name="loggerConfiguration"></param>
        /// <param name="fileName">The name of the file this appender will write to.</param>
        /// <param name="textFormatter"></param>
        /// <remarks>This does not call appender.ActivateOptions or add the appender to the logger.</remarks>
        private static LoggerConfiguration ConfigureRollingLogSink(this LoggerConfiguration loggerConfiguration, string fileName, ITextFormatter textFormatter)
        {
            // check that the log file is accessible
            try
            {
                // Create the directory if necessary
                var directory = Path.GetDirectoryName(fileName);
                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory);
                using (File.Open(fileName, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Write)) { }
            }
            catch (Exception exception)
            {
                Serilog.Log.Logger.Warning(exception, $"Unable to write logfile at \"{fileName}\"", fileName);
                throw;
            }

            try
            {
                return loggerConfiguration
                    .WriteTo.File(
                        path: fileName,
                        formatter: textFormatter,
                        fileSizeLimitBytes: 50 * 1024 * 1024,
                        encoding: Encoding.UTF8,
                        rollOnFileSizeLimit: true,
                        retainedFileCountLimit: 4,
                        buffered: false
                    );
            }
            catch (Exception exception)
            {
                Serilog.Log.Logger.Error(exception, $"Unable to configure file logging for \"{fileName}\"");
                throw;
            }
        }

        private static LoggerConfiguration IncludeOnlyAuditLog(this LoggerConfiguration loggerConfiguration)
        {
            return loggerConfiguration.Filter.ByIncludingOnly(logEvent =>
                logEvent.Properties.ContainsKey(AuditLogLevelName));

        }
        private static LoggerConfiguration ExcludeAuditLog(this LoggerConfiguration loggerConfiguration)
        {
            return loggerConfiguration.Filter.ByExcluding(logEvent =>
                logEvent.Properties.ContainsKey(AuditLogLevelName));

        }

    }
}

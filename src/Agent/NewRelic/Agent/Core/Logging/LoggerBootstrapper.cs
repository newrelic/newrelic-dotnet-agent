// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Core.Config;
using System;
using System.IO;
using System.Text;
using Serilog;
using Serilog.Core;
using Logger = NewRelic.Agent.Core.Logging.Logger;
using NewRelic.Agent.Core.Logging;
using Serilog.Events;
#if NETSTANDARD2_0
using System.Runtime.InteropServices;
#endif

namespace NewRelic.Agent.Core
{
    public static class LoggerBootstrapper
    {

        // Watch out!  If you change the time format that the agent puts into its log files, other log parsers may fail.
        //private static ILayout AuditLogLayout = new PatternLayout("%utcdate{yyyy-MM-dd HH:mm:ss,fff} NewRelic %level: %message\r\n");
        //private static ILayout FileLogLayout = new PatternLayout("%utcdate{yyyy-MM-dd HH:mm:ss,fff} NewRelic %6level: [pid: %property{pid}, tid: %property{threadid}] %message\r\n");

        private const string AuditLogLayout = "{UTCTimestamp} NewRelic Audit: {Message:l}\n";

        private const string FileLogLayout = "{UTCTimestamp} NewRelic {NRLogLevel,6}: [pid: {pid}, tid: {tid}] {Message:l}\n{Exception:l}";

        private static LoggingLevelSwitch _loggingLevelSwitch = new LoggingLevelSwitch();

        private static InMemorySink _inMemorySink = new InMemorySink();

        public static void UpdateLoggingLevel(string newLogLevel)
        {
            _loggingLevelSwitch.MinimumLevel = newLogLevel.MapToSerilogLogLevel();
        }

        public static void Initialize()
        {
            var startupLoggerConfig = new LoggerConfiguration()
                .Enrich.With(new ThreadIdEnricher(), new ProcessIdEnricher(), new NrLogLevelEnricher(), new UTCTimestampEnricher())
                .MinimumLevel.Information()
                .ConfigureInMemoryLogSink()
                .ConfigureEventLogSink();

            // set the global Serilog logger to our startup logger instance, this gets replaced when ConfigureLogger() is called
            Log.Logger = startupLoggerConfig.CreateLogger();
        }

        /// <summary>
        /// Configures the agent logger.
        /// </summary>
        /// <remarks>This should only be called once, as soon as you have a valid config.</remarks>
        public static void ConfigureLogger(ILogConfig config)
        {
            SetupLogLevel(config);

            AuditLog.IsAuditLogEnabled = config.IsAuditLogEnabled;

            var loggerConfig = new LoggerConfiguration()
                .MinimumLevel.ControlledBy(_loggingLevelSwitch)
                .Enrich.With(new ThreadIdEnricher(), new ProcessIdEnricher(), new NrLogLevelEnricher(), new UTCTimestampEnricher())
                .ConfigureAuditLogSink(config)
                .ConfigureFileSink(config)
                .ConfigureDebugSink();

            if (config.Console)
            {
                loggerConfig = loggerConfig.ConfigureConsoleSink();
            }

            // configure the global singleton logger instance (which remains specific to the Agent by way of ILRepack)
            var configuredLogger = loggerConfig.CreateLogger();

            EchoInMemoryLogsToConfiguredLogger(configuredLogger);

            Log.Logger = configuredLogger;

            NewRelic.Core.Logging.Log.Initialize(new Logger());
        }

        private static void EchoInMemoryLogsToConfiguredLogger(ILogger configuredLogger)
        {
            foreach (var logEvent in _inMemorySink.LogEvents)
            {
                configuredLogger.Write(logEvent);
            }

            _inMemorySink.Dispose();
        }

        /// <summary>
        /// Sets the log level for logger to either the level provided by the config or an public default.
        /// </summary>
        /// <param name="config">The LogConfig to look for the level setting in.</param>
        private static void SetupLogLevel(ILogConfig config) => _loggingLevelSwitch.MinimumLevel = config.LogLevel.MapToSerilogLogLevel();

        private static LoggerConfiguration ConfigureInMemoryLogSink(this LoggerConfiguration loggerConfiguration)
        {
            // formatter not needed since this will be pushed to other sinks for output.
            return loggerConfiguration
                .WriteTo.Logger(configuration =>
                {
                    configuration
                        .ExcludeAuditLog()
                        .WriteTo.Sink(_inMemorySink);
                });
        }

        /// <summary>
        /// Configure an Event Log sink if running on Windows. Logs messages at Warning level and above. Intended for
        /// use during bootstrapping and as a fallback if the file logging sink can't be created.
        ///
        /// The Agent will create the event log source if it doesn't exist *and* if the app is running with
        /// administrator privileges. Otherwise, it will silently do nothing.
        ///
        /// It is possible to manually create the event log source in an elevated Powershell window:
        ///    New-EventLog -LogName "Application" -Source "New Relic .NET Agent"
        /// 
        /// </summary>
        /// <param name="loggerConfiguration"></param>
        private static LoggerConfiguration ConfigureEventLogSink(this LoggerConfiguration loggerConfiguration)
        {
#if NETSTANDARD2_0
            var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
#else
            var isWindows = true;
#endif
            if (isWindows)
            {
                const string eventLogName = "Application";
                const string eventLogSourceName = "New Relic .NET Agent";
                try
                {
                    loggerConfiguration
                            .WriteTo.Logger(configuration =>
                            {
                                configuration
                                .ExcludeAuditLog()
                                .WriteTo.EventLog(
                                    source: eventLogSourceName,
                                    logName: eventLogName,
                                    restrictedToMinimumLevel: LogEventLevel.Warning,
                                    outputTemplate: "{Level}: {Message}{NewLine}{Exception}",
                                    manageEventSource: true
                                );
                            });
                }
                catch
                {
                    // ignored -- there's nothing we can do at this point, as EventLog is our "fallback" logger and if it fails, we're out of luck
                }
            }
            return loggerConfiguration;
        }

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
                        .WriteTo.Debug(outputTemplate: FileLogLayout);
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
                .WriteTo.Async(a =>
                    a.Logger(configuration =>
                    {
                        configuration
                            .ExcludeAuditLog()
                            .WriteTo.Console(outputTemplate: FileLogLayout);
                    })
                );
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
                    .Async(a =>
                        a.Logger(configuration =>
                            {
                                configuration
                                    .ExcludeAuditLog()
                                    .ConfigureRollingLogSink(logFileName, FileLogLayout);
                            })
                        );
            }
            catch (Exception ex)
            {
                Log.Logger.Warning(ex, "Unexpected exception when configuring file sink.");

                // Fallback to the event log sink if we cannot setup a file logger.
                Log.Logger.Warning("Falling back to EventLog sink.");
                loggerConfiguration.ConfigureEventLogSink();
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
                        .MinimumLevel.Fatal() // We've hijacked Fatal log level as the level to use when writing an audit log
                        .IncludeOnlyAuditLog()
                        .ConfigureRollingLogSink(logFileName, AuditLogLayout);
                });
        }

        /// <summary>
        /// Sets up a rolling file appender using defaults shared for all our rolling file appenders.
        /// </summary>
        /// <param name="loggerConfiguration"></param>
        /// <param name="fileName">The name of the file this appender will write to.</param>
        /// <param name="outputFormat"></param>
        /// <remarks>This does not call appender.ActivateOptions or add the appender to the logger.</remarks>
        private static LoggerConfiguration ConfigureRollingLogSink(this LoggerConfiguration loggerConfiguration, string fileName, string outputFormat)
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
                Log.Logger.Warning(exception, $"Unable to write logfile at \"{fileName}\"");
                throw;
            }

            try
            {
                return loggerConfiguration
                    .WriteTo
                    .File(path: fileName,
                            outputTemplate:outputFormat,
                            fileSizeLimitBytes: 50 * 1024 * 1024,
                            encoding: Encoding.UTF8,
                            rollOnFileSizeLimit: true,
                            retainedFileCountLimit: 4, // TODO: Will make configurable
                            shared: true,
                            buffered: false
                            );
            }
            catch (Exception exception)
            {
                Log.Logger.Warning(exception, $"Unexpected exception while configuring file logging for \"{fileName}\"");
                throw;
            }
        }
    }
}

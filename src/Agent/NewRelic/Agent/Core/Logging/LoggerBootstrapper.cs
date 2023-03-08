// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Core.Config;
using System;
using System.IO;
using System.Text;
#if NETSTANDARD2_0
using System.Runtime.InteropServices;
#endif
using Serilog;
using Serilog.Core;
using Log = NewRelic.Core.Logging.Log;
using Serilog.Events;
using Serilog.Formatting;
using Logger = NewRelic.Agent.Core.Logging.Logger;

namespace NewRelic.Agent.Core
{
    public static class LoggerBootstrapper
    {

        ///// <summary>
        ///// The name of the Audit log appender.
        ///// </summary>
        //private static readonly string AuditLogAppenderName = "AuditLog";

        ///// <summary>
        ///// The name of the Console log appender.
        ///// </summary>
        //private static readonly string ConsoleLogAppenderName = "ConsoleLog";

  //      /// <summary>
  //      /// The name of the temporary event log appender.
  //      /// </summary>
  //      private static readonly string TemporaryEventLogAppenderName = "TemporaryEventLog";

		///// <summary>
		///// The name of the event log appender.
		///// </summary>
		//private static readonly string EventLogAppenderName = "EventLog";

		/// <summary>
		/// The name of the event log to log to.
		/// </summary>
		private static readonly string EventLogName = "Application";

		/// <summary>
		/// The event source name.
		/// </summary>
		private static readonly string EventLogSourceName = "New Relic .NET Agent";

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


        //private static ILayout eventLoggerLayout = new PatternLayout("%level: %message");

        //private static string STARTUP_APPENDER_NAME = "NEWRELIC_DOTNET_AGENT_STARTUP_APPENDER";

        //private static List<Level> DeprecatedLogLevels = new List<Level>() { Level.Alert, Level.Critical, Level.Emergency, Level.Fatal, Level.Finer, Level.Trace, Level.Notice, Level.Severe, Level.Verbose, Level.Fine };

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
                .ConfigureConsoleSink()
                .ConfigureEventLogSink();

// TODO: MAT Figure out how to log to memory during startup, for now logs to console only

            // set the global Serilog logger to our startup logger instance, this gets replaced when ConfigureLogger() is called
            Serilog.Log.Logger = startupLoggerConfig.CreateLogger();


            //var hierarchy = log4net.LogManager.GetRepository(Assembly.GetCallingAssembly()) as log4net.Repository.Hierarchy.Hierarchy;
            //var logger = hierarchy.Root;

            // initially we will log to console and event log so it should only log items that need action
            //logger.Level = Level.Info;

            //GlobalContext.Properties["pid"] = new ProcessStatic().GetCurrentProcess().Id;

            //SetupStartupLogAppender(logger);
            //SetupConsoleLogAppender(logger);
            //SetupTemporaryEventLogAppender(logger);
        }

        /// <summary>
        /// Configures the agent logger.
        /// </summary>
        /// <remarks>This should only be called once, as soon as you have a valid config.</remarks>
        public static void ConfigureLogger(ILogConfig config)
        {
            var loggerConfig = new LoggerConfiguration()
                .Enrich.With(new ThreadIdEnricher())
                .Enrich.With(new ProcessIdEnricher())
                .SetupLogLevel(config)
                .ConfigureFileSink(config)
                .ConfigureAuditLogSink(config)
                .ConfigureDebugSink();

            if (config.Console)
            {
                loggerConfig = loggerConfig.ConfigureConsoleSink();
            }

            // TODO: get the current global logger (which is the startup logger, should be logging to memory)
            //var startupLogger = Serilog.Log.Logger;

            // configure the global singleton logger instance (which remains specific to the Agent by way of ILRepack)
            Serilog.Log.Logger = loggerConfig.CreateLogger();

            // TODO: figure out how extract the memory-based logger data from startup logger and inject it into Log.Logger
            // We have now bootstrapped the agent logger, so
            // remove the startup appender, then send its messages 
            // to the agent logger, which will get picked up by 
            // the other appenders.
            //ShutdownStartupLogAppender(logger);

            Log.Initialize(new Logger());
        }

        /// <summary>
        /// Gets a string identifying the Audit log level
        /// </summary>
        public static string GetAuditLevel() => AuditLogLevelName;

        //private static void ShutdownStartupLogAppender(log4netLogger logger)
        //{
        //    var startupAppender = logger.GetAppender(STARTUP_APPENDER_NAME) as MemoryAppender;
        //    if (startupAppender != null)
        //    {
        //        LoggingEvent[] events = startupAppender.GetEvents();
        //        logger.RemoveAppender(startupAppender);

        //        if (events != null)
        //        {
        //            foreach (LoggingEvent logEvent in events)
        //            {
        //                logger.Log(logEvent.Level, logEvent.MessageObject, null);
        //            }
        //        }
        //    }
        //}

        /// <summary>
        /// Sets the log level for logger to either the level provided by the config or an public default.
        /// </summary>
        /// <param name="loggerConfiguration"></param>
        /// <param name="config">The LogConfig to look for the level setting in.</param>
        private static LoggerConfiguration SetupLogLevel(this LoggerConfiguration loggerConfiguration, ILogConfig config)
        {
            _loggingLevelSwitch.MinimumLevel = config.LogLevel.MapToSerilogLogLevel();

            return loggerConfiguration.MinimumLevel.ControlledBy(_loggingLevelSwitch);
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

        ///// <summary>
        ///// A memory appender for logging to memory during startup. Log messages will be re-logged after configuration is loaded.
        ///// </summary>
        ///// <param name="logger"></param>
        //private static void SetupStartupLogAppender(log4netLogger logger)
        //{
        //    var startupAppender = new MemoryAppender();
        //    startupAppender.Name = STARTUP_APPENDER_NAME;
        //    startupAppender.Layout = LoggerBootstrapper.FileLogLayout;
        //    startupAppender.ActivateOptions();

        //    logger.AddAppender(startupAppender);
        //    logger.Repository.Configured = true;
        //}

        /// <summary>
        /// Add the Event Log sink if running on Windows
        /// </summary>
        /// <param name="loggerConfiguration"></param>
        private static LoggerConfiguration ConfigureEventLogSink(this LoggerConfiguration loggerConfiguration)
        {
            var addEventLogSink = true;

#if NETSTANDARD2_0
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                addEventLogSink = false;
            }
#endif

            if (addEventLogSink)
            {
                loggerConfiguration
                    .WriteTo.Logger(configuration =>
                    {
                        ExcludeAuditLog(configuration);

                        configuration
                            .WriteTo.EventLog(
                                source: EventLogSourceName,
                                logName: EventLogName,
                                restrictedToMinimumLevel: LogEventLevel.Warning
                            );
                    });
            }

            return loggerConfiguration;

//#if NETFRAMEWORK
			//var appender = new EventLogAppender();
			//appender.Layout = eventLoggerLayout;
			//appender.Threshold = Level.Warn;
			//appender.Name = EventLogAppenderName;
			//appender.LogName = EventLogName;
			//appender.ApplicationName = EventLogSourceName;
			//appender.AddFilter(GetNoAuditFilter());
			//appender.ActivateOptions();

			//logger.AddAppender(appender);
//#endif
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
                        .WriteTo.Debug(formatter: new CustomTextFormatter());

                });

            //// Create the debug appender and connect it to our logger.
            //var debugAppender = new DebugAppender();
            //debugAppender.Layout = FileLogLayout;
            //debugAppender.AddFilter(GetNoAuditFilter());
            //logger.AddAppender(debugAppender);
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


            //var appender = new ConsoleAppender();
            //appender.Name = ConsoleLogAppenderName;
            //appender.Layout = FileLogLayout;
            //appender.AddFilter(GetNoAuditFilter());
            //appender.ActivateOptions();
            //logger.AddAppender(appender);
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
                // configure a sub-logger for the audit log file - will only include messages at the 
                loggerConfiguration
                    .WriteTo
                    .Logger(configuration =>
                    {
                        ExcludeAuditLog(configuration);

                        ConfigureRollingLogSink(configuration, logFileName, new CustomTextFormatter());
                    });
            }
            catch (Exception)
            {
                // Fallback to the event log sink if we cannot setup a file logger.
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

            // configure a sub-logger for the audit log file - will only include messages at the 
            return loggerConfiguration
                .WriteTo
                .Logger(configuration =>
                {
                    configuration
                        .IncludeOnlyAuditLog()
                        .ConfigureRollingLogSink(logFileName, new CustomAuditLogTextFormatter());
                });

            //try
            //{
            //    var appender = SetupRollingFileAppender(config, logFileName, AuditLogAppenderName);
            //    appender.AddFilter(GetAuditFilter());
            //    appender.AddFilter(new DenyAllFilter());
            //    appender.ActivateOptions();
            //    logger.AddAppender(appender);
            //}
            //catch (Exception)
            //{ }
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

                //var appender = new RollingFileAppender();

                //appender.LockingModel = GetFileLockingModel(config);
                //appender.Layout = layout;
                //appender.File = fileName;
                //appender.Encoding = System.Text.Encoding.UTF8;
                //appender.AppendToFile = true;
                //appender.RollingStyle = RollingFileAppender.RollingMode.Size;
                //appender.MaxSizeRollBackups = 4;
                //appender.MaxFileSize = 50 * 1024 * 1024; // 50MB
                //appender.StaticLogFileName = true;
                //appender.ImmediateFlush = true;

                //return appender;
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

// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using log4net;
using log4net.Appender;
using log4net.Core;
using log4net.Filter;
using log4net.Layout;
using NewRelic.Agent.Core.Config;
using NewRelic.Agent.Core.Logging;
using NewRelic.Core.Logging;
using NewRelic.SystemInterfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using log4netLogger = log4net.Repository.Hierarchy.Logger;

namespace NewRelic.Agent.Core
{
    public static class LoggerBootstrapper
    {

        /// <summary>
        /// The name of the Audit log appender.
        /// </summary>
        private static readonly string AuditLogAppenderName = "AuditLog";

        /// <summary>
        /// The name of the Console log appender.
        /// </summary>
        private static readonly string ConsoleLogAppenderName = "ConsoleLog";

        /// <summary>
        /// The name of the temporary event log appender.
        /// </summary>
        private static readonly string TemporaryEventLogAppenderName = "TemporaryEventLog";

#if NET45
		/// <summary>
		/// The name of the event log appender.
		/// </summary>
		private static readonly string EventLogAppenderName = "EventLog";

		/// <summary>
		/// The name of the event log to log to.
		/// </summary>
		private static readonly string EventLogName = "Application";

		/// <summary>
		/// The event source name.
		/// </summary>
		private static readonly string EventLogSourceName = "New Relic .NET Agent";
#endif

        /// <summary>
        /// The numeric level of the Audit log.
        /// </summary>
        private static int AuditLogLevel = 150000;

        /// <summary>
        /// The string name of the Audit log.
        /// </summary>
        private static string AuditLogName = "Audit";

        // Watch out!  If you change the time format that the agent puts into its log files, other log parsers may fail.
        // See, specifically, the orion IIS QA package, file lib/qa-tools-utils/dotnet_agent_log_parser.rb
        private static ILayout AuditLogLayout = new PatternLayout("%utcdate{yyyy-MM-dd HH:mm:ss,fff} NewRelic %level: %message\r\n");
        private static ILayout FileLogLayout = new PatternLayout("%utcdate{yyyy-MM-dd HH:mm:ss,fff} NewRelic %6level: [pid: %property{pid}, tid: %thread] %message\r\n");

        private static ILayout eventLoggerLayout = new PatternLayout("%level: %message");

        private static string STARTUP_APPENDER_NAME = "NEWRELIC_DOTNET_AGENT_STARTUP_APPENDER";

        private static List<Level> DeprecatedLogLevels = new List<Level>() { Level.Alert, Level.Critical, Level.Emergency, Level.Fatal, Level.Finer, Level.Trace, Level.Notice, Level.Severe, Level.Verbose, Level.Fine };

        public static void Initialize()
        {
            var hierarchy = log4net.LogManager.GetRepository(Assembly.GetCallingAssembly()) as log4net.Repository.Hierarchy.Hierarchy;
            var logger = hierarchy.Root;

            // initially we will log to console and event log so it should only log items that need action
            logger.Level = Level.Info;

            GlobalContext.Properties["pid"] = new ProcessStatic().GetCurrentProcess().Id;

            SetupStartupLogAppender(logger);
            SetupConsoleLogAppender(logger);
            SetupTemporaryEventLogAppender(logger);
        }

        /// <summary>
        /// Configures the agent logger.
        /// </summary>
        /// <param name="debug">
        /// A <see cref="bool"/>
        /// </param>
        /// <param name="config">
        /// A <see cref="ILogConfig"/>
        /// </param>
        /// <returns>
        /// A <see cref="ILogger"/>
        /// </returns>
        /// <remarks>This should only be called once, as soon as you have a valid config.</remarks>
        public static void ConfigureLogger(ILogConfig config)
        {
            CreateAuditLogLevel();

            var hierarchy = log4net.LogManager.GetRepository(Assembly.GetCallingAssembly()) as log4net.Repository.Hierarchy.Hierarchy;
            var logger = hierarchy.Root;

            SetupLogLevel(logger, config);

            SetupFileLogAppender(logger, config);
            SetupAuditLogger(logger, config);
            SetupDebugLogAppender(logger);
            logger.RemoveAppender(TemporaryEventLogAppenderName);
            if (!config.Console) logger.RemoveAppender(ConsoleLogAppenderName);

            logger.Repository.Configured = true;

            // We have now bootstrapped the agent logger, so
            // remove the startup appender, then send its messages 
            // to the agent logger, which will get picked up by 
            // the other appenders.
            ShutdownStartupLogAppender(logger);

            Log.Initialize(new Logger());
        }

        /// <summary>
        /// Gets the log4net Level of the "Audit" log level.
        /// </summary>
        /// <returns>The "Audit" log4net Level.</returns>
        public static Level GetAuditLevel()
        {
            return LogManager.GetRepository(Assembly.GetCallingAssembly()).LevelMap[AuditLogName];
        }

        private static void ShutdownStartupLogAppender(log4netLogger logger)
        {
            var startupAppender = logger.GetAppender(STARTUP_APPENDER_NAME) as MemoryAppender;
            if (startupAppender != null)
            {
                LoggingEvent[] events = startupAppender.GetEvents();
                logger.RemoveAppender(startupAppender);

                if (events != null)
                {
                    foreach (LoggingEvent logEvent in events)
                    {
                        logger.Log(logEvent.Level, logEvent.MessageObject, null);
                    }
                }
            }
        }

        private static FileAppender.LockingModelBase GetFileLockingModel(ILogConfig config)
        {
            if (config.FileLockingModelSpecified)
            {
                if (config.FileLockingModel.Equals(configurationLogFileLockingModel.minimal))
                {
                    return (FileAppender.LockingModelBase)new FileAppender.MinimalLock();
                }
                else
                {
                    return new FileAppender.ExclusiveLock();
                }

            }
            return new FileAppender.MinimalLock();
        }

        /// <summary>
        /// Creates a new AuditLogName log level at level AuditLogLevel (higher than Emergency log level) and registers it as a log4net level.
        /// </summary>
        private static void CreateAuditLogLevel()
        {
            Level auditLevel = new Level(AuditLogLevel, AuditLogName);
            LogManager.GetRepository(Assembly.GetCallingAssembly()).LevelMap.Add(auditLevel);
        }

        /// <summary>
        /// Returns a filter set to immediately deny any log events that are "Audit" level.
        /// </summary>
        /// <returns>A filter set to imediately deny any log events that are "Audit" level.</returns>
        private static IFilter GetNoAuditFilter()
        {
            LevelMatchFilter filter = new LevelMatchFilter();
            filter.LevelToMatch = GetAuditLevel();
            filter.AcceptOnMatch = false;
            return filter;
        }

        /// <summary>
        /// Returns a filter set to immediately accept any log events that are "Audit" level.
        /// </summary>
        /// <returns>A filter set to immediately accept any log events that are "Audit" level.</returns>
        private static IFilter GetAuditFilter()
        {
            LevelMatchFilter filter = new LevelMatchFilter();
            filter.LevelToMatch = GetAuditLevel();
            filter.AcceptOnMatch = true;
            return filter;
        }

        /// <summary>
        /// Sets the log level for logger to either the level provided by the config or an public default.
        /// </summary>
        /// <param name="logger">The logger to set the level of.</param>
        /// <param name="config">The LogConfig to look for the level setting in.</param>
        private static void SetupLogLevel(log4netLogger logger, ILogConfig config)
        {
            logger.Level = logger.Hierarchy.LevelMap[config.LogLevel];

            if (logger.Level == null)
            {
                logger.Level = log4net.Core.Level.Info;
            }

            if (logger.Level == GetAuditLevel())
            {
                logger.Level = Level.Info;
                logger.Log(Level.Warn, $"Log level was set to {AuditLogName} which is not a valid log level. To enable audit logging, set the auditLog configuration option to true. Log level will be treated as INFO for this run.", null);
            }

            if (IsLogLevelDeprecated(logger.Level))
            {
                logger.Log(Level.Warn, string.Format(
                    "The log level, {0}, set in your configuration file has been deprecated. The agent will still log correctly, but you should change to a supported logging level as described in newrelic.config or the online documentation.",
                    logger.Level.ToString()), null);
            }
        }

        private static bool IsLogLevelDeprecated(Level level)
        {
            foreach (var l in DeprecatedLogLevels)
            {
                if (l.Name.Equals(level.Name, StringComparison.InvariantCultureIgnoreCase)) return true;
            }
            return false;
        }

        /// <summary>
        /// A memory appender for logging to memory during startup. Log messages will be re-logged after configuration is loaded.
        /// </summary>
        /// <param name="logger"></param>
        private static void SetupStartupLogAppender(log4netLogger logger)
        {
            var startupAppender = new MemoryAppender();
            startupAppender.Name = STARTUP_APPENDER_NAME;
            startupAppender.Layout = LoggerBootstrapper.FileLogLayout;
            startupAppender.ActivateOptions();

            logger.AddAppender(startupAppender);
            logger.Repository.Configured = true;
        }

        /// <summary>
        /// A temporary event log appender for logging during startup (before config is loaded)
        /// </summary>
        /// <param name="logger"></param>
        private static void SetupTemporaryEventLogAppender(log4netLogger logger)
        {
#if NET45
			var appender = new EventLogAppender();
			appender.Layout = eventLoggerLayout;
			appender.Name = TemporaryEventLogAppenderName;
			appender.LogName = EventLogName;
			appender.ApplicationName = EventLogSourceName;
			appender.Threshold = Level.Warn;
			appender.AddFilter(GetNoAuditFilter());
			appender.ActivateOptions();

			logger.AddAppender(appender);
#endif
        }

        /// <summary>
        /// Setup the event log appender and attach it to a logger.
        /// </summary>
        /// <param name="logger">The logger you want to attach the event log appender to.</param>
        /// <param name="config">The configuration for the appender.</param>
        private static void SetupEventLogAppender(log4netLogger logger, ILogConfig config)
        {
#if NET45
			var appender = new EventLogAppender();
			appender.Layout = eventLoggerLayout;
			appender.Threshold = Level.Warn;
			appender.Name = EventLogAppenderName;
			appender.LogName = EventLogName;
			appender.ApplicationName = EventLogSourceName;
			appender.AddFilter(GetNoAuditFilter());
			appender.ActivateOptions();

			logger.AddAppender(appender);
#endif
        }

        /// <summary>
        /// Setup the debug log appender and attach it to a logger.
        /// </summary>
        /// <param name="logger">The logger you want to attach the event log appender to.</param>
        private static void SetupDebugLogAppender(log4netLogger logger)
        {
#if DEBUG
			// Create the debug appender and connect it to our logger.
			var debugAppender = new DebugAppender();
			debugAppender.Layout = FileLogLayout;
			debugAppender.AddFilter(GetNoAuditFilter());
			logger.AddAppender(debugAppender);
#endif
        }

        /// <summary>
        /// Setup the console log appender and attach it to a logger.
        /// </summary>
        /// <param name="logger">The logger you want to attach the console log appender to.</param>
        /// <param name="config">The configuration for the appender.</param>
        private static void SetupConsoleLogAppender(log4netLogger logger)
        {
            var appender = new ConsoleAppender();
            appender.Name = ConsoleLogAppenderName;
            appender.Layout = FileLogLayout;
            appender.Threshold = Level.Warn;
            appender.AddFilter(GetNoAuditFilter());
            appender.ActivateOptions();
            logger.AddAppender(appender);
        }

        /// <summary>
        /// Setup the file log appender and attach it to a logger.
        /// </summary>
        /// <param name="logger">The logger you want to attach the file appender to.</param>
        /// <param name="config">The configuration for the appender.</param>
        /// <exception cref="System.Exception">If an exception occurs, the Event Log Appender is setup
        /// to handle output.</exception>
        private static void SetupFileLogAppender(log4netLogger logger, ILogConfig config)
        {
            string logFileName = config.GetFullLogFileName();

            try
            {
                var appender = SetupRollingFileAppender(config, logFileName, "FileLog", FileLogLayout);
                appender.AddFilter(GetNoAuditFilter());
                appender.ActivateOptions();
                logger.AddAppender(appender);
            }
            catch (Exception)
            {
                // Fallback to the event logger if we cannot setup a file logger.
                SetupEventLogAppender(logger, config);
            }
        }

        /// <summary>
        /// Setup the audit log file appender and attach it to a logger.
        /// </summary>
        /// <param name="logger">The logger you want to attach the audit log appender to.</param>
        /// <param name="config">The configuration for the appender.</param>
        private static void SetupAuditLogger(log4netLogger logger, ILogConfig config)
        {
            if (!config.IsAuditLogEnabled) return;

            string logFileName = config.GetFullLogFileName().Replace(".log", "_audit.log");

            try
            {
                var appender = SetupRollingFileAppender(config, logFileName, AuditLogAppenderName, AuditLogLayout);
                appender.AddFilter(GetAuditFilter());
                appender.AddFilter(new DenyAllFilter());
                appender.ActivateOptions();
                logger.AddAppender(appender);
            }
            catch (Exception)
            { }
        }

        /// <summary>
        /// Sets up a rolling file appender using defaults shared for all our rolling file appenders.
        /// </summary>
        /// <param name="config">The configuration for the appender.</param>
        /// <param name="fileName">The name of the file this appender will write to.</param>
        /// <param name="appenderName">The name of this appender.</param>
        /// <remarks>This does not call appender.ActivateOptions or add the appender to the logger.</remarks>
        private static RollingFileAppender SetupRollingFileAppender(ILogConfig config, string fileName, string appenderName, ILayout layout)
        {
            var log = log4net.LogManager.GetLogger(typeof(AgentManager));

            // check that the log file is accessible
            try
            {
                using (File.Open(fileName, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Write)) { }
            }
            catch (Exception exception)
            {
                log.ErrorFormat("Unable to write the {0} log to \"{1}\": {2}", appenderName, fileName, exception.Message);
                throw;
            }

            try
            {
                var appender = new RollingFileAppender();

                appender.LockingModel = GetFileLockingModel(config);
                appender.Layout = layout;
                appender.File = fileName;
                appender.Encoding = System.Text.Encoding.UTF8;
                appender.AppendToFile = true;
                appender.RollingStyle = RollingFileAppender.RollingMode.Size;
                appender.MaxSizeRollBackups = 4;
                appender.MaxFileSize = 50 * 1024 * 1024; // 50MB
                appender.StaticLogFileName = true;
                appender.ImmediateFlush = true;

                return appender;
            }
            catch (Exception exception)
            {
                log.ErrorFormat("Unable to configure the {0} log file appender for \"{1}\": {2}", appenderName, fileName, exception.Message);
                throw;
            }
        }
    }
}

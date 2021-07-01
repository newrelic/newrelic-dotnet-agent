// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Reflection;
using System.Linq;
using System.Threading;
using NewRelic.Agent.Core.Config;
using NewRelic.Core.Logging;
using NUnit.Framework;
using log4net.Appender;
using log4net.Core;

namespace NewRelic.Agent.Core
{

    [TestFixture]
    public class LoggerBootstrapperTest
    {
        [Test]
        public static void IsDebugEnabled_is_false_when_config_log_is_off()
        {
            ILogConfig config = GetLogConfig("off");
            LoggerBootstrapper.Initialize();
            LoggerBootstrapper.ConfigureLogger(config);
            Assert.IsFalse(Log.IsDebugEnabled);
        }

        [Test]
        public static void IsDebugEnabled_is_true_when_config_log_is_all()
        {
            ILogConfig config = GetLogConfig("all");
            LoggerBootstrapper.Initialize();
            LoggerBootstrapper.ConfigureLogger(config);
            Assert.That(Log.IsDebugEnabled);
        }

        [Test]
        public static void IsInfoEnabled_is_true_when_config_log_is_info()
        {
            ILogConfig config = GetLogConfig("info");
            LoggerBootstrapper.Initialize();
            LoggerBootstrapper.ConfigureLogger(config);
            Assert.That(Log.IsInfoEnabled);
        }

        [Test]
        public static void IsDebugEnabled_is_false_when_config_log_is_info()
        {
            ILogConfig config = GetLogConfig("info");
            LoggerBootstrapper.Initialize();
            LoggerBootstrapper.ConfigureLogger(config);
            Assert.IsFalse(Log.IsDebugEnabled);
        }

        [Test]
        public static void IsDebugEnabled_is_true_when_config_log_is_debug()
        {
            ILogConfig config = GetLogConfig("debug");
            LoggerBootstrapper.Initialize();
            LoggerBootstrapper.ConfigureLogger(config);
            Assert.That(Log.IsDebugEnabled);
        }

        [Test]
        public static void IsEnabledFor_finest_is_false_when_config_log_is_debug()
        {
            ILogConfig config = GetLogConfig("debug");
            LoggerBootstrapper.Initialize();
            LoggerBootstrapper.ConfigureLogger(config);
            Assert.IsFalse(Log.IsFinestEnabled);

        }

        [Test]
        public static void ConsoleAppender_exists_and_has_correct_level_when_console_true_in_config()
        {
            ILogConfig config = LogConfigFixtureWithConsoleLogEnabled("debug");
            LoggerBootstrapper.Initialize();
            LoggerBootstrapper.ConfigureLogger(config);
            var logger = GetLogger();
            var consoleAppender = logger.Appenders.OfType<ConsoleAppender>().First();

            Assert.IsFalse(Log.IsFinestEnabled);
            Assert.That(logger.Level == Level.Debug);
            // If the appender's threshold is null, it basically
            // inherits the parent logger's level.
            Assert.That(consoleAppender.Threshold == null);
        }

        [Test]
        public static void ConsoleAppender_does_not_exist_when_console_false_in_config()
        {
            ILogConfig config = GetLogConfig("debug");
            LoggerBootstrapper.Initialize();
            LoggerBootstrapper.ConfigureLogger(config);
            var logger = GetLogger();
            var consoleAppenders = logger.Appenders.OfType<ConsoleAppender>();

            Assert.IsFalse(Log.IsFinestEnabled);
            Assert.IsEmpty(consoleAppenders);
        }

        [Test]
        public static void Config_IsAuditEnabled_for_config_is_true_when_auditLog_true_in_config()
        {
            ILogConfig config = LogConfigFixtureWithAuditLogEnabled("debug");
            Assert.That(config.IsAuditLogEnabled);
        }

        [Test]
        public static void Config_IsAuditEnabled_for_config_is_false_when_not_added_to_config()
        {
            ILogConfig config = GetLogConfig("debug");
            Assert.IsFalse(config.IsAuditLogEnabled);
        }

        [Test]
        public static void Config_IsConsoleEnabled_for_config_is_true_when_console_true_in_config()
        {
            ILogConfig config = LogConfigFixtureWithConsoleLogEnabled("debug");
            Assert.That(config.Console);
        }

        [Test]
        public static void Config_IsConsoleEnabled_for_config_is_false_when_not_added_to_config()
        {
            ILogConfig config = GetLogConfig("debug");
            Assert.IsFalse(config.Console);
        }

        [Test]
        public static void Logging_sets_threadid_property_for_Info()
        {
            log4net.ThreadContext.Properties["threadid"] = null;

            ILogConfig config = GetLogConfig("all");
            LoggerBootstrapper.Initialize();
            LoggerBootstrapper.ConfigureLogger(config);

            Log.Info("Please set my thread id.");

            Assert.AreEqual(log4net.ThreadContext.Properties["threadid"], Thread.CurrentThread.ManagedThreadId);
        }

        [Test]
        public static void Logging_sets_threadid_property_for_Info_Exception()
        {
            log4net.ThreadContext.Properties["threadid"] = null;

            ILogConfig config = GetLogConfig("all");
            LoggerBootstrapper.Initialize();
            LoggerBootstrapper.ConfigureLogger(config);

            Log.Info(new Exception("oh no!"));

            Assert.AreEqual(log4net.ThreadContext.Properties["threadid"], Thread.CurrentThread.ManagedThreadId);
        }

        [Test]
        public static void Logging_sets_threadid_property_for_InfoFormat()
        {
            log4net.ThreadContext.Properties["threadid"] = null;

            ILogConfig config = GetLogConfig("all");
            LoggerBootstrapper.Initialize();
            LoggerBootstrapper.ConfigureLogger(config);

            Log.InfoFormat("My message {0}", "works");

            Assert.AreEqual(log4net.ThreadContext.Properties["threadid"], Thread.CurrentThread.ManagedThreadId);
        }

        [Test]
        public static void Logging_sets_threadid_property_for_Debug()
        {
            log4net.ThreadContext.Properties["threadid"] = null;

            ILogConfig config = GetLogConfig("all");
            LoggerBootstrapper.Initialize();
            LoggerBootstrapper.ConfigureLogger(config);

            Log.Debug("debug mah");

            Assert.AreEqual(log4net.ThreadContext.Properties["threadid"], Thread.CurrentThread.ManagedThreadId);
        }

        [Test]
        public static void Logging_sets_threadid_property_for_Debug_Exception()
        {
            log4net.ThreadContext.Properties["threadid"] = null;

            ILogConfig config = GetLogConfig("all");
            LoggerBootstrapper.Initialize();
            LoggerBootstrapper.ConfigureLogger(config);

            Log.Debug(new Exception("oh no!"));

            Assert.AreEqual(log4net.ThreadContext.Properties["threadid"], Thread.CurrentThread.ManagedThreadId);
        }

        [Test]
        public static void Logging_sets_threadid_property_for_DebugFormat()
        {
            log4net.ThreadContext.Properties["threadid"] = null;

            ILogConfig config = GetLogConfig("all");
            LoggerBootstrapper.Initialize();
            LoggerBootstrapper.ConfigureLogger(config);

            Log.DebugFormat("My message {0}", "works");

            Assert.AreEqual(log4net.ThreadContext.Properties["threadid"], Thread.CurrentThread.ManagedThreadId);
        }

        [Test]
        public static void Logging_sets_threadid_property_for_ErrorFormat()
        {
            log4net.ThreadContext.Properties["threadid"] = null;

            ILogConfig config = GetLogConfig("all");
            LoggerBootstrapper.Initialize();
            LoggerBootstrapper.ConfigureLogger(config);

            Log.ErrorFormat("My message {0}", "works");

            Assert.AreEqual(log4net.ThreadContext.Properties["threadid"], Thread.CurrentThread.ManagedThreadId);
        }

        [Test]
        public static void Logging_sets_threadid_property_for_Error()
        {
            log4net.ThreadContext.Properties["threadid"] = null;

            ILogConfig config = GetLogConfig("all");
            LoggerBootstrapper.Initialize();
            LoggerBootstrapper.ConfigureLogger(config);

            Log.Error("debug mah");

            Assert.AreEqual(log4net.ThreadContext.Properties["threadid"], Thread.CurrentThread.ManagedThreadId);
        }

        [Test]
        public static void Logging_sets_threadid_property_for_Error_Exception()
        {
            log4net.ThreadContext.Properties["threadid"] = null;

            ILogConfig config = GetLogConfig("all");
            LoggerBootstrapper.Initialize();
            LoggerBootstrapper.ConfigureLogger(config);

            Log.Error(new Exception("oh no!"));

            Assert.AreEqual(log4net.ThreadContext.Properties["threadid"], Thread.CurrentThread.ManagedThreadId);
        }

        [Test]
        public static void Logging_sets_threadid_property_for_FinestFormat()
        {
            log4net.ThreadContext.Properties["threadid"] = null;

            ILogConfig config = GetLogConfig("all");
            LoggerBootstrapper.Initialize();
            LoggerBootstrapper.ConfigureLogger(config);

            Log.FinestFormat("My message {0}", "works");

            Assert.AreEqual(log4net.ThreadContext.Properties["threadid"], Thread.CurrentThread.ManagedThreadId);
        }

        [Test]
        public static void Logging_sets_threadid_property_for_Finest()
        {
            log4net.ThreadContext.Properties["threadid"] = null;

            ILogConfig config = GetLogConfig("all");
            LoggerBootstrapper.Initialize();
            LoggerBootstrapper.ConfigureLogger(config);

            Log.Finest("debug mah");

            Assert.AreEqual(log4net.ThreadContext.Properties["threadid"], Thread.CurrentThread.ManagedThreadId);
        }

        [Test]
        public static void Logging_sets_threadid_property_for_Finest_Exception()
        {
            log4net.ThreadContext.Properties["threadid"] = null;

            ILogConfig config = GetLogConfig("all");
            LoggerBootstrapper.Initialize();
            LoggerBootstrapper.ConfigureLogger(config);

            Log.Finest(new Exception("oh no!"));

            Assert.AreEqual(log4net.ThreadContext.Properties["threadid"], Thread.CurrentThread.ManagedThreadId);
        }

        [Test]
        public static void Logging_sets_threadid_property_for_WarnFormat()
        {
            log4net.ThreadContext.Properties["threadid"] = null;

            ILogConfig config = GetLogConfig("all");
            LoggerBootstrapper.Initialize();
            LoggerBootstrapper.ConfigureLogger(config);

            Log.WarnFormat("My message {0}", "works");

            Assert.AreEqual(log4net.ThreadContext.Properties["threadid"], Thread.CurrentThread.ManagedThreadId);
        }

        [Test]
        public static void Logging_sets_threadid_property_for_Warn()
        {
            log4net.ThreadContext.Properties["threadid"] = null;

            ILogConfig config = GetLogConfig("all");
            LoggerBootstrapper.Initialize();
            LoggerBootstrapper.ConfigureLogger(config);

            Log.Warn("warn mah");

            Assert.AreEqual(log4net.ThreadContext.Properties["threadid"], Thread.CurrentThread.ManagedThreadId);
        }

        [Test]
        public static void Logging_sets_threadid_property_for_Warn_Exception()
        {
            log4net.ThreadContext.Properties["threadid"] = null;

            ILogConfig config = GetLogConfig("all");
            LoggerBootstrapper.Initialize();
            LoggerBootstrapper.ConfigureLogger(config);

            Log.Warn(new Exception("oh no!"));

            Assert.AreEqual(log4net.ThreadContext.Properties["threadid"], Thread.CurrentThread.ManagedThreadId);
        }

        [Test]
        public static void Logging_sets_threadid_property_for_LogMessage()
        {
            log4net.ThreadContext.Properties["threadid"] = null;

            ILogConfig config = GetLogConfig("all");
            LoggerBootstrapper.Initialize();
            LoggerBootstrapper.ConfigureLogger(config);

            Log.LogMessage(LogLevel.Info, "Test Message");

            Assert.AreEqual(log4net.ThreadContext.Properties["threadid"], Thread.CurrentThread.ManagedThreadId);
        }

        [Test]
        public static void Logging_sets_threadid_property_for_LogException()
        {
            log4net.ThreadContext.Properties["threadid"] = null;

            ILogConfig config = GetLogConfig("all");
            LoggerBootstrapper.Initialize();
            LoggerBootstrapper.ConfigureLogger(config);

            Log.LogException(LogLevel.Info, new Exception("Test exception"));

            Assert.AreEqual(log4net.ThreadContext.Properties["threadid"], Thread.CurrentThread.ManagedThreadId);
        }



        static private ILogConfig GetLogConfig(string logLevel)
        {
            var xml = string.Format(
                "<configuration xmlns=\"urn:newrelic-config\">" +
                "   <service licenseKey=\"dude\"/>" +
                "   <application>" +
                "       <name>Test</name>" +
                "   </application>" +
                "   <log level=\"{0}\"/>" +
                "</configuration>",
                logLevel);
            var configuration = ConfigurationLoader.InitializeFromXml(xml);
            return configuration.LogConfig;
        }

        static private ILogConfig LogConfigFixtureWithAuditLogEnabled(string logLevel)
        {
            var xml = string.Format(
                "<configuration xmlns=\"urn:newrelic-config\">" +
                "   <service licenseKey=\"dude\"/>" +
                "   <application>" +
                "       <name>Test</name>" +
                "   </application>" +
                "   <log level=\"{0}\" auditLog=\"true\"/>" +
                "</configuration>",
                logLevel);
            var configuration = ConfigurationLoader.InitializeFromXml(xml);
            return configuration.LogConfig;
        }

        static private ILogConfig LogConfigFixtureWithConsoleLogEnabled(string logLevel)
        {
            var xml = string.Format(
                "<configuration xmlns=\"urn:newrelic-config\">" +
                "   <service licenseKey=\"dude\"/>" +
                "   <application>" +
                "       <name>Test</name>" +
                "   </application>" +
                "   <log level=\"{0}\" console=\"true\"/>" +
                "</configuration>",
                logLevel);
            var configuration = ConfigurationLoader.InitializeFromXml(xml);
            return configuration.LogConfig;
        }

        private static log4net.Repository.Hierarchy.Logger GetLogger()
        {
            var hierarchy =
                log4net.LogManager.GetRepository(Assembly.GetCallingAssembly()) as
                    log4net.Repository.Hierarchy.Hierarchy;
            var logger = hierarchy.Root;
            return logger;
        }
    }
}

// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Core.Config;
using NewRelic.Core.Logging;
using NUnit.Framework;
using System.IO;
using NewRelic.Testing.Assertions;

namespace NewRelic.Agent.Core
{

    [TestFixture]
    public class LoggerBootstrapperTest
    {
        [Test]
        public static void No_log_levels_are_enabled_when_config_log_is_off()
        {
            ILogConfig config = GetLogConfig("off");
            LoggerBootstrapper.Initialize();
            LoggerBootstrapper.ConfigureLogger(config);
            NrAssert.Multiple(
                () => Assert.That(Log.IsFinestEnabled, Is.False),
                () => Assert.That(Log.IsDebugEnabled, Is.False),
                () => Assert.That(Log.IsInfoEnabled, Is.False),
                () => Assert.That(Log.IsWarnEnabled, Is.False),
                () => Assert.That(Log.IsErrorEnabled, Is.False)
            );
        }

        [Test]
        public static void All_log_levels_are_enabled_when_config_log_is_all()
        {
            ILogConfig config = GetLogConfig("all");
            LoggerBootstrapper.Initialize();
            LoggerBootstrapper.ConfigureLogger(config);

            NrAssert.Multiple(
                () => Assert.That(Log.IsFinestEnabled, Is.True),
                () => Assert.That(Log.IsDebugEnabled, Is.True),
                () => Assert.That(Log.IsInfoEnabled, Is.True),
                () => Assert.That(Log.IsWarnEnabled, Is.True),
                () => Assert.That(Log.IsErrorEnabled, Is.True)
            );
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
            Assert.That(Log.IsDebugEnabled, Is.False);
        }

        [Test]
        public static void IsDebugEnabled_is_true_when_config_log_is_debug()
        {
            ILogConfig config = LogConfigFixtureWithConsoleLogEnabled("debug"); // just to increase code coverage
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
            Assert.That(Log.IsFinestEnabled, Is.False);

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
            Assert.That(config.IsAuditLogEnabled, Is.False);
        }

        [Test]
        public static void Config_IsConsoleEnabled_for_config_is_true_when_console_true_in_config()
        {
            ILogConfig config = LogConfigFixtureWithConsoleLogEnabled("debug");
            Assert.That(config.Console);
        }

        [Test]
        public static void Config_FileSizeRollingStrategy_for_config_is_Size_when_logRollingStrategy_is_size_in_config()
        {
            ILogConfig config = LogConfigFixtureWithFileSizeRollingStrategyEnabled();
            Assert.That(config.LogRollingStrategy, Is.EqualTo(LogRollingStrategy.Size));
        }

        [Test]
        public static void Config_FileSizeRollingStrategy_for_config_is_Size_when_logRollingStrategy_is_size_in_config_and_maxLogFileSizeMB_and_maxLogFiles_are_set()
        {
            ILogConfig config = LogConfigFixtureWithFileSizeRollingStrategyEnabledAndMaxSizeAndLogFileCountSet(100, 10);
            Assert.That(config.LogRollingStrategy, Is.EqualTo(LogRollingStrategy.Size));
            Assert.That(config.MaxLogFileSizeMB, Is.EqualTo(100));
            Assert.That(config.MaxLogFiles, Is.EqualTo(10));
        }

        [Test]
        public static void Config_FileSizeRollingStrategy_for_config_is_Day_when_logRollingStrategy_is_day_in_config()
        {
            ILogConfig config = LogConfigFixtureWithDayRollingStrategyEnabled();
            Assert.That(config.LogRollingStrategy, Is.EqualTo(LogRollingStrategy.Day));
        }

        [Test]
        public static void Config_FileSizeRollingStrategy_for_config_is_Day_when_logRollingStrategy_is_day_in_config_and_maxLogFiles_are_set()
        {
            ILogConfig config = LogConfigFixtureWithDayRollingStrategyEnabledAndLogFileCountSet(10);
            Assert.That(config.LogRollingStrategy, Is.EqualTo(LogRollingStrategy.Day));
            Assert.That(config.MaxLogFiles, Is.EqualTo(10));
        }

        [Test]
        public static void Config_IsConsoleEnabled_for_config_is_false_when_not_added_to_config()
        {
            ILogConfig config = GetLogConfig("debug");
            Assert.That(config.Console, Is.False);
        }


        private static ILogConfig GetLogConfig(string logLevel)
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

            var xsdFile = Path.Combine(TestContext.CurrentContext.TestDirectory, "Configuration.xsd");
            Func<string> configSchemaSource = () => File.ReadAllText(xsdFile);

            var configuration = ConfigurationLoader.InitializeFromXml(xml, configSchemaSource);

            return new BootstrapConfiguration(configuration, "testfilename").LogConfig;
        }

        private static ILogConfig LogConfigFixtureWithAuditLogEnabled(string logLevel)
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

            var xsdFile = Path.Combine(TestContext.CurrentContext.TestDirectory, "Configuration.xsd");
            Func<string> configSchemaSource = () => File.ReadAllText(xsdFile);

            var configuration = ConfigurationLoader.InitializeFromXml(xml, configSchemaSource);

            return new BootstrapConfiguration(configuration, "testfilename").LogConfig;
        }

        private static ILogConfig LogConfigFixtureWithConsoleLogEnabled(string logLevel)
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

            var xsdFile = Path.Combine(TestContext.CurrentContext.TestDirectory, "Configuration.xsd");
            Func<string> configSchemaSource = () => File.ReadAllText(xsdFile);

            var configuration = ConfigurationLoader.InitializeFromXml(xml, configSchemaSource);

            return new BootstrapConfiguration(configuration, "testfilename").LogConfig;
        }
        private static ILogConfig LogConfigFixtureWithLogEnabled(bool enabled)
        {
            var xml = string.Format(
                "<configuration xmlns=\"urn:newrelic-config\">" +
                "   <service licenseKey=\"dude\"/>" +
                "   <application>" +
                "       <name>Test</name>" +
                "   </application>" +
                "   <log level=\"debug\" console=\"true\" enabled=\"{0}\" />" +
                "</configuration>",
                enabled.ToString().ToLower());

            var xsdFile = Path.Combine(TestContext.CurrentContext.TestDirectory, "Configuration.xsd");
            Func<string> configSchemaSource = () => File.ReadAllText(xsdFile);

            var configuration = ConfigurationLoader.InitializeFromXml(xml, configSchemaSource);

            return new BootstrapConfiguration(configuration, "testfilename").LogConfig;
        }

        private static ILogConfig LogConfigFixtureWithConsoleEnabled(bool enabled)
        {
            var xml = string.Format(
                "<configuration xmlns=\"urn:newrelic-config\">" +
                "   <service licenseKey=\"dude\"/>" +
                "   <application>" +
                "       <name>Test</name>" +
                "   </application>" +
                "   <log level=\"debug\" console=\"{0}\" />" +
                "</configuration>",
                enabled.ToString().ToLower());

            var xsdFile = Path.Combine(TestContext.CurrentContext.TestDirectory, "Configuration.xsd");
            Func<string> configSchemaSource = () => File.ReadAllText(xsdFile);

            var configuration = ConfigurationLoader.InitializeFromXml(xml, configSchemaSource);

            return new BootstrapConfiguration(configuration, "testfilename").LogConfig;
        }

        private static ILogConfig LogConfigFixtureWithFileSizeRollingStrategyEnabled()
        {
            var xml = string.Format(
                "<configuration xmlns=\"urn:newrelic-config\">" +
                "   <service licenseKey=\"dude\"/>" +
                "   <application>" +
                "       <name>Test</name>" +
                "   </application>" +
                "   <log level=\"debug\" logRollingStrategy=\"size\" />" +
                "</configuration>");

            var xsdFile = Path.Combine(TestContext.CurrentContext.TestDirectory, "Configuration.xsd");
            Func<string> configSchemaSource = () => File.ReadAllText(xsdFile);

            var configuration = ConfigurationLoader.InitializeFromXml(xml, configSchemaSource);

            return new BootstrapConfiguration(configuration, "testfilename").LogConfig;
        }
        private static ILogConfig LogConfigFixtureWithFileSizeRollingStrategyEnabledAndMaxSizeAndLogFileCountSet(int maxLogFileSizeMB, int maxLogFiles)
        {
            var xml = string.Format(
                "<configuration xmlns=\"urn:newrelic-config\">" +
                "   <service licenseKey=\"dude\"/>" +
                "   <application>" +
                "       <name>Test</name>" +
                "   </application>" +
                "   <log level=\"debug\" logRollingStrategy=\"size\" maxLogFileSizeMB=\"{0}\" maxLogFiles=\"{1}\" />" +
                "</configuration>",
                maxLogFileSizeMB, maxLogFiles);

            var xsdFile = Path.Combine(TestContext.CurrentContext.TestDirectory, "Configuration.xsd");
            Func<string> configSchemaSource = () => File.ReadAllText(xsdFile);

            var configuration = ConfigurationLoader.InitializeFromXml(xml, configSchemaSource);

            return new BootstrapConfiguration(configuration, "testfilename").LogConfig;
        }

        private static ILogConfig LogConfigFixtureWithDayRollingStrategyEnabled()
        {
            var xml = string.Format(
                "<configuration xmlns=\"urn:newrelic-config\">" +
                "   <service licenseKey=\"dude\"/>" +
                "   <application>" +
                "       <name>Test</name>" +
                "   </application>" +
                "   <log level=\"debug\" logRollingStrategy=\"day\" />" +
                "</configuration>");

            var xsdFile = Path.Combine(TestContext.CurrentContext.TestDirectory, "Configuration.xsd");
            Func<string> configSchemaSource = () => File.ReadAllText(xsdFile);

            var configuration = ConfigurationLoader.InitializeFromXml(xml, configSchemaSource);

            return new BootstrapConfiguration(configuration, "testfilename").LogConfig;
        }

        private static ILogConfig LogConfigFixtureWithDayRollingStrategyEnabledAndLogFileCountSet(int maxLogFiles)
        {
            var xml = string.Format(
                "<configuration xmlns=\"urn:newrelic-config\">" +
                "   <service licenseKey=\"dude\"/>" +
                "   <application>" +
                "       <name>Test</name>" +
                "   </application>" +
                "   <log level=\"debug\" logRollingStrategy=\"day\" maxLogFiles=\"{0}\" />" +
                "</configuration>",
                maxLogFiles);

            var xsdFile = Path.Combine(TestContext.CurrentContext.TestDirectory, "Configuration.xsd");
            Func<string> configSchemaSource = () => File.ReadAllText(xsdFile);

            var configuration = ConfigurationLoader.InitializeFromXml(xml, configSchemaSource);
            return new BootstrapConfiguration(configuration, "testfilename").LogConfig;
        }
    }
}

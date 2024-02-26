// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Core.Config;
using NewRelic.Core.Logging;
using NUnit.Framework;
using System.IO;
using NewRelic.Testing.Assertions;
using System.Collections.Generic;

namespace NewRelic.Agent.Core
{

    [TestFixture]
    public class LoggerBootstrapperTest
    {
        private Func<string, string> _originalGetEnvironmentVar;
        private Dictionary<string, string> _envVars = new Dictionary<string, string>();

        private void SetEnvironmentVar(string name, string value)
        {
            _envVars[name] = value;
        }

        private void ClearEnvironmentVars() =>_envVars.Clear();

        private string MockGetEnvironmentVar(string name)
        {
            if (_envVars.TryGetValue(name, out var value)) return value;
            return null;
        }

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _originalGetEnvironmentVar = ConfigurationLoader.GetEnvironmentVar;
            ConfigurationLoader.GetEnvironmentVar = MockGetEnvironmentVar;
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            ConfigurationLoader.GetEnvironmentVar = _originalGetEnvironmentVar;
        }

        [SetUp]
        public void Setup()
        {
            ClearEnvironmentVars();
        }

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
        public void Config_LogRollingStrategy_for_config_is_Day_when_overridden_by_environment_variable()
        {
            SetEnvironmentVar("NEW_RELIC_LOG_ROLLING_STRATEGY", "day");
            ILogConfig config = LogConfigFixtureWithFileSizeRollingStrategyEnabled();
            Assert.That(config.LogRollingStrategy, Is.EqualTo(LogRollingStrategy.Day));
        }

        [Test]
        public void Config_LogRollingStrategy_for_config_throws_exception_when_overridden_by_invalid_environment_variable()
        {
            SetEnvironmentVar("NEW_RELIC_LOG_ROLLING_STRATEGY", "invalid");
            ILogConfig config = LogConfigFixtureWithFileSizeRollingStrategyEnabled();
            Assert.That(() => config.LogRollingStrategy, Throws.Exception.TypeOf<ConfigurationLoaderException>());
        }

        [Test]
        public void Config_maxLogFileSizeMB_for_config_is_1000_when_overridden_by_environment_variable()
        {
            SetEnvironmentVar("NEW_RELIC_LOG_MAX_FILE_SIZE_MB", "1000");
            ILogConfig config = LogConfigFixtureWithFileSizeRollingStrategyEnabled();
            Assert.That(config.MaxLogFileSizeMB, Is.EqualTo(1000));
        }

        [Test]
        public void Config_MaxLogFiles_for_config_is_10_when_overridden_by_environment_variable()
        {
            SetEnvironmentVar("NEW_RELIC_LOG_MAX_FILES", "10");
            ILogConfig config = LogConfigFixtureWithFileSizeRollingStrategyEnabled();
            Assert.That(config.MaxLogFiles, Is.EqualTo(10));
        }

        [Test]
        public static void Config_IsConsoleEnabled_for_config_is_false_when_not_added_to_config()
        {
            ILogConfig config = GetLogConfig("debug");
            Assert.That(config.Console, Is.False);
        }

        [Test]
        [TestCase(null, false, false)]
        [TestCase("0", true, false)]
        [TestCase("0", false, false)]
        [TestCase("false", true, false)]
        [TestCase("false", false, false)]
        [TestCase("1", true, true)]
        [TestCase("1", false, true)]
        [TestCase("true", true, true)]
        [TestCase("true", false, true)]
        [TestCase("not a valid bool", true, true)]
        [TestCase("not a valid bool", false, false)]
        public void test_ways_to_disable_logging(string envVarValue, bool logsEnabledInConfig, bool expectedLogConfig)
        {
            ILogConfig config;

            if (envVarValue != null)
            {
                SetEnvironmentVar("NEW_RELIC_LOG_ENABLED", envVarValue);
            }
            config = LogConfigFixtureWithLogEnabled(logsEnabledInConfig);
            Assert.That(expectedLogConfig, Is.EqualTo(config.Enabled));
        }

        [Test]
        [TestCase(null, false, false)]
        [TestCase("0", true, false)]
        [TestCase("0", false, false)]
        [TestCase("false", true, false)]
        [TestCase("false", false, false)]
        [TestCase("1", true, true)]
        [TestCase("1", false, true)]
        [TestCase("true", true, true)]
        [TestCase("true", false, true)]
        [TestCase("not a valid bool", true, true)]
        [TestCase("not a valid bool", false, false)]
        public void test_ways_to_enable_console_logging(string envVarValue, bool consoleLogEnabledInConfig, bool expectedConsoleLogConfig)
        {
            ILogConfig config;

            config = LogConfigFixtureWithConsoleEnabled(true);
            Assert.That(config.Console, Is.True);

            if (envVarValue != null)
            {
                SetEnvironmentVar("NEW_RELIC_LOG_CONSOLE", envVarValue);
            }
            config = LogConfigFixtureWithConsoleEnabled(consoleLogEnabledInConfig);
            Assert.That(expectedConsoleLogConfig, Is.EqualTo(config.Console));
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
            return configuration.LogConfig;
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
            return configuration.LogConfig;
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
            return configuration.LogConfig;
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
            return configuration.LogConfig;
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
            return configuration.LogConfig;
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
            return configuration.LogConfig;
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
            return configuration.LogConfig;
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
            return configuration.LogConfig;
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
            return configuration.LogConfig;
        }
    }
}
